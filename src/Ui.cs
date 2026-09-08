// The settings window. F10 by default.
//
// TWO RULES THE TAB CODE HOLDS TO, both load-bearing, both carried over from the Green Hell version
// because they came from how he actually uses a window:
//
//   ORDER NEVER CHANGES. Tabs are appended, never inserted, and the order of rows inside a tab is
//   fixed. He finds an option by where it sits, not by reading down the column - an option that
//   moves between launches cannot be found by memory at all.
//
//   NO OPTION APPEARS TWICE. The Keys tab OWNS every hotkey wherever it was bound, and those rows
//   are skipped in their home tab. An option in two places has no location.
//
// The tooltip strip at the bottom is on a DELAY on purpose. With an instant tooltip, sweeping down a
// column covers the row you were trying to read. Interface.TooltipDelaySeconds is the dial, and zero
// gives the instant behaviour back.

using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace LDPickupDoctor
{
    internal static class Ui
    {
        public static bool Open;

        private static readonly string[] Tabs =
        {
            "Items", "Pickup", "Highlight", "Colours", "Grind", "Keys", "Advanced", "Interface"
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
            }
            else
            {
                _rebinding = null;
                if (Settings.WindowPausesCursor.Value)
                {
                    Cursor.lockState = _lockWas;
                    Cursor.visible = _visibleWas;
                }
            }
        }

        /// <summary>Called every frame while open - the game re-locks the cursor, so we re-free it.</summary>
        public static void HoldCursor()
        {
            if (!Open || !Settings.WindowPausesCursor.Value) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public static void Draw()
        {
            if (!Open) return;
            EnsureSkin();

            // A rebind swallows the next key press, wherever the cursor is.
            if (_rebinding != null && Event.current != null && Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode != KeyCode.None)
                {
                    if (Event.current.keyCode != KeyCode.Escape)
                    {
                        _rebinding.Value = Event.current.keyCode.ToString();
                        MelonPreferences.Save();
                    }
                    _rebinding = null;
                    Event.current.Use();
                    return;
                }
            }

            if (Event.current != null && Event.current.type == EventType.KeyDown
                && Event.current.keyCode == KeyCode.Escape)
            {
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
                // Never let it leave the screen entirely - a window he cannot reach is a window gone.
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
                default: InterfaceTab(); break;
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
            Note("What the sweep has actually seen this session, and how often. This is the list to "
               + "copy names from - a name here is guaranteed to match, and a name you typed from "
               + "memory is not.");

            GUILayout.BeginHorizontal();
            GUILayout.Label("filter", _label, GUILayout.Width(60f));
            _itemFilter = GUILayout.TextField(_itemFilter ?? "", GUILayout.Width(240f));
            if (GUILayout.Button("clear", GUILayout.Width(60f))) _itemFilter = "";
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
                if (now != e.Value) { e.Value = now; MelonPreferences.Save(); }
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
                if (!Mathf.Approximately(now, e.Value)) { e.Value = now; MelonPreferences.Save(); }
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
                if (now != e.Value) { e.Value = now; MelonPreferences.Save(); }
                GUILayout.FlexibleSpace();
            });
        }

        private static void Text(MelonPreferences_Entry<string> e, string label)
        {
            Row(e, label, delegate
            {
                string now = GUILayout.TextField(e.Value ?? "", GUILayout.Width(380f));
                if (now != e.Value) { e.Value = now; MelonPreferences.Save(); }
                GUILayout.FlexibleSpace();
            });
        }

        private static void Colour(MelonPreferences_Entry<string> e, string label)
        {
            Row(e, label, delegate
            {
                string now = GUILayout.TextField(e.Value ?? "", GUILayout.Width(110f));
                if (now != e.Value) { e.Value = now; MelonPreferences.Save(); }
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
            MelonPreferences.Save();
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
