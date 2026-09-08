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
            _sceneStamp++;
            _worldReady = false;
        }

        public override void OnUpdate()
        {
            if (!Settings.Enabled.Value) return;

            Hotkeys();

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
                Log.Info("world " + _sceneStamp + " is live - sweeping every "
                    + Settings.ScanIntervalSeconds.Value.ToString("0.00") + "s within "
                    + Settings.ScanRadius.Value.ToString("0") + "m.");
            }

            float now = Time.realtimeSinceStartup;
            if (now >= _nextSweep)
            {
                _nextSweep = now + Mathf.Max(0.05f, Settings.ScanIntervalSeconds.Value);

                Sweep.Run(player.position, Settings.ScanRadius.Value);
                Pickup.Pass(Settings.PickupRadius.Value, Mathf.Max(1, Settings.PickupPerSweep.Value));
                Grind.AutoHarvestPass();
                Grind.BreakDownPass();
                Highlight.Refresh();
            }

            // Drawn every frame, not every sweep: the outline must follow the camera, not stutter
            // at the sweep interval.
            Silhouette.Draw();

            Diagnostics.Tick(now);
            Ui.HoldCursor();
        }

        public override void OnGUI()
        {
            if (!Settings.Enabled.Value) return;
            Ui.DrawLabels();
            Ui.Draw();
        }

        private void Hotkeys()
        {
            try
            {
                if (Input.GetKeyDown(Settings.Key(Settings.KeyWindow, KeyCode.F10)))
                    Ui.Toggle();

                if (Input.GetKeyDown(Settings.Key(Settings.KeyToggleHighlight, KeyCode.F8)))
                {
                    Settings.HighlightEnabled.Value = !Settings.HighlightEnabled.Value;
                    if (!Settings.HighlightEnabled.Value) Highlight.Off();
                    MelonPreferences.Save();
                    Log.Info("outlines " + (Settings.HighlightEnabled.Value ? "on" : "off"));
                }

                if (Input.GetKeyDown(Settings.Key(Settings.KeyTogglePickup, KeyCode.F9)))
                {
                    Settings.PickupEnabled.Value = !Settings.PickupEnabled.Value;
                    MelonPreferences.Save();
                    Log.Info("auto pickup " + (Settings.PickupEnabled.Value ? "on" : "off"));
                }

                if (Input.GetKeyDown(Settings.Key(Settings.KeyReport, KeyCode.F11)))
                    Diagnostics.Report(true);

                if (Input.GetKeyDown(Settings.Key(Settings.KeySweepRoom, KeyCode.F7)))
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
