// Grind removal, held to one line: take away the REPETITION, never the PRICE.
//
// Harvesting a branch pile is a hold, an animation, and a yield. The hold and the animation teach
// nothing and decide nothing - that is grind, and it goes. The yield, the tool it demands and the
// weight it adds are the price, and every one of them is left exactly as the game set it.
//
// Break-down is the other side of that line and is treated differently on purpose. Breaking an item
// down costs in-game HOURS, and hours in this game are weather, hunger, daylight and wolves. Making
// that free is not convenience, it is a refund. So it exists, it is off, and the tooltip says which
// of the two it is.

using System.Collections.Generic;
using Il2Cpp;
using UnityEngine;

namespace LDPickupDoctor
{
    internal static class Grind
    {
        public static int HarvestedTotal;
        public static int SweepRoomTotal;

        // Per-object backoff. A harvestable that refuses is not retried every frame forever - it is
        // tried again later, because "refused now" and "will always refuse" are different things and
        // only one of them is worth giving up on. Nothing is ever given up on permanently.
        private static readonly Dictionary<int, float> _retryAt = new Dictionary<int, float>();
        private static readonly Dictionary<int, int> _failures = new Dictionary<int, int>();

        // Break-down objects we have zeroed, and what they were, so the switch is reversible.
        private static readonly Dictionary<int, float> _originalHours = new Dictionary<int, float>();

        public static void AutoHarvestPass()
        {
            if (!Settings.HarvestPlants.Value) return;

            float radius = Settings.HarvestRadius.Value;
            float now = Time.realtimeSinceStartup;

            for (int i = 0; i < Sweep.Plants.Count; i++)
            {
                Found f = Sweep.Plants[i];
                if (f.Distance > radius) break;
                Harvestable h = f.Plant;
                if (h == null) continue;

                int id;
                try { id = h.GetInstanceID(); } catch (System.Exception) { continue; }

                float when;
                if (_retryAt.TryGetValue(id, out when) && now < when) continue;

                try { if (h.IsHarvested()) continue; } catch (System.Exception) { }

                if (Settings.HarvestRequireTool.Value && !ToolReady(h))
                {
                    // Not a failure - a price he has not paid yet. Backed off gently so walking
                    // past a sapling with no hatchet does not cost a lookup every frame.
                    Defer(id, 5f);
                    continue;
                }

                try
                {
                    h.Harvest();
                }
                catch (System.Exception e)
                {
                    Log.OnceWarn("harvest-threw", "Harvestable.Harvest threw: " + e.Message
                        + " - that plant is retried later, and auto-harvest keeps running.");
                    Defer(id, 10f);
                    continue;
                }

                bool done = false;
                try { done = h.IsHarvested(); } catch (System.Exception) { done = true; }

                if (done)
                {
                    HarvestedTotal++;
                    _failures.Remove(id);
                    Defer(id, 1f);
                }
                else
                {
                    int n;
                    n = _failures.TryGetValue(id, out n) ? n + 1 : 1;
                    _failures[id] = n;
                    // Escalating backoff, capped. Never abandoned: a plant that refuses because the
                    // player is mid-animation will succeed a few seconds later, and a permanent
                    // refusal costs one attempt a minute, which is nothing.
                    Defer(id, Mathf.Min(60f, 2f * n));
                    if (n == 3)
                    {
                        Log.OnceWarn("harvest-refused",
                            "a harvestable accepted Harvest() and still reports itself unharvested. "
                            + "Backing off to once a minute for that object and carrying on. If this "
                            + "is every plant rather than one, the Harvest entry point has moved.");
                    }
                }
            }
        }

        /// <summary>
        /// Does he have what this plant demands?
        ///
        /// HasToolRequired says the plant wants a tool. GetRequiredTool answers with the tool that
        /// satisfies it, and comes back null when nothing in the pack does - so the pair is read as
        /// "wants one" plus "has one". If that reading is ever wrong the failure is the safe one:
        /// the harvest is deferred rather than performed, and the player harvests by hand as usual.
        /// </summary>
        private static bool ToolReady(Harvestable h)
        {
            try
            {
                if (!h.HasToolRequired()) return true;
                return h.GetRequiredTool() != null;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        private static void Defer(int id, float seconds)
        {
            _retryAt[id] = Time.realtimeSinceStartup + seconds;
        }

        /// <summary>One key press: take everything eligible in the room, ignoring the per-sweep cap.</summary>
        public static int SweepRoom(Vector3 origin)
        {
            if (!Settings.SweepRoomOnKey.Value)
            {
                Log.Info("sweep key pressed, but Grind.SweepRoomOnKey is off.");
                return 0;
            }

            float radius = Settings.SweepRoomRadius.Value;
            Sweep.Run(origin, Mathf.Max(radius, Settings.ScanRadius.Value));

            int taken = 0;
            int blocked = 0;
            Dictionary<Outcome, int> why = new Dictionary<Outcome, int>();

            for (int i = 0; i < Sweep.Items.Count; i++)
            {
                Found f = Sweep.Items[i];
                if (f.Distance > radius) break;
                if (f.Outcome != Outcome.WouldSucceed)
                {
                    if (f.Outcome != Outcome.NotTargetName && f.Outcome != Outcome.AlreadyHeld)
                    {
                        int n;
                        why[f.Outcome] = why.TryGetValue(f.Outcome, out n) ? n + 1 : 1;
                        blocked++;
                    }
                    continue;
                }
                if (Settings.PickupDryRun.Value) { blocked++; continue; }
                if (Pickup.Take(f.Gear, f.Name)) taken++;
            }

            SweepRoomTotal += taken;

            // The report is the feature. "Nothing happened" is the failure mode this mod exists to
            // make impossible, so a press that takes nothing still says exactly what stopped it.
            string reasons = "";
            foreach (KeyValuePair<Outcome, int> kv in why) reasons += "  " + kv.Key + "=" + kv.Value;
            Log.Info("room sweep at " + radius.ToString("0.0") + "m: took " + taken
                + ", blocked " + blocked + (reasons.Length > 0 ? " ->" + reasons : ""));
            return taken;
        }

        /// <summary>
        /// The refund switch, applied and reversible. Nearby break-down objects have their hour cost
        /// zeroed while it is on, and their own original value put back the moment it goes off - so
        /// turning it on to try it does not permanently alter the world he saves.
        /// </summary>
        public static void BreakDownPass()
        {
            bool on = Settings.InstantBreakDown.Value;

            if (!on)
            {
                if (_originalHours.Count == 0) return;
                Restore();
                return;
            }

            Il2CppSystem.Collections.Generic.List<BreakDown> all;
            try { all = BreakDown.m_BreakDownObjects; }
            catch (System.Exception e)
            {
                Log.OnceWarn("breakdown-list", "BreakDown.m_BreakDownObjects could not be read ("
                    + e.Message + ") - instant break-down is doing nothing, and this is it saying so.");
                return;
            }
            if (all == null) return;

            for (int i = 0; i < all.Count; i++)
            {
                BreakDown b = all[i];
                if (b == null) continue;
                int id;
                try { id = b.GetInstanceID(); } catch (System.Exception) { continue; }
                if (_originalHours.ContainsKey(id)) continue;
                try
                {
                    _originalHours[id] = b.m_TimeCostHours;
                    b.m_TimeCostHours = 0f;
                }
                catch (System.Exception) { }
            }
        }

        private static void Restore()
        {
            Il2CppSystem.Collections.Generic.List<BreakDown> all = null;
            try { all = BreakDown.m_BreakDownObjects; } catch (System.Exception) { }
            if (all != null)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    BreakDown b = all[i];
                    if (b == null) continue;
                    int id;
                    try { id = b.GetInstanceID(); } catch (System.Exception) { continue; }
                    float hours;
                    if (_originalHours.TryGetValue(id, out hours))
                    {
                        try { b.m_TimeCostHours = hours; } catch (System.Exception) { }
                    }
                }
            }
            int n = _originalHours.Count;
            _originalHours.Clear();
            if (n > 0) Log.Info("instant break-down off - " + n + " object(s) put back to their own hour cost.");
        }

        /// <summary>Called when a level unloads: instance ids from the old scene mean nothing now.</summary>
        public static void ForgetScene()
        {
            _retryAt.Clear();
            _failures.Clear();
            _originalHours.Clear();
        }
    }
}
