# LD Pickup Doctor - The Long Dark

**Finds out why an item is not being picked up, says so in plain words, and then picks it up.**

A MelonLoader mod for The Long Dark, carried across from the Green Hell mod of the same name.
Every part is independently switchable, and anything you do not turn on costs nothing:

- **Diagnostics** - names the *exact* reason each nearby item was not taken.
- **Pickup** - takes matching items you walk near, with no click and no inspect screen.
- **Highlight** - coloured outlines on nearby items, coloured by what they are *for*.
- **Harvest** - plants and branch piles gathered with no hold and no animation.
- **Sweep key** - one press takes everything eligible in the room, and reports what it could not.
- **Settings window** - F10, tabbed, with an explanation for every row.

---

## The rule this mod is built to

**Pickup Doctor takes away the grind. It does not make the game cheaper to play.**

Those are different things, and the difference decides every feature here.

*Grind* is repetition that costs you time and teaches you nothing: clicking the same stick forty
times, holding the button through a harvest animation you have seen a thousand times. Removing it
costs the game nothing, because nothing was being decided while it happened.

*Cost* is what the game charges you: the hours a break-down burns, the weight you must carry, the
tool you had to find first. That is where The Long Dark actually lives. A mod that quietly lowers it
is not saving you time, it is playing an easier game and calling it yours.

So, concretely:

- Auto-pickup removes the clicking. It does not raise your carry weight, and it refuses a pickup
  that would put you over the cap - the same cap the game would have enforced.
- Auto-harvest removes the hold. It yields exactly what the hold would have yielded, and it still
  demands the tool the plant demands.
- Ruined items are skipped by default, because carrying nothing is not a saving.
- **Instant break-down** is the one switch that refunds a price rather than removing a chore. It is
  off, it is labelled as such in the window, and it puts every hour cost back when you turn it off.

When a feature is convenient *and* cheaper, the cheapness is a bug in the feature, not a bonus.

---

## Requirements

- The Long Dark (tested against the Unity 6000.0.60f1 IL2CPP build)
- MelonLoader 0.7.3

## Install

1. Install MelonLoader for The Long Dark.
2. Drop `LDPickupDoctor.dll` into `TheLongDark\Mods\`.
3. Start the game once. Settings are written to `TheLongDark\UserData\MelonPreferences.cfg`.
4. The log is `TheLongDark\MelonLoader\Latest.log` - look for `[LD Pickup Doctor]`.

### If MelonLoader appears to do nothing at all

No log, no `Il2CppAssemblies` folder, no error - the game just boots clean. That is not MelonLoader
failing, it is MelonLoader never being *loaded*, and it has a specific cause worth knowing:

MelonLoader ships its bootstrap as `version.dll` in the game folder, and relies on the game asking
Windows for `version.dll` so the folder copy is found first. On some machines the Windows
application-compatibility shim engine (`apphelp.dll` plus `AcGenral.DLL`) is loaded into `tld.exe`
*before* `UnityPlayer.dll`, and `AcGenral` imports `version.dll` - from `System32`. A module that is
already loaded is never resolved again, so the game folder copy is never even looked at.

The fix is to rename the proxy to one of the other names MelonLoader supports:

    version.dll  ->  winmm.dll

`winmm` is loaded later, by `UnityPlayer` itself, and that lookup does honour the application
directory. `tools\loader-doctor.ps1` detects this and repairs it:

    powershell -ExecutionPolicy Bypass -File tools\loader-doctor.ps1            # report
    powershell -ExecutionPolicy Bypass -File tools\loader-doctor.ps1 -Repair    # rotate the name

---

## Reading the log

```
[LD Pickup Doctor] world 1 is live - sweeping every 0.25s within 30m.
[LD Pickup Doctor] [state] weight 12.4/30.0 kg  sweeps=2401  lastSweep=0.31ms  nearby items=14 ...
[LD Pickup Doctor] [tally] taken=38 harvested=6  TooHeavy=11  InsideContainer=4
```

- `[state]` answers the commonest question before it is asked: how much weight is left, how much the
  sweep costs, and how many things are actually nearby.
- `[tally]` counts what each gate decided per report window, so the dominant refusal is visible at a
  glance rather than inferred from a wall of lines.

### Every outcome it can report

| Outcome | Meaning |
|---|---|
| `Success` | Taken. |
| `WouldSucceed` | Passed every gate but was not taken - `DryRun` is on, or the per-sweep cap was reached. |
| `TooHeavy` | Would exceed your carry capacity. The game's cap, not the mod's. |
| `Ruined` | Zero condition, and `SkipRuined` is on. |
| `CannotInteract` | The game itself says this cannot be interacted with right now. |
| `InsideContainer` | It is inside a container rather than on the ground. |
| `Locked` | Locked in a container. |
| `Hidden` | Hidden by the game - unrevealed loot, or a mission state. |
| `AlreadyHeld` | Already in your inventory. |
| `AttachedElsewhere` | Attached to a place point or a travois. |
| `NotTargetName` | A real item, just not on your Names list. Counted, not printed - it is most of the world. |
| `TakeFailed` | Every gate passed and the game still refused. **This is the one worth reporting.** |
| `NoGearItem` | A collider on the gear layer with no `GearItem` on it. |

---

## Settings window - F10

Nine kinds of row across eight tabs, and two rules the tab code holds to:

- **Order never changes.** Tabs are appended, never inserted, and rows inside a tab are fixed. An
  option that moves between launches cannot be found by memory.
- **No option appears twice.** The Keys tab owns every hotkey wherever it was bound. An option in
  two places has no location.

**Hovering a row shows its explanation** in the strip at the bottom, after the cursor has rested for
`Interface.TooltipDelaySeconds` (default 2s). The delay is deliberate: with an instant tooltip,
sweeping down a column covers the row you were trying to read. Set it to `0` for instant.

| Tab | Holds |
|---|---|
| **Items** | Every item name the sweep has seen, with a one-click add or remove from the Names list. |
| **Pickup** | Radius, caps, the name list, weight gating, ruined items. |
| **Highlight** | Radius, thickness, count, labels, what else to outline. |
| **Colours** | One hex colour per category. |
| **Grind** | Auto-harvest, the sweep key, and the one switch that refunds hours. |
| **Keys** | Every hotkey, click to rebind. |
| **Advanced** | Sweep interval and radius, diagnostics, live sweep cost. |
| **Interface** | The window itself: opacity, explanation delay, cursor. |
| **Cheats** | The exception to the rule above. See below. |

### Default keys

| Key | Does |
|---|---|
| `F7` | Sweep the room - take everything eligible within `SweepRoomRadius`, and report what it could not. |
| `F8` | Outlines on or off. |
| `F9` | Auto pickup on or off. |
| `F10` | The settings window. |
| `F11` | Write the diagnostic report to the log now. |
| `Ctrl` and `S` | Save the game where you stand, through the game's own save and its own message. |
| `PageUp` / `PageDown` | Raise or lower the movement speed multiplier (Cheats tab). |

---

## The Cheats tab

**This tab does not follow the rule the rest of the mod follows.** Everything else removes
repetition and leaves the game's price where Hinterland put it. Everything here removes the price,
because testing needs it: you cannot judge a pickup radius against a carry cap you keep hitting, or
outline colours across a map you have to walk.

| Cheat | Does | Default |
|---|---|---|
| Movement speed | Scales `vp_FPSController.MotorAcceleration`. `1.00` is off. | `1.00` |
| Instant harvest | Zeroes `HarvestBase.m_DurationMinutes` on items and `BodyHarvest.m_QuarterDurationMinutes` on carcasses. | off |
| Unlimited carrying weight | Raises `Encumber.m_MaxCarryCapacity` to `CarryKG`. | off |
| Unlimited ammo | Keeps the clip of the gun **in your hands** full. Ammo in the pack is untouched. | off |
| Perpetual fire | Sets the game's own `Fire.m_IsPerpetual`, the flag it uses for scripted fires. | off |
| No recoil | Zeroes `vp_FPSShooter.MotionPositionRecoil`, `MotionRotationRecoil` and the dry-fire kick. | off |
| Cold rate | Scales `Freezing.m_FreezingIncreasePerHourPerDegreeCelsius`. | `1.00` |
| Tiredness rate | Scales every `Fatigue.m_FatigueIncreasePerHour*`. | `1.00` |
| Thirst rate | Scales `Thirst.m_ThirstIncreasePerDay` awake and resting. | `1.00` |
| Food rate | Scales all ten `Hunger.m_CalorieBurnPerHour*` figures together. | `1.00` |
| Stamina rate | Scales `PlayerMovement.m_SprintStaminaUsagePerSecond`; at the bottom it uses the game's own unlimited-sprint flag. | `1.00` |
| Item fuel | Lamp fuel, torch and flare burn time, flashlight battery. At the bottom of the slider it is infinite. | `1.00` |
| Fuel covers placed items | Applies the fuel dial to lanterns and torches left burning, not only the one in hand. | on |
| Hold buff countdowns | Stops the timer on Improved Rest, Warming Up, Reduced Fatigue, the condition bonus and the pie bonus. Only holds a clock already running. | off |
| Hold Well Fed | Keeps Well Fed from lapsing. It is a state rather than a timer, so it is re-asserted rather than topped up. | off |

The rate dials go **both ways** - below 1 is gentler, above 1 is harsher - so they are as much
a difficulty dial as a cheat. Each scales the game's own per-hour numbers rather than replacing them
with one figure of ours, which keeps the relationships Hinterland balanced: fatigue still builds
faster sprinting than standing, and cold still bites harder the colder it gets.

While a dial sits at `1.00` the stored baseline is **kept refreshed from the live value**, so a
difficulty change or a buff is picked up rather than overwritten. The moment it leaves `1.00` the
refresh stops and every write is `baseline x multiplier` - never the previous write times the
multiplier again, which is how a dial like this quietly runs away.

Three properties every one of them has:

1. **Reversible.** Nothing is written without its original being stored first, and everything is put
   back when the switch goes off. Three of these touch objects the game *serialises*, and a cheat
   that forgets what it overwrote quietly edits a save you keep.
2. **Re-applied, not set once.** The game rewrites acceleration when you crouch or get encumbered,
   and recomputes the carry cap from buffs and fatigue. A single write would last until the first
   crouch and then silently stop.
3. **Announced.** While any of them is on, the diagnostic report carries a `[cheats]` line above the
   tally, so a number read back next week has "speed was at 3x" sitting right above it.

Perpetual fire is also *verified* rather than trusted: if the remaining life of the nearest fire
keeps falling three checks in a row with the flag set, the mod winds the fuel clock back directly
and says in the log that the flag was not enough on its own.

**Speed above about 4x** starts passing the character through thin geometry. That is the engine, not
a setting to raise.

---

## Colours mean categories

A single outline colour answers "there is a thing there", which you already knew - the thing is on
your screen. Colour answers the question you actually have while sweeping a house in a blizzard:
*is that worth the walk?* At eight metres in the dark, food, fuel and medicine are three identical
grey lumps.

The category is read off the game's **own component** on each item - `m_FoodItem`,
`m_FuelSourceItem`, `m_FirstAidItem` - not from a list of names this mod carries. A name list rots
the moment Hinterland adds an item; a component is what the game itself uses to decide what a thing
is. Nothing is hard-coded that the game already knows.

| Category | Default |
|---|---|
| Food and drink | `#e8b44d` |
| Water | `#5fb8e8` |
| Fuel and firewood | `#c8763c` |
| Tools and weapons | `#b0b8c0` |
| Clothing | `#9a8ce0` |
| First aid | `#e0607a` |
| Ammunition | `#e8e04d` |
| Fire and light | `#ff8c3c` |
| Materials | `#7fd4a8` |
| Everything else | `#dfe6ec` |
| Harvestable plants | `#63c96b` |
| Unsearched containers | `#8fa8c0` |

An item that is nearby but cannot be taken right now - too heavy, ruined - is drawn **dimmer**
rather than not drawn. "Why is that one dark" has an answer in the log. An item that silently stops
being outlined does not.

### How the outline is drawn, and its two honest limits

No shader is compiled and no Unity project is needed. The outline is the **inverted hull**: the mesh
drawn again slightly larger with front faces culled, using `Hidden/Internal-Colored`, a built-in
shader Unity always ships and never strips. Colour is a *material property*, which is what makes one
shader serve any number of colours.

1. The rim **scales with the object**, because the hull is grown by scaling rather than by pushing
   vertices along their normals - normals need a vertex shader. A log gets a fatter rim than a match.
   `Thickness` is a setting for exactly that reason.
2. It grows about the **renderer's bounds centre**, not the object's pivot. Dropped items often have
   their pivot at one end, and scaling about a pivot swings the hull off the object entirely.

---

## Building

    powershell -ExecutionPolicy Bypass -File build.ps1

References come straight from the game install, so the build always matches the installed version.
The interop assemblies under `MelonLoader\Il2CppAssemblies` are generated by MelonLoader on its
first successful launch - if they are missing, the build stops and says so rather than producing
four hundred unresolved types.

## Licence

MIT.
