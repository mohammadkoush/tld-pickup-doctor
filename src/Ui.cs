// The settings window. F10 by default.
//
// TWO RULES THE TAB CODE HOLDS TO, both load-bearing, both carried over from the Green Hell version
// because they come from how a window like this is actually used:
//
//   ORDER NEVER CHANGES. Tabs are appended, never inserted, and the order of rows inside a tab is
//   fixed. An option is found by where it sits, not by reading down the column - one that
//   moves between launches cannot be found by memory at all.
//
//   NO OPTION APPEARS TWICE. The Keys tab OWNS every hotkey wherever it was bound, and those rows
//   are skipped in their home tab. An option in two places has no location.
//
// The tooltip strip at the bottom is on a DELAY on purpose. With an instant tooltip, sweeping down a
// column covers the row you were trying to read. Interface.TooltipDelaySeconds is the dial, and zero
// gives the instant behaviour back.

using System.Collections.Generic;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace LDPickupDoctor
{
    internal static class Ui
    {
        public static bool Open;

        private static readonly string[] Tabs =
        {
            "Items", "Pickup", "Highlight", "Colours", "Grind", "Keys", "Advanced", "Interface", "Cheats"
        };
        private static int _tab;

        private static Rect _rect = new Rect(60f, 60f, 720f, 560f);
        private static Vector2 _scroll;
        private static string _itemFilter = "";

        private static string _hoverText = "";
        private static string _hoverKey = "";
        private static float _hoverSince;

        private static MelonPreferences_Entry<string> _rebinding;

        private static GUIStyle _panel, _head, _foot, _label, _mono;
        private static Texture2D _bg, _stripBg;
        private static bool _skinReady;

        // Cursor state we took, so it can be handed back exactly as it was.
        private static CursorLockMode _lockWas;
        private static bool _visibleWas;

        public static void Toggle()
        {
            Open = !Open;
            if (Open)
            {
                _lockWas = Cursor.lockState;
                _visibleWas = Cursor.visible;
                PauseOn();
            }
            else
            {
                if (_focusKey.Length > 0) Commit();
                _rebinding = null;
                Settings.SaveNow();
                if (Settings.WindowPausesCursor.Value)
                {
                    Cursor.lockState = _lockWas;
                    Cursor.visible = _visibleWas;
                }
                PauseOff();
            }
        }

        /// <summary>Called every frame while open - the game re-locks the cursor, so we re-free it.</summary>
        public static void HoldCursor()
        {
            if (Open && Settings.WindowPausesCursor.Value)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            HoldPause();
            SwallowPauseMenu();
            GuardUnpause();
        }

        // ---- pausing -----------------------------------------------------------------------------
        //
        // GameManager.m_IsPaused is the game's own flag, read by its own UpdatePaused/UpdateNotPaused
        // split, so setting it is the honest way to pause rather than freezing the clock underneath
        // a game that does not know it stopped.
        //
        // It is RE-ASSERTED EVERY FRAME, and then READ BACK. Setting a flag the game also writes is
        // not evidence that anything is paused, and this station has a rule about the difference. If
        // the flag keeps coming back false, the game is fighting us, and rather than pretending
        // otherwise the mod escalates to Time.timeScale = 0 and says in the log that it had to.

        private static bool _pausedWas;
        private static bool _havePaused;
        private static int _pauseLosses;
        private static float _timeScaleWas = 1f;
        private static bool _frozeTime;
        private static float _escapeClosedAt = -99f;

        private static void PauseOn()
        {
            if (!Settings.WindowPausesGame.Value) return;
            try
            {
                if (GameManager.IsMainMenuActive()) return;   // nothing to pause in a menu
                _pausedWas = GameManager.m_IsPaused;
                _havePaused = true;
                _pauseLosses = 0;
                GameManager.m_IsPaused = true;
            }
            catch (System.Exception e)
            {
                Log.OnceWarn("pause-threw", "pausing threw: " + e.Message
                    + " - the window still opens, the world just keeps running behind it.");
            }
        }

        private static void HoldPause()
        {
            if (!Open || !_havePaused) return;
            try
            {
                if (!GameManager.m_IsPaused)
                {
                    _pauseLosses++;
                    GameManager.m_IsPaused = true;

                    if (_pauseLosses == 5 && !_frozeTime)
                    {
                        // ESCALATE. Five frames of the game clearing our flag is the game winning,
                        // not a wobble. Freeze the clock instead - our own code runs on realtime, so
                        // the window keeps working.
                        _timeScaleWas = Time.timeScale;
                        Time.timeScale = 0f;
                        _frozeTime = true;
                        Log.Warn("the game cleared its own pause flag five frames running, so time is "
                            + "frozen instead while the window is open. Both are undone on the way out.");
                    }
                }
            }
            catch (System.Exception) { }
        }

        private static void PauseOff()
        {
            if (!_havePaused && !_frozeTime) return;
            try { GameManager.m_IsPaused = _pausedWas; } catch (System.Exception) { }
            if (_frozeTime)
            {
                Time.timeScale = _timeScaleWas <= 0f ? 1f : _timeScaleWas;
                _frozeTime = false;
            }
            _havePaused = false;
            _pauseLosses = 0;
        }

        /// <summary>
        /// THE GAME MUST NEVER BE LEFT PAUSED BY US. This is the guard that makes that true.
        ///
        /// The order of events on Escape is what broke it, and the log gave the whole sequence:
        ///
        ///   1. our window sees Escape, closes, and unpauses    -> m_IsPaused = false
        ///   2. the game polls the SAME Escape, opens its pause menu, and pauses ITSELF
        ///   3. we close that menu, because it was not what the key was pressed for
        ///
        /// After step 3 the menu is gone and the flag the menu set is still true, so the world is
        /// frozen with nothing on screen to unfreeze it - and pressing Escape again just repeats the
        /// whole dance. Unpausing once at step 1 was never going to be enough, because the pause that
        /// mattered had not happened yet.
        ///
        /// So for two seconds after an Escape that closed our window, the flag is checked every frame
        /// and cleared if it comes back. Two seconds, and only after OUR Escape, so a pause menu opened
        /// deliberately a moment later is left completely alone.
        /// </summary>
        private static void GuardUnpause()
        {
            if (Open) return;
            if (Time.realtimeSinceStartup - _escapeClosedAt > 2f) return;
            try
            {
                // Both mechanisms, because there are two ways to be frozen and only one of them is
                // the flag. If the game's own menu also stopped the clock on its way in, clearing
                // the flag alone would leave the world just as still and the log just as innocent.
                if (Time.timeScale == 0f && !_frozeTime)
                {
                    Time.timeScale = 1f;
                    Log.OnceInfo("guard-timescale",
                        "time was still stopped after the settings window closed, so it was started "
                        + "again. This only happens in the two seconds after an Escape.");
                }

                if (GameManager.m_IsPaused)
                {
                    GameManager.m_IsPaused = false;
                    Log.OnceInfo("guard-unpause",
                        "the game paused itself on the same Escape that closed the settings window, "
                        + "one frame after we had already unpaused. It is cleared again for two "
                        + "seconds after that key, which is the only window in which it can happen.");
                }
            }
            catch (System.Exception e)
            {
                Log.OnceWarn("guard-threw", "could not clear the game's pause flag: " + e.Message
                    + " - press Escape once to open the pause menu and once more to close it.");
            }
        }

        /// <summary>
        /// Escape closes this window, and the game reads Escape too - through its own input polling,
        /// which no IMGUI Event.Use can reach. So the press that closes the settings opens the game's
        /// pause menu a frame later. This closes that menu, but ONLY within a third of a second of an
        /// Escape that closed our window, so a deliberate Escape is left alone.
        /// </summary>
        private static void SwallowPauseMenu()
        {
            if (Time.realtimeSinceStartup - _escapeClosedAt > 0.35f) return;
            try
            {
                if (InterfaceManager.IsPanelEnabled<Panel_PauseMenu>())
                {
                    InterfaceManager.TrySetPanelEnabled<Panel_PauseMenu>(false);
                    // The timestamp is deliberately NOT cleared here. It is what keeps GuardUnpause
                    // running for the two seconds after this, and clearing it was the reason the
                    // world stayed frozen: the menu went away and the flag it had set did not.
                    Log.OnceInfo("swallowed-pause",
                        "the Escape that closed the settings window also opened the game's pause menu, "
                        + "so that menu was closed again. The game reads Escape through its own input "
                        + "polling, which a mod cannot intercept.");
                }
            }
            catch (System.Exception e)
            {
                // The intent is not cleared: GuardUnpause still runs, so even if the menu stays up
                // the world is not left frozen behind it.
                Log.OnceWarn("swallow-threw", "could not close the game's pause menu after Escape: "
                    + e.Message + " - press Escape once more to dismiss it.");
            }
        }

        public static void Draw()
        {
            if (!Open) return;
            EnsureSkin();

            // Typing goes through our own field before anything else looks at the key.
            if (TypeInto()) return;

            // A rebind swallows the next key press, wherever the cursor is.
            if (_rebinding != null && Event.current != null && Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode != KeyCode.None)
                {
                    if (Event.current.keyCode != KeyCode.Escape)
                    {
                        _rebinding.Value = Event.current.keyCode.ToString();
                        Settings.SaveSoon();
                    }
                    _rebinding = null;
                    Event.current.Use();
                    return;
                }
            }

            if (Event.current != null && Event.current.type == EventType.KeyDown
                && Event.current.keyCode == KeyCode.Escape)
            {
                _escapeClosedAt = Time.realtimeSinceStartup;
                Toggle();
                Event.current.Use();
                return;
            }

            // NO GUI.Window HERE, AND THAT IS THE FIX FOR THE FIRST BUG THIS WINDOW HAD.
            //
            // GUI.Window takes a GUI.WindowFunction, which under IL2CPP is a generated Il2Cpp
            // delegate rather than a managed one. The cast compiled, the call did not throw, and the
            // body simply never ran - so the window opened, drew its frame, and was EMPTY. Nothing in
            // the log, because nothing failed; the callback was just never invoked.
            //
            // A panel drawn with BeginArea needs no delegate at all, so the whole class of problem is
            // gone rather than worked around. The cost is that dragging has to be done by hand, which
            // is the ten lines below.
            Drag();

            GUI.DrawTexture(_rect, _bg, ScaleMode.StretchToFill);
            GUI.DrawTexture(new Rect(_rect.x, _rect.y, _rect.width, 2f), _stripBg, ScaleMode.StretchToFill);

            GUILayout.BeginArea(new Rect(_rect.x + 12f, _rect.y + 10f, _rect.width - 24f, _rect.height - 20f));
            Body(0);
            GUILayout.EndArea();

            // Say it once, out loud: the body ran and how much it drew. A window that is blank again
            // one day is then a question the log has already answered.
            if (Event.current != null && Event.current.type == EventType.Repaint && !_saidDrawn)
            {
                _saidDrawn = true;
                Log.Info("settings window drawn (" + Tabs.Length + " tabs, tab '" + Tabs[_tab] + "').");
            }
        }

        private static bool _saidDrawn;
        private static bool _dragging;
        private static Vector2 _dragFrom;

        /// <summary>Drag by the title strip, done by hand because there is no window to do it for us.</summary>
        private static void Drag()
        {
            Event e = Event.current;
            if (e == null) return;
            Rect bar = new Rect(_rect.x, _rect.y, _rect.width, 26f);

            if (e.type == EventType.MouseDown && e.button == 0 && bar.Contains(e.mousePosition))
            {
                _dragging = true;
                _dragFrom = e.mousePosition - new Vector2(_rect.x, _rect.y);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _dragging)
            {
                _rect.x = e.mousePosition.x - _dragFrom.x;
                _rect.y = e.mousePosition.y - _dragFrom.y;
                // Never let it leave the screen entirely - a window that cannot be reached is gone.
                _rect.x = Mathf.Clamp(_rect.x, -_rect.width + 80f, Screen.width - 80f);
                _rect.y = Mathf.Clamp(_rect.y, 0f, Screen.height - 40f);
                e.Use();
            }
            else if (e.type == EventType.MouseUp && _dragging)
            {
                _dragging = false;
                e.Use();
            }
        }

        private static void Body(int id)
        {
            GUILayout.BeginVertical();

            GUILayout.Label("LD PICKUP DOCTOR", _head);
            GUILayout.Space(2f);

            // ---- tabs -------------------------------------------------------------------------
            GUILayout.BeginHorizontal();
            for (int i = 0; i < Tabs.Length; i++)
            {
                bool on = _tab == i;
                GUI.color = on ? new Color(0.72f, 0.89f, 1f) : Color.white;
                if (GUILayout.Button(Tabs[i], GUILayout.Height(24f))) { _tab = i; _scroll = Vector2.zero; }
                GUI.color = Color.white;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            switch (_tab)
            {
                case 0: ItemsTab(); break;
                case 1: PickupTab(); break;
                case 2: HighlightTab(); break;
                case 3: ColoursTab(); break;
                case 4: GrindTab(); break;
                case 5: KeysTab(); break;
                case 6: AdvancedTab(); break;
                case 7: InterfaceTab(); break;
                default: CheatsTab(); break;
            }
            GUILayout.EndScrollView();

            // ---- the explanation strip --------------------------------------------------------
            GUILayout.Space(2f);
            string strip = "";
            if (_hoverKey.Length > 0
                && Time.realtimeSinceStartup - _hoverSince >= Mathf.Max(0f, Settings.TooltipDelaySeconds.Value))
                strip = _hoverText;
            GUILayout.Label(strip, _foot, GUILayout.Height(52f));

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 26f));
        }

        // ---- tabs ---------------------------------------------------------------------------------

        private static void ItemsTab()
        {
            Note("Every item the sweep has seen this session, and HOW MANY of them - distinct "
               + "objects, not how many times it looked. This is the list to add names from: a name "
               + "here is guaranteed to match, and one typed from memory is not. Green means it is "
               + "already on the pickup list.");

            GUILayout.BeginHorizontal();
            GUILayout.Label("filter", _label, GUILayout.Width(60f));
            Field("itemfilter", null, _itemFilter, 240f);
            if (GUILayout.Button("clear", GUILayout.Width(60f)))
            {
                _itemFilter = "";
                if (_focusKey == "itemfilter") { _focusKey = ""; _focusEntry = null; }
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label(Sweep.SeenNames.Count + " seen", _label);
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);

            string needle = (_itemFilter ?? "").Trim().ToLowerInvariant();
            int shown = 0;
            foreach (KeyValuePair<string, int> kv in Sweep.SeenNames)
            {
                if (needle.Length > 0 && !kv.Key.ToLowerInvariant().Contains(needle)) continue;
                if (shown++ > 300) break;

                bool listed = Sweep.Matches(kv.Key, null);
                GUILayout.BeginHorizontal();
                GUI.color = listed ? new Color(0.55f, 0.95f, 0.62f) : Color.white;
                GUILayout.Label(kv.Key, _label, GUILayout.Width(380f));
                GUI.color = Color.white;
                GUILayout.Label("x" + kv.Value, _mono, GUILayout.Width(60f));
                if (GUILayout.Button(listed ? "remove" : "add", GUILayout.Width(80f))) ToggleName(kv.Key);
                GUILayout.EndHorizontal();
            }
            if (shown == 0) GUILayout.Label("nothing seen yet - walk near some items.", _label);
        }

        private static void PickupTab()
        {
            Toggle(Settings.PickupEnabled, "Auto pickup");
            Toggle(Settings.PickupDryRun, "Dry run (decide, take nothing)");
            Slider(Settings.PickupRadius, 1f, 12f, "Radius (m)");
            IntSlider(Settings.PickupPerSweep, 1, 40, "Max per sweep");
            Toggle(Settings.PickupAllItems, "Take everything eligible");
            Text(Settings.PickupNames, "Names (comma separated)");
            Toggle(Settings.PickupRespectWeight, "Respect carry weight");
            Slider(Settings.PickupWeightHeadroomKG, 0f, 5f, "Weight headroom (kg)");
            Toggle(Settings.PickupSkipRuined, "Skip ruined items");

            GUILayout.Space(8f);
            Stat("taken this session", Pickup.TakenThisSession.ToString());
            Stat("pickup path in use", Pickup.PathName);
            if (Pickup.Escalations > 0) Stat("escalations", Pickup.Escalations.ToString());
        }

        private static void HighlightTab()
        {
            Toggle(Settings.HighlightEnabled, "Outlines");
            Slider(Settings.HighlightRadius, 2f, 60f, "Radius (m)");
            Slider(Settings.HighlightThickness, 0.005f, 0.3f, "Outline thickness");
            IntSlider(Settings.HighlightMaxMarks, 1, 200, "Max outlines at once");
            Toggle(Settings.HighlightSeeThrough, "See through walls");
            Toggle(Settings.HighlightLabels, "Name labels");
            Slider(Settings.HighlightLabelRadius, 1f, 30f, "Label radius (m)");
            Toggle(Settings.HighlightHarvestables, "Outline harvestables");
            Toggle(Settings.HighlightContainers, "Outline unsearched containers");
            Slider(Settings.HighlightBrightness, 0.1f, 1.6f, "Brightness");

            GUILayout.Space(8f);
            Stat("outlines drawn", Highlight.LitCount.ToString());
            if (Silhouette.ShaderMissing)
            {
                GUI.color = new Color(1f, 0.6f, 0.5f);
                GUILayout.Label("Hidden/Internal-Colored is missing from this build, so outlines "
                    + "cannot be drawn. Nothing else is affected.", _label);
                GUI.color = Color.white;
            }
        }

        private static void ColoursTab()
        {
            Note("Hex, like #e8b44d. A colour is a category, not decoration: at eight metres in a "
               + "blizzard, food, fuel and medicine are three identical grey lumps until they are "
               + "three colours. Categories come from the game's own components on each item.");
            Colour(Settings.ColourFood, "Food and drink");
            Colour(Settings.ColourWater, "Water");
            Colour(Settings.ColourFuel, "Fuel and firewood");
            Colour(Settings.ColourTool, "Tools and weapons");
            Colour(Settings.ColourClothing, "Clothing");
            Colour(Settings.ColourMedical, "First aid");
            Colour(Settings.ColourAmmo, "Ammunition");
            Colour(Settings.ColourFire, "Fire and light");
            Colour(Settings.ColourMaterial, "Materials");
            Colour(Settings.ColourOther, "Everything else");
            Colour(Settings.ColourHarvestable, "Harvestable plants");
            Colour(Settings.ColourContainer, "Unsearched containers");
        }

        private static void GrindTab()
        {
            Toggle(Settings.HarvestPlants, "Auto harvest plants and branch piles");
            Slider(Settings.HarvestRadius, 1f, 8f, "Harvest radius (m)");
            Toggle(Settings.HarvestRequireTool, "Honour required tools");
            Toggle(Settings.SweepRoomOnKey, "Sweep key takes the room");
            Slider(Settings.SweepRoomRadius, 2f, 30f, "Sweep key radius (m)");

            GUILayout.Space(10f);
            GUI.color = new Color(1f, 0.78f, 0.55f);
            GUILayout.Label("Below this line is the one switch that refunds a PRICE rather than "
                + "removing a chore. Everything above removes clicking only.", _label);
            GUI.color = Color.white;
            Toggle(Settings.InstantBreakDown, "Instant break down (no hours)");

            GUILayout.Space(8f);
            Stat("harvested this session", Grind.HarvestedTotal.ToString());
            Stat("taken by the sweep key", Grind.SweepRoomTotal.ToString());
        }

        private static void KeysTab()
        {
            Note("Every hotkey lives here, wherever it was bound. Click a key to rebind it, then "
               + "press the new one. Escape cancels the rebind.");
            Key(Settings.KeyWindow, "Open this window");
            Key(Settings.KeySweepRoom, "Sweep the room");
            Key(Settings.KeyToggleHighlight, "Outlines on or off");
            Key(Settings.KeyTogglePickup, "Auto pickup on or off");
            Key(Settings.KeyReport, "Write the report to the log");
            Key(Settings.KeySave, "Save the game");
            Toggle(Settings.KeySaveNeedsCtrl, "Save key needs Ctrl held");
            Slider(Settings.SaveCooldownSeconds, 1f, 60f, "Least seconds between saves");
            Key(Settings.KeySpeedUp, "Speed up");
            Key(Settings.KeySpeedDown, "Speed down");
        }

        private static void AdvancedTab()
        {
            Toggle(Settings.Enabled, "Master switch");
            Slider(Settings.ScanIntervalSeconds, 0.05f, 2f, "Sweep interval (s)");
            Slider(Settings.ScanRadius, 5f, 80f, "Sweep radius (m)");
            Toggle(Settings.DiagEnabled, "Diagnostics");
            Slider(Settings.DiagReportSeconds, 10f, 600f, "Report every (s)");
            Toggle(Settings.DiagLogEveryRefusal, "Log every refusal (noisy)");

            GUILayout.Space(8f);
            Stat("sweeps", Sweep.SweepCount.ToString());
            Stat("colliders last sweep", Sweep.LastColliderCount.ToString());
            Stat("last sweep cost", Sweep.LastSweepMs.ToString("0.00") + " ms");
            Stat("items / plants / boxes",
                Sweep.Items.Count + " / " + Sweep.Plants.Count + " / " + Sweep.Boxes.Count);

            GUILayout.Space(6f);
            if (GUILayout.Button("write the report to the log now", GUILayout.Height(24f)))
                Diagnostics.Report(true);
        }

        private static void InterfaceTab()
        {
            Slider(Settings.WindowOpacity, 0.3f, 1f, "Window opacity");
            Slider(Settings.TooltipDelaySeconds, 0f, 5f, "Explanation delay (s)");
            Toggle(Settings.WindowPausesCursor, "Free the mouse while open");
            Toggle(Settings.WindowPausesGame, "Pause the game while open");
        }

        private static void CheatsTab()
        {
            GUI.color = new Color(1f, 0.72f, 0.45f);
            GUILayout.Label("This page is meant for testing. Every other page removes repetition only "
                + "and leaves the game's price exactly where it was. This page removes the price. "
                + "All of it is off or neutral by default, all of it is reversible, and the log "
                + "names every one that is on.", _label);
            GUI.color = Color.white;
            GUILayout.Space(8f);

            Slider(Settings.CheatSpeed, 0.25f, 8f, "Movement speed (1.00 is off)");
            Slider(Settings.CheatSpeedStep, 0.05f, 1f, "Speed step per key press");
            GUILayout.BeginHorizontal();
            GUILayout.Space(280f);
            if (GUILayout.Button("slower", GUILayout.Width(90f))) Cheats.NudgeSpeed(-1);
            if (GUILayout.Button("normal", GUILayout.Width(90f)))
            {
                Settings.CheatSpeed.Value = 1f;
                Settings.SaveSoon();
            }
            if (GUILayout.Button("faster", GUILayout.Width(90f))) Cheats.NudgeSpeed(1);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            Toggle(Settings.CheatInstantHarvest, "Instant harvest (items and carcasses)");
            Toggle(Settings.CheatUnlimitedCarry, "Unlimited carrying weight");
            Slider(Settings.CheatCarryKG, 30f, 2000f, "What unlimited means (kg)");
            Toggle(Settings.CheatUnlimitedAmmo, "Unlimited ammo in the gun you hold");
            Toggle(Settings.CheatPerpetualFire, "Fires, stoves and fireplaces never go out");

            GUILayout.Space(10f);
            GUI.color = new Color(0.75f, 0.85f, 0.95f);
            GUILayout.Label("Survival rates. Each is a multiple of the game's own per-hour number, "
                + "and 1.00 is off. Below 1 is gentler, above 1 is harsher - these go both ways, so "
                + "they are as much a difficulty dial as a cheat.", _label);
            GUI.color = Color.white;
            Slider(Settings.RateCold, 0f, 3f, "Cold - how fast you freeze");
            Slider(Settings.RateTired, 0f, 3f, "Tiredness - how fast fatigue builds");
            Slider(Settings.RateThirst, 0f, 3f, "Thirst - how fast you dry out");
            Slider(Settings.RateHunger, 0f, 3f, "Food - how fast calories burn");
            Slider(Settings.RateStamina, 0f, 3f, "Stamina - how fast sprinting drains it");
            Slider(Settings.RateHeldFuel, 0.05f, 3f, "Item fuel - lamp, torch, flare, battery");
            Toggle(Settings.FuelIncludesPlaced, "Fuel dial covers placed items too");
            GUILayout.BeginHorizontal();
            GUILayout.Space(280f);
            GUILayout.Label(Cheats.FuelSummary(), _mono);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(12f);
            GUI.color = new Color(0.75f, 0.85f, 0.95f);
            GUILayout.Label("Feats - the game's own positive effects. These are the only switches on "
                + "this page that reach the save file, because a feat is a permanent unlock with save "
                + "data of its own. Turning one off puts back the progress and the enabled state it "
                + "found.", _label);
            GUI.color = Color.white;

            GUILayout.BeginHorizontal();
            GUILayout.Space(280f);
            if (GUILayout.Button("all on", GUILayout.Width(90f))) Cheats.AllFeats(true);
            if (GUILayout.Button("all off", GUILayout.Width(90f))) Cheats.AllFeats(false);
            GUILayout.Space(12f);
            GUILayout.Label(Cheats.FeatsOn + " of " + Cheats.FeatsSeen + " on", _mono);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);

            for (int i = 0; i < Settings.FeatOrder.Count; i++)
            {
                Il2Cpp.FeatType ft = Settings.FeatOrder[i];
                Toggle(Settings.FeatSwitches[ft], Settings.Spaced(ft.ToString()));
            }

            GUILayout.Space(10f);
            string on = Cheats.Active();
            Stat("currently on", on.Length == 0 ? "nothing" : on.Trim());
        }

        // ---- text entry, built from parts that survive stripping ---------------------------------
        //
        // GUILayout.TextField CANNOT BE USED IN THIS GAME. It reaches UnityEngine.TextEditor, which
        // Hinterland's build has stripped, and Il2CppInterop cannot unstrip it - every call threw
        // "Method unstripping failed" and took the rest of the tab down with it. The stack said so
        // exactly:
        //
        //     at UnityEngine.TextEditor.SaveBackup()
        //     at UnityEngine.GUI.DoTextField(...)
        //     at LDPickupDoctor.Ui.ItemsTab()
        //
        // So the field is made of a Button (for the click and the frame) and the raw key events,
        // both of which are present. It is less capable than a real text field - no selection, no
        // clipboard - and it is the only kind that works here.

        private static string _focusKey = "";
        private static string _focusText = "";
        private static MelonPreferences_Entry<string> _focusEntry;

        /// <summary>
        /// Feed key events into the focused field. Returns true when the event was ours, so the
        /// rest of the window does not also act on the same press.
        /// </summary>
        private static bool TypeInto()
        {
            if (_focusKey.Length == 0) return false;
            Event e = Event.current;
            if (e == null || e.type != EventType.KeyDown) return false;

            if (e.keyCode == KeyCode.Escape || e.keyCode == KeyCode.Return
                || e.keyCode == KeyCode.KeypadEnter)
            {
                Commit();
                e.Use();
                return true;
            }
            if (e.keyCode == KeyCode.Backspace)
            {
                if (_focusText.Length > 0) _focusText = _focusText.Substring(0, _focusText.Length - 1);
                e.Use();
                return true;
            }
            if (e.character != '\0' && !char.IsControl(e.character))
            {
                _focusText += e.character;
                e.Use();
                return true;
            }
            return false;
        }

        private static void Commit()
        {
            if (_focusEntry != null && _focusEntry.Value != _focusText)
            {
                _focusEntry.Value = _focusText;
                Settings.SaveSoon();
            }
            else if (_focusEntry == null)
            {
                _itemFilter = _focusText;
            }
            _focusKey = "";
            _focusEntry = null;
        }

        /// <summary>One editable field. Click it, type, press Enter or Escape to finish.</summary>
        private static void Field(string key, MelonPreferences_Entry<string> entry, string plain, float width)
        {
            bool focused = _focusKey == key;
            string shown = focused ? _focusText + "_" : (entry != null ? (entry.Value ?? "") : (plain ?? ""));
            if (shown.Length == 0) shown = focused ? "_" : " ";

            GUI.color = focused ? new Color(1f, 0.9f, 0.55f) : Color.white;
            if (GUILayout.Button(shown, GUILayout.Width(width)))
            {
                if (focused) Commit();
                else
                {
                    if (_focusKey.Length > 0) Commit();
                    _focusKey = key;
                    _focusEntry = entry;
                    _focusText = entry != null ? (entry.Value ?? "") : (plain ?? "");
                }
            }
            GUI.color = Color.white;
        }

        // ---- row kinds ----------------------------------------------------------------------------

        private static void Row(MelonPreferences_Entry entry, string label, System.Action control)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _label, GUILayout.Width(280f));
            control();
            GUILayout.EndHorizontal();
            Hover(entry == null ? label : entry.Identifier,
                  entry == null ? "" : (label + " - " + entry.Description));
        }

        private static void Toggle(MelonPreferences_Entry<bool> e, string label)
        {
            Row(e, label, delegate
            {
                bool now = GUILayout.Toggle(e.Value, e.Value ? " on" : " off", GUILayout.Width(90f));
                if (now != e.Value) { e.Value = now; Settings.SaveSoon(); }
                GUILayout.FlexibleSpace();
            });
        }

        private static void Slider(MelonPreferences_Entry<float> e, float lo, float hi, string label)
        {
            Row(e, label, delegate
            {
                float now = GUILayout.HorizontalSlider(e.Value, lo, hi, GUILayout.Width(240f));
                GUILayout.Space(8f);
                GUILayout.Label(now.ToString("0.00"), _mono, GUILayout.Width(60f));
                if (!Mathf.Approximately(now, e.Value)) { e.Value = now; Settings.SaveSoon(); }
                GUILayout.FlexibleSpace();
            });
        }

        private static void IntSlider(MelonPreferences_Entry<int> e, int lo, int hi, string label)
        {
            Row(e, label, delegate
            {
                int now = Mathf.RoundToInt(GUILayout.HorizontalSlider(e.Value, lo, hi, GUILayout.Width(240f)));
                GUILayout.Space(8f);
                GUILayout.Label(now.ToString(), _mono, GUILayout.Width(60f));
                if (now != e.Value) { e.Value = now; Settings.SaveSoon(); }
                GUILayout.FlexibleSpace();
            });
        }

        private static void Text(MelonPreferences_Entry<string> e, string label)
        {
            Row(e, label, delegate
            {
                Field(e.Identifier, e, null, 380f);
                GUILayout.FlexibleSpace();
            });
        }

        private static void Colour(MelonPreferences_Entry<string> e, string label)
        {
            Row(e, label, delegate
            {
                Field(e.Identifier, e, null, 110f);
                GUILayout.Space(8f);
                Color c;
                if (ColorUtility.TryParseHtmlString((e.Value ?? "").Trim(), out c))
                {
                    Color was = GUI.color;
                    GUI.color = c;
                    GUILayout.Label("################", _mono, GUILayout.Width(180f));
                    GUI.color = was;
                }
                else
                {
                    GUILayout.Label("not a hex colour", _label, GUILayout.Width(180f));
                }
                GUILayout.FlexibleSpace();
            });
        }

        private static void Key(MelonPreferences_Entry<string> e, string label)
        {
            Row(e, label, delegate
            {
                bool waiting = _rebinding == e;
                GUI.color = waiting ? new Color(1f, 0.85f, 0.4f) : Color.white;
                if (GUILayout.Button(waiting ? "press a key..." : (e.Value ?? "none"), GUILayout.Width(150f)))
                    _rebinding = waiting ? null : e;
                GUI.color = Color.white;
                GUILayout.FlexibleSpace();
            });
        }

        private static void Stat(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _label, GUILayout.Width(280f));
            GUILayout.Label(value, _mono);
            GUILayout.EndHorizontal();
        }

        private static void Note(string text)
        {
            GUI.color = new Color(0.75f, 0.82f, 0.88f);
            GUILayout.Label(text, _label);
            GUI.color = Color.white;
            GUILayout.Space(6f);
        }

        private static void Hover(string key, string text)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return;
            Rect last = GUILayoutUtility.GetLastRect();
            if (!last.Contains(Event.current.mousePosition)) return;
            if (_hoverKey != key)
            {
                _hoverKey = key;
                _hoverText = text;
                _hoverSince = Time.realtimeSinceStartup;
            }
        }

        private static void ToggleName(string name)
        {
            string list = Settings.PickupNames.Value ?? "";
            List<string> parts = new List<string>(list.Split(','));
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                parts[i] = parts[i].Trim();
                if (parts[i].Length == 0) parts.RemoveAt(i);
            }
            string lower = name.ToLowerInvariant();
            bool removed = false;
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                if (lower.Contains(parts[i].ToLowerInvariant())) { parts.RemoveAt(i); removed = true; }
            }
            if (!removed) parts.Add(name);
            Settings.PickupNames.Value = string.Join(",", parts.ToArray());
            Settings.SaveSoon();
        }

        // ---- skin ---------------------------------------------------------------------------------

        private static void EnsureSkin()
        {
            if (_skinReady && _bg != null) return;

            _bg = Solid(new Color(0.07f, 0.09f, 0.11f, Mathf.Clamp01(Settings.WindowOpacity.Value)));
            _stripBg = Solid(new Color(0.13f, 0.16f, 0.19f, 0.95f));

            _panel = new GUIStyle(GUI.skin.window);
            _panel.normal.background = _bg;
            _panel.onNormal.background = _bg;
            _panel.padding = new RectOffset(12, 12, 10, 10);

            _head = new GUIStyle(GUI.skin.label);
            _head.fontStyle = FontStyle.Bold;
            _head.normal.textColor = new Color(0.72f, 0.89f, 1f);

            _label = new GUIStyle(GUI.skin.label);
            _label.wordWrap = true;
            _label.normal.textColor = new Color(0.90f, 0.92f, 0.94f);

            _mono = new GUIStyle(GUI.skin.label);
            _mono.normal.textColor = new Color(0.78f, 0.86f, 0.95f);

            _foot = new GUIStyle(GUI.skin.label);
            _foot.wordWrap = true;
            _foot.normal.background = _stripBg;
            _foot.normal.textColor = new Color(0.85f, 0.89f, 0.93f);
            _foot.padding = new RectOffset(8, 8, 6, 6);

            // The game's own GUI.skin is NOT written to. An earlier version set
            // GUI.skin.window.normal.background here, which is a global the whole process shares -
            // one mod tinting it black is how every other IMGUI window in the game turns black.
            _skinReady = true;
        }

        private static Texture2D Solid(Color c)
        {
            Texture2D t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.hideFlags = HideFlags.HideAndDontSave;
            Color[] px = new Color[4];
            for (int i = 0; i < px.Length; i++) px[i] = c;
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        /// <summary>The name labels, drawn straight to the screen rather than into the window.</summary>
        public static void DrawLabels()
        {
            if (!Settings.HighlightEnabled.Value || !Settings.HighlightLabels.Value) return;
            if (Highlight.Labels.Count == 0) return;
            EnsureSkin();

            for (int i = 0; i < Highlight.Labels.Count; i++)
            {
                Highlight.Label l = Highlight.Labels[i];
                Color was = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.7f);
                GUI.Label(new Rect(l.Screen.x + 1f, l.Screen.y + 1f, 260f, 20f), l.Text);
                GUI.color = l.Colour;
                GUI.Label(new Rect(l.Screen.x, l.Screen.y, 260f, 20f), l.Text);
                GUI.color = was;
            }
        }
    }
}
