// The sweep: one look around, shared by everything that needs to know what is nearby.
//
// WHY ONE SWEEP AND NOT THREE
//
// Pickup wants nearby items. Highlight wants nearby items. Auto-harvest wants nearby harvestables.
// Three independent OverlapSphere loops would triple the cost of the only expensive thing this mod
// does, and would let the three disagree about what is there. So the sweep runs once, publishes a
// list, and the features read it.
//
// WHY EVERY REFUSAL HAS A NAME
//
// This is the whole point the Green Hell version was built around, and it transfers exactly. A
// pickup has to clear several gates. If any gate refuses and the mod returns silently, then "the
// mod is broken" and "the mod tried and was blocked" look IDENTICAL: nothing in the log, nothing in
// your pack. So every gate here ends in a named Outcome, and the tally counts them. When something
// stops working, the log already says which gate said no, and you have not lost an evening to it.

using System.Collections.Generic;
using Il2Cpp;
using Il2CppTLD.IntBackedUnit;
using UnityEngine;

namespace LDPickupDoctor
{
    /// Why one item was, or was not, taken. Order is roughly "most interesting first" in reports.
    internal enum Outcome
    {
        Success,            // taken
        WouldSucceed,       // passed every gate, but DryRun is on or the per-sweep cap was hit
        TooHeavy,           // would exceed carry capacity, or eat the headroom
        Ruined,             // zero condition and SkipRuined is on
        CannotInteract,     // the game itself says this cannot be interacted with right now
        InsideContainer,    // sitting in a container, not on the ground
        Locked,             // locked inside a container
        Hidden,             // hidden by the game (unrevealed loot, mission state)
        AlreadyHeld,        // already in the player inventory
        AttachedElsewhere,  // parented to a place point, travois, or similar
        NotTargetName,      // a real item, just not one on the name list
        TakeFailed,         // every gate passed and the game still refused - the interesting one
        NoGearItem          // a collider on the gear layer with no GearItem on it
    }

    internal struct Found
    {
        public GearItem Gear;
        public Harvestable Plant;
        public Container Box;
        public Transform Where;
        public float Distance;
        public Outcome Outcome;
        public string Name;
    }

    internal static class Sweep
    {
        // Rebuilt each sweep and handed to the features. A field rather than a return value because
        // OnGUI, OnUpdate and the pickup pass all read the same one within a frame.
        public static readonly List<Found> Items = new List<Found>();
        public static readonly List<Found> Plants = new List<Found>();
        public static readonly List<Found> Boxes = new List<Found>();

        public static int LastColliderCount;
        public static float LastSweepMs;
        public static int SweepCount;

        /// Names seen at least once this session, for the Items tab of the window. Kept small by
        /// being a set of item names rather than a list of instances.
        public static readonly SortedDictionary<string, int> SeenNames = new SortedDictionary<string, int>();

        private static int _mask;
        private static bool _maskReady;
        private static int _emptySweeps;
        private static bool _wideFallback;

        /// <summary>
        /// Layer mask for the sweep. Built from the game's own named layers rather than from
        /// numbers, because a layer index is a thing Hinterland can renumber in a patch and a name
        /// is not. If the names ever stop resolving we fall back to everything and SAY SO, rather
        /// than quietly sweeping an empty mask and looking broken.
        /// </summary>
        private static int Mask()
        {
            if (_maskReady) return _mask;
            try
            {
                _mask = (1 << vp_Layer.Gear)
                      | (1 << vp_Layer.InteractivePropNoCollideGear)
                      | (1 << vp_Layer.InteractiveProp)
                      | (1 << vp_Layer.Container);
                if (_mask == 0) throw new System.Exception("layer mask came out empty");
            }
            catch (System.Exception e)
            {
                _mask = ~0;
                Log.Warn("could not build a layer mask from vp_Layer (" + e.Message
                    + ") - sweeping every layer instead. Slower, but nothing is missed.");
            }
            _maskReady = true;
            return _mask;
        }

        public static void Run(Vector3 origin, float radius)
        {
            float t0 = Time.realtimeSinceStartup;
            Items.Clear(); Plants.Clear(); Boxes.Clear();
            SweepCount++;

            int mask = _wideFallback ? ~0 : Mask();
            Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Collider> hits;
            try
            {
                hits = Physics.OverlapSphere(origin, radius, mask, QueryTriggerInteraction.Collide);
            }
            catch (System.Exception e)
            {
                Log.OnceWarn("overlap-threw", "Physics.OverlapSphere threw: " + e.Message
                    + " - the sweep is skipped this frame and retried on the next one.");
                return;
            }

            LastColliderCount = hits == null ? 0 : hits.Length;

            // SELF-HEAL, and the reason it is here. In Green Hell the equivalent code found nothing
            // for a completely different reason (sleeping rigidbodies), and because it never said so,
            // "the mod is broken" was indistinguishable from "there is nothing nearby". So: if the
            // masked sweep keeps coming back empty while the player is in a loaded world, widen to
            // every layer, keep working, and say what changed. It never gives up and never lies.
            if (LastColliderCount == 0 && !_wideFallback)
            {
                _emptySweeps++;
                if (_emptySweeps >= 40)   // ~10 seconds at the default interval
                {
                    _wideFallback = true;
                    Log.Warn("40 sweeps in a row found nothing on the gear layers. Widening to every "
                        + "layer and carrying on - if items start appearing now, the layer names moved "
                        + "in a game update and that is the thing to fix.");
                }
            }
            else if (LastColliderCount > 0)
            {
                _emptySweeps = 0;
            }

            if (hits == null) { LastSweepMs = (Time.realtimeSinceStartup - t0) * 1000f; return; }

            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i];
                if (col == null) continue;
                Transform tr = col.transform;
                if (tr == null) continue;

                float dist = Vector3.Distance(origin, tr.position);

                GearItem gi = col.GetComponentInParent<GearItem>();
                if (gi != null)
                {
                    Found f = new Found();
                    f.Gear = gi;
                    f.Where = gi.transform;
                    f.Distance = Vector3.Distance(origin, gi.transform.position);
                    f.Name = NameOf(gi);
                    f.Outcome = Judge(gi, f.Name);
                    Remember(f.Name);
                    if (!Contains(Items, gi)) Items.Add(f);
                    continue;
                }

                Harvestable plant = col.GetComponentInParent<Harvestable>();
                if (plant != null)
                {
                    Found f = new Found();
                    f.Plant = plant;
                    f.Where = plant.transform;
                    f.Distance = dist;
                    f.Name = plant.gameObject == null ? "harvestable" : plant.gameObject.name;
                    f.Outcome = plant.IsHarvested() ? Outcome.AlreadyHeld : Outcome.WouldSucceed;
                    if (!ContainsPlant(Plants, plant)) Plants.Add(f);
                    continue;
                }

                Container box = col.GetComponentInParent<Container>();
                if (box != null)
                {
                    Found f = new Found();
                    f.Box = box;
                    f.Where = box.transform;
                    f.Distance = dist;
                    f.Name = box.gameObject == null ? "container" : box.gameObject.name;
                    f.Outcome = box.m_Inspected ? Outcome.AlreadyHeld : Outcome.WouldSucceed;
                    if (!ContainsBox(Boxes, box)) Boxes.Add(f);
                }
            }

            Items.Sort(delegate (Found a, Found b) { return a.Distance.CompareTo(b.Distance); });
            Plants.Sort(delegate (Found a, Found b) { return a.Distance.CompareTo(b.Distance); });
            Boxes.Sort(delegate (Found a, Found b) { return a.Distance.CompareTo(b.Distance); });
            LastSweepMs = (Time.realtimeSinceStartup - t0) * 1000f;
        }

        private static bool Contains(List<Found> list, GearItem gi)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].Gear == gi) return true;
            return false;
        }
        private static bool ContainsPlant(List<Found> list, Harvestable h)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].Plant == h) return true;
            return false;
        }
        private static bool ContainsBox(List<Found> list, Container c)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].Box == c) return true;
            return false;
        }

        private static void Remember(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            int n;
            SeenNames[name] = SeenNames.TryGetValue(name, out n) ? n + 1 : 1;
        }

        public static string NameOf(GearItem gi)
        {
            if (gi == null) return "";
            // Display name first because it is what he reads on screen; the object name is the
            // fallback because the display name goes through localisation and can come back empty
            // before the tables are loaded.
            try
            {
                string dn = gi.DisplayName;
                if (!string.IsNullOrEmpty(dn)) return dn;
            }
            catch (System.Exception) { }
            try
            {
                if (gi.gameObject != null) return gi.gameObject.name.Replace("(Clone)", "");
            }
            catch (System.Exception) { }
            return "item";
        }

        /// <summary>
        /// Every gate, in the order that answers the question fastest, each one naming itself.
        /// Nothing here touches the world - judging must be free of side effects so that the
        /// highlight can use the same verdict without picking anything up.
        /// </summary>
        public static Outcome Judge(GearItem gi, string name)
        {
            if (gi == null) return Outcome.NoGearItem;

            if (gi.m_InPlayerInventory) return Outcome.AlreadyHeld;
            if (gi.m_LockedInContainer) return Outcome.Locked;
            if (gi.m_InsideContainer) return Outcome.InsideContainer;
            if (gi.m_IsHidden) return Outcome.Hidden;

            bool canInteract;
            try { canInteract = gi.CanInteract; } catch (System.Exception) { canInteract = true; }
            if (!canInteract) return Outcome.CannotInteract;

            try { if (gi.IsAttachedToPlacePoint()) return Outcome.AttachedElsewhere; }
            catch (System.Exception) { }

            if (Settings.PickupSkipRuined.Value)
            {
                try { if (gi.m_CurrentHP <= 0f) return Outcome.Ruined; }
                catch (System.Exception) { }
            }

            if (!Matches(name, gi)) return Outcome.NotTargetName;

            if (Settings.PickupRespectWeight.Value && !WeightAllows(gi)) return Outcome.TooHeavy;

            return Outcome.WouldSucceed;
        }

        public static bool Matches(string name, GearItem gi)
        {
            if (Settings.PickupAllItems.Value) return true;
            string list = Settings.PickupNames.Value;
            if (string.IsNullOrEmpty(list)) return false;

            string haystack = (name == null ? "" : name).ToLowerInvariant();
            string objName = "";
            try { if (gi != null && gi.gameObject != null) objName = gi.gameObject.name.ToLowerInvariant(); }
            catch (System.Exception) { }

            string[] parts = list.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string needle = parts[i].Trim().ToLowerInvariant();
                if (needle.Length == 0) continue;
                if (haystack.Contains(needle) || objName.Contains(needle)) return true;
            }
            return false;
        }

        /// <summary>
        /// Would taking this put him over the line? Weight is the game's own price for hoarding and
        /// this mod does not discount it - so the answer is computed from the game's own numbers
        /// rather than from a figure of ours.
        /// </summary>
        public static bool WeightAllows(GearItem gi)
        {
            try
            {
                Inventory inv = GameManager.GetInventoryComponent();
                Encumber enc = GameManager.GetEncumberComponent();
                if (inv == null || enc == null) return true;   // no numbers, no refusal

                ItemWeight current = inv.GetTotalWeightKG();
                ItemWeight max = enc.m_MaxCarryCapacity;
                ItemWeight item = gi.WeightKG;
                ItemWeight headroom = ItemWeight.FromKilograms(
                    Mathf.Max(0f, Settings.PickupWeightHeadroomKG.Value));

                return current + item + headroom <= max;
            }
            catch (System.Exception e)
            {
                Log.OnceWarn("weight-threw",
                    "could not read carry weight (" + e.Message + ") - weight gating is skipped until "
                    + "it reads again. Pickup still works; it just will not stop you at the cap.");
                return true;
            }
        }

        public static float KG(ItemWeight w)
        {
            try { return w / ItemWeight.FromKilograms(1f); }
            catch (System.Exception) { return 0f; }
        }
    }
}
