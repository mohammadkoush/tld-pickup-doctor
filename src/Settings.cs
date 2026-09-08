// LD Pickup Doctor - every knob, in one place, with the reason for its default written down.
//
// Config lives in MelonLoader's own UserData\MelonPreferences.cfg, under categories prefixed
// "LDPD_". That is deliberate: a file the loader already writes, backs up and hot-reloads costs
// nothing to maintain, and he can edit it with the game shut without going through the window.
//
// THE RULE THESE DEFAULTS ANSWER TO, inherited from the Green Hell mod and unchanged:
//
//   This mod takes away the GRIND. It does not make the game CHEAPER.
//
//   Grind is repetition that costs time and teaches nothing - walking onto forty sticks and
//   clicking each one. Removing it costs the game nothing, because nothing was being decided.
//
//   Cost is what the game charges: the hours a break-down burns, the minutes a container search
//   burns, the weight you must carry. That is where The Long Dark actually lives.
//
// So anything that only removes clicks is ON by default. Anything that would give back GAME TIME -
// break-down hours, container search minutes - is OFF by default and says so in its own tooltip.

using MelonLoader;
using UnityEngine;

namespace LDPickupDoctor
{
    internal static class Settings
    {
        // ---- categories ------------------------------------------------------------------------
        private static MelonPreferences_Category _general;
        private static MelonPreferences_Category _pickup;
        private static MelonPreferences_Category _highlight;
        private static MelonPreferences_Category _grind;
        private static MelonPreferences_Category _keys;
        private static MelonPreferences_Category _iface;
        private static MelonPreferences_Category _diag;

        // ---- general ---------------------------------------------------------------------------
        public static MelonPreferences_Entry<bool> Enabled;
        public static MelonPreferences_Entry<float> ScanIntervalSeconds;
        public static MelonPreferences_Entry<float> ScanRadius;

        // ---- pickup ----------------------------------------------------------------------------
        public static MelonPreferences_Entry<bool> PickupEnabled;
        public static MelonPreferences_Entry<bool> PickupDryRun;
        public static MelonPreferences_Entry<float> PickupRadius;
        public static MelonPreferences_Entry<int> PickupPerSweep;
        public static MelonPreferences_Entry<bool> PickupAllItems;
        public static MelonPreferences_Entry<string> PickupNames;
        public static MelonPreferences_Entry<bool> PickupRespectWeight;
        public static MelonPreferences_Entry<float> PickupWeightHeadroomKG;
        public static MelonPreferences_Entry<bool> PickupSkipRuined;

        // ---- highlight -------------------------------------------------------------------------
        public static MelonPreferences_Entry<bool> HighlightEnabled;
        public static MelonPreferences_Entry<float> HighlightRadius;
        public static MelonPreferences_Entry<float> HighlightThickness;
        public static MelonPreferences_Entry<int> HighlightMaxMarks;
        public static MelonPreferences_Entry<bool> HighlightSeeThrough;
        public static MelonPreferences_Entry<bool> HighlightLabels;
        public static MelonPreferences_Entry<float> HighlightLabelRadius;
        public static MelonPreferences_Entry<bool> HighlightHarvestables;
        public static MelonPreferences_Entry<bool> HighlightContainers;
        public static MelonPreferences_Entry<float> HighlightBrightness;

        // Category colours. Text rather than a colour picker on purpose: a hex string survives a
        // config file, a screenshot and a forum post, and the window parses it live.
        public static MelonPreferences_Entry<string> ColourFood;
        public static MelonPreferences_Entry<string> ColourWater;
        public static MelonPreferences_Entry<string> ColourFuel;
        public static MelonPreferences_Entry<string> ColourTool;
        public static MelonPreferences_Entry<string> ColourClothing;
        public static MelonPreferences_Entry<string> ColourMedical;
        public static MelonPreferences_Entry<string> ColourAmmo;
        public static MelonPreferences_Entry<string> ColourFire;
        public static MelonPreferences_Entry<string> ColourMaterial;
        public static MelonPreferences_Entry<string> ColourOther;
        public static MelonPreferences_Entry<string> ColourHarvestable;
        public static MelonPreferences_Entry<string> ColourContainer;

        // ---- grind -----------------------------------------------------------------------------
        public static MelonPreferences_Entry<bool> HarvestPlants;
        public static MelonPreferences_Entry<float> HarvestRadius;
        public static MelonPreferences_Entry<bool> HarvestRequireTool;
        public static MelonPreferences_Entry<bool> SweepRoomOnKey;
        public static MelonPreferences_Entry<float> SweepRoomRadius;
        // The one that hands back GAME TIME. Off by default, and the window says why.
        //
        // There is deliberately no InstantContainerSearch here yet. The reveal timing lives on
        // GearItem.m_NormalizedRevealTimeInContainer, which is only reachable once a container has
        // instantiated its contents, and a switch that cannot be made to work is worse than a
        // missing one: it looks like the mod is broken. It goes in when it can be verified.
        public static MelonPreferences_Entry<bool> InstantBreakDown;

        // ---- keys ------------------------------------------------------------------------------
        public static MelonPreferences_Entry<string> KeyWindow;
        public static MelonPreferences_Entry<string> KeySweepRoom;
        public static MelonPreferences_Entry<string> KeyToggleHighlight;
        public static MelonPreferences_Entry<string> KeyTogglePickup;
        public static MelonPreferences_Entry<string> KeyReport;

        // ---- interface -------------------------------------------------------------------------
        public static MelonPreferences_Entry<float> WindowOpacity;
        public static MelonPreferences_Entry<float> TooltipDelaySeconds;
        public static MelonPreferences_Entry<bool> WindowPausesCursor;

        // ---- diagnostics -----------------------------------------------------------------------
        public static MelonPreferences_Entry<bool> DiagEnabled;
        public static MelonPreferences_Entry<float> DiagReportSeconds;
        public static MelonPreferences_Entry<bool> DiagLogEveryRefusal;

        public static void Load()
        {
            _general = MelonPreferences.CreateCategory("LDPD_General", "Pickup Doctor - General");
            _pickup = MelonPreferences.CreateCategory("LDPD_Pickup", "Pickup Doctor - Pickup");
            _highlight = MelonPreferences.CreateCategory("LDPD_Highlight", "Pickup Doctor - Highlight");
            _grind = MelonPreferences.CreateCategory("LDPD_Grind", "Pickup Doctor - Grind");
            _keys = MelonPreferences.CreateCategory("LDPD_Keys", "Pickup Doctor - Keys");
            _iface = MelonPreferences.CreateCategory("LDPD_Interface", "Pickup Doctor - Interface");
            _diag = MelonPreferences.CreateCategory("LDPD_Diagnostics", "Pickup Doctor - Diagnostics");

            Enabled = _general.CreateEntry("Enabled", true,
                description: "Master switch. Off means the mod does nothing at all, not even scan.");
            ScanIntervalSeconds = _general.CreateEntry("ScanIntervalSeconds", 0.25f,
                description: "Seconds between sweeps. The sweep is one Physics.OverlapSphere plus a "
                    + "component lookup per hit, so 0.25 is cheap. Raise it if you are frame limited.");
            ScanRadius = _general.CreateEntry("ScanRadius", 30f,
                description: "How far out the sweep looks, in metres. This is the OUTER limit - "
                    + "pickup and highlight each have their own, smaller, radius inside it.");

            PickupEnabled = _pickup.CreateEntry("Enabled", true,
                description: "Pick up matching items you walk near, with no click and no inspect screen.");
            PickupDryRun = _pickup.CreateEntry("DryRun", false,
                description: "Decide everything, take nothing. The log then reads WouldSucceed for "
                    + "each item that would have been taken. This is how you test a name list safely.");
            PickupRadius = _pickup.CreateEntry("Radius", 3f,
                description: "Metres. Vanilla reach is about 2, so 3 is a walk-over rather than a vacuum. "
                    + "Large values are what turn a quality-of-life mod into a cheat, so this one is "
                    + "deliberately small by default.");
            PickupPerSweep = _pickup.CreateEntry("MaxPerSweep", 4,
                description: "How many items one sweep may take. A cap keeps a pile of forty sticks "
                    + "from becoming one long frame; the rest are taken on the next sweep.");
            PickupAllItems = _pickup.CreateEntry("AllItems", false,
                description: "Take everything eligible rather than only the names below. Off by default: "
                    + "a blanket vacuum empties a house into your back before you have decided anything.");
            PickupNames = _pickup.CreateEntry("Names", "stick,branch,cattail,coal,cloth,rosehip,reishi,birchbark",
                description: "Comma separated. Matched against the item name and display name, case "
                    + "insensitive, as a SUBSTRING - so 'stick' catches every stick variant. "
                    + "The Items tab of the window lists what it has actually seen, so you can copy from it.");
            PickupRespectWeight = _pickup.CreateEntry("RespectWeight", true,
                description: "Refuse a pickup that would put you over your carry capacity. On, because "
                    + "weight is the game's real price for hoarding and this mod does not lower prices.");
            PickupWeightHeadroomKG = _pickup.CreateEntry("WeightHeadroomKG", 0.5f,
                description: "Leave this much capacity spare, in kilograms, so auto-pickup cannot be the "
                    + "thing that tips you into encumbered without you noticing.");
            PickupSkipRuined = _pickup.CreateEntry("SkipRuined", true,
                description: "Do not pick up items at zero condition. They are worth nothing and fill "
                    + "the backpack. Turn off if you collect ruined cloth to break down.");

            HighlightEnabled = _highlight.CreateEntry("Enabled", true,
                description: "Draw a coloured outline around items on the ground.");
            HighlightRadius = _highlight.CreateEntry("Radius", 18f,
                description: "Metres. Outlines further than this are not drawn.");
            HighlightThickness = _highlight.CreateEntry("Thickness", 0.04f,
                description: "How far the outline hull is grown, as a fraction of the item's size. "
                    + "The hull is SCALED rather than pushed along normals - that needs a vertex shader, "
                    + "which cannot be compiled at runtime - so a big item gets a slightly fatter rim.");
            HighlightMaxMarks = _highlight.CreateEntry("MaxMarks", 40,
                description: "Most outlines drawn at once, nearest first. This is the frame-cost dial.");
            HighlightSeeThrough = _highlight.CreateEntry("SeeThrough", false,
                description: "Draw outlines through walls. Off by default because seeing loot through "
                    + "a cabin wall is not a convenience, it is a different game.");
            HighlightLabels = _highlight.CreateEntry("Labels", false,
                description: "Print the item name and distance next to each outline.");
            HighlightLabelRadius = _highlight.CreateEntry("LabelRadius", 8f,
                description: "Metres. Labels are noisier than outlines, so they stop sooner.");
            HighlightHarvestables = _highlight.CreateEntry("Harvestables", true,
                description: "Also outline harvestable plants and branch piles.");
            HighlightContainers = _highlight.CreateEntry("Containers", false,
                description: "Also outline containers you have not searched yet.");
            HighlightBrightness = _highlight.CreateEntry("Brightness", 0.9f,
                description: "Scales every outline colour. Lower it if the colours read as neon at night.");

            ColourFood = _highlight.CreateEntry("ColourFood", "#e8b44d", description: "Food and drink.");
            ColourWater = _highlight.CreateEntry("ColourWater", "#5fb8e8", description: "Water and containers of it.");
            ColourFuel = _highlight.CreateEntry("ColourFuel", "#c8763c", description: "Firewood, coal, accelerant.");
            ColourTool = _highlight.CreateEntry("ColourTool", "#b0b8c0", description: "Tools and weapons.");
            ColourClothing = _highlight.CreateEntry("ColourClothing", "#9a8ce0", description: "Clothing.");
            ColourMedical = _highlight.CreateEntry("ColourMedical", "#e0607a", description: "First aid and medicine.");
            ColourAmmo = _highlight.CreateEntry("ColourAmmo", "#e8e04d", description: "Ammunition and arrows.");
            ColourFire = _highlight.CreateEntry("ColourFire", "#ff8c3c", description: "Matches, tinder, lanterns, torches.");
            ColourMaterial = _highlight.CreateEntry("ColourMaterial", "#7fd4a8", description: "Cloth, scrap, cured materials.");
            ColourOther = _highlight.CreateEntry("ColourOther", "#dfe6ec", description: "Everything with no category of its own.");
            ColourHarvestable = _highlight.CreateEntry("ColourHarvestable", "#63c96b", description: "Plants and branch piles.");
            ColourContainer = _highlight.CreateEntry("ColourContainer", "#8fa8c0", description: "Unsearched containers.");

            HarvestPlants = _grind.CreateEntry("AutoHarvest", true,
                description: "Harvest plants and branch piles you stand next to, with no hold and no "
                    + "animation. It yields exactly what the hold would have yielded - this removes the "
                    + "waiting, not the price.");
            HarvestRadius = _grind.CreateEntry("HarvestRadius", 2.5f,
                description: "Metres. Kept short on purpose: this should feel like reaching down, not "
                    + "like stripping a clearing from the middle of it.");
            HarvestRequireTool = _grind.CreateEntry("RequireTool", true,
                description: "Honour the tool a harvestable asks for. On, because the tool requirement "
                    + "is a cost the game charges, not a click it makes you perform.");
            SweepRoomOnKey = _grind.CreateEntry("SweepRoomOnKey", true,
                description: "Let the sweep key take everything eligible within its radius in one press.");
            SweepRoomRadius = _grind.CreateEntry("SweepRoomRadius", 12f,
                description: "Metres for that key press. Room sized, not house sized.");
            InstantBreakDown = _grind.CreateEntry("InstantBreakDown", false,
                description: "THIS ONE REFUNDS A PRICE RATHER THAN REMOVING A CHORE. Breaking an item "
                    + "down burns in-game HOURS, and hours here are weather, hunger and daylight. "
                    + "Everything else in this mod removes clicking; this removes cost. Off by "
                    + "default, and it puts every hour cost back the moment you turn it off.");

            KeyWindow = _keys.CreateEntry("Window", "F10", description: "Open and close the settings window.");
            KeySweepRoom = _keys.CreateEntry("SweepRoom", "F7", description: "Take everything eligible nearby, once.");
            KeyToggleHighlight = _keys.CreateEntry("ToggleHighlight", "F8", description: "Outlines on or off.");
            KeyTogglePickup = _keys.CreateEntry("TogglePickup", "F9", description: "Auto pickup on or off.");
            KeyReport = _keys.CreateEntry("Report", "F11", description: "Write the diagnostic report to the log now.");

            WindowOpacity = _iface.CreateEntry("Opacity", 0.94f, description: "Settings window background opacity.");
            TooltipDelaySeconds = _iface.CreateEntry("TooltipDelaySeconds", 2f,
                description: "How long the cursor must rest on a row before its explanation appears. "
                    + "The delay is deliberate: with an instant tooltip, running down a column covers "
                    + "the row you were trying to read. Zero gives the instant behaviour back.");
            WindowPausesCursor = _iface.CreateEntry("FreeCursor", true,
                description: "Release the mouse from the game while the window is open.");

            DiagEnabled = _diag.CreateEntry("Enabled", true,
                description: "Count what the sweep decided and write it to the MelonLoader log.");
            DiagReportSeconds = _diag.CreateEntry("ReportSeconds", 60f,
                description: "Seconds between tally reports.");
            DiagLogEveryRefusal = _diag.CreateEntry("LogEveryRefusal", false,
                description: "Log every single refusal rather than counting them. Very noisy - this is "
                    + "for the ten seconds when you are asking why one specific item was skipped.");

            MelonPreferences.Save();
        }

        /// <summary>Hex to colour, with the brightness dial applied and a loud fallback.</summary>
        public static Color Colour(MelonPreferences_Entry<string> entry, Color fallback)
        {
            Color c;
            string raw = entry == null ? null : entry.Value;
            if (string.IsNullOrEmpty(raw) || !ColorUtility.TryParseHtmlString(raw.Trim(), out c))
            {
                // Never silently substitute: a colour he typed and cannot see is a bug he cannot find.
                Log.OnceWarn("colour-" + (entry == null ? "?" : entry.Identifier),
                    "colour '" + raw + "' is not a hex value like #e8b44d - using the default for now.");
                c = fallback;
            }
            float b = Mathf.Clamp(HighlightBrightness.Value, 0.1f, 2f);
            return new Color(Mathf.Clamp01(c.r * b), Mathf.Clamp01(c.g * b), Mathf.Clamp01(c.b * b), 1f);
        }

        public static KeyCode Key(MelonPreferences_Entry<string> entry, KeyCode fallback)
        {
            string raw = entry == null ? null : entry.Value;
            if (string.IsNullOrEmpty(raw)) return fallback;
            try { return (KeyCode)System.Enum.Parse(typeof(KeyCode), raw.Trim(), true); }
            catch (System.Exception)
            {
                Log.OnceWarn("key-" + entry.Identifier,
                    "'" + raw + "' is not a Unity key name - falling back to " + fallback + ".");
                return fallback;
            }
        }
    }
}
