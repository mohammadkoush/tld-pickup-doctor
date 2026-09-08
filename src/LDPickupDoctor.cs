// LD Pickup Doctor - The Long Dark.
//
// The Green Hell mod of the same name, brought across: it finds out why an item is not being picked
// up, says so in plain words, picks it up, outlines what is worth walking to in a colour that means
// something, and takes the repetition out of harvesting.
//
// WHAT IS DELIBERATELY NOT HERE
//
// Nothing that lowers the game's price. Auto-pickup removes clicking, not weight. Auto-harvest
// removes the hold, not the yield or the tool. The one switch that refunds hours is off, labelled,
// and puts everything back when you turn it off. When a feature is convenient AND cheaper, the
// cheapness is a bug in the feature, not a bonus.
//
// THE ONE THING TO KNOW ABOUT THIS GAME'S LOADER, written here because it cost an hour to find:
// MelonLoader's default proxy is version.dll, and on this machine the Windows application
// compatibility shim engine (apphelp plus AcGenral) loads System32's version.dll into tld.exe
// BEFORE UnityPlayer does. A module already loaded is never resolved again, so the game folder copy
// was never looked at and MelonLoader silently did nothing at all - no log, no interop assemblies,
// no error. Renaming the proxy to winmm.dll fixes it, because winmm is loaded later, by UnityPlayer
// itself, which does honour the application directory. tools\loader-doctor.ps1 detects and repairs
// exactly that.

using Il2Cpp;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(LDPickupDoctor.PickupDoctorMod), "LD Pickup Doctor", "0.1.0", "mohammadkoush")]
[assembly: MelonGame("Hinterland", "TheLongDark")]

namespace LDPickupDoctor
{
    public class PickupDoctorMod : MelonMod
    {
        private float _nextSweep;
        private bool _worldReady;
        private int _sceneStamp;
        private float _lastWorldLog;

        public override void OnInitializeMelon()
        {
            Log.Attach(LoggerInstance);
            Settings.Load();
            Log.Info("LD Pickup Doctor 0.1.0 ready. " + Settings.KeyWindow.Value + " opens the settings window.");
            Log.Info("grind is removed; price is not. Auto-pickup takes clicking away, never weight.");
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            // Instance ids from the old scene mean nothing now, and a mark that outlives its scene
            // is a null renderer drawn every frame.
            Silhouette.ClearAll();
            Highlight.Off();
            Grind.ForgetScene();
            Cheats.ForgetScene();
            _sceneStamp++;
            _worldReady = false;
        }

        public override void OnUpdate()
        {
            if (!Settings.Enabled.Value) return;

            Hotkeys();
            Settings.FlushSaves();
            ShotWatch.Tick();

            Transform player = null;
            try { player = GameManager.GetPlayerTransform(); } catch (System.Exception) { }

            bool inWorld = player != null;
            try { if (GameManager.IsMainMenuActive()) inWorld = false; } catch (System.Exception) { }

            if (!inWorld)
            {
                if (_worldReady)
                {
                    // Leaving the world is not a failure, but it IS a state change worth saying once,
                    // so a quiet mod in the menu never reads as a broken one.
                    Log.OnceInfo("left-world", "no player in the world - sweeping is paused until there is one.");
                    Highlight.Off();
                    _worldReady = false;
                }
                return;
            }

            if (!_worldReady)
            {
                _worldReady = true;
                Log.Forget("left-world");
                // RATE LIMITED, because the first run wrote this seven times in three minutes.
                // The Long Dark initialises a scene for every interior, cave mouth and transition,
                // and a line per transition turns the one line that matters into scrollback.
                float since = Time.realtimeSinceStartup - _lastWorldLog;
                if (since > 120f || _lastWorldLog == 0f)
                {
                    _lastWorldLog = Time.realtimeSinceStartup;
                    Log.Info("world is live - sweeping every "
                        + Settings.ScanIntervalSeconds.Value.ToString("0.00") + "s within "
                        + Settings.ScanRadius.Value.ToString("0") + "m.");
                }
            }

            float now = Time.realtimeSinceStartup;

            // NO SWEEPING WHILE THE SETTINGS WINDOW IS OPEN. The world is paused behind it, so
            // nothing out there can have changed - and a sweep that runs anyway does two unwanted
            // things: it makes every counter in the Items tab climb while it is being read, and it
            // lets auto-pickup and auto-harvest fire behind a window opened in order to stop and
            // think. Outlines and cheats carry on below; only the world-changing pass stops.
            if (Ui.Open) { Diagnostics.Tick(now); Ui.HoldCursor(); Silhouette.Draw(); return; }

            if (now >= _nextSweep)
            {
                _nextSweep = now + Mathf.Max(0.05f, Settings.ScanIntervalSeconds.Value);

                Sweep.Run(player.position, Settings.ScanRadius.Value);
                Pickup.Pass(Settings.PickupRadius.Value, Mathf.Max(1, Settings.PickupPerSweep.Value));
                Grind.AutoHarvestPass();
                Grind.BreakDownPass();
                Cheats.SlowTick();
                Highlight.Refresh();
            }

            // Every frame, both of them: the speed cheat is fighting the game's own movement state
            // machine for one field, and a clip that refills a quarter second late is a click that
            // did nothing.
            Cheats.FastTick();

            // Drawn every frame, not every sweep: the outline must follow the camera, not stutter
            // at the sweep interval.
            Silhouette.Draw();

            Diagnostics.Tick(now);
            Ui.HoldCursor();
        }

        public override void OnGUI()
        {
            if (!Settings.Enabled.Value) return;
            Ui.DrawCollisionBanner();
            Ui.DrawLabels();
            Ui.Draw();
        }

        private float _lastSave;

        /// <summary>
        /// Save where you stand, through the game's own save so the "game saved" message and the
        /// slot handling are the game's rather than ours.
        ///
        /// EVERY REFUSAL SAYS WHY, same rule as pickup. A save key that sometimes does nothing and
        /// never explains is worse than no save key: you stop trusting it and save by sleeping
        /// anyway, which is the chore this was meant to remove.
        /// </summary>
        private void SaveNow()
        {
            float now = Time.realtimeSinceStartup;
            float cooldown = Mathf.Max(1f, Settings.SaveCooldownSeconds.Value);
            if (now - _lastSave < cooldown)
            {
                Log.Info("save key: " + (cooldown - (now - _lastSave)).ToString("0.0")
                    + "s left on the cooldown.");
                return;
            }

            try
            {
                if (GameManager.IsMainMenuActive() || GameManager.GetPlayerTransform() == null)
                {
                    Log.Info("save key: not in a world.");
                    return;
                }
                if (GameManager.SaveIsBlockedDueToRestoreGame())
                {
                    Log.Info("save key: the game is blocking saves right now (a restore is in flight).");
                    return;
                }
                if (SaveGameSystem.IsAsyncSaveRunning())
                {
                    Log.Info("save key: a save is already running.");
                    return;
                }

                GameManager.SaveGameAndDisplayHUDMessage();
                _lastSave = now;
                Log.Info("save key: saved.");
            }
            catch (System.Exception e)
            {
                // The intent is never cleared here: the cooldown is NOT stamped on failure, so the
                // next press tries again immediately rather than being told to wait for a save that
                // never happened.
                Log.OnceWarn("save-threw", "the save key threw: " + e.Message
                    + " - the key still works and will try again on the next press.");
            }
        }

        /// <summary>
        /// Is the modifier satisfied? See Keys.NeedCtrl for why this exists at all.
        ///
        /// THE COST OF NOT HAVING IT, measured rather than imagined: fifty-two high-resolution
        /// screenshots, about 550 MB, appeared on the desktop across one evening of testing. The
        /// proof was one line of our own log against the file times -
        ///
        ///     01:04:42.252  auto pickup off          (the mod, on F9)
        ///     01:04:42, 01:04:42, 01:04:43           (three screenshots)
        ///
        /// - because The Long Dark binds its own high-resolution screenshot to the same bare key.
        /// A modifier removes the whole class of collision instead of dodging the one key that was
        /// caught, which matters because the next collision would be just as silent.
        /// </summary>
        private static bool Modifier()
        {
            if (!Settings.KeysNeedCtrl.Value) return true;
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        }

        private void Hotkeys()
        {
            try
            {
                bool mod = Modifier();

                if (mod && Input.GetKeyDown(Settings.Key(Settings.KeyWindow, KeyCode.F10)))
                {
                    ShotWatch.Note("the window key");
                    Ui.Toggle();
                }

                if (mod && Input.GetKeyDown(Settings.Key(Settings.KeyToggleHighlight, KeyCode.F8)))
                {
                    ShotWatch.Note("the outline key");
                    Settings.HighlightEnabled.Value = !Settings.HighlightEnabled.Value;
                    if (!Settings.HighlightEnabled.Value) Highlight.Off();
                    Settings.SaveSoon();
                    Log.Info("outlines " + (Settings.HighlightEnabled.Value ? "on" : "off"));
                }

                if (mod && Input.GetKeyDown(Settings.Key(Settings.KeyTogglePickup, KeyCode.F9)))
                {
                    ShotWatch.Note("the pickup key");
                    Settings.PickupEnabled.Value = !Settings.PickupEnabled.Value;
                    Settings.SaveSoon();
                    Log.Info("auto pickup " + (Settings.PickupEnabled.Value ? "on" : "off"));
                }

                if (mod && Input.GetKeyDown(Settings.Key(Settings.KeyReport, KeyCode.F11)))
                {
                    ShotWatch.Note("the report key");
                    Diagnostics.Report(true);
                }

                if (mod && Input.GetKeyDown(Settings.Key(Settings.KeySpeedUp, KeyCode.PageUp)))
                    Cheats.NudgeSpeed(1);

                if (mod && Input.GetKeyDown(Settings.Key(Settings.KeySpeedDown, KeyCode.PageDown)))
                    Cheats.NudgeSpeed(-1);

                if (Input.GetKeyDown(Settings.Key(Settings.KeySave, KeyCode.S)))
                {
                    bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                    if ((!Settings.KeySaveNeedsCtrl.Value && !Settings.KeysNeedCtrl.Value) || ctrl)
                        SaveNow();
                }

                if (mod && Input.GetKeyDown(Settings.Key(Settings.KeySweepRoom, KeyCode.F7)))
                {
                    Transform p = null;
                    try { p = GameManager.GetPlayerTransform(); } catch (System.Exception) { }
                    if (p != null) Grind.SweepRoom(p.position);
                    else Log.Info("sweep key pressed with no player in the world.");
                }
            }
            catch (System.Exception e)
            {
                // If legacy Input is ever turned off in a game update this is where it shows up.
                // It must not take the rest of the mod down with it, and it must not be silent.
                Log.OnceWarn("input-threw",
                    "reading the keyboard threw: " + e.Message + " - hotkeys are unavailable this "
                    + "session, but sweeping, pickup and outlines carry on. Settings can still be "
                    + "edited in UserData\\MelonPreferences.cfg.");
            }
        }
    }
}
