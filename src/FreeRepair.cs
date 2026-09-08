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

            int ok = 0;
            ok += Patch(harmony, "GetNumMaterialsRequired", nameof(ZeroInt), 0);
            ok += Patch(harmony, "GetRequiredGearUnits", nameof(ZeroInt), 1);
            ok += Patch(harmony, "GetDurationMinutes", nameof(ZeroInt), 0);

            _patched = true;
            Applied = ok;

            if (ok == 3)
            {
                Log.Info("free repair is available - the three questions the repair screen asks about "
                    + "cost and time are patched, and they only answer zero while the switch is on.");
            }
            else
            {
                // SAY SO RATHER THAN LEAVE A DEAD SWITCH. A cheat that silently cannot work is the
                // exact failure this mod was written to refuse.
                Log.Warn("free repair could only patch " + ok + " of its 3 methods, so the switch may "
                    + "not do what it says. The method names on Repairable have probably moved.");
            }
        }

        private static int Patch(HarmonyLib.Harmony harmony, string method, string postfix, int argCount)
        {
            try
            {
                System.Reflection.MethodInfo target = null;
                System.Reflection.MethodInfo[] all = typeof(Repairable).GetMethods();
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].Name != method) continue;
                    if (all[i].GetParameters().Length != argCount) continue;
                    target = all[i];
                    break;
                }
                if (target == null)
                {
                    Log.Warn("free repair: Repairable." + method + " with " + argCount
                        + " argument(s) was not found.");
                    return 0;
                }

                System.Reflection.MethodInfo post = typeof(FreeRepair).GetMethod(postfix,
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                harmony.Patch(target, postfix: new HarmonyMethod(post));
                return 1;
            }
            catch (System.Exception e)
            {
                Log.Warn("free repair: patching Repairable." + method + " threw: " + e.Message);
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

        public static int Uses;
    }
}
