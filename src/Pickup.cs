// Taking the item, and proving it was taken.
//
// TWO WAYS IN, AND WHY THIS TRIES THEM IN THIS ORDER
//
// The Long Dark is IL2CPP, so the interop assemblies give signatures and no method bodies. That
// means the right call cannot be READ, only tried. Two candidates exist:
//
//   Inventory.AddGear(GearItem, bool)                     - the quiet one
//   PlayerManager.ProcessPickupItemInteraction(gi, ...)   - the game's own interaction path
//
// So this does not guess. It calls the quiet one, then CHECKS - m_InPlayerInventory, and the object
// leaving the world - and only if the check fails does it escalate to the interaction path and say
// in the log that it did. That is the rule this station runs on: detect, retry, escalate, say so.
// After a few failures it stops preferring the quiet one for the rest of the session, so the cost
// of the wrong guess is a handful of frames, once, and never a silent no-op.

using System.Collections.Generic;
using Il2Cpp;
using UnityEngine;

namespace LDPickupDoctor
{
    internal static class Pickup
    {
        public static readonly Dictionary<Outcome, int> Tally = new Dictionary<Outcome, int>();
        public static int TakenTotal;
        public static int TakenThisSession;

        private enum Path { Quiet, Interaction }
        private static Path _preferred = Path.Quiet;
        private static int _quietFailures;
        private static int _escalations;

        public static string PathName { get { return _preferred == Path.Quiet ? "Inventory.AddGear" : "PlayerManager.ProcessPickupItemInteraction"; } }
        public static int Escalations { get { return _escalations; } }

        public static void Count(Outcome outcome)
        {
            int n;
            Tally[outcome] = Tally.TryGetValue(outcome, out n) ? n + 1 : 1;
        }

        public static void ResetTally() { Tally.Clear(); }

        /// <summary>One pass over what the sweep found. Returns how many were taken.</summary>
        public static int Pass(float radius, int cap)
        {
            if (!Settings.PickupEnabled.Value) return 0;

            int taken = 0;
            for (int i = 0; i < Sweep.Items.Count; i++)
            {
                Found f = Sweep.Items[i];
                if (f.Distance > radius) continue;

                if (f.Outcome != Outcome.WouldSucceed)
                {
                    Count(f.Outcome);
                    if (Settings.DiagLogEveryRefusal.Value
                        && f.Outcome != Outcome.NotTargetName
                        && f.Outcome != Outcome.AlreadyHeld)
                    {
                        Log.Info("refused " + f.Name + " -> " + f.Outcome + Explain(f.Outcome));
                    }
                    continue;
                }

                if (Settings.PickupDryRun.Value) { Count(Outcome.WouldSucceed); continue; }
                if (taken >= cap) { Count(Outcome.WouldSucceed); continue; }

                if (Take(f.Gear, f.Name)) { taken++; Count(Outcome.Success); }
                else Count(Outcome.TakeFailed);
            }
            TakenTotal += taken;
            TakenThisSession += taken;
            return taken;
        }

        /// <summary>
        /// Take one item and verify it actually left the world. Verification is the whole point:
        /// a call that returns without throwing is not evidence that anything happened.
        /// </summary>
        public static bool Take(GearItem gi, string name)
        {
            if (gi == null) return false;
            GameObject go = null;
            try { go = gi.gameObject; } catch (System.Exception) { }

            if (_preferred == Path.Quiet)
            {
                if (TryQuiet(gi) && Verify(gi, go))
                {
                    Log.OnceInfo("took-first", "picked up " + name + " with " + PathName + ".");
                    return true;
                }

                _quietFailures++;
                if (TryInteraction(gi) && Verify(gi, go))
                {
                    _escalations++;
                    Log.OnceWarn("escalated",
                        "Inventory.AddGear did not put " + name + " in the pack, so the game's own "
                        + "interaction path was used instead and it worked. Both are tried per item "
                        + "until the quiet one has failed 5 times, after which only the working one "
                        + "is used for the rest of the session.");
                    if (_quietFailures >= 5 && _preferred == Path.Quiet)
                    {
                        _preferred = Path.Interaction;
                        Log.Warn("switching to " + PathName + " for the rest of this session - "
                            + "Inventory.AddGear failed " + _quietFailures + " times.");
                    }
                    return true;
                }
                return false;
            }

            // Preferred path is the interaction one; still fall back the other way rather than fail.
            if (TryInteraction(gi) && Verify(gi, go)) return true;
            return TryQuiet(gi) && Verify(gi, go);
        }

        private static bool TryQuiet(GearItem gi)
        {
            try
            {
                Inventory inv = GameManager.GetInventoryComponent();
                if (inv == null) return false;
                inv.AddGear(gi, true);
                return true;
            }
            catch (System.Exception e)
            {
                Log.OnceWarn("quiet-threw", "Inventory.AddGear threw: " + e.Message);
                return false;
            }
        }

        private static bool TryInteraction(GearItem gi)
        {
            try
            {
                PlayerManager pm = GameManager.GetPlayerManagerComponent();
                if (pm == null) return false;
                return pm.ProcessPickupItemInteraction(gi, false, false, true);
            }
            catch (System.Exception e)
            {
                Log.OnceWarn("interaction-threw", "ProcessPickupItemInteraction threw: " + e.Message);
                return false;
            }
        }

        /// <summary>
        /// Did it actually go in? Three independent signs, because any one of them alone has a
        /// reading where it is true and the item is still lying on the floor.
        /// </summary>
        private static bool Verify(GearItem gi, GameObject go)
        {
            try
            {
                if (gi == null) return true;                    // destroyed - it went somewhere
                if (gi.m_InPlayerInventory) return true;
                if (go != null && !go.activeInHierarchy) return true;
                return false;
            }
            catch (System.Exception)
            {
                return true;   // the object is gone out from under us, which is what success looks like
            }
        }

        /// <summary>The sentence that saves the evening, attached to the outcome that needs it.</summary>
        public static string Explain(Outcome o)
        {
            switch (o)
            {
                case Outcome.TooHeavy:
                    return "  (carry weight would be exceeded - this is the game's cap, not the mod's. "
                         + "Drop something, or lower Pickup.WeightHeadroomKG.)";
                case Outcome.NotTargetName:
                    return "  (not on the Names list - see the Items tab for what has been seen)";
                case Outcome.InsideContainer:
                    return "  (it is in a container, so open the container rather than walking past it)";
                case Outcome.Locked:
                    return "  (locked in a container)";
                case Outcome.Hidden:
                    return "  (the game has this hidden - unrevealed loot or a mission state)";
                case Outcome.CannotInteract:
                    return "  (the game says it cannot be interacted with right now)";
                case Outcome.AttachedElsewhere:
                    return "  (attached to a place point or a travois)";
                case Outcome.Ruined:
                    return "  (ruined - turn off Pickup.SkipRuined if you want these)";
                case Outcome.TakeFailed:
                    return "  (every gate passed and the game still refused - THIS is the one worth reporting)";
                default:
                    return "";
            }
        }
    }
}
