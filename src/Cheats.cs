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
        private static readonly Dictionary<int, float> _origVelMaxById = new Dictionary<int, float>();
        private static float _origVelocityMax;

        // Measurement, so "it did not work" is never the end of the conversation again. The mod
        // watches how fast the player actually moves and prints the fastest it has seen, which is a
        // fact a log can carry and an impression cannot.
        private static Vector3 _lastPos;
        private static float _lastPosAt;
        private static float _fastestSeen;
        private static float _nextSpeedReport;

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
        private static readonly Dictionary<int, float> _origMaxOn = new Dictionary<int, float>();

        /// A year in seconds. Large enough that no night of sleep reaches it, small enough that it is
        /// still a number rather than an infinity sitting inside somebody else's arithmetic.
        private const float YearOfSeconds = 31536000f;
        private static float _nextFireScan;
        private static readonly List<Fire> _fires = new List<Fire>();
        private static float _fireLifeWas = -1f;
        private static int _fireLifeFalling;

        // ---- ammo --------------------------------------------------------------------------------
        private static int _ammoTopUps;

        // ---- recoil ------------------------------------------------------------------------------
        private static readonly Dictionary<int, Vector3> _origRecoilPos = new Dictionary<int, Vector3>();
        private static readonly Dictionary<int, Vector3> _origRecoilRot = new Dictionary<int, Vector3>();
        private static readonly Dictionary<int, float> _origDryFire = new Dictionary<int, float>();
        private static int _recoilHeld;
        private static int _gunsHeld;
        private static float _nextRecoilScan;
        private static readonly Dictionary<int, Vector4> _origGunRecoil = new Dictionary<int, Vector4>();

        // ---- sway --------------------------------------------------------------------------------
        // Aim sway is a separate system from recoil: the wobble while holding a sight, driven by
        // fatigue. GunItem keeps the two ends of that range plus how fast it builds.
        private static readonly Dictionary<int, Vector3> _origSway = new Dictionary<int, Vector3>();
        private static int _swayHeld;
        private static float _nextSwayScan;
        private static bool _aimShakeWas;
        private static bool _aimSwayWas;
        private static bool _haveAimShakeWas;
        private static int _weaponsHeld;

        private struct WeaponSway
        {
            public Vector3 Look, Strafe, Fall, Limits, Shake;
            public Vector4 Bob;
            public float Slope;
        }

        private static readonly Dictionary<int, WeaponSway> _origWeaponSway =
            new Dictionary<int, WeaponSway>();

        // THE CAMERA WAS THE MISSING THIRD PLACE, and it is the one that moves the aim.
        //
        // The gun's sway numbers were zeroed and the weapon model's motion was zeroed, and the sight
        // still drifted. vp_FPSCamera carries AMBIENT SWAY IN DEGREES - a slow wander applied to the
        // camera itself, with a separate figure for while aiming - and degrees of camera rotation are
        // exactly "where the shot goes". Neither of the first two places could have fixed it.
        private struct CameraSway
        {
            public float Ambient, AmbientAiming, ShakeSpeed;
            public Vector3 Shake;
            public Vector4 Bob;
        }

        private static readonly Dictionary<int, CameraSway> _origCamSway =
            new Dictionary<int, CameraSway>();
        private static int _camsHeld;
        private static List<vp_FPSCamera> _camList; private static float _camNext;

        // ---- survival rates ----------------------------------------------------------------------
        // One stored original per field, keyed by a name rather than an instance id: these five are
        // singletons that live for the session, so a name is stable where an id is not.
        private static readonly Dictionary<string, float> _origRates = new Dictionary<string, float>();
        private static readonly HashSet<string> _scaledKeys = new HashSet<string>();
        private static bool _sprintWas;
        private static bool _haveSprintWas;

        // ------------------------------------------------------------------------------------------
        // ONE SHARED SCAN CACHE, AND THE REASON IT HAD TO EXIST
        //
        // Every pass in this file used to ask the scene for its objects directly. Fuel wanted four
        // types four times a second, recoil wanted two, sway two, curing one, fires one - something
        // like a dozen full-scene type scans every second, on a map with tens of thousands of
        // objects. The game became unplayable, and the arithmetic was sitting in our own log:
        //
        //     fuel pass: ... top-ups this session = 10608
        //
        // ten thousand top-ups, which is 6.5 a second across two lamps, which is three and a half
        // scans a second for lamps alone.
        //
        // A scene's cast of lanterns and rifles does not change three times a second. So the scan
        // happens on a slow period and the passes walk the cached list at whatever rate they like,
        // which costs nothing. The cost of scanning is measured and reported, because a performance
        // fix that is not measured is a hope.
        private static float _scanMs;
        private static int _scanCount;

        public static string ScanCost()
        {
            return _scanCount + " scans, " + _scanMs.ToString("0.0") + "ms";
        }

        public static void ResetScanCost() { _scanMs = 0f; _scanCount = 0; }

        private static List<T> Scan<T>(ref List<T> store, ref float nextAt, float period)
            where T : Object
        {
            float now = Time.realtimeSinceStartup;
            if (store == null || now >= nextAt)
            {
                nextAt = now + period;
                float t0 = Time.realtimeSinceStartup;
                try
                {
                    Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<T> found =
                        Object.FindObjectsOfType<T>();
                    if (store == null) store = new List<T>();
                    store.Clear();
                    for (int i = 0; i < found.Length; i++) if (found[i] != null) store.Add(found[i]);
                }
                catch (System.Exception e)
                {
                    if (store == null) store = new List<T>();
                    Log.OnceWarn("scan-threw", "a scene scan threw: " + e.Message
                        + " - the previous list is kept and it is tried again next period.");
                }
                _scanMs += (Time.realtimeSinceStartup - t0) * 1000f;
                _scanCount++;
            }

            // Objects do get destroyed between scans - a lantern picked up, a rifle stowed.
            for (int i = store.Count - 1; i >= 0; i--) if (store[i] == null) store.RemoveAt(i);
            return store;
        }

        // How often each cast list is refreshed. Ten seconds is a compromise with one visible edge:
        // a lantern put down is picked up by the next scan rather than instantly. The held item is
        // handled every frame by its own path, so the thing in hand is never late.
        private const float ScanPeriod = 10f;

        private static List<KeroseneLampItem> _lampList; private static float _lampNext;
        private static List<TorchItem> _torchList; private static float _torchNext;
        private static List<FlareItem> _flareList; private static float _flareNext;
        private static List<FlashlightItem> _lightList; private static float _lightNext;
        private static List<GunItem> _gunList; private static float _gunNext;
        private static List<vp_FPSShooter> _shooterList; private static float _shooterNext;
        private static List<vp_FPSWeapon> _weaponList; private static float _weaponNext;
        private static List<EvolveItem> _evolveList; private static float _evolveNext;
        private static List<BodyHarvest> _carcassList; private static float _carcassNext;
        private static List<HarvestBase> _harvestList; private static float _harvestNext;
        private static List<Fire> _fireList; private static float _fireListNext;

        /// <summary>Force every cached list to be rebuilt - used when a scene changes.</summary>
        private static void DropScans()
        {
            _lampNext = 0f; _torchNext = 0f; _flareNext = 0f; _lightNext = 0f;
            _gunNext = 0f; _shooterNext = 0f; _weaponNext = 0f; _evolveNext = 0f;
            _carcassNext = 0f; _harvestNext = 0f; _nextFireScan = 0f;
            if (_lampList != null) _lampList.Clear();
            if (_torchList != null) _torchList.Clear();
            if (_flareList != null) _flareList.Clear();
            if (_lightList != null) _lightList.Clear();
            if (_gunList != null) _gunList.Clear();
            if (_shooterList != null) _shooterList.Clear();
            if (_weaponList != null) _weaponList.Clear();
            if (_evolveList != null) _evolveList.Clear();
            if (_carcassList != null) _carcassList.Clear();
            if (_harvestList != null) _harvestList.Clear();
        }

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
            if (BuffsHeld > 0) s += " buffsHeld=" + BuffsHeld;
            if (!Mathf.Approximately(Settings.CheatSpeed.Value, 1f))
                s += " speed=" + Settings.CheatSpeed.Value.ToString("0.00") + "x";
            if (Settings.CheatInstantHarvest.Value) s += " instantHarvest";
            if (Settings.CheatUnlimitedCarry.Value) s += " carry=" + Settings.CheatCarryKG.Value.ToString("0") + "kg";
            if (Settings.CheatUnlimitedAmmo.Value) s += " ammo(" + _ammoTopUps + " topups)";
            if (Settings.CheatPerpetualFire.Value) s += " perpetualFire(" + _fires.Count + ")";
            if (Settings.CheatSteadyAim.Value)
                s += " steadyAim(guns=" + _gunsHeld + " shooters=" + _recoilHeld
                    + " weapons=" + _weaponsHeld + " cams=" + _camsHeld + ")";
            if (!Mathf.Approximately(Settings.RateDaylight.Value, 1f))
                s += " daylight=" + Settings.RateDaylight.Value.ToString("0.00") + "x";
            if (!Mathf.Approximately(Settings.RateCuring.Value, 1f))
                s += " curing=" + Settings.RateCuring.Value.ToString("0.00") + "x(" + CuringHeld + ")";
            if (Settings.CheatNoDegrade.Value) s += " noDegrade(" + RepairsMade + ")";
            if (Settings.CheatFreeRepair.Value) s += " freeRepair(" + FreeRepair.Uses + ")";

            return s;
        }

        public static bool AnyOn()
        {
            return !Mathf.Approximately(Settings.CheatSpeed.Value, 1f)
                || Settings.CheatInstantHarvest.Value
                || Settings.CheatUnlimitedCarry.Value
                || Settings.CheatUnlimitedAmmo.Value
                || Settings.CheatPerpetualFire.Value
                || Settings.CheatSteadyAim.Value
                || Settings.CheatNoDegrade.Value
                || Settings.CheatFreeRepair.Value
                || !Mathf.Approximately(Settings.RateCuring.Value, 1f)
                || !Mathf.Approximately(Settings.RateDaylight.Value, 1f)
                || !Mathf.Approximately(Settings.RateCold.Value, 1f)
                || !Mathf.Approximately(Settings.RateTired.Value, 1f)
                || !Mathf.Approximately(Settings.RateThirst.Value, 1f)
                || !Mathf.Approximately(Settings.RateHunger.Value, 1f)
                || !Mathf.Approximately(Settings.RateStamina.Value, 1f)
                || !Mathf.Approximately(Settings.RateHeldFuel.Value, 1f)
                || Settings.HoldBuffTimers.Value
                || Settings.HoldWellFed.Value;
        }

        /// <summary>Every frame. Only the ones that have to be, and all of them cheap.</summary>
        public static void FastTick()
        {
            Speed();
            Ammo();
            HeldFuel();
            HeldCondition();
        }

        // ------------------------------------------------------------------------------------------
        // THE HELD ITEM DOES NOT WEAR OUT
        //
        // Condition is one number on GearItem, and the game exposes a normalised setter for it, so
        // this is the one cheat here with no arithmetic in it at all: while the switch is on, the
        // thing in hand is held at full.
        //
        // Held only, and the reason is the same as the fuel dial's: it answers "the rifle I am
        // shooting keeps degrading", which is what was asked, without quietly repairing an entire
        // pack of clothing that nobody mentioned.
        // ------------------------------------------------------------------------------------------
        public static int RepairsMade;

        private static void HeldCondition()
        {
            if (!Settings.CheatNoDegrade.Value) return;
            try
            {
                PlayerManager pm = GameManager.GetPlayerManagerComponent();
                if (pm == null) return;
                GearItem held = pm.m_ItemInHands;
                if (held == null) return;

                // 100 is full condition in this game's units. Only written when it has actually
                // slipped, so an item at full costs one comparison a frame and nothing else.
                if (held.m_CurrentHP < 99.999f)
                {
                    held.SetNormalizedHP(1f, false);
                    RepairsMade++;
                    if (RepairsMade <= 3)
                    {
                        Log.Info("held item repaired to full (" + Sweep.NameOf(held)
                            + "). Said for the first three only.");
                    }
                }
            }
            catch (System.Exception e)
            {
                Log.OnceWarn("degrade-threw", "the held item's condition could not be set: " + e.Message
                    + " - tried again next frame, and the switch stays on.");
            }
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
        private static float _nextFuelReport;
        private static int _placedLamps, _placedTorches, _placedFlares, _lampsToppedUp;
        private static string _placedCounts = "0/0/0/0";
        private static string _lastLampReading = "";

        private static void PlacedFuel()
        {
            if (!Settings.FuelIncludesPlaced.Value) return;
            if (Mathf.Approximately(Settings.RateHeldFuel.Value, 1f)) return;

            float now = Time.realtimeSinceStartup;
            if (now < _nextPlacedFuelScan) return;
            // Four times a second while infinite, because a top-up is only as good as its interval -
            // once a second lets a lantern lose a second of fuel between passes, which is exactly
            // what "still being consumed" looks like from the other side of the screen.
            _nextPlacedFuelScan = now + (FuelIsInfinite ? 0.25f : 1f);

            float m = FuelMultiplier();
            try
            {
                List<KeroseneLampItem> lamps = Scan(ref _lampList, ref _lampNext, ScanPeriod);
                for (int i = 0; i < lamps.Count; i++) ApplyLamp(lamps[i], m);
                _placedLamps = lamps.Count;
                if (lamps.Count > 0 && lamps[0] != null)
                {
                    _lastLampReading = Sweep.Litres(lamps[0].m_CurrentFuelLiters).ToString("0.000")
                        + "L of " + Sweep.Litres(lamps[0].m_MaxFuel).ToString("0.000")
                        + "L, burn " + Sweep.Litres(lamps[0].m_FuelBurnPerHour).ToString("0.000") + "L/h";
                }
            }
            catch (System.Exception e) { FuelComplain("placed lamps", e); }

            try
            {
                List<TorchItem> torches = Scan(ref _torchList, ref _torchNext, ScanPeriod);
                for (int i = 0; i < torches.Count; i++) ApplyTorch(torches[i], m);
                _placedTorches = torches.Count;
            }
            catch (System.Exception e) { FuelComplain("placed torches", e); }

            try
            {
                List<FlareItem> flares = Scan(ref _flareList, ref _flareNext, ScanPeriod);
                for (int i = 0; i < flares.Count; i++) ApplyFlare(flares[i], m);
                _placedFlares = flares.Count;
            }
            catch (System.Exception e) { FuelComplain("placed flares", e); }

            try
            {
                List<FlashlightItem> lights = Scan(ref _lightList, ref _lightNext, ScanPeriod);
                for (int i = 0; i < lights.Count; i++) ApplyFlashlight(lights[i], m);
                _placedCounts = _placedLamps + "/" + _placedTorches + "/" + _placedFlares + "/" + lights.Count;
            }
            catch (System.Exception e) { FuelComplain("placed flashlights", e); }

            // MEASUREMENT, not reassurance. If a lantern is still draining, the next line says
            // whether it was found at all and what its tank actually reads.
            if (now >= _nextFuelReport)
            {
                _nextFuelReport = now + 20f;
                Log.Info("fuel pass: lamps/torches/flares/lights = " + _placedCounts
                    + ", top-ups this session = " + _lampsToppedUp
                    + (_lastLampReading.Length > 0 ? ", nearest lamp " + _lastLampReading : ""));
            }
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
        //
        // AND, WHEN INFINITE, THE TANK IS ALSO REFILLED. Zeroing the burn rate is an argument about
        // what the game ought to do next; refilling the tank is a fact about what it currently holds.
        // A placed lantern kept draining with the rate at zero, which says the burn is computed
        // somewhere this cannot see - so infinite stops asking and simply tops the fuel up. It is the
        // difference between telling the clock to stop and winding it.
        private static void ApplyLamp(KeroseneLampItem lamp, float m)
        {
            if (lamp == null) return;
            float litres = Sweep.Litres(lamp.m_FuelBurnPerHour);
            float want = FuelIsInfinite
                ? FuelHold(lamp.GetInstanceID(), litres, 0f)
                : FuelScale(lamp.GetInstanceID(), litres, m);
            lamp.m_FuelBurnPerHour = ItemLiquidVolume.FromLiters(want);

            if (FuelIsInfinite)
            {
                lamp.m_CurrentFuelLiters = lamp.m_MaxFuel;
                _lampsToppedUp++;
            }
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

            // Wind the clock back rather than only lengthening it - see the note on the lamp.
            if (FuelIsInfinite) torch.m_ElapsedBurnMinutes = 0f;
        }

        private static void ApplyFlare(FlareItem flare, float m)
        {
            if (flare == null) return;
            float mins = flare.m_BurnLifetimeMinutes;
            flare.m_BurnLifetimeMinutes = FuelIsInfinite
                ? FuelHold(flare.GetInstanceID(), mins, Forever)
                : FuelScale(flare.GetInstanceID(), mins, 1f / m);

            if (FuelIsInfinite) flare.m_ElapsedBurnMinutes = 0f;
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

            if (FuelIsInfinite) light.m_CurrentBatteryCharge = 1f;
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
            DayNight();
            BuffTimers();
            Recoil();
            Sway();
            Curing();
        }

        // ------------------------------------------------------------------------------------------
        // THE TIMED BUFFS - the positive effects that arrive with a countdown
        //
        // Improved Rest, Warming Up, Reduced Fatigue, the condition-over-time bonus, the pie bonus.
        // The game keeps each one as a pair of floats on PlayerManager - hours remaining and the
        // duration it started from - and counts the first one down in game time.
        //
        // THIS ONLY HOLDS A CLOCK THAT IS ALREADY RUNNING. If a buff is not active its remaining
        // hours are zero, and zero is left alone, so nothing here can grant an effect that was not
        // earned in the ordinary way. That is a deliberate line: stopping a countdown is a different
        // thing from handing out the buff, and only one of them was asked for.
        //
        // Nothing needs restoring afterwards. A held timer is simply not decremented; switch it off
        // and the same clock resumes falling from wherever it stands.
        // ------------------------------------------------------------------------------------------
        public static int BuffsHeld;

        private struct BuffRow
        {
            public string Name;
            public float Remaining;
            public float Duration;
        }

        private static readonly List<BuffRow> _buffRows = new List<BuffRow>();

        private static void BuffTimers()
        {
            _buffRows.Clear();
            BuffsHeld = 0;

            PlayerManager pm = null;
            try { pm = GameManager.GetPlayerManagerComponent(); } catch (System.Exception) { }
            if (pm == null) return;

            bool hold = Settings.HoldBuffTimers.Value;

            try
            {
                pm.m_ConditionRestBuffHoursRemaining =
                    Hold("Improved Rest", pm.m_ConditionRestBuffHoursRemaining, pm.m_ConditionRestBuffHoursDuration, hold);
                pm.m_FreezingBuffHoursRemaining =
                    Hold("Warming Up", pm.m_FreezingBuffHoursRemaining, pm.m_FreezingBuffHoursDuration, hold);
                pm.m_FatigueBuffHoursRemaining =
                    Hold("Reduced Fatigue", pm.m_FatigueBuffHoursRemaining, pm.m_FatigueBuffHoursDuration, hold);
                pm.m_ConditionPerHourHoursRemaining =
                    Hold("Condition bonus", pm.m_ConditionPerHourHoursRemaining, pm.m_ConditionPerHourHoursDuration, hold);
                pm.m_PumpkinPieBuffHoursRemaining =
                    Hold("Pie bonus", pm.m_PumpkinPieBuffHoursRemaining, pm.m_PumpkinPieBuffHoursDuration, hold);
            }
            catch (System.Exception e)
            {
                Log.OnceWarn("buff-timers", "the buff timers could not be read or written: " + e.Message
                    + " - retried every sweep, and the switch stays where it was put.");
            }

            // Well Fed has no clock. It is a state that ends when the stomach empties, so holding it
            // means holding the flag, and the same rule applies: only if it is already true.
            if (Settings.HoldWellFed.Value)
            {
                try
                {
                    // Cached like every other lookup in this file: WellFed is one component that
                    // lives as long as the run, and asking the scene for it four times a second was
                    // part of what made the game unplayable.
                    if (_wellFed == null || Time.realtimeSinceStartup >= _wellFedNext)
                    {
                        _wellFedNext = Time.realtimeSinceStartup + ScanPeriod;
                        _wellFed = Object.FindObjectOfType<WellFed>();
                    }
                    WellFed wf = _wellFed;
                    if (wf != null)
                    {
                        if (wf.m_Active)
                        {
                            _wellFedWasActive = true;
                            BuffsHeld++;
                        }
                        else if (_wellFedWasActive)
                        {
                            // It lapsed while being held. Put it back, once, and say so - a switch
                            // that silently fails to hold the one buff it names is worse than none.
                            wf.m_Active = true;
                            Log.OnceInfo("wellfed-held", "Well Fed lapsed while it was being held, so "
                                + "it was set active again. It is a state rather than a timer, which "
                                + "is why this one needs re-asserting instead of topping up.");
                            BuffsHeld++;
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Log.OnceWarn("wellfed", "Well Fed could not be held: " + e.Message
                        + " - retried every sweep.");
                }
            }
            else
            {
                _wellFedWasActive = false;
            }
        }

        private static bool _wellFedWasActive;
        private static WellFed _wellFed;
        private static float _wellFedNext;

        /// <summary>
        /// Top one countdown back up to the duration it started from - but only if it is running.
        /// The row is recorded either way, so the window can show what is active without a second
        /// pass over the same fields.
        /// </summary>
        private static float Hold(string name, float remaining, float duration, bool hold)
        {
            BuffRow row = new BuffRow();
            row.Name = name;
            row.Remaining = remaining;
            row.Duration = duration;
            _buffRows.Add(row);

            if (!hold) return remaining;
            if (remaining <= 0.001f) return remaining;      // not running: never start one

            BuffsHeld++;
            if (duration <= 0f)
            {
                // A running timer with no duration to top up to. Hold it where it is rather than
                // inventing a number, and say once that the pair did not make sense.
                Log.OnceWarn("buff-no-duration", "a buff is counting down with no recorded duration, "
                    + "so it is being held at its current value rather than topped up.");
                return remaining;
            }
            return duration > remaining ? duration : remaining;
        }

        // ------------------------------------------------------------------------------------------
        // NO RECOIL
        //
        // The kick lives on the shooter, not on the gun: vp_FPSShooter carries MotionPositionRecoil
        // and MotionRotationRecoil - the shove given to the weapon when it fires - plus a separate
        // dry-fire kick. Zeroing those three removes the recoil at its source, so the camera spring
        // that follows it has nothing to follow and no second switch is needed.
        //
        // Every weapon has its own shooter, so this walks them rather than assuming one, and stores
        // each original vector against its instance id. Aim sway from cold is a different system and
        // is deliberately left alone: it is not recoil, and taking it out was not asked for.
        // ------------------------------------------------------------------------------------------
        private static void Recoil()
        {
            if (!Settings.CheatSteadyAim.Value)
            {
                if (_origRecoilPos.Count > 0 || _origGunRecoil.Count > 0) RestoreRecoil();
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (now < _nextRecoilScan) return;
            _nextRecoilScan = now + 1f;      // weapons are swapped by hand, not sixty times a second

            // THE ACTUAL RECOIL LIVES ON THE GUN, and the first build missed it entirely.
            //
            // The log was clear that the code ran and found its target - noRecoil(1) - and the rifle
            // kicked anyway. So the shooter's motion vectors are not what this game uses. GunItem
            // carries its own four numbers, and those are the ones:
            //
            //     m_PitchRecoilMin / m_PitchRecoilMax     the upward kick, randomised between them
            //     m_YawRecoilMin   / m_YawRecoilMax       the sideways one
            //
            // The shooter is still zeroed below, because it costs nothing and covers any weapon that
            // does go through the UFPS path. But this is the pass that does the work.
            try
            {
                List<GunItem> guns = Scan(ref _gunList, ref _gunNext, ScanPeriod);
                for (int i = 0; i < guns.Count; i++)
                {
                    GunItem g = guns[i];
                    if (g == null) continue;
                    int id = g.GetInstanceID();
                    if (!_origGunRecoil.ContainsKey(id))
                    {
                        _origGunRecoil[id] = new Vector4(g.m_PitchRecoilMin, g.m_PitchRecoilMax,
                                                         g.m_YawRecoilMin, g.m_YawRecoilMax);
                        Log.OnceInfo("gun-recoil", "no recoil on - the gun's own pitch and yaw kick "
                            + "are zeroed. That is where this game keeps recoil; the shooter's motion "
                            + "vectors are zeroed too, but they were never the ones doing it.");
                    }
                    g.m_PitchRecoilMin = 0f;
                    g.m_PitchRecoilMax = 0f;
                    g.m_YawRecoilMin = 0f;
                    g.m_YawRecoilMax = 0f;
                }
                _gunsHeld = guns.Count;
            }
            catch (System.Exception e) { Log.OnceWarn("gun-recoil-threw",
                "the gun's recoil values could not be written: " + e.Message + " - retried each second."); }

            try
            {
                List<vp_FPSShooter> shooters = Scan(ref _shooterList, ref _shooterNext, ScanPeriod);
                int held = 0;
                for (int i = 0; i < shooters.Count; i++)
                {
                    vp_FPSShooter sh = shooters[i];
                    if (sh == null) continue;
                    int id = sh.GetInstanceID();

                    if (!_origRecoilPos.ContainsKey(id))
                    {
                        _origRecoilPos[id] = sh.MotionPositionRecoil;
                        _origRecoilRot[id] = sh.MotionRotationRecoil;
                        _origDryFire[id] = sh.MotionDryFireRecoil;
                        Log.OnceInfo("recoil-on", "no recoil on - the shooter's position and rotation "
                            + "kick are zeroed, and put back when the switch goes off.");
                    }

                    sh.MotionPositionRecoil = Vector3.zero;
                    sh.MotionRotationRecoil = Vector3.zero;
                    sh.MotionDryFireRecoil = 0f;
                    held++;
                }
                _recoilHeld = held;

                if (held == 0)
                {
                    Log.OnceWarn("recoil-none", "no recoil is on but there is no vp_FPSShooter in the "
                        + "scene yet - one appears with a weapon. It keeps looking every second.");
                }
                else
                {
                    Log.Forget("recoil-none");
                }
            }
            catch (System.Exception e)
            {
                Log.OnceWarn("recoil-threw", "the recoil vectors could not be written: " + e.Message
                    + " - retried every second, and the switch stays on.");
            }
        }

        // ------------------------------------------------------------------------------------------
        // NO SWAY
        //
        // A different system from recoil and worth its own switch. Sway is the wobble while holding
        // a sight, and GunItem keeps it as a range between the value at zero fatigue and the value
        // at maximum, plus how fast it builds. Zeroing all three holds the sight still.
        //
        // The cold shake is a third thing again, and the game already has a switch for it, so this
        // uses that rather than inventing one - and remembers what it was.
        // ------------------------------------------------------------------------------------------
        private static void Sway()
        {
            if (!Settings.CheatSteadyAim.Value)
            {
                if (_origSway.Count > 0 || _haveAimShakeWas) RestoreSway();
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (now < _nextSwayScan) return;
            _nextSwayScan = now + 1f;

            try
            {
                List<GunItem> guns = Scan(ref _gunList, ref _gunNext, ScanPeriod);
                for (int i = 0; i < guns.Count; i++)
                {
                    GunItem g = guns[i];
                    if (g == null) continue;
                    int id = g.GetInstanceID();
                    if (!_origSway.ContainsKey(id))
                    {
                        _origSway[id] = new Vector3(g.m_SwayValueZeroFatigue, g.m_SwayValueMaxFatigue,
                                                    g.m_SwayIncreasePerSecond);
                        Log.OnceInfo("sway-on", "no sway on - the gun's sway range and its build-up "
                            + "rate are zeroed, and the game's own aim-shake switch is used for the "
                            + "shake that comes from cold.");
                    }
                    g.m_SwayValueZeroFatigue = 0f;
                    g.m_SwayValueMaxFatigue = 0f;
                    g.m_SwayIncreasePerSecond = 0f;
                }
                _swayHeld = guns.Count;
            }
            catch (System.Exception e)
            {
                Log.OnceWarn("sway-threw", "the sway values could not be written: " + e.Message
                    + " - retried each second.");
            }

            try
            {
                if (!_haveAimShakeWas)
                {
                    _aimShakeWas = vp_FPSWeapon.IsAimShakeDisabled();
                    _aimSwayWas = vp_FPSWeapon.IsAimSwayDisabled();
                    _haveAimShakeWas = true;
                }
                vp_FPSWeapon.SetDisableAimShake(true);
                vp_FPSWeapon.SetDisableAimSway(true);
            }
            catch (System.Exception e)
            {
                Log.OnceWarn("aimshake-threw", "the aim shake switch could not be set: " + e.Message
                    + " - the sway values are still zeroed.");
            }

            // THE VISUAL HALF, which is a different thing again from where the shot goes.
            //
            // With the gun's sway zeroed the aim point stops drifting - that was confirmed by
            // testing, and it is the half that decides whether a shot lands. The weapon MODEL kept
            // wobbling, because that motion lives on vp_FPSWeapon and is pure presentation: look
            // sway, strafe sway, fall and slope sway, the idle bob and the shake. Zeroed here so the
            // sight sits still to look at as well as to shoot with.
            try
            {
                List<vp_FPSWeapon> weapons = Scan(ref _weaponList, ref _weaponNext, ScanPeriod);
                for (int i = 0; i < weapons.Count; i++)
                {
                    vp_FPSWeapon w = weapons[i];
                    if (w == null) continue;
                    int id = w.GetInstanceID();
                    if (!_origWeaponSway.ContainsKey(id))
                    {
                        WeaponSway was = new WeaponSway();
                        was.Look = w.RotationLookSway;
                        was.Strafe = w.RotationStrafeSway;
                        was.Fall = w.RotationFallSway;
                        was.Slope = w.RotationSlopeSway;
                        was.Limits = w.SwayLimits;
                        was.Shake = w.ShakeAmplitude;
                        was.Bob = w.BobAmplitude;
                        _origWeaponSway[id] = was;
                    }
                    w.RotationLookSway = Vector3.zero;
                    w.RotationStrafeSway = Vector3.zero;
                    w.RotationFallSway = Vector3.zero;
                    w.RotationSlopeSway = 0f;
                    w.SwayLimits = Vector3.zero;
                    w.ShakeAmplitude = Vector3.zero;
                    w.BobAmplitude = Vector4.zero;
                }
                _weaponsHeld = weapons.Count;
            }
            catch (System.Exception e)
            {
                Log.OnceWarn("weaponsway-threw", "the weapon's visual sway could not be zeroed: "
                    + e.Message + " - the aim itself is still held steady.");
            }

            try
            {
                List<vp_FPSCamera> cams = Scan(ref _camList, ref _camNext, ScanPeriod);
                for (int i = 0; i < cams.Count; i++)
                {
                    vp_FPSCamera c = cams[i];
                    if (c == null) continue;
                    int id = c.GetInstanceID();
                    if (!_origCamSway.ContainsKey(id))
                    {
                        CameraSway was = new CameraSway();
                        was.Ambient = c.m_MaxAmbientSwayAngleDegreesA;
                        was.AmbientAiming = c.m_MaxAmbientAimingSwayAngleDegreesA;
                        was.Shake = c.ShakeAmplitude;
                        was.ShakeSpeed = c.ShakeSpeed;
                        was.Bob = c.BobAmplitude;
                        _origCamSway[id] = was;
                        Log.OnceInfo("camsway", "no sway: the camera's ambient sway was "
                            + was.Ambient.ToString("0.000") + " degrees and "
                            + was.AmbientAiming.ToString("0.000")
                            + " while aiming. Those are degrees of camera rotation, which is where "
                            + "the shot goes - the gun and the weapon model could never have fixed "
                            + "this on their own.");
                    }
                    c.m_MaxAmbientSwayAngleDegreesA = 0f;
                    c.m_MaxAmbientAimingSwayAngleDegreesA = 0f;
                    c.ShakeAmplitude = Vector3.zero;
                    c.BobAmplitude = Vector4.zero;
                }
                _camsHeld = cams.Count;
            }
            catch (System.Exception e)
            {
                Log.OnceWarn("camsway-threw", "the camera's ambient sway could not be zeroed: "
                    + e.Message + " - retried each second.");
            }
        }

        // ------------------------------------------------------------------------------------------
        // CURING SPEED
        //
        // Hides, guts and anything else that turns into something else over time go through
        // EvolveItem: m_TimeToEvolveGameDays is how long it takes, and m_TimeSpentEvolvingGameHours
        // is how far along it is. Scaling the first is a dial on how fast curing happens.
        //
        // Above 1.00 is faster, which is the direction anyone would expect from something called
        // "curing speed" - so the time is DIVIDED by the dial rather than multiplied. That is the
        // opposite of the fuel dial on purpose: fuel is named for how fast it drains, this is named
        // for how fast the job finishes, and each reads correctly for what it is called.
        // ------------------------------------------------------------------------------------------
        private static readonly Dictionary<int, float> _origEvolveDays = new Dictionary<int, float>();
        private static float _nextCuringScan;
        public static int CuringHeld;

        private static void Curing()
        {
            // The ceiling was 20, which turned a five day cure into six hours - fast, and still a
            // wait. 100 makes it about seventy minutes, and the top of the dial is now a named
            // position rather than a bigger number: at 99 or above the time is set to a floor of
            // 0.002 game days, which is a couple of minutes. Not zero, deliberately - a zero
            // duration sits inside the game's own progress arithmetic and division is unforgiving.
            float dial = Mathf.Clamp(Settings.RateCuring.Value, 0.05f, 100f);
            bool instant = dial >= 99f;
            bool neutral = Mathf.Approximately(dial, 1f);

            if (neutral)
            {
                if (_origEvolveDays.Count > 0) RestoreCuring();
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (now < _nextCuringScan) return;
            _nextCuringScan = now + 1f;

            try
            {
                List<EvolveItem> items = Scan(ref _evolveList, ref _evolveNext, ScanPeriod);
                for (int i = 0; i < items.Count; i++)
                {
                    EvolveItem e = items[i];
                    if (e == null) continue;
                    int id = e.GetInstanceID();
                    float baseline;
                    if (!_origEvolveDays.TryGetValue(id, out baseline))
                    {
                        baseline = e.m_TimeToEvolveGameDays;
                        _origEvolveDays[id] = baseline;
                        Log.OnceInfo("curing-on", "curing speed on - the game's own time to cure is "
                            + baseline.ToString("0.00") + " days for the first item seen, divided by "
                            + "the dial from here.");
                    }
                    e.m_TimeToEvolveGameDays = instant
                        ? 0.002f
                        : baseline / dial;
                }
                CuringHeld = items.Count;
            }
            catch (System.Exception ex)
            {
                Log.OnceWarn("curing-threw", "the curing time could not be set: " + ex.Message
                    + " - retried each second, and the dial stays where it was put.");
            }
        }

        private static void RestoreCuring()
        {
            int n = 0;
            try
            {
                Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<EvolveItem> items =
                    Object.FindObjectsOfType<EvolveItem>();
                for (int i = 0; i < items.Length; i++)
                {
                    EvolveItem e = items[i];
                    if (e == null) continue;
                    float was;
                    if (!_origEvolveDays.TryGetValue(e.GetInstanceID(), out was)) continue;
                    e.m_TimeToEvolveGameDays = was;
                    n++;
                }
            }
            catch (System.Exception) { }

            int held = _origEvolveDays.Count;
            _origEvolveDays.Clear();
            CuringHeld = 0;
            _nextCuringScan = 0f;
            Log.Info("curing speed back to normal - " + n + " of " + held + " item(s) restored.");
        }

        private static void RestoreSway()
        {
            int n = 0;

            // The camera first, because it is the one that was actually moving the aim.
            try
            {
                Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<vp_FPSCamera> cams =
                    Object.FindObjectsOfType<vp_FPSCamera>();
                for (int i = 0; i < cams.Length; i++)
                {
                    vp_FPSCamera c = cams[i];
                    if (c == null) continue;
                    CameraSway cw;
                    if (!_origCamSway.TryGetValue(c.GetInstanceID(), out cw)) continue;
                    c.m_MaxAmbientSwayAngleDegreesA = cw.Ambient;
                    c.m_MaxAmbientAimingSwayAngleDegreesA = cw.AmbientAiming;
                    c.ShakeAmplitude = cw.Shake;
                    c.ShakeSpeed = cw.ShakeSpeed;
                    c.BobAmplitude = cw.Bob;
                }
            }
            catch (System.Exception) { }
            _origCamSway.Clear();
            _camsHeld = 0;

            try
            {
                Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<GunItem> guns =
                    Object.FindObjectsOfType<GunItem>();
                for (int i = 0; i < guns.Length; i++)
                {
                    GunItem g = guns[i];
                    if (g == null) continue;
                    Vector3 v;
                    if (!_origSway.TryGetValue(g.GetInstanceID(), out v)) continue;
                    g.m_SwayValueZeroFatigue = v.x;
                    g.m_SwayValueMaxFatigue = v.y;
                    g.m_SwayIncreasePerSecond = v.z;
                    n++;
                }
            }
            catch (System.Exception) { }

            if (_haveAimShakeWas)
            {
                try
                {
                    vp_FPSWeapon.SetDisableAimShake(_aimShakeWas);
                    vp_FPSWeapon.SetDisableAimSway(_aimSwayWas);
                }
                catch (System.Exception) { }
                _haveAimShakeWas = false;
            }

            try
            {
                Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<vp_FPSWeapon> weapons =
                    Object.FindObjectsOfType<vp_FPSWeapon>();
                for (int i = 0; i < weapons.Length; i++)
                {
                    vp_FPSWeapon w = weapons[i];
                    if (w == null) continue;
                    WeaponSway was;
                    if (!_origWeaponSway.TryGetValue(w.GetInstanceID(), out was)) continue;
                    w.RotationLookSway = was.Look;
                    w.RotationStrafeSway = was.Strafe;
                    w.RotationFallSway = was.Fall;
                    w.RotationSlopeSway = was.Slope;
                    w.SwayLimits = was.Limits;
                    w.ShakeAmplitude = was.Shake;
                    w.BobAmplitude = was.Bob;
                }
            }
            catch (System.Exception) { }
            _origWeaponSway.Clear();
            _weaponsHeld = 0;

            int held = _origSway.Count;
            _origSway.Clear();
            _swayHeld = 0;
            _nextSwayScan = 0f;
            Log.Info("no sway off - " + n + " of " + held + " gun(s) got their sway back.");
        }

        private static void RestoreRecoil()
        {
            int n = 0;
            try
            {
                Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<vp_FPSShooter> shooters =
                    Object.FindObjectsOfType<vp_FPSShooter>();
                for (int i = 0; i < shooters.Length; i++)
                {
                    vp_FPSShooter sh = shooters[i];
                    if (sh == null) continue;
                    int id = sh.GetInstanceID();
                    Vector3 pos, rot;
                    float dry;
                    if (_origRecoilPos.TryGetValue(id, out pos)) { sh.MotionPositionRecoil = pos; n++; }
                    if (_origRecoilRot.TryGetValue(id, out rot)) sh.MotionRotationRecoil = rot;
                    if (_origDryFire.TryGetValue(id, out dry)) sh.MotionDryFireRecoil = dry;
                }
            }
            catch (System.Exception) { }

            int guns = 0;
            try
            {
                Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<GunItem> gs =
                    Object.FindObjectsOfType<GunItem>();
                for (int i = 0; i < gs.Length; i++)
                {
                    GunItem g = gs[i];
                    if (g == null) continue;
                    Vector4 v;
                    if (!_origGunRecoil.TryGetValue(g.GetInstanceID(), out v)) continue;
                    g.m_PitchRecoilMin = v.x;
                    g.m_PitchRecoilMax = v.y;
                    g.m_YawRecoilMin = v.z;
                    g.m_YawRecoilMax = v.w;
                    guns++;
                }
            }
            catch (System.Exception) { }

            int held = _origRecoilPos.Count;
            int gunsHeld = _origGunRecoil.Count;
            _origRecoilPos.Clear();
            _origRecoilRot.Clear();
            _origDryFire.Clear();
            _origGunRecoil.Clear();
            _recoilHeld = 0;
            _gunsHeld = 0;
            _nextRecoilScan = 0f;
            Log.Info("no recoil off - " + guns + " of " + gunsHeld + " gun(s) and " + n + " of "
                + held + " shooter(s) got their kick back.");
        }

        // ------------------------------------------------------------------------------------------
        // LONGER DAYS, SHORTER NIGHTS
        //
        // TimeOfDay keeps the two halves as separate numbers - m_DayDurationInMinutes and
        // m_NightDurationInMinutes - which is what makes one dial able to do both at once: the day is
        // multiplied and the night divided by the same figure. At 2.00 the day is twice as long and
        // the night half as long, and a full cycle still takes roughly the time it used to.
        //
        // ONE DIAL RATHER THAN TWO, because the thing actually wanted is the ratio. Two sliders would
        // let the pair drift into a 40-hour day nobody asked for, and would need a third number to
        // say what a "day" now means.
        //
        // These are ints, so the write is rounded and floored at one minute - a zero-length night is
        // a division waiting to happen inside somebody else's code.
        private static bool _haveDayNight;
        private static int _origDayMinutes;
        private static int _origNightMinutes;
        public static string DayNightNow = "";

        private static void DayNight()
        {
            TimeOfDay tod = null;
            try { tod = GameManager.GetTimeOfDayComponent(); } catch (System.Exception) { }
            if (tod == null) return;

            float dial = Mathf.Clamp(Settings.RateDaylight.Value, 0.25f, 4f);
            bool neutral = Mathf.Approximately(dial, 1f);

            try
            {
                if (neutral)
                {
                    if (_haveDayNight)
                    {
                        tod.m_DayDurationInMinutes = _origDayMinutes;
                        tod.m_NightDurationInMinutes = _origNightMinutes;
                        _haveDayNight = false;
                        Log.Info("day length back to the game's own " + _origDayMinutes
                            + " minutes of day and " + _origNightMinutes + " of night.");
                    }
                    else
                    {
                        // Neutral means the game's numbers ARE the baseline, so they are tracked
                        // rather than remembered - the same discipline the survival rates use, and
                        // for the same reason: never re-baseline from a value we wrote ourselves.
                        _origDayMinutes = tod.m_DayDurationInMinutes;
                        _origNightMinutes = tod.m_NightDurationInMinutes;
                    }
                    DayNightNow = "";
                    return;
                }

                if (!_haveDayNight)
                {
                    _haveDayNight = true;
                    Log.Info("day length dial on - the game's own cycle is " + _origDayMinutes
                        + " minutes of day and " + _origNightMinutes + " of night.");
                }

                int day = Mathf.Max(1, Mathf.RoundToInt(_origDayMinutes * dial));
                int night = Mathf.Max(1, Mathf.RoundToInt(_origNightMinutes / dial));
                tod.m_DayDurationInMinutes = day;
                tod.m_NightDurationInMinutes = night;
                DayNightNow = day + "m day / " + night + "m night";
            }
            catch (System.Exception e)
            {
                Log.OnceWarn("daynight", "the day and night lengths could not be set: " + e.Message
                    + " - retried every sweep, and the dial stays where it was put.");
            }
        }

        /// <summary>Every timed buff and where its clock stands, for the window.</summary>
        public static List<string> BuffLines()
        {
            List<string> lines = new List<string>();
            for (int i = 0; i < _buffRows.Count; i++)
            {
                BuffRow r = _buffRows[i];
                if (r.Remaining <= 0.001f) lines.Add(r.Name + ": not active");
                else lines.Add(r.Name + ": " + r.Remaining.ToString("0.00") + "h of "
                    + r.Duration.ToString("0.00") + "h");
            }
            return lines;
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
                    _origVelocityMax = _controller.MotorVelocityMax;
                    _origVelMaxById[id] = _origVelocityMax;
                    Log.Info("speed cheat on - acceleration " + _origAcceleration.ToString("0.0000")
                        + ", velocity cap " + _origVelocityMax.ToString("0.0000")
                        + ", both scaled by " + mult.ToString("0.00") + "x.");
                }
                _origVelMaxById.TryGetValue(id, out _origVelocityMax);
                _haveAcceleration = true;

                // BOTH FIELDS, and the second one is why the first build did nothing.
                //
                // Raising MotorAcceleration only makes the character reach its top speed sooner; the
                // top speed itself is MotorVelocityMax, and it was left where it was. The log looked
                // right - acceleration 0.0300 scaled by 8.00x - and the character walked at exactly
                // the same pace, because it was hitting the same cap a fraction earlier. A number
                // changing is not the same as an effect happening, which is the whole reason this
                // file measures rather than assumes.
                _controller.MotorAcceleration = _origAcceleration * mult;
                _controller.MotorVelocityMax = _origVelocityMax * mult;
                Measure();
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

        /// <summary>
        /// How fast the player is actually moving, in metres a second, horizontally.
        ///
        /// This exists because "player speed did not work" and the log said the cheat was applied -
        /// both true at once, and neither of them a measurement. A number written into a field is
        /// not an effect; this is the difference, printed every half minute while the dial is off
        /// its neutral position.
        /// </summary>
        private static void Measure()
        {
            Transform p = null;
            try { p = GameManager.GetPlayerTransform(); } catch (System.Exception) { }
            if (p == null) return;

            float now = Time.realtimeSinceStartup;
            Vector3 here = p.position;

            if (_lastPosAt > 0f && now > _lastPosAt)
            {
                float dt = now - _lastPosAt;
                if (dt > 0.001f && dt < 0.5f)
                {
                    Vector3 d = here - _lastPos;
                    d.y = 0f;
                    float mps = d.magnitude / dt;
                    if (mps < 40f && mps > _fastestSeen) _fastestSeen = mps;   // 40 filters teleports
                }
            }
            _lastPos = here;
            _lastPosAt = now;

            if (now >= _nextSpeedReport)
            {
                _nextSpeedReport = now + 30f;
                if (_fastestSeen > 0.1f)
                {
                    Log.Info("fastest ground speed seen: " + _fastestSeen.ToString("0.00")
                        + " m/s at a " + Settings.CheatSpeed.Value.ToString("0.00")
                        + "x dial. Walking is about 1.4 and sprinting about 3.5 unmodified.");
                }
                _fastestSeen = 0f;
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
                    RestoreEncumberBands(enc, encId);
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

                // AND THE FIVE OTHER WEIGHTS, WHICH IS WHAT THE FIRST VERSION MISSED.
                //
                // Raising the cap alone bought the right to CARRY 500 kg and nothing else. The
                // encumbrance system does not work in fractions of the cap - it has its own absolute
                // weights, and they were untouched:
                //
                //     m_EncumberLowThreshold / Med / High     when it complains, and how loudly
                //     m_NoSprintCarryCapacity                 when sprinting stops
                //     m_NoWalkCarryCapacity                   when walking stops
                //     m_MaxCarryCapacityWhenExhausted         the tired cap
                //
                // So at 66 kg the pack was legal and the character was still past every band: told
                // to drop something, and slowed down by GetEncumbranceSlowdownMultiplier which reads
                // those same numbers. The cheat was half a cheat.
                //
                // They are scaled by the SAME RATIO as the cap rather than flattened to it, so the
                // bands keep their shape - a 500 kg cap simply puts the first complaint around 300 kg
                // instead of 30. Flattening them would have removed the encumbrance system entirely,
                // which is a different feature nobody asked for.
                ScaleEncumberBands(enc, encId, want);
            }
            catch (System.Exception e)
            {
                Log.OnceWarn("carry-threw", "raising the carry cap threw: " + e.Message
                    + " - it is retried on the next sweep and the cheat stays on.");
            }
        }

        /// <summary>The five weights beside the cap, kept in the same proportion to it.</summary>
        private struct EncumberBands
        {
            public ItemWeight Exhausted, NoSprint, NoWalk, Low, Med, High;
        }

        private static readonly Dictionary<int, EncumberBands> _origBands =
            new Dictionary<int, EncumberBands>();

        private static void ScaleEncumberBands(Encumber enc, int encId, ItemWeight newMax)
        {
            try
            {
                EncumberBands was;
                if (!_origBands.TryGetValue(encId, out was))
                {
                    was.Exhausted = enc.m_MaxCarryCapacityWhenExhausted;
                    was.NoSprint = enc.m_NoSprintCarryCapacity;
                    was.NoWalk = enc.m_NoWalkCarryCapacity;
                    was.Low = enc.m_EncumberLowThreshold;
                    was.Med = enc.m_EncumberMedThreshold;
                    was.High = enc.m_EncumberHighThreshold;
                    _origBands[encId] = was;

                    Log.Info("carry cheat: the game's encumbrance bands are low "
                        + Sweep.KG(was.Low).ToString("0.0") + "kg, med "
                        + Sweep.KG(was.Med).ToString("0.0") + "kg, high "
                        + Sweep.KG(was.High).ToString("0.0") + "kg, no-sprint "
                        + Sweep.KG(was.NoSprint).ToString("0.0") + "kg, no-walk "
                        + Sweep.KG(was.NoWalk).ToString("0.0")
                        + "kg. Raising the cap alone left every one of these where it was, which is "
                        + "why the complaining and the slowdown carried on.");
                }

                float origMaxKg = Sweep.KG(_origCapacity);
                if (origMaxKg <= 0.01f) return;              // no baseline to scale against
                float ratio = Sweep.KG(newMax) / origMaxKg;

                enc.m_MaxCarryCapacityWhenExhausted = was.Exhausted * ratio;
                enc.m_NoSprintCarryCapacity = was.NoSprint * ratio;
                enc.m_NoWalkCarryCapacity = was.NoWalk * ratio;
                enc.m_EncumberLowThreshold = was.Low * ratio;
                enc.m_EncumberMedThreshold = was.Med * ratio;
                enc.m_EncumberHighThreshold = was.High * ratio;
            }
            catch (System.Exception e)
            {
                Log.OnceWarn("bands-threw", "the encumbrance bands could not be scaled: " + e.Message
                    + " - the cap is still raised, so the pack holds more, but the game may still "
                    + "complain about the weight.");
            }
        }

        private static void RestoreEncumberBands(Encumber enc, int encId)
        {
            EncumberBands was;
            if (!_origBands.TryGetValue(encId, out was)) return;
            try
            {
                enc.m_MaxCarryCapacityWhenExhausted = was.Exhausted;
                enc.m_NoSprintCarryCapacity = was.NoSprint;
                enc.m_NoWalkCarryCapacity = was.NoWalk;
                enc.m_EncumberLowThreshold = was.Low;
                enc.m_EncumberMedThreshold = was.Med;
                enc.m_EncumberHighThreshold = was.High;
            }
            catch (System.Exception) { }
            _origBands.Remove(encId);
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
                    List<BodyHarvest> all = Scan(ref _carcassList, ref _carcassNext, ScanPeriod);
                    for (int i = 0; i < all.Count; i++)
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

                // NO EARLY RETURN ON m_RoundsInClip, AND THAT WAS THE BUG.
                //
                // The first build read that field, found it already equal to the clip size, and went
                // home. The log said so for five minutes while rounds were being fired:
                //
                //     ammo(2 topups)   ... ammo(2 topups)   ... ammo(2 topups)
                //
                // Two top-ups at the start and never again. The count was not moving because the
                // game does not spend a round by decrementing that field - it takes the round out of
                // m_Clip, the list that says what each round IS. So the count stayed full, the list
                // emptied, and the guard blocked every refill.
                //
                // Both are now checked and both are written, every frame, with no shortcut. It costs
                // two integer comparisons and it cannot be fooled by whichever one the game happens
                // to use this patch.
                Il2CppSystem.Collections.Generic.List<int> clip = gun.m_Clip;
                int clipCount = clip == null ? size : clip.Count;
                int rounds = gun.m_RoundsInClip;
                if (clipCount >= size && rounds >= size && gun.m_SpentCasingsInClip == 0) return;

                // Contents first, then the count. The other order leaves a frame in which the gun
                // believes it holds rounds it cannot describe.
                if (clip != null)
                {
                    int pattern = clip.Count > 0 ? clip[clip.Count - 1] : 0;
                    while (clip.Count < size) clip.Add(pattern);
                }
                gun.m_RoundsInClip = size;
                gun.m_SpentCasingsInClip = 0;
                gun.m_HasMisfired = false;
                _ammoTopUps++;

                if (_ammoTopUps <= 3)
                {
                    Log.Info("ammo topped up: the clip list held " + clipCount + " and the count said "
                        + rounds + ", against a clip size of " + size
                        + ". Both are set to full now. (Said for the first three only.)");
                }
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
                    List<Fire> all = Scan(ref _fireList, ref _fireListNext, ScanPeriod);
                    for (int i = 0; i < all.Count; i++) if (all[i] != null) _fires.Add(all[i]);
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
                        _origMaxOn[id] = f.m_MaxOnTODSeconds;
                        Log.OnceInfo("fire-on", "perpetual fire on - the game's own m_IsPerpetual flag "
                            + "is being set on burning fires, and cleared again when you turn this off.");
                    }
                    f.m_IsPerpetual = true;

                    // A NIGHT'S SLEEP IS NOT A SEQUENCE OF FRAMES, WHICH IS WHY THE FLAG ALONE LOST.
                    //
                    // Reported from play: stoves, outdoor fires, barbecues and fire drums are all out
                    // by morning. Sleeping does not run the fire through eight hours of updates - it
                    // fast-forwards, resolving the whole night in one call, and a pass that only
                    // re-asserts a flag every few seconds never gets a turn in the middle of that.
                    //
                    // So the fuel clock itself is held rather than watched: the maximum burn is set
                    // to a year of seconds and the elapsed time is wound back whenever it climbs past
                    // a tenth of it. Whatever arithmetic the fast-forward does, it starts from a fire
                    // that has a year left rather than four hours.
                    f.m_MaxOnTODSeconds = YearOfSeconds;
                    if (f.m_ElapsedOnTODSeconds > YearOfSeconds * 0.1f)
                    {
                        f.m_ElapsedOnTODSeconds = 0f;
                        f.m_ElapsedOnTODSecondsUnmodified = 0f;
                    }

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
                    int fid = f.GetInstanceID();
                    bool was;
                    if (_origPerpetual.TryGetValue(fid, out was)) { f.m_IsPerpetual = was; n++; }
                    float maxWas;
                    if (_origMaxOn.TryGetValue(fid, out maxWas)) f.m_MaxOnTODSeconds = maxWas;
                }
            }
            catch (System.Exception) { }

            int held = _origPerpetual.Count;
            _origPerpetual.Clear();
            _origMaxOn.Clear();
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
            DropScans();

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
