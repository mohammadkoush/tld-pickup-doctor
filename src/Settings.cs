// LD Pickup Doctor - every knob, in one place, with the reason for its default written down.
//
// Config lives in MelonLoader's own UserData\MelonPreferences.cfg, under categories prefixed
// "LDPD_". That is deliberate: a file the loader already writes, backs up and hot-reloads costs
// nothing to maintain, and it can be edited with the game shut, without going through the window.
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
        private static MelonPreferences_Category _cheats;

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

        // ---- cheats ----------------------------------------------------------------------------
        //
        // These are NOT held to the rule above, and that is the whole reason they live in their own
        // category with their own tab. Everything else in this mod removes repetition and leaves the
        // price alone. This section removes the price, on purpose, because testing needs it: a
        // pickup radius cannot be judged against a carry cap that keeps being hit, nor an outline
        // colour across a map that has to be walked.
        //
        // Every one is off (or neutral) by default, every one is reversible, and every one is
        // announced in the diagnostic report while it is on - so a strange number in a tally a week
        // from now has "the speed cheat was at 3x" sitting right above it.
        public static MelonPreferences_Entry<float> CheatSpeed;
        public static MelonPreferences_Entry<float> CheatSpeedStep;
        public static MelonPreferences_Entry<bool> CheatInstantHarvest;
        public static MelonPreferences_Entry<bool> CheatUnlimitedCarry;
        public static MelonPreferences_Entry<float> CheatCarryKG;
        public static MelonPreferences_Entry<bool> CheatUnlimitedAmmo;
        public static MelonPreferences_Entry<bool> CheatPerpetualFire;
        public static MelonPreferences_Entry<bool> CheatNoRecoil;

        // Survival rates. Multipliers on the game's own per-hour numbers, 1.00 meaning "leave it
        // alone" and also meaning OFF - at 1.00 nothing is written and the stored originals are kept
        // refreshed from the live values, so a difficulty change is picked up rather than overwritten.
        public static MelonPreferences_Entry<float> RateCold;
        public static MelonPreferences_Entry<float> RateTired;
        public static MelonPreferences_Entry<float> RateThirst;
        public static MelonPreferences_Entry<float> RateHunger;
        public static MelonPreferences_Entry<float> RateStamina;
        public static MelonPreferences_Entry<float> RateHeldFuel;
        public static MelonPreferences_Entry<bool> FuelIncludesPlaced;

        // The timed buffs - Improved Rest, Warming Up, Reduced Fatigue and the rest. These hold a
        // countdown that is already running; they never start one.
        public static MelonPreferences_Entry<bool> HoldBuffTimers;
        public static MelonPreferences_Entry<bool> HoldWellFed;

        // ---- keys ------------------------------------------------------------------------------
        public static MelonPreferences_Entry<string> KeyWindow;
        public static MelonPreferences_Entry<string> KeySweepRoom;
        public static MelonPreferences_Entry<string> KeyToggleHighlight;
        public static MelonPreferences_Entry<string> KeyTogglePickup;
        public static MelonPreferences_Entry<string> KeyReport;
        public static MelonPreferences_Entry<string> KeySave;
        public static MelonPreferences_Entry<bool> KeySaveNeedsCtrl;
        public static MelonPreferences_Entry<float> SaveCooldownSeconds;
        public static MelonPreferences_Entry<string> KeySpeedUp;
        public static MelonPreferences_Entry<string> KeySpeedDown;

        // ---- interface -------------------------------------------------------------------------
        public static MelonPreferences_Entry<float> WindowOpacity;
        public static MelonPreferences_Entry<float> TooltipDelaySeconds;
        public static MelonPreferences_Entry<bool> WindowPausesCursor;
        public static MelonPreferences_Entry<bool> WindowPausesGame;
        public static MelonPreferences_Entry<float> WindowX;
        public static MelonPreferences_Entry<float> WindowY;

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
            _cheats = MelonPreferences.CreateCategory("LDPD_Cheats", "Pickup Doctor - Cheats");

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

            CheatSpeed = _cheats.CreateEntry("SpeedMultiplier", 1.0f,
                description: "Movement speed, as a multiple. 1.00 is the game's own speed and is also "
                    + "the OFF position - at 1.00 the original acceleration is put back and nothing "
                    + "is touched. Above about 4 the character starts passing through thin geometry, "
                    + "which is the engine's limit rather than a setting to raise.");
            CheatSpeedStep = _cheats.CreateEntry("SpeedStep", 0.25f,
                description: "How much the speed keys move the multiplier per press.");
            CheatInstantHarvest = _cheats.CreateEntry("InstantHarvest", false,
                description: "Zero the harvest duration on items and carcasses. Reversible: each "
                    + "original duration is stored and put back when you turn this off.");
            CheatUnlimitedCarry = _cheats.CreateEntry("UnlimitedCarry", false,
                description: "Raise the carry cap to CarryKG. The mod's own weight gate then passes "
                    + "on its own, because it reads the game's number rather than keeping one.");
            CheatCarryKG = _cheats.CreateEntry("CarryKG", 500f,
                description: "What 'unlimited' means, in kilograms. Not infinity on purpose - the "
                    + "encumbrance bar and the calorie burn are computed from this, and a real "
                    + "infinity makes both meaningless instead of generous.");
            CheatUnlimitedAmmo = _cheats.CreateEntry("UnlimitedAmmo", false,
                description: "Keep the clip of the firearm in your hands full. It tops up the gun you "
                    + "are holding only, so ammo in the pack is untouched.");
            CheatPerpetualFire = _cheats.CreateEntry("PerpetualFire", false,
                description: "Fires, stoves and fireplaces never go out. This sets the game's own "
                    + "m_IsPerpetual flag, the one it uses for scripted fires, and clears it again "
                    + "when you turn this off.");

            KeyWindow = _keys.CreateEntry("Window", "F10", description: "Open and close the settings window.");
            KeySweepRoom = _keys.CreateEntry("SweepRoom", "F7", description: "Take everything eligible nearby, once.");
            KeyToggleHighlight = _keys.CreateEntry("ToggleHighlight", "F8", description: "Outlines on or off.");
            KeyTogglePickup = _keys.CreateEntry("TogglePickup", "F9", description: "Auto pickup on or off.");
            KeyReport = _keys.CreateEntry("Report", "F11", description: "Write the diagnostic report to the log now.");
            CheatNoRecoil = _cheats.CreateEntry("NoRecoil", false,
                description: "Take the kick out of firearms. It zeroes the shooter's own recoil "
                    + "vectors - the position and rotation kick applied on firing, and the dry-fire "
                    + "kick - and puts every one of them back when switched off. Aim sway from cold "
                    + "is a separate thing and is left alone.");

            RateCold = _cheats.CreateEntry("ColdRate", 1.0f,
                description: "How fast you freeze, as a multiple of the game's own rate. 1.00 is off. "
                    + "It scales the per-degree freezing coefficient, so cold still bites harder the "
                    + "colder it is - the curve is the game's, only the steepness is yours.");
            RateTired = _cheats.CreateEntry("TirednessRate", 1.0f,
                description: "How fast fatigue builds, standing, walking and sprinting alike. 1.00 is off.");
            RateThirst = _cheats.CreateEntry("ThirstRate", 1.0f,
                description: "How fast thirst builds, awake and resting. 1.00 is off.");
            RateHunger = _cheats.CreateEntry("HungerRate", 1.0f,
                description: "How fast calories burn - every activity the game tracks separately, "
                    + "scaled together so the relationship between them is untouched. 1.00 is off.");
            RateStamina = _cheats.CreateEntry("StaminaRate", 1.0f,
                description: "How fast sprinting drains stamina. 1.00 is off. At the very bottom of "
                    + "the slider it switches on the game's own unlimited-sprint flag instead of "
                    + "dividing by something near zero.");

            RateHeldFuel = _cheats.CreateEntry("HeldFuelRate", 1.0f,
                description: "How fast the item in hand uses itself up: lamp fuel, torch and flare "
                    + "burn time, flashlight battery. 1.00 is off, below 1 lasts longer, above 1 "
                    + "burns quicker. It changes only the item being held, so a lantern left burning "
                    + "on a table is untouched, and nothing here is written into a save - the numbers "
                    + "come back from the game's own data on the next launch.");

            FuelIncludesPlaced = _cheats.CreateEntry("FuelIncludesPlaced", true,
                description: "Apply the fuel dial to placed and dropped items too, not only the one "
                    + "in hand - a lantern left burning on a table, a torch stuck in the snow. Turn "
                    + "it off to affect the held item alone.");

            HoldBuffTimers = _cheats.CreateEntry("HoldBuffTimers", false,
                description: "Stop the countdown on the timed buffs: Improved Rest, Warming Up, "
                    + "Reduced Fatigue, the condition-over-time bonus and the pie bonus. It only "
                    + "holds a timer that is ALREADY RUNNING - it never grants a buff that was not "
                    + "earned, so a buff has to be picked up in the ordinary way first.");
            HoldWellFed = _cheats.CreateEntry("HoldWellFed", false,
                description: "Keep Well Fed from lapsing. Well Fed has no clock of its own - it ends "
                    + "when the stomach empties - so this holds the state rather than a timer, and "
                    + "like the others it will not start one that was not already there.");

            KeySpeedUp = _keys.CreateEntry("SpeedUp", "PageUp",
                description: "Raise the speed multiplier by SpeedStep.");
            KeySpeedDown = _keys.CreateEntry("SpeedDown", "PageDown",
                description: "Lower the speed multiplier by SpeedStep. It stops at 0.25 rather than "
                    + "at zero, because a multiplier of zero is a character that cannot move and "
                    + "looks exactly like a crash.");
            KeySave = _keys.CreateEntry("SaveGame", "S",
                description: "Save the game where you stand, with the game's own save and its own "
                    + "'game saved' message. This is not a cheat - it is the same save the game makes "
                    + "when you sleep or pass a trigger, taken at a moment you chose.");
            KeySaveNeedsCtrl = _keys.CreateEntry("SaveNeedsCtrl", true,
                description: "Require Ctrl to be held with the save key. On by default because S "
                    + "alone is a movement key, and an unmodified S would save on every step back.");
            SaveCooldownSeconds = _keys.CreateEntry("SaveCooldownSeconds", 5f,
                description: "Least time between two saves from the key. A save writes to disk and "
                    + "stutters, so holding the key must not queue a hundred of them.");

            WindowOpacity = _iface.CreateEntry("Opacity", 0.94f, description: "Settings window background opacity.");
            TooltipDelaySeconds = _iface.CreateEntry("TooltipDelaySeconds", 2f,
                description: "How long the cursor must rest on a row before its explanation appears. "
                    + "The delay is deliberate: with an instant tooltip, running down a column covers "
                    + "the row you were trying to read. Zero gives the instant behaviour back.");
            WindowPausesCursor = _iface.CreateEntry("FreeCursor", true,
                description: "Release the mouse from the game while the window is open.");
            WindowX = _iface.CreateEntry("WindowX", 60f,
                description: "Where the window sits, across. It is remembered when the window is "
                    + "dragged, so it opens where it was left rather than back in the corner.");
            WindowY = _iface.CreateEntry("WindowY", 60f,
                description: "Where the window sits, down.");
            WindowPausesGame = _iface.CreateEntry("PauseGame", true,
                description: "Pause the game while the window is open, and resume on the way out. "
                    + "It sets the game's own pause flag and re-asserts it every frame; if the game "
                    + "keeps clearing it, the mod falls back to freezing time and says so in the log. "
                    + "Turn it off if you want to watch something happen while you change a setting.");

            DiagEnabled = _diag.CreateEntry("Enabled", true,
                description: "Count what the sweep decided and write it to the MelonLoader log.");
            DiagReportSeconds = _diag.CreateEntry("ReportSeconds", 60f,
                description: "Seconds between tally reports.");
            DiagLogEveryRefusal = _diag.CreateEntry("LogEveryRefusal", false,
                description: "Log every single refusal rather than counting them. Very noisy - this is "
                    + "for the ten seconds when you are asking why one specific item was skipped.");

            MelonPreferences.Save();
        }

        // ---- saving, debounced -------------------------------------------------------------------
        //
        // Dragging one slider wrote the whole preferences file eight times in sixty milliseconds -
        // the log said so in a row of "Preferences Saved!" lines. Every frame of a drag is a change,
        // and every change was a disk write of the entire config.
        //
        // So changes mark the file dirty and one write happens a second later, plus an immediate one
        // whenever the window closes. Nothing can be lost: the value is already live in memory the
        // instant it changes, and the only thing being deferred is the copy on disk.
        private static bool _dirty;
        private static float _dirtySince;

        public static void SaveSoon()
        {
            if (!_dirty) _dirtySince = Time.realtimeSinceStartup;
            _dirty = true;
        }

        /// <summary>Called every frame. Writes at most once a second, and only after a change.</summary>
        public static void FlushSaves()
        {
            if (!_dirty) return;
            if (Time.realtimeSinceStartup - _dirtySince < 1f) return;
            SaveNow();
        }

        public static void SaveNow()
        {
            _dirty = false;
            try { MelonPreferences.Save(); }
            catch (System.Exception e)
            {
                // The dirty flag goes back ON, so a failed write is retried rather than dropped.
                _dirty = true;
                _dirtySince = Time.realtimeSinceStartup;
                Log.OnceWarn("save-prefs", "writing MelonPreferences threw: " + e.Message
                    + " - the settings are still live in memory and the write is retried.");
            }
        }

        /// <summary>"BlizzardWalker" reads as "Blizzard Walker" on a row someone has to scan.</summary>
        public static string Spaced(string camel)
        {
            if (string.IsNullOrEmpty(camel)) return camel;
            System.Text.StringBuilder sb = new System.Text.StringBuilder(camel.Length + 4);
            for (int i = 0; i < camel.Length; i++)
            {
                if (i > 0 && char.IsUpper(camel[i]) && !char.IsUpper(camel[i - 1])) sb.Append(' ');
                sb.Append(camel[i]);
            }
            return sb.ToString();
        }

        /// <summary>Hex to colour, with the brightness dial applied and a loud fallback.</summary>
        public static Color Colour(MelonPreferences_Entry<string> entry, Color fallback)
        {
            Color c;
            string raw = entry == null ? null : entry.Value;
            if (string.IsNullOrEmpty(raw) || !ColorUtility.TryParseHtmlString(raw.Trim(), out c))
            {
                // Never silently substitute: a colour that was typed and cannot be seen is a bug
                // that cannot be found.
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
