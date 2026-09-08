// Cheats. Added for testing, and kept because they are useful for testing.
//
// THIS FILE IS THE EXCEPTION TO THE MOD'S RULE, AND IT IS FENCED OFF FOR THAT REASON.
//
// Everything else here removes repetition and leaves the game's price exactly where Hinterland put
// it. This removes the price. That is not a slip, it is the request: a pickup radius cannot be tested
// against a carry cap that keeps being hit, outline colours cannot be judged across a map that has
// to be walked, and a full clip on the HUD cannot be checked without a full clip.
//
// So it lives in its own file, its own preferences category and its own tab, everything is off or
// neutral by default, and the diagnostic report names every cheat that is on while it is on - so a
// strange tally read back next week has "speed was at 3x" sitting above it rather than a mystery.
//
// THE ONE ENGINEERING RULE THIS FILE HOLDS TO: EVERY CHEAT IS REVERSIBLE.
//
// Nothing is set without its original being stored first, and nothing stays set after its switch
// goes off. That is not politeness - three of these write to objects the game SERIALISES, and a
// cheat that forgets what it overwrote is a cheat that quietly edits a save someone keeps.

using System.Collections.Generic;
using Il2Cpp;
using Il2CppTLD.Gear;
using Il2CppTLD.IntBackedUnit;
using UnityEngine;

namespace LDPickupDoctor
{
    internal static class Cheats
    {
        // ---- speed -------------------------------------------------------------------------------
        private static vp_FPSController _controller;
        private static float _origAcceleration;
        private static bool _haveAcceleration;

        // The baseline acceleration, KEYED BY CONTROLLER INSTANCE.
        //
        // The first build of this stored one loose float and re-captured it whenever the controller
        // handle went stale. The log caught it within a minute of shipping:
        //
        //     speed cheat on - the game's own acceleration is 0.0300, now scaled by 2.29x
        //     speed cheat on - the game's own acceleration is 0.0687, now scaled by 2.29x
        //
        // 0.0300 x 2.29 is 0.0687. The second capture read OUR OWN write and called it the game's
        // number, so every re-lookup multiplied the speed again - and the "original" it would put
        // back on the way out was wrong too. Keyed by instance id, a controller we have already seen
        // is never re-baselined, and a genuinely new one starts from its own clean value.
        private static readonly Dictionary<int, float> _origAccelById = new Dictionary<int, float>();

        // ---- carry -------------------------------------------------------------------------------
        //
        // Keyed by Encumber instance id for exactly the reason the speed cheat is, and it was caught
        // the same way - by reading the log rather than by thinking about it:
        //
        //     carry cheat on - the game's cap is 30.0 kg, raised to 500.0 kg
        //     carry cheat on - the game's cap is 500.0 kg, raised to 500.0 kg
        //
        // A scene initialise cleared the "we have the original" flag, the next sweep re-captured, and
        // what it captured was our own 500. Turning the cheat off would then have restored 500 kg as
        // though the game had always allowed it - a permanent edit to a save, from a switch whose
        // whole promise is that it is reversible.
        //
        // Encumber is a session-long singleton, so its baseline must SURVIVE a scene change. That is
        // why this map is not cleared in ForgetScene while the per-scene ones are.
        private static readonly Dictionary<int, ItemWeight> _origCapacityById = new Dictionary<int, ItemWeight>();
        private static ItemWeight _origCapacity;
        private static bool _haveCapacity;

        // ---- harvest durations -------------------------------------------------------------------
        private static readonly Dictionary<int, int> _origHarvestMinutes = new Dictionary<int, int>();
        private static readonly Dictionary<int, float> _origQuarterMinutes = new Dictionary<int, float>();

        // ---- fires -------------------------------------------------------------------------------
        private static readonly Dictionary<int, bool> _origPerpetual = new Dictionary<int, bool>();
        private static float _nextFireScan;
        private static readonly List<Fire> _fires = new List<Fire>();
        private static float _fireLifeWas = -1f;
        private static int _fireLifeFalling;

        // ---- ammo --------------------------------------------------------------------------------
        private static int _ammoTopUps;

        // ---- survival rates ----------------------------------------------------------------------
        // One stored original per field, keyed by a name rather than an instance id: these five are
        // singletons that live for the session, so a name is stable where an id is not.
        private static readonly Dictionary<string, float> _origRates = new Dictionary<string, float>();
        private static readonly HashSet<string> _scaledKeys = new HashSet<string>();
        private static bool _sprintWas;
        private static bool _haveSprintWas;

        /// <summary>One line for the report, naming everything that is currently on.</summary>
        public static string Active()
        {
            string s = "";
            if (!Mathf.Approximately(Settings.RateCold.Value, 1f)) s += " cold=" + Settings.RateCold.Value.ToString("0.00") + "x";
            if (!Mathf.Approximately(Settings.RateTired.Value, 1f)) s += " tired=" + Settings.RateTired.Value.ToString("0.00") + "x";
            if (!Mathf.Approximately(Settings.RateThirst.Value, 1f)) s += " thirst=" + Settings.RateThirst.Value.ToString("0.00") + "x";
            if (!Mathf.Approximately(Settings.RateHunger.Value, 1f)) s += " hunger=" + Settings.RateHunger.Value.ToString("0.00") + "x";
            if (!Mathf.Approximately(Settings.RateStamina.Value, 1f)) s += " stamina=" + Settings.RateStamina.Value.ToString("0.00") + "x";
            if (!Mathf.Approximately(Settings.RateHeldFuel.Value, 1f))
                s += " fuel=" + (FuelIsInfinite ? "infinite" : Settings.RateHeldFuel.Value.ToString("0.00") + "x");
            if (FeatsOn > 0) s += " feats=" + FeatsOn;
            if (!Mathf.Approximately(Settings.CheatSpeed.Value, 1f))
                s += " speed=" + Settings.CheatSpeed.Value.ToString("0.00") + "x";
            if (Settings.CheatInstantHarvest.Value) s += " instantHarvest";
            if (Settings.CheatUnlimitedCarry.Value) s += " carry=" + Settings.CheatCarryKG.Value.ToString("0") + "kg";
            if (Settings.CheatUnlimitedAmmo.Value) s += " ammo(" + _ammoTopUps + " topups)";
            if (Settings.CheatPerpetualFire.Value) s += " perpetualFire(" + _fires.Count + ")";
            return s;
        }

        public static bool AnyOn()
        {
            return !Mathf.Approximately(Settings.CheatSpeed.Value, 1f)
                || Settings.CheatInstantHarvest.Value
                || Settings.CheatUnlimitedCarry.Value
                || Settings.CheatUnlimitedAmmo.Value
                || Settings.CheatPerpetualFire.Value
                || !Mathf.Approximately(Settings.RateCold.Value, 1f)
                || !Mathf.Approximately(Settings.RateTired.Value, 1f)
                || !Mathf.Approximately(Settings.RateThirst.Value, 1f)
                || !Mathf.Approximately(Settings.RateHunger.Value, 1f)
                || !Mathf.Approximately(Settings.RateStamina.Value, 1f)
                || !Mathf.Approximately(Settings.RateHeldFuel.Value, 1f)
                || FeatsOn > 0;
        }

        /// <summary>Every frame. Only the three that have to be: speed, the clip, and the held item.</summary>
        public static void FastTick()
        {
            Speed();
            Ammo();
            HeldFuel();
        }

        // ------------------------------------------------------------------------------------------
        // FUEL DRAIN IN THE ITEM BEING HELD
        //
        // Four different fields, because the game measures four different things and none of them is
        // "fuel":
        //
        //   KeroseneLampItem.m_FuelBurnPerHour   litres an hour        - scaled UP to drain faster
        //   TorchItem.m_BurnLifetimeMinutes      total minutes of life - scaled DOWN to drain faster
        //   FlareItem.m_BurnLifetimeMinutes      the same
        //   FlashlightItem.m_LowBeamDuration     seconds of battery    - the same
        //
        // So the dial cannot simply be multiplied through: a rate and a lifetime move in opposite
        // directions. Below 1.00 always means "lasts longer", whichever field is behind it, which is
        // the only thing worth being consistent about here.
        //
        // Held items only, as asked. A lantern left burning on a table is somebody's light source in
        // a place they chose, not the thing in hand, and quietly doubling its life is a different
        // feature that nobody switched on.
        // ------------------------------------------------------------------------------------------
        private static readonly Dictionary<int, float> _origFuel = new Dictionary<int, float>();
        private static readonly HashSet<int> _fuelScaled = new HashSet<int>();

        /// <summary>The bottom of the dial is not "very slow", it is "does not run out".</summary>
        private static bool FuelIsInfinite { get { return Settings.RateHeldFuel.Value <= 0.06f; } }

        private static void HeldFuel()
        {
            GearItem held = null;
            try
            {
                PlayerManager pm = GameManager.GetPlayerManagerComponent();
                if (pm != null) held = pm.m_ItemInHands;
            }
            catch (System.Exception) { }
            if (held == null) return;
            ApplyFuelTo(held);
        }

        /// <summary>
        /// The same treatment for things that were put down rather than held: a lantern left burning
        /// on a table, a torch stuck in the snow. Once a second rather than every frame, because it
        /// means asking the scene for every light source and none of them changes that fast.
        /// </summary>
        private static float _nextPlacedFuelScan;

        private static void PlacedFuel()
        {
            if (!Settings.FuelIncludesPlaced.Value) return;
            if (Mathf.Approximately(Settings.RateHeldFuel.Value, 1f)) return;

            float now = Time.realtimeSinceStartup;
            if (now < _nextPlacedFuelScan) return;
            _nextPlacedFuelScan = now + 1f;

            float m = FuelMultiplier();
            try
            {
                Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<KeroseneLampItem> lamps =
                    Object.FindObjectsOfType<KeroseneLampItem>();
                for (int i = 0; i < lamps.Length; i++) ApplyLamp(lamps[i], m);
            }
            catch (System.Exception e) { FuelComplain("placed lamps", e); }

            try
            {
                Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<TorchItem> torches =
                    Object.FindObjectsOfType<TorchItem>();
                for (int i = 0; i < torches.Length; i++) ApplyTorch(torches[i], m);
            }
            catch (System.Exception e) { FuelComplain("placed torches", e); }

            try
            {
                Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<FlareItem> flares =
                    Object.FindObjectsOfType<FlareItem>();
                for (int i = 0; i < flares.Length; i++) ApplyFlare(flares[i], m);
            }
            catch (System.Exception e) { FuelComplain("placed flares", e); }

            try
            {
                Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<FlashlightItem> lights =
                    Object.FindObjectsOfType<FlashlightItem>();
                for (int i = 0; i < lights.Length; i++) ApplyFlashlight(lights[i], m);
            }
            catch (System.Exception e) { FuelComplain("placed flashlights", e); }
        }

        private static float FuelMultiplier()
        {
            return Mathf.Clamp(Settings.RateHeldFuel.Value, 0.01f, 5f);
        }

        private static void ApplyFuelTo(GearItem gi)
        {
            if (gi == null) return;
            float m = FuelMultiplier();
            try { ApplyLamp(gi.m_KeroseneLampItem, m); } catch (System.Exception e) { FuelComplain("lamp", e); }
            try { ApplyTorch(gi.m_TorchItem, m); } catch (System.Exception e) { FuelComplain("torch", e); }
            try { ApplyFlare(gi.m_FlareItem, m); } catch (System.Exception e) { FuelComplain("flare", e); }
            try { ApplyFlashlight(gi.m_FlashlightItem, m); } catch (System.Exception e) { FuelComplain("flashlight", e); }
        }

        // A RATE: multiply. Litres an hour goes up when the dial goes up. Infinite means zero.
        private static void ApplyLamp(KeroseneLampItem lamp, float m)
        {
            if (lamp == null) return;
            float litres = Sweep.Litres(lamp.m_FuelBurnPerHour);
            float want = FuelIsInfinite
                ? FuelHold(lamp.GetInstanceID(), litres, 0f)
                : FuelScale(lamp.GetInstanceID(), litres, m);
            lamp.m_FuelBurnPerHour = ItemLiquidVolume.FromLiters(want);
        }

        // A LIFETIME: divide. More minutes of life is a slower drain. Infinite is a very large number
        // rather than infinity, because a NaN or an infinity in a progress bar is a crash waiting.
        private const float Forever = 10000000f;

        private static void ApplyTorch(TorchItem torch, float m)
        {
            if (torch == null) return;
            float mins = torch.m_BurnLifetimeMinutes;
            torch.m_BurnLifetimeMinutes = FuelIsInfinite
                ? FuelHold(torch.GetInstanceID(), mins, Forever)
                : FuelScale(torch.GetInstanceID(), mins, 1f / m);
        }

        private static void ApplyFlare(FlareItem flare, float m)
        {
            if (flare == null) return;
            float mins = flare.m_BurnLifetimeMinutes;
            flare.m_BurnLifetimeMinutes = FuelIsInfinite
                ? FuelHold(flare.GetInstanceID(), mins, Forever)
                : FuelScale(flare.GetInstanceID(), mins, 1f / m);
        }

        private static void ApplyFlashlight(FlashlightItem light, float m)
        {
            if (light == null) return;
            int id = light.GetInstanceID();
            light.m_LowBeamDuration = FuelIsInfinite
                ? FuelHold(id * 2, light.m_LowBeamDuration, Forever)
                : FuelScale(id * 2, light.m_LowBeamDuration, 1f / m);
            light.m_HighBeamDuration = FuelIsInfinite
                ? FuelHold(id * 2 + 1, light.m_HighBeamDuration, Forever)
                : FuelScale(id * 2 + 1, light.m_HighBeamDuration, 1f / m);
        }

        /// <summary>
        /// Force a value while remembering the real one. Same baseline discipline as FuelScale: the
        /// first call records what the game had, every call after returns the forced value, and the
        /// recorded number is never taken from something already written here.
        /// </summary>
        private static float FuelHold(int id, float live, float forced)
        {
            if (!_origFuel.ContainsKey(id)) _origFuel[id] = live;
            _fuelScaled.Add(id);
            return forced;
        }

        /// <summary>
        /// The same baseline discipline as Scale, keyed by instance because these are real objects
        /// rather than singletons: remember the base while the dial is neutral, restore it on the way
        /// back to neutral, and never compute a new value from a value already written here.
        /// </summary>
        private static float FuelScale(int id, float live, float multiplier)
        {
            if (Mathf.Approximately(multiplier, 1f))
            {
                if (_fuelScaled.Contains(id))
                {
                    _fuelScaled.Remove(id);
                    float restored = _origFuel[id];
                    _origFuel.Remove(id);
                    return restored;
                }
                return live;
            }

            float baseline;
            if (!_origFuel.TryGetValue(id, out baseline))
            {
                baseline = live;
                _origFuel[id] = baseline;
            }
            _fuelScaled.Add(id);
            return baseline * multiplier;
        }

        /// <summary>What the fuel dial is doing right now, in one line for the window.</summary>
        public static string FuelSummary()
        {
            if (Mathf.Approximately(Settings.RateHeldFuel.Value, 1f)) return "off - the game's own rates";
            if (FuelIsInfinite) return "infinite" + (Settings.FuelIncludesPlaced.Value ? ", placed included" : ", held only");
            return (Settings.RateHeldFuel.Value < 1f ? "lasts longer" : "burns quicker")
                + (Settings.FuelIncludesPlaced.Value ? ", placed included" : ", held only");
        }

        private static void FuelComplain(string what, System.Exception e)
        {
            Log.OnceWarn("fuel-" + what, "the " + what + " burn rate could not be set: " + e.Message
                + " - it is retried on the next frame and the dial stays where it was put.");
        }

        /// <summary>Once per sweep. The ones that walk lists.</summary>
        public static void SlowTick()
        {
            Carry();
            HarvestDurations();
            Fires();
            Rates();
            PlacedFuel();
            Feats();
        }

        // ------------------------------------------------------------------------------------------
        // FEATS - the game's own positive effects
        //
        // Thirteen of them, and the game keeps every one in FeatsManager.m_Feats, so there is no list
        // to hard-code here: the switches are generated from the FeatType enum and matched against
        // whatever the manager is holding.
        //
        // THESE ARE THE ONE THING ON THIS PAGE THAT REACHES THE SAVE FILE. A feat is a permanent
        // unlock with its own save data, so "reversible" here cannot mean "stops applying when the
        // game restarts" - it has to mean actively putting back what was found. So each one stores
        // the progress it had and whether it was in the enabled list, and turning the switch off
        // writes both back.
        // ------------------------------------------------------------------------------------------
        private static readonly Dictionary<int, float> _featProgressWas = new Dictionary<int, float>();
        private static readonly HashSet<int> _featWasEnabled = new HashSet<int>();
        private static readonly HashSet<int> _featTouched = new HashSet<int>();

        public static int FeatsOn;
        public static int FeatsSeen;

        private static void Feats()
        {
            Il2CppSystem.Collections.Generic.List<Feat> all;
            try { all = FeatsManager.m_Feats; }
            catch (System.Exception e)
            {
                Log.OnceWarn("feats-list", "FeatsManager.m_Feats could not be read (" + e.Message
                    + ") - the feat switches are doing nothing, and this is them saying so.");
                return;
            }
            if (all == null) return;

            FeatsSeen = all.Count;
            int on = 0;

            // An empty list with switches turned on is the silent no-op this whole mod exists to
            // refuse: the tab would show them on and nothing at all would happen. Say it once.
            if (FeatsSeen == 0)
            {
                bool anyWanted = false;
                for (int i = 0; i < Settings.FeatOrder.Count; i++)
                    if (Settings.FeatSwitches[Settings.FeatOrder[i]].Value) { anyWanted = true; break; }
                if (anyWanted)
                {
                    Log.OnceWarn("feats-empty", "feat switches are on but FeatsManager is holding no "
                        + "feats yet - they are instantiated when a run loads, so this is normal in a "
                        + "menu and a real problem in a world. It keeps trying every sweep.");
                }
                return;
            }
            Log.Forget("feats-empty");

            for (int i = 0; i < all.Count; i++)
            {
                Feat f = all[i];
                if (f == null) continue;

                MelonLoader.MelonPreferences_Entry<bool> entry;
                Il2Cpp.FeatType type;
                try { type = f.m_FeatType; } catch (System.Exception) { continue; }
                if (!Settings.FeatSwitches.TryGetValue(type, out entry)) continue;

                int key = (int)type;

                try
                {
                    if (entry.Value)
                    {
                        if (!_featTouched.Contains(key))
                        {
                            _featProgressWas[key] = f.GetNormalizedProgress();
                            if (IsEnabledForRun(type)) _featWasEnabled.Add(key);
                            _featTouched.Add(key);
                            Log.Info("feat " + Settings.Spaced(type.ToString()) + " on (its progress was "
                                + _featProgressWas[key].ToString("0.00") + ").");
                        }

                        // Written every sweep rather than once, because the game recomputes progress
                        // as it is earned and would walk an unlocked feat back down again.
                        if (f.GetNormalizedProgress() < 1f) f.SetNormalizedProgress(1f);
                        EnableForRun(type, true);
                        on++;
                    }
                    else if (_featTouched.Contains(key))
                    {
                        float was;
                        _featProgressWas.TryGetValue(key, out was);
                        f.SetNormalizedProgress(was);
                        EnableForRun(type, _featWasEnabled.Contains(key));
                        _featTouched.Remove(key);
                        _featWasEnabled.Remove(key);
                        _featProgressWas.Remove(key);
                        Log.Info("feat " + Settings.Spaced(type.ToString()) + " off - progress put back to "
                            + was.ToString("0.00") + ".");
                    }
                }
                catch (System.Exception e)
                {
                    Log.OnceWarn("feat-" + type, "the " + Settings.Spaced(type.ToString())
                        + " feat could not be set: " + e.Message + " - retried on the next sweep.");
                }
            }
            FeatsOn = on;
        }

        private static bool IsEnabledForRun(Il2Cpp.FeatType type)
        {
            try
            {
                Il2CppSystem.Collections.Generic.List<Il2Cpp.FeatType> list =
                    FeatEnabledTracker.m_FeatsEnabledThisSandbox;
                return list != null && list.Contains(type);
            }
            catch (System.Exception) { return false; }
        }

        private static void EnableForRun(Il2Cpp.FeatType type, bool enabled)
        {
            try
            {
                Il2CppSystem.Collections.Generic.List<Il2Cpp.FeatType> list =
                    FeatEnabledTracker.m_FeatsEnabledThisSandbox;
                if (list == null) return;
                bool has = list.Contains(type);
                if (enabled && !has) list.Add(type);
                else if (!enabled && has) list.Remove(type);
            }
            catch (System.Exception e)
            {
                Log.OnceWarn("feat-tracker", "the enabled-feats list could not be changed: " + e.Message
                    + " - the unlock still happens, the run flag may not.");
            }
        }

        /// <summary>Every switch on this page turned off in one go, restores included.</summary>
        public static void AllFeats(bool on)
        {
            for (int i = 0; i < Settings.FeatOrder.Count; i++)
                Settings.FeatSwitches[Settings.FeatOrder[i]].Value = on;
            Settings.SaveSoon();
            Log.Info("all feats switched " + (on ? "on" : "off") + " - applied on the next sweep.");
        }

        // ------------------------------------------------------------------------------------------
        // SURVIVAL RATES
        //
        // Cold, tiredness, thirst, hunger and stamina, each a multiplier on the game's own per-hour
        // numbers rather than a number of ours. That distinction is the whole design: the game keeps
        // several rates per system - fatigue while standing, walking and sprinting; calories for ten
        // separate activities - and scaling them TOGETHER leaves the relationships between them
        // exactly as Hinterland balanced them. Replacing them with one flat figure would not.
        //
        // WHILE A DIAL IS AT 1.00, THE STORED ORIGINAL IS KEPT REFRESHED FROM THE LIVE VALUE. That is
        // what makes this safe to leave in: change difficulty, load another save, take a buff that
        // moves a rate, and the baseline follows. The moment the dial leaves 1.00 the refresh stops
        // and the value written is always original times multiplier - never the previous write times
        // the multiplier again, which is how these things quietly run away to zero.
        // ------------------------------------------------------------------------------------------
        private static void Rates()
        {
            try
            {
                Freezing fr = GameManager.GetFreezingComponent();
                if (fr != null)
                {
                    float m = Mathf.Clamp(Settings.RateCold.Value, 0f, 5f);
                    fr.m_FreezingIncreasePerHourPerDegreeCelsius =
                        Scale("cold.freeze", fr.m_FreezingIncreasePerHourPerDegreeCelsius, m);
                }
            }
            catch (System.Exception e) { Complain("cold", e); }

            try
            {
                Fatigue fa = GameManager.GetFatigueComponent();
                if (fa != null)
                {
                    float m = Mathf.Clamp(Settings.RateTired.Value, 0f, 5f);
                    fa.m_FatigueIncreasePerHourStanding = Scale("tired.stand", fa.m_FatigueIncreasePerHourStanding, m);
                    fa.m_FatigueIncreasePerHourWalking = Scale("tired.walk", fa.m_FatigueIncreasePerHourWalking, m);
                    fa.m_FatigueIncreasePerHourSprintingMin = Scale("tired.sprintmin", fa.m_FatigueIncreasePerHourSprintingMin, m);
                    fa.m_FatigueIncreasePerHourSprintingMax = Scale("tired.sprintmax", fa.m_FatigueIncreasePerHourSprintingMax, m);
                }
            }
            catch (System.Exception e) { Complain("tiredness", e); }

            try
            {
                Thirst th = GameManager.GetThirstComponent();
                if (th != null)
                {
                    float m = Mathf.Clamp(Settings.RateThirst.Value, 0f, 5f);
                    th.m_ThirstIncreasePerDay = Scale("thirst.awake", th.m_ThirstIncreasePerDay, m);
                    th.m_ThirstIncreasePerDayWhenResting = Scale("thirst.rest", th.m_ThirstIncreasePerDayWhenResting, m);
                }
            }
            catch (System.Exception e) { Complain("thirst", e); }

            try
            {
                Hunger hu = GameManager.GetHungerComponent();
                if (hu != null)
                {
                    float m = Mathf.Clamp(Settings.RateHunger.Value, 0f, 5f);
                    hu.m_CalorieBurnPerHourSleeping = Scale("hunger.sleep", hu.m_CalorieBurnPerHourSleeping, m);
                    hu.m_CalorieBurnPerHourStanding = Scale("hunger.stand", hu.m_CalorieBurnPerHourStanding, m);
                    hu.m_CalorieBurnPerHourWalking = Scale("hunger.walk", hu.m_CalorieBurnPerHourWalking, m);
                    hu.m_CalorieBurnPerHourSprinting = Scale("hunger.sprint", hu.m_CalorieBurnPerHourSprinting, m);
                    hu.m_CalorieBurnPerHourBreakingDown = Scale("hunger.break", hu.m_CalorieBurnPerHourBreakingDown, m);
                    hu.m_CalorieBurnPerHourHarvestingCarcass = Scale("hunger.carcass", hu.m_CalorieBurnPerHourHarvestingCarcass, m);
                    hu.m_CalorieBurnPerHourClimbing = Scale("hunger.climb", hu.m_CalorieBurnPerHourClimbing, m);
                    hu.m_CalorieBurnPerHourBuildingSnowShelter = Scale("hunger.build", hu.m_CalorieBurnPerHourBuildingSnowShelter, m);
                    hu.m_CalorieBurnPerHourRepairingSnowShelter = Scale("hunger.repair", hu.m_CalorieBurnPerHourRepairingSnowShelter, m);
                    hu.m_CalorieBurnPerHourDismantlingSnowShelter = Scale("hunger.dismantle", hu.m_CalorieBurnPerHourDismantlingSnowShelter, m);
                }
            }
            catch (System.Exception e) { Complain("hunger", e); }

            try
            {
                PlayerMovement pm = GameManager.GetPlayerMovementComponent();
                if (pm != null)
                {
                    float m = Mathf.Clamp(Settings.RateStamina.Value, 0f, 5f);
                    pm.m_SprintStaminaUsagePerSecond = Scale("stamina.use", pm.m_SprintStaminaUsagePerSecond, m);

                    // At the bottom of the slider, use the game's own flag rather than dividing by
                    // something indistinguishable from zero. Its previous value is remembered, so
                    // turning the dial back up hands the game back exactly what it had.
                    bool unlimited = m <= 0.01f;
                    if (unlimited)
                    {
                        if (!_haveSprintWas) { _sprintWas = PlayerMovement.m_UnlimitedSprint; _haveSprintWas = true; }
                        PlayerMovement.m_UnlimitedSprint = true;
                    }
                    else if (_haveSprintWas)
                    {
                        PlayerMovement.m_UnlimitedSprint = _sprintWas;
                        _haveSprintWas = false;
                    }
                }
            }
            catch (System.Exception e) { Complain("stamina", e); }
        }

        /// <summary>
        /// The one rule that makes these dials safe: remember the base while the dial is neutral,
        /// and never compute a new value from a value we ourselves wrote.
        /// </summary>
        private static float Scale(string key, float live, float multiplier)
        {
            if (Mathf.Approximately(multiplier, 1f))
            {
                // THE TRAP THIS BRANCH AVOIDS: on the way back to neutral, the live value is still
                // OUR last write. Refreshing the baseline from it would bake the cheat in permanently
                // and there would be no way back to the game's own number. So a key we were scaling
                // is restored from its baseline first, and only then does it go back to tracking.
                if (_scaledKeys.Contains(key))
                {
                    _scaledKeys.Remove(key);
                    float restored = _origRates[key];
                    Log.OnceInfo("rate-restore", "a survival rate dial went back to 1.00 - the game's "
                        + "own numbers are restored, and they are tracked again from here.");
                    return restored;
                }
                _origRates[key] = live;      // neutral: the game's value IS the baseline, keep it fresh
                return live;
            }

            float baseline;
            if (!_origRates.TryGetValue(key, out baseline))
            {
                baseline = live;
                _origRates[key] = baseline;
            }
            _scaledKeys.Add(key);
            return baseline * multiplier;
        }

        private static void Complain(string what, System.Exception e)
        {
            Log.OnceWarn("rate-" + what, "the " + what + " rate could not be set: " + e.Message
                + " - it is retried every sweep and the dial stays where you put it.");
        }

        // ------------------------------------------------------------------------------------------
        // SPEED
        //
        // vp_FPSController is UFPS, where top speed is a function of MotorAcceleration against
        // MotorDamping - there is no "max speed" to set. So the acceleration is scaled and the top
        // speed follows. It is written EVERY FRAME rather than once, because the game's own movement
        // state machine also writes that field when you crouch, limp or get encumbered; setting it
        // once would last until the first crouch and then silently stop, which is the exact shape of
        // bug this mod exists to refuse.
        // ------------------------------------------------------------------------------------------
        private static void Speed()
        {
            float mult = Mathf.Clamp(Settings.CheatSpeed.Value, 0.25f, 8f);
            bool wanted = !Mathf.Approximately(mult, 1f);

            if (!wanted)
            {
                if (_haveAcceleration && _controller != null)
                {
                    try
                    {
                        float was;
                        if (_origAccelById.TryGetValue(_controller.GetInstanceID(), out was))
                        {
                            _controller.MotorAcceleration = was;
                            Log.Info("speed cheat off - acceleration back to " + was.ToString("0.0000") + ".");
                        }
                    }
                    catch (System.Exception) { }
                }
                _haveAcceleration = false;
                return;
            }

            if (_controller == null)
            {
                try
                {
                    GameObject player = GameManager.GetPlayerObject();
                    if (player != null) _controller = player.GetComponentInChildren<vp_FPSController>();
                    if (_controller == null) _controller = Object.FindObjectOfType<vp_FPSController>();
                }
                catch (System.Exception) { }
                if (_controller == null)
                {
                    Log.OnceWarn("no-controller", "the speed cheat cannot find vp_FPSController on the "
                        + "player. It keeps looking every frame and will start working the moment one "
                        + "exists, so this is usually just the main menu.");
                    return;
                }
                _haveAcceleration = false;
                Log.Forget("no-controller");
            }

            try
            {
                int id = _controller.GetInstanceID();
                if (!_origAccelById.TryGetValue(id, out _origAcceleration))
                {
                    _origAcceleration = _controller.MotorAcceleration;
                    _origAccelById[id] = _origAcceleration;
                    Log.Info("speed cheat on - the game's own acceleration is "
                        + _origAcceleration.ToString("0.0000") + ", now scaled by "
                        + mult.ToString("0.00") + "x.");
                }
                _haveAcceleration = true;
                _controller.MotorAcceleration = _origAcceleration * mult;
            }
            catch (System.Exception e)
            {
                // The controller went away under us - drop it and look again next frame rather than
                // clearing the intent. The cheat stays ON; only the handle is forgotten.
                _controller = null;
                _haveAcceleration = false;
                Log.OnceWarn("speed-threw", "writing MotorAcceleration threw: " + e.Message
                    + " - the controller is looked up again next frame and the cheat stays on.");
            }
        }

        public static void NudgeSpeed(int direction)
        {
            float step = Mathf.Max(0.05f, Settings.CheatSpeedStep.Value);
            float now = Mathf.Clamp(Settings.CheatSpeed.Value + direction * step, 0.25f, 8f);
            Settings.CheatSpeed.Value = now;
            Settings.SaveSoon();
            Log.Info("speed multiplier " + now.ToString("0.00") + "x"
                + (Mathf.Approximately(now, 1f) ? "  (this is the off position)" : ""));
        }

        // ------------------------------------------------------------------------------------------
        // CARRY WEIGHT
        //
        // The cap is raised at its source, Encumber.m_MaxCarryCapacity, rather than by teaching the
        // mod's own weight gate to look the other way. That matters: the gate reads the game's number,
        // so raising the number makes the gate pass on its own, the encumbrance bar agrees, and there
        // is exactly one truth about how much can be carried instead of two.
        // ------------------------------------------------------------------------------------------
        private static void Carry()
        {
            Encumber enc = null;
            try { enc = GameManager.GetEncumberComponent(); } catch (System.Exception) { }
            if (enc == null) return;

            int encId;
            try { encId = enc.GetInstanceID(); } catch (System.Exception) { return; }

            if (!Settings.CheatUnlimitedCarry.Value)
            {
                ItemWeight was;
                if (_origCapacityById.TryGetValue(encId, out was))
                {
                    try { enc.m_MaxCarryCapacity = was; } catch (System.Exception) { }
                    _origCapacityById.Remove(encId);
                    _haveCapacity = false;
                    Log.Info("carry cheat off - cap back to " + Sweep.KG(was).ToString("0.0") + " kg.");
                }
                return;
            }

            try
            {
                ItemWeight want = ItemWeight.FromKilograms(Mathf.Clamp(Settings.CheatCarryKG.Value, 1f, 100000f));
                if (!_origCapacityById.TryGetValue(encId, out _origCapacity))
                {
                    _origCapacity = enc.m_MaxCarryCapacity;
                    _origCapacityById[encId] = _origCapacity;
                    Log.Info("carry cheat on - the game's cap is " + Sweep.KG(_origCapacity).ToString("0.0")
                        + " kg, raised to " + Sweep.KG(want).ToString("0.0") + " kg.");
                }
                _haveCapacity = true;
                // Written every sweep, not once: the game recomputes this from buffs, fatigue and
                // clothing, so a single write would be overwritten within the minute.
                enc.m_MaxCarryCapacity = want;
            }
            catch (System.Exception e)
            {
                Log.OnceWarn("carry-threw", "raising the carry cap threw: " + e.Message
                    + " - it is retried on the next sweep and the cheat stays on.");
            }
        }

        // ------------------------------------------------------------------------------------------
        // INSTANT HARVEST
        //
        // Two different durations, because the game has two: HarvestBase.m_DurationMinutes on an item
        // you break into materials, and BodyHarvest.m_QuarterDurationMinutes on a carcass. Both are
        // stored before they are zeroed, keyed by instance id, and both are put back on the way out.
        // ------------------------------------------------------------------------------------------
        private static void HarvestDurations()
        {
            if (!Settings.CheatInstantHarvest.Value)
            {
                if (_origHarvestMinutes.Count > 0 || _origQuarterMinutes.Count > 0) RestoreHarvest();
                return;
            }

            for (int i = 0; i < Sweep.Items.Count; i++)
            {
                GearItem gi = Sweep.Items[i].Gear;
                if (gi == null) continue;

                try
                {
                    HarvestBase h = gi.m_Harvest;
                    if (h != null)
                    {
                        int id = h.GetInstanceID();
                        if (!_origHarvestMinutes.ContainsKey(id))
                        {
                            _origHarvestMinutes[id] = h.m_DurationMinutes;
                            h.m_DurationMinutes = 0;
                        }
                    }
                }
                catch (System.Exception) { }

                try
                {
                    BodyHarvest bh = gi.m_BodyHarvest;
                    if (bh != null)
                    {
                        int id = bh.GetInstanceID();
                        if (!_origQuarterMinutes.ContainsKey(id))
                        {
                            _origQuarterMinutes[id] = bh.m_QuarterDurationMinutes;
                            bh.m_QuarterDurationMinutes = 0f;
                        }
                    }
                }
                catch (System.Exception) { }
            }

            // Carcasses sit on their own layer and are not in the sweep's mask, so they get their own
            // look - cheap, because it only runs while this cheat is on.
            try
            {
                Transform p = GameManager.GetPlayerTransform();
                if (p != null)
                {
                    Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<BodyHarvest> all =
                        Object.FindObjectsOfType<BodyHarvest>();
                    for (int i = 0; i < all.Length; i++)
                    {
                        BodyHarvest bh = all[i];
                        if (bh == null) continue;
                        int id = bh.GetInstanceID();
                        if (_origQuarterMinutes.ContainsKey(id)) continue;
                        _origQuarterMinutes[id] = bh.m_QuarterDurationMinutes;
                        bh.m_QuarterDurationMinutes = 0f;
                    }
                }
            }
            catch (System.Exception e)
            {
                Log.OnceWarn("carcass-scan", "looking for carcasses threw: " + e.Message
                    + " - item harvesting is still instant; carcasses are retried each sweep.");
            }
        }

        private static void RestoreHarvest()
        {
            int n = 0;
            try
            {
                Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<HarvestBase> hs =
                    Object.FindObjectsOfType<HarvestBase>();
                for (int i = 0; i < hs.Length; i++)
                {
                    HarvestBase h = hs[i];
                    if (h == null) continue;
                    int mins;
                    if (_origHarvestMinutes.TryGetValue(h.GetInstanceID(), out mins))
                    {
                        h.m_DurationMinutes = mins;
                        n++;
                    }
                }
            }
            catch (System.Exception) { }

            try
            {
                Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<BodyHarvest> bhs =
                    Object.FindObjectsOfType<BodyHarvest>();
                for (int i = 0; i < bhs.Length; i++)
                {
                    BodyHarvest bh = bhs[i];
                    if (bh == null) continue;
                    float mins;
                    if (_origQuarterMinutes.TryGetValue(bh.GetInstanceID(), out mins))
                    {
                        bh.m_QuarterDurationMinutes = mins;
                        n++;
                    }
                }
            }
            catch (System.Exception) { }

            int held = _origHarvestMinutes.Count + _origQuarterMinutes.Count;
            _origHarvestMinutes.Clear();
            _origQuarterMinutes.Clear();
            Log.Info("instant harvest off - " + n + " of " + held
                + " duration(s) put back. The rest belonged to objects this scene no longer has.");
        }

        // ------------------------------------------------------------------------------------------
        // UNLIMITED AMMO
        //
        // Only the gun in hand, and only the clip. Ammo in the pack is not touched, because the
        // ask was "unlimited ammo when using a firearm" and a pack that silently refills is a
        // different, larger thing that would also confuse every weight reading in the report.
        //
        // m_Clip is a parallel list the game reads for what each round IS. Topping up m_RoundsInClip
        // without it would leave the count and the contents disagreeing, so both are filled.
        // ------------------------------------------------------------------------------------------
        private static void Ammo()
        {
            if (!Settings.CheatUnlimitedAmmo.Value) return;

            try
            {
                PlayerManager pm = GameManager.GetPlayerManagerComponent();
                if (pm == null) return;
                GearItem held = pm.m_ItemInHands;
                if (held == null) return;
                GunItem gun = held.m_GunItem;
                if (gun == null) return;

                int size = gun.m_ClipSize;
                if (size <= 0) return;
                if (gun.m_RoundsInClip >= size) return;

                // Fill the contents first, then the count. The other order leaves a frame in which
                // the gun believes it holds rounds it cannot describe.
                Il2CppSystem.Collections.Generic.List<int> clip = gun.m_Clip;
                if (clip != null)
                {
                    int pattern = clip.Count > 0 ? clip[clip.Count - 1] : 0;
                    while (clip.Count < size) clip.Add(pattern);
                }
                gun.m_RoundsInClip = size;
                gun.m_SpentCasingsInClip = 0;
                gun.m_HasMisfired = false;
                _ammoTopUps++;
            }
            catch (System.Exception e)
            {
                Log.OnceWarn("ammo-threw", "topping up the clip threw: " + e.Message
                    + " - it is tried again on the next frame and the cheat stays on.");
            }
        }

        // ------------------------------------------------------------------------------------------
        // PERPETUAL FIRE
        //
        // The game already has this: Fire.m_IsPerpetual, the flag it uses for scripted fires that are
        // not allowed to die. Using the game's own switch is better than fighting its fuel arithmetic
        // every frame, and it is one bool to put back.
        //
        // But it is not TRUSTED. The remaining life is watched, and if it keeps falling with the flag
        // set, the fuel clock is reset directly instead and the log says the flag was not enough.
        // ------------------------------------------------------------------------------------------
        private static void Fires()
        {
            if (!Settings.CheatPerpetualFire.Value)
            {
                if (_origPerpetual.Count > 0) RestoreFires();
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (now >= _nextFireScan)
            {
                _nextFireScan = now + 2f;      // fires are few and do not appear mid-second
                _fires.Clear();
                try
                {
                    Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<Fire> all =
                        Object.FindObjectsOfType<Fire>();
                    for (int i = 0; i < all.Length; i++) if (all[i] != null) _fires.Add(all[i]);
                }
                catch (System.Exception e)
                {
                    Log.OnceWarn("fire-scan", "looking for fires threw: " + e.Message
                        + " - retried in two seconds, and the cheat stays on.");
                    return;
                }
            }

            float nearestLife = -1f;
            for (int i = 0; i < _fires.Count; i++)
            {
                Fire f = _fires[i];
                if (f == null) continue;
                try
                {
                    if (!f.IsBurning()) continue;
                    int id = f.GetInstanceID();
                    if (!_origPerpetual.ContainsKey(id))
                    {
                        _origPerpetual[id] = f.m_IsPerpetual;
                        Log.OnceInfo("fire-on", "perpetual fire on - the game's own m_IsPerpetual flag "
                            + "is being set on burning fires, and cleared again when you turn this off.");
                    }
                    f.m_IsPerpetual = true;

                    float life = f.GetRemainingLifeTimeHours();
                    if (nearestLife < 0f || life < nearestLife) nearestLife = life;

                    if (_fireLifeFalling >= 3)
                    {
                        // ESCALATION. The flag did not hold, so the fuel clock is wound back directly.
                        f.m_ElapsedOnTODSeconds = 0f;
                        f.m_ElapsedOnTODSecondsUnmodified = 0f;
                    }
                }
                catch (System.Exception) { }
            }

            // Detect, then escalate. Three consecutive drops is a falling fire, not a rounding wobble.
            if (nearestLife >= 0f)
            {
                if (_fireLifeWas >= 0f && nearestLife < _fireLifeWas - 0.01f)
                {
                    _fireLifeFalling++;
                    if (_fireLifeFalling == 3)
                    {
                        Log.Warn("m_IsPerpetual is set and the nearest fire is still burning down "
                            + "(" + nearestLife.ToString("0.00") + "h left). Winding its fuel clock "
                            + "back directly from now on. If this line appears every session, the "
                            + "flag no longer does what it did.");
                    }
                }
                else if (nearestLife > _fireLifeWas)
                {
                    _fireLifeFalling = 0;
                }
                _fireLifeWas = nearestLife;
            }
        }

        private static void RestoreFires()
        {
            int n = 0;
            try
            {
                Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<Fire> all =
                    Object.FindObjectsOfType<Fire>();
                for (int i = 0; i < all.Length; i++)
                {
                    Fire f = all[i];
                    if (f == null) continue;
                    bool was;
                    if (_origPerpetual.TryGetValue(f.GetInstanceID(), out was)) { f.m_IsPerpetual = was; n++; }
                }
            }
            catch (System.Exception) { }

            int held = _origPerpetual.Count;
            _origPerpetual.Clear();
            _fires.Clear();
            _fireLifeWas = -1f;
            _fireLifeFalling = 0;
            Log.Info("perpetual fire off - " + n + " of " + held + " fire(s) put back.");
        }

        /// <summary>
        /// A scene change invalidates every instance id we are holding, so the maps are dropped -
        /// but the SWITCHES are untouched. The cheats stay on and re-apply themselves to the new
        /// scene on the next sweep. Clearing the intent here is exactly the mistake this station has
        /// a rule about.
        /// </summary>
        public static void ForgetScene()
        {
            // PER-SCENE objects only. Their instance ids mean nothing in the new scene and the
            // objects themselves are gone, so holding their originals is holding nothing.
            _origHarvestMinutes.Clear();
            _origQuarterMinutes.Clear();
            _origPerpetual.Clear();
            _fires.Clear();
            _fireLifeWas = -1f;
            _fireLifeFalling = 0;
            _nextFireScan = 0f;

            // Handles are dropped so they are looked up fresh...
            _controller = null;
            _haveAcceleration = false;
            _haveCapacity = false;

            // ...but the BASELINES are not, and that distinction is the whole lesson of this file.
            // _origAccelById, _origCapacityById and _origRates all describe session-long singletons.
            // Clearing them here is what made the carry cheat re-capture its own 500 kg as though the
            // game had always allowed it. Handles are cheap to lose; a baseline is not.
        }
    }
}
