// Repair anything, at no cost.
//
// WHY THIS ONE IS A HARMONY PATCH WHEN NOTHING ELSE IN THIS MOD IS
//
// Every other cheat here writes a field on an object it can reach. That does not work for repair,
// and the reason is worth writing down before somebody tries it again: items in the pack are
// INACTIVE GameObjects. FindObjectsOfType does not return components on inactive objects, so a pass
// that walks the scene would find the rifle in your hands and nothing else - and the whole point is
// clicking an item in the inventory.
//
// So this patches the three questions the repair screen asks, at the source:
//
//     Repairable.GetNumMaterialsRequired()   how many kinds of material
//     Repairable.GetRequiredGearUnits(int)   how much of each
//     Repairable.GetDurationMinutes()        how long it takes
//
// Answer zero to all three and the repair costs nothing and takes no time, whether the item is in
// your hands, in your pack, or in a container you have not opened yet. The game does the repairing;
// this only changes the price it quotes.
//
// EVERY PATCH IS GATED ON THE SWITCH. With the cheat off they return exactly what the game returned,
// so the patches are inert rather than absent - which is what makes the switch reversible without a
// restart.

using HarmonyLib;
using Il2Cpp;
using MelonLoader;

namespace LDPickupDoctor
{
    internal static class FreeRepair
    {
        public static int Applied;
        private static bool _patched;

        public static void Apply(HarmonyLib.Harmony harmony)
        {
            if (_patched) return;

            // THE PRICE AND THE PERMISSION ARE TWO DIFFERENT GATES, which is why zeroing the cost was
            // not enough on its own. The repair screen asks Repairable what it costs, and then asks
            // ITSELF whether repairing is allowed - and that second answer is what paints the button
            // red. Both have to say yes.
            int ok = 0;

            // What it costs.
            ok += PatchOn(harmony, typeof(Repairable), "GetNumMaterialsRequired", nameof(ZeroInt), 0, false);
            ok += PatchOn(harmony, typeof(Repairable), "GetRequiredGearUnits", nameof(ZeroInt), 1, false);
            ok += PatchOn(harmony, typeof(Repairable), "GetDurationMinutes", nameof(ZeroInt), 0, false);

            // Whether it is allowed at all - the red button.
            ok += PatchOn(harmony, typeof(Panel_Inventory_Examine), "CanRepair", nameof(TrueBool), 0, false);
            ok += PatchOn(harmony, typeof(Panel_Inventory_Examine), "RepairHasRequiredTool", nameof(TrueBool), 0, false);

            // And do not take the materials it decided were free. A prefix that returns false skips
            // the original entirely, which is the only honest way to spend nothing.
            ok += PatchOn(harmony, typeof(Panel_Inventory_Examine), "ConsumeMaterialsUsedForRepair",
                nameof(SkipWhenFree), 0, true);

            _patched = true;
            Applied = ok;

            if (ok == 6)
            {
                Log.Info("free repair is available - cost, permission and consumption are all patched, "
                    + "and every one of them answers normally while the switch is off.");
            }
            else
            {
                // SAY SO RATHER THAN LEAVE A DEAD SWITCH. A cheat that silently cannot work is the
                // exact failure this mod was written to refuse.
                Log.Warn("free repair patched " + ok + " of its 6 methods, so the switch may not do "
                    + "everything it says. Whichever names moved are named in the warnings above.");
            }
        }

        private static int PatchOn(HarmonyLib.Harmony harmony, System.Type type, string method,
                                   string helper, int argCount, bool asPrefix)
        {
            try
            {
                System.Reflection.MethodInfo target = null;
                System.Reflection.MethodInfo[] all = type.GetMethods();
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].Name != method) continue;
                    if (all[i].GetParameters().Length != argCount) continue;
                    target = all[i];
                    break;
                }
                if (target == null)
                {
                    Log.Warn("free repair: " + type.Name + "." + method + " with " + argCount
                        + " argument(s) was not found - that part of the switch will do nothing.");
                    return 0;
                }

                System.Reflection.MethodInfo fn = typeof(FreeRepair).GetMethod(helper,
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                if (asPrefix) harmony.Patch(target, prefix: new HarmonyMethod(fn));
                else harmony.Patch(target, postfix: new HarmonyMethod(fn));
                return 1;
            }
            catch (System.Exception e)
            {
                Log.Warn("free repair: patching " + type.Name + "." + method + " threw: " + e.Message);
                return 0;
            }
        }

        /// <summary>Zero the answer, but only while the switch is on.</summary>
        private static void ZeroInt(ref int __result)
        {
            if (!Settings.CheatFreeRepair.Value) return;
            if (__result != 0) Uses++;
            __result = 0;
        }

        /// <summary>Say yes, but only while the switch is on. This is what un-reds the button.</summary>
        private static void TrueBool(ref bool __result)
        {
            if (!Settings.CheatFreeRepair.Value) return;
            if (!__result) Uses++;
            __result = true;
        }

        /// <summary>Returning false skips the original, so nothing is taken from the pack.</summary>
        private static bool SkipWhenFree()
        {
            return !Settings.CheatFreeRepair.Value;
        }

        public static int Uses;
    }
}
