// The report. This is the half of the mod that is not a feature.
//
// COUNT BEFORE FIXING. A log line that says "it did not work" is worth almost nothing; a line that
// says WHICH gate refused, and HOW OFTEN, is the whole answer. The Green Hell version proved that
// the loudest failure is usually not the commonest one - and if nothing is counted, the loudest one
// wins the attention every time.
//
// So every report carries three things: the state (weight, radius, what is loaded), the tally (what
// each gate decided, counted), and the repeats (which once-only warnings have been firing silently).

using System.Collections.Generic;
using Il2Cpp;
using Il2CppTLD.IntBackedUnit;

namespace LDPickupDoctor
{
    internal static class Diagnostics
    {
        private static float _lastReport;

        public static void Tick(float now)
        {
            if (!Settings.DiagEnabled.Value) return;
            float every = System.Math.Max(5f, Settings.DiagReportSeconds.Value);
            if (now - _lastReport < every) return;
            _lastReport = now;
            Report(false);
        }

        public static void Report(bool forced)
        {
            // State first, because it answers the commonest question before it is asked.
            string weight = "unknown";
            try
            {
                Inventory inv = GameManager.GetInventoryComponent();
                Encumber enc = GameManager.GetEncumberComponent();
                if (inv != null && enc != null)
                {
                    float have = Sweep.KG(inv.GetTotalWeightKG());
                    float max = Sweep.KG(enc.m_MaxCarryCapacity);
                    weight = have.ToString("0.0") + "/" + max.ToString("0.0") + " kg";
                }
            }
            catch (System.Exception) { }

            Log.Info("[state] weight " + weight
                + "  sweeps=" + Sweep.SweepCount
                + "  lastSweep=" + Sweep.LastSweepMs.ToString("0.00") + "ms"
                + "  nearby items=" + Sweep.Items.Count
                + " plants=" + Sweep.Plants.Count
                + " boxes=" + Sweep.Boxes.Count
                + "  outlines=" + Silhouette.Count
                + "  path=" + Pickup.PathName);

            if (Pickup.Tally.Count == 0)
            {
                Log.Info("[tally] nothing decided yet this window.");
            }
            else
            {
                string line = "";
                foreach (KeyValuePair<Outcome, int> kv in Pickup.Tally)
                {
                    // NotTargetName is most of the world and is not news; it is counted, not printed,
                    // unless it is the ONLY thing happening, which is itself the answer to
                    // "why is nothing being picked up".
                    if (kv.Key == Outcome.NotTargetName && Pickup.Tally.Count > 1) continue;
                    line += "  " + kv.Key + "=" + kv.Value;
                }
                if (line.Length == 0) line = "  NotTargetName only - nothing nearby is on the Names list";
                Log.Info("[tally] taken=" + Pickup.TakenTotal
                    + " harvested=" + Grind.HarvestedTotal + line);
            }

            // SAY SO. A tally read back a week later must not have to guess why the numbers look
            // wrong; if a cheat was on, it is on the line above them.
            if (Cheats.AnyOn()) Log.Info("[cheats]" + Cheats.Active());

            bool anyRepeat = false;
            foreach (KeyValuePair<string, int> kv in Log.Repeats())
            {
                if (!anyRepeat) { Log.Info("[repeats] warnings that fired more than once:"); anyRepeat = true; }
                Log.Info("   " + kv.Key + " x" + kv.Value);
            }

            Pickup.ResetTally();
        }
    }
}
