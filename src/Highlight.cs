// What colour a thing is, and why that is the useful question.
//
// A single outline colour answers "there is a thing there", which you mostly already knew - the
// thing is on your screen. Colour answers the question you actually have while sweeping a house in
// a blizzard: is that worth the walk? Food, fuel and medicine are three different decisions, and at
// eight metres in the dark they are three identical grey lumps.
//
// The category comes from the game's OWN component on the item - m_FoodItem, m_FuelSourceItem,
// m_FirstAidItem - not from a list of names this mod carries. That matters for two reasons: a name
// list rots the moment Hinterland adds an item, and a component is what the game itself uses to
// decide what a thing is. Nothing is hard-coded that the game already knows.

using System.Collections.Generic;
using Il2Cpp;
using UnityEngine;

namespace LDPickupDoctor
{
    internal enum Kind
    {
        Food, Water, Fuel, Tool, Clothing, Medical, Ammo, Fire, Material, Other, Harvestable, Container
    }

    internal static class Highlight
    {
        private const string Tag = "ldpd";

        private static readonly List<GameObject> _lit = new List<GameObject>();
        public static int LitCount { get { return _lit.Count; } }

        // Label rows for OnGUI. Built in Update, drawn in OnGUI - screen positions must not be
        // recomputed inside OnGUI, which runs several times a frame for layout and repaint.
        internal struct Label { public Vector2 Screen; public string Text; public Color Colour; }
        public static readonly List<Label> Labels = new List<Label>();

        public static void Refresh()
        {
            Silhouette.Clear(Tag);
            _lit.Clear();
            Labels.Clear();

            if (!Settings.HighlightEnabled.Value) return;

            float radius = Settings.HighlightRadius.Value;
            int budget = Mathf.Max(0, Settings.HighlightMaxMarks.Value);
            bool labels = Settings.HighlightLabels.Value;
            float labelRadius = Settings.HighlightLabelRadius.Value;
            Camera cam = null;
            if (labels) { try { cam = GameManager.GetMainCamera(); } catch (System.Exception) { } if (cam == null) cam = Camera.main; }

            for (int i = 0; i < Sweep.Items.Count && _lit.Count < budget; i++)
            {
                Found f = Sweep.Items[i];
                if (f.Distance > radius) break;              // the list is sorted by distance
                if (f.Gear == null) continue;
                if (f.Outcome == Outcome.AlreadyHeld || f.Outcome == Outcome.Hidden) continue;
                if (f.Outcome == Outcome.InsideContainer || f.Outcome == Outcome.Locked) continue;

                Kind kind = KindOf(f.Gear);
                Color colour = ColourOf(kind);
                // Something you cannot take right now is drawn dimmer rather than not drawn: the
                // question "why is that one dark" has an answer in the log, and an item that
                // silently stops being outlined does not.
                if (f.Outcome == Outcome.TooHeavy || f.Outcome == Outcome.Ruined)
                    colour = new Color(colour.r * 0.45f, colour.g * 0.45f, colour.b * 0.45f, 1f);

                GameObject go = null;
                try { go = f.Gear.gameObject; } catch (System.Exception) { }
                if (go == null) continue;

                Silhouette.Add(go, colour, Tag);
                _lit.Add(go);

                if (labels && cam != null && f.Distance <= labelRadius)
                    AddLabel(cam, f.Where, f.Name + "  " + f.Distance.ToString("0.0") + "m", colour);
            }

            if (Settings.HighlightHarvestables.Value)
            {
                Color colour = Settings.Colour(Settings.ColourHarvestable, new Color(0.39f, 0.79f, 0.42f));
                for (int i = 0; i < Sweep.Plants.Count && _lit.Count < budget; i++)
                {
                    Found f = Sweep.Plants[i];
                    if (f.Distance > radius) break;
                    if (f.Plant == null || f.Outcome == Outcome.AlreadyHeld) continue;
                    GameObject go = null;
                    try { go = f.Plant.gameObject; } catch (System.Exception) { }
                    if (go == null) continue;
                    Silhouette.Add(go, colour, Tag);
                    _lit.Add(go);
                    if (labels && cam != null && f.Distance <= labelRadius)
                        AddLabel(cam, f.Where, "harvest  " + f.Distance.ToString("0.0") + "m", colour);
                }
            }

            if (Settings.HighlightContainers.Value)
            {
                Color colour = Settings.Colour(Settings.ColourContainer, new Color(0.56f, 0.66f, 0.75f));
                for (int i = 0; i < Sweep.Boxes.Count && _lit.Count < budget; i++)
                {
                    Found f = Sweep.Boxes[i];
                    if (f.Distance > radius) break;
                    if (f.Box == null || f.Outcome == Outcome.AlreadyHeld) continue;   // already searched
                    GameObject go = null;
                    try { go = f.Box.gameObject; } catch (System.Exception) { }
                    if (go == null) continue;
                    Silhouette.Add(go, colour, Tag);
                    _lit.Add(go);
                }
            }
        }

        private static void AddLabel(Camera cam, Transform where, string text, Color colour)
        {
            if (where == null) return;
            Vector3 p = cam.WorldToScreenPoint(where.position + Vector3.up * 0.15f);
            if (p.z <= 0f) return;                       // behind the camera
            Label l = new Label();
            l.Screen = new Vector2(p.x, Screen.height - p.y);
            l.Text = text;
            l.Colour = colour;
            Labels.Add(l);
        }

        /// <summary>
        /// The item's category, read off the game's own components. Order is most specific first:
        /// a lantern is a light before it is a liquid container, and a can of peaches is food before
        /// it is a thing that can be broken down.
        /// </summary>
        public static Kind KindOf(GearItem gi)
        {
            if (gi == null) return Kind.Other;
            try
            {
                if (gi.m_FirstAidItem != null || gi.m_EmergencyStim != null) return Kind.Medical;
                if (gi.m_MatchesItem != null || gi.m_FireStarterItem != null || gi.m_TorchItem != null
                    || gi.m_FlareItem != null || gi.m_KeroseneLampItem != null
                    || gi.m_FlashlightItem != null) return Kind.Fire;
                if (gi.m_AmmoItem != null || gi.m_ArrowItem != null || gi.m_AmmoCasingItem != null
                    || gi.m_FlareGunRoundItem != null) return Kind.Ammo;
                if (gi.m_FoodItem != null || gi.m_Cookable != null) return Kind.Food;
                if (gi.m_WaterSupply != null || gi.m_LiquidItem != null || gi.m_PurifyWater != null)
                    return Kind.Water;
                if (gi.m_FuelSourceItem != null || gi.m_CharcoalItem != null) return Kind.Fuel;
                if (gi.m_ClothingItem != null) return Kind.Clothing;
                if (gi.m_GunItem != null || gi.m_BowItem != null || gi.m_BearSpearItem != null
                    || gi.m_ToolsItem != null || gi.m_Sharpenable != null) return Kind.Tool;
                if (gi.m_RopeItem != null || gi.m_StoneItem != null || gi.m_PowderItem != null
                    || gi.m_Millable != null || gi.m_BreakDownItem != null) return Kind.Material;
            }
            catch (System.Exception)
            {
                // A field that cannot be read is not a reason to stop drawing the item - it is a
                // reason to draw it in the "no category" colour, which is exactly what Other is.
            }
            return Kind.Other;
        }

        public static Color ColourOf(Kind kind)
        {
            switch (kind)
            {
                case Kind.Food: return Settings.Colour(Settings.ColourFood, new Color(0.91f, 0.71f, 0.30f));
                case Kind.Water: return Settings.Colour(Settings.ColourWater, new Color(0.37f, 0.72f, 0.91f));
                case Kind.Fuel: return Settings.Colour(Settings.ColourFuel, new Color(0.78f, 0.46f, 0.24f));
                case Kind.Tool: return Settings.Colour(Settings.ColourTool, new Color(0.69f, 0.72f, 0.75f));
                case Kind.Clothing: return Settings.Colour(Settings.ColourClothing, new Color(0.60f, 0.55f, 0.88f));
                case Kind.Medical: return Settings.Colour(Settings.ColourMedical, new Color(0.88f, 0.38f, 0.48f));
                case Kind.Ammo: return Settings.Colour(Settings.ColourAmmo, new Color(0.91f, 0.88f, 0.30f));
                case Kind.Fire: return Settings.Colour(Settings.ColourFire, new Color(1.00f, 0.55f, 0.24f));
                case Kind.Material: return Settings.Colour(Settings.ColourMaterial, new Color(0.50f, 0.83f, 0.66f));
                case Kind.Harvestable: return Settings.Colour(Settings.ColourHarvestable, new Color(0.39f, 0.79f, 0.42f));
                case Kind.Container: return Settings.Colour(Settings.ColourContainer, new Color(0.56f, 0.66f, 0.75f));
                default: return Settings.Colour(Settings.ColourOther, new Color(0.87f, 0.90f, 0.93f));
            }
        }

        public static void Off()
        {
            Silhouette.Clear(Tag);
            _lit.Clear();
            Labels.Clear();
        }
    }
}
