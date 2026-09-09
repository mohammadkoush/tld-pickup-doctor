// Which stat is moving, and when.
//
// WHY THIS EXISTS
//
// Reported from play: "one stat at a time gets drained randomly, not all at once". The explanation
// offered for it - a daylight dial that compressed the night and so ran game time fast after dark -
// was WRONG, and it was wrong in a way worth remembering: that mechanism would have sped up every
// drain together. One stat at a time does not fit it, and the person playing said so.
//
// So the cause is unknown, and the answer to an unknown cause on this station is not a better guess.
// It is a measurement. This samples the five survival stats every few seconds and writes a line
// whenever one of them moves faster than it should, naming which one, by how much, and what the mod
// was doing to that system at the time.
//
// It costs five field reads every two seconds and it is off by default.

using Il2Cpp;
using UnityEngine;

namespace LDPickupDoctor
{
    internal static class StatWatch
    {
        private struct Sample
        {
            public float Condition, Hunger, Thirst, Fatigue, Freezing;
            public bool Valid;
        }

        private static Sample _last;
        private static float _nextAt;
        private static float _lastAt;
        public static int Alarms;

        public static void Tick()
        {
            if (!Settings.WatchStats.Value) return;

            float now = Time.realtimeSinceStartup;
            if (now < _nextAt) return;
            _nextAt = now + 2f;

            Sample s = new Sample();
            try
            {
                Condition c = GameManager.GetConditionComponent();
                Hunger h = GameManager.GetHungerComponent();
                Thirst t = GameManager.GetThirstComponent();
                Fatigue f = GameManager.GetFatigueComponent();
                Freezing z = GameManager.GetFreezingComponent();
                if (c == null || h == null || t == null || f == null || z == null) return;

                s.Condition = c.m_CurrentHP;
                s.Hunger = h.m_CurrentReserveCalories;
                s.Thirst = t.m_CurrentThirst;
                s.Fatigue = f.m_CurrentFatigue;
                s.Freezing = z.m_CurrentFreezing;
                s.Valid = true;
            }
            catch (System.Exception e)
            {
                Log.OnceWarn("statwatch", "the stat watch could not read a value: " + e.Message
                    + " - it is off for this session and nothing else is affected.");
                return;
            }

            float dt = now - _lastAt;
            _lastAt = now;

            if (!_last.Valid || dt <= 0f || dt > 10f) { _last = s; return; }

            // THRESHOLDS PER SECOND OF REAL TIME, set high enough that ordinary play is silent and
            // low enough that anything alarming is caught. They are deliberately per-stat: calories
            // are counted in thousands and condition in hundreds, so one number could not serve both.
            Check("condition", _last.Condition - s.Condition, dt, 0.5f, s.Condition);
            Check("calories", _last.Hunger - s.Hunger, dt, 20f, s.Hunger);
            Check("thirst", s.Thirst - _last.Thirst, dt, 1.0f, s.Thirst);
            Check("fatigue", s.Fatigue - _last.Fatigue, dt, 1.0f, s.Fatigue);
            Check("freezing", s.Freezing - _last.Freezing, dt, 1.0f, s.Freezing);

            _last = s;
        }

        /// <summary>
        /// One stat, one direction, one threshold. The line names the rate AND what the mod is doing
        /// to that system, because "fatigue fell fast" and "fatigue fell fast while the tiredness
        /// dial was at 0.00" are different reports and only the second one is useful.
        /// </summary>
        private static void Check(string name, float worse, float dt, float perSecondLimit, float now)
        {
            float rate = worse / dt;
            if (rate < perSecondLimit) return;

            Alarms++;
            Log.Warn("stat watch: " + name + " moved " + rate.ToString("0.0")
                + " per second (now " + now.ToString("0.0") + "). Mod dials at this moment - "
                + "cold " + Settings.RateCold.Value.ToString("0.00")
                + ", tired " + Settings.RateTired.Value.ToString("0.00")
                + ", thirst " + Settings.RateThirst.Value.ToString("0.00")
                + ", hunger " + Settings.RateHunger.Value.ToString("0.00")
                + ", daylight " + Settings.RateDaylight.Value.ToString("0.00")
                + ", buffsHeld " + Cheats.BuffsHeld + ".");
        }
    }
}
