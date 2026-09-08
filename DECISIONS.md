# Decisions - LD Pickup Doctor

Read this before changing anything. It is the record of what was decided and *why*, so a later
session does not re-argue a settled question or undo something for a reason that was already
considered. Dates are absolute.

---

## 2026-09-07 - the project

Port the Green Hell **Pickup Doctor** to **The Long Dark**, keeping the parts about picking items
up, removing grind, and highlighting items on the ground in colours. Name: **LDPickupDoctor**
(his choice, this session). Source lives at `C:\Users\moham\Downloads\Claude\ld-pickup-doctor`.

The Green Hell original is at `C:\Users\moham\Downloads\Claude\green-hell-pickup-doctor` and is the
reference for behaviour and for the writing in the README. It is **not** a code source: Green Hell
is Mono/BepInEx/Unity 2019 and The Long Dark is IL2CPP/MelonLoader/Unity 6, so the concepts port and
the code does not. `Silhouette.cs` is the one file that is a near-literal port, because the inverted
hull trick and `Hidden/Internal-Colored` are engine-level and identical in both.

---

## The environment, established by measurement rather than assumption

| Thing | Value | How it was established |
|---|---|---|
| Unity | `6000.0.60f1` | Cpp2IL said so in the MelonLoader log during generation |
| IL2CPP metadata | 31.1 | same line |
| MelonLoader | 0.7.3 | `ProductVersion` on the proxy DLL |
| Mod target | `net6.0` | MelonLoader 0.7.3 runs mods on net6 |
| Interop namespace | `Il2Cpp` for game types, `Il2CppTLD.*` for their own libraries | the generated assemblies |
| Build SDK | .NET SDK 8.0.424 at `%USERPROFILE%\.dotnet` | installed this session; Program Files dotnet has runtimes only |

---

## 2026-09-07 - MelonLoader was installed and had never once run

**Counted before fixing.** The evidence, before any theory: no `MelonLoader\Logs`, no
`MelonLoader\Il2CppAssemblies`, no `UserData\MelonPreferences.cfg`, and a game launch observed live
with none of them appearing. So it had run zero times, not "sometimes".

**The line that stated the cause** was the module list of the running `tld.exe`, in load order:

    apphelp.dll, AcGenral.DLL, ... , VERSION.dll  (C:\windows\SYSTEM32\VERSION.dll), UnityPlayer.dll

The Windows application-compatibility shim engine is loaded into `tld.exe` before `UnityPlayer`, and
`AcGenral.DLL` imports `version.dll` - which brings **System32's** copy into the process. A module
already loaded is never resolved again, so `UnityPlayer`'s later request for `version.dll` was
answered from memory and the game-folder proxy was never looked at.

Two hypotheses were killed on the way, both cheaply, both worth recording so they are not re-tried:

- *version.dll is a Windows KnownDLL* - **no**. The `KnownDLLs` registry key was read; it is not there.
- *a compatibility layer was set on the exe by hand* - **no**. `AppCompatFlags\Layers` under both
  HKCU and HKLM was read; `tld.exe` is not in either. The shim comes from the built-in database.

**The fix**: rename the proxy `version.dll` -> `winmm.dll`. `winmm` is also imported by `AcGenral`
but is *not* pulled in early - in the observed load order it appears after `UnityPlayer`, which
means `UnityPlayer` is what loads it, and that lookup honours the application directory. Confirmed
working: MelonLoader loaded, 161 interop assemblies generated, three mods loaded.

**The self-heal** (the rule: anything that broke once heals itself next time) is
`tools\loader-doctor.ps1`. It detects a dead loader by asking the running process for its modules,
or by the absence of a log, and `-Repair` rotates the proxy through MelonLoader's three supported
names. `build.ps1` runs it after every deploy.

---

## Design decisions in the mod itself

**One sweep, not three.** Pickup, highlight and auto-harvest all need "what is nearby". Three
`OverlapSphere` loops would triple the only expensive thing the mod does and let the three disagree
about what is there. `Sweep.Run` publishes lists; features read them.

**Every refusal has a name.** This is the whole point of the original and transfers unchanged. A
pickup crosses several gates; if a gate refuses silently, "the mod is broken" and "the mod tried and
was blocked" look identical. So every gate ends in a named `Outcome` and the tally counts them.

**Judging has no side effects.** `Sweep.Judge` decides but never takes, so the highlight can use the
same verdict to dim an item it cannot take without picking anything up.

**Two pickup paths, tried and verified rather than guessed.** IL2CPP interop gives signatures and no
method bodies, so the correct call cannot be *read*. `Inventory.AddGear` is tried first, then
**verified** (`m_InPlayerInventory`, or the object leaving the world); on failure it escalates to
`PlayerManager.ProcessPickupItemInteraction` and says so in the log. After five failures it stops
preferring the quiet path for the session. A wrong guess therefore costs a few frames once, and can
never be a silent no-op.

**Categories come from the game's own components**, not from a name list this mod carries
(`m_FoodItem`, `m_FuelSourceItem`, `m_FirstAidItem`, ...). Nothing hard-coded that the game already
knows; a name list would rot on the next content patch.

**Layer mask built from `vp_Layer` names, not numbers.** A layer index is a thing Hinterland can
renumber in a patch; a name is not. If the names stop resolving, the sweep widens to every layer and
says so rather than quietly sweeping nothing.

**Forty empty sweeps widen the search.** If the masked sweep finds nothing for ten seconds in a
loaded world, it falls back to every layer and logs why. In Green Hell the same symptom had a
different cause (sleeping rigidbodies) and cost hours precisely because it was silent.

**Backoff, never abandonment.** A harvestable that refuses is retried on an escalating delay capped
at once a minute. Nothing is given up on permanently; a resource that can come back is always
retried.

**`InstantBreakDown` is reversible.** It stores each `BreakDown.m_TimeCostHours` before zeroing it
and puts the original back the moment the switch goes off, so trying it does not permanently alter
the world he saves.

**There is deliberately no `InstantContainerSearch`.** The reveal timing lives on
`GearItem.m_NormalizedRevealTimeInContainer`, which is only reachable once a container has
instantiated its contents. A switch that cannot be made to work is worse than a missing one, because
it makes the whole mod look broken. It goes in when it can be verified.

---

## Known-good API surface (Unity 6000.0.60f1 build, 2026-09-07)

    GameManager.GetPlayerTransform() / GetMainCamera() / IsMainMenuActive()
    GameManager.GetInventoryComponent() -> Inventory
    GameManager.GetEncumberComponent()  -> Encumber      (m_MaxCarryCapacity)
    GameManager.GetPlayerManagerComponent() -> PlayerManager
    Inventory.AddGear(GearItem, bool) / GetTotalWeightKG()
    PlayerManager.ProcessPickupItemInteraction(GearItem, bool, bool, bool) -> bool
    PlayerManager.ProcessPickupWithNoInspectScreen(GearItem, bool)
    GearItem: DisplayName, WeightKG, CanInteract, m_CurrentHP, m_InPlayerInventory,
              m_InsideContainer, m_LockedInContainer, m_IsHidden, IsAttachedToPlacePoint(),
              m_MeshRenderers, m_SkinnedMeshRenderers, and one field per category component
    Harvestable: Harvest(), IsHarvested(), HasToolRequired(), GetRequiredTool()
    Container: m_Inspected, m_Items
    BreakDown: m_BreakDownObjects (static list), m_TimeCostHours, m_RequiresTool
    vp_Layer: Gear, Container, InteractiveProp, InteractivePropNoCollideGear
    ItemWeight (Il2CppTLD.IntBackedUnit): FromKilograms(float), operators, m_Units

To regenerate the reference: `ilspycmd -p -o <out> MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll`.
Bodies are stubs - signatures are the only ground truth available, which is why the mod verifies
what it calls instead of trusting it.

---

## Not done yet

- **In-world verification.** As of this entry the mod loads, initialises and logs; nothing has been
  confirmed against a loaded save. Pickup path, harvest, and outline appearance all need one run.
- `InstantContainerSearch`, as above.
- A constant-width screen-space outline would need a real shader in an asset bundle built against
  Unity 6000.0.60f1. He has offered to compile it in Unity if it is ever wanted; the current
  inverted hull needs no Unity at all.

## Unrelated, found on the way

`ContainerRespawnTweaker.dll` in his Mods folder fails at load:
`Could not load file or assembly 'ModSettings, Version=2.2.5.0'`. `UserLibs` is empty. That mod
needs `ModSettings.dll` dropped into `TheLongDark\UserLibs`. Not this mod's problem, but it is in
the same log and would otherwise look like ours.

---

## 2026-09-08 - first in-world run, and what it corrected

Pickup, outlines and the sweep all worked on the first run. Three things came back from it.

**The F10 window opened blank.** `GUI.Window` takes a `GUI.WindowFunction`, which under IL2CPP is a
generated Il2Cpp delegate, not a managed one. The cast compiled, the call did not throw, and the
body was simply never invoked - so the frame drew and the contents did not, with nothing in the log
because nothing failed. Replaced with a `GUILayout.BeginArea` panel plus hand-written dragging,
which needs no delegate at all. The panel now logs once that its body ran, so "blank again" is a
question the log answers.

Also removed a write to `GUI.skin.window.normal.background`. That is a process-wide global; one mod
tinting it is how every other IMGUI window in the game turns black.

**`CannotInteract=2259` against `Success=3` in one minute.** Counted before concluding: those were
fixtures and props on the gear layer being state-checked before anything asked whether he wanted
them. `Sweep.Judge` now runs the name gate before the state gates, so the tally reports on listed
items and the dominant number means something.

**The world-live line wrote itself seven times in three minutes**, because The Long Dark initialises
a scene per interior and transition. Rate limited to once every two minutes.

**New: a save key.** `Ctrl` and `S` calls `GameManager.SaveGameAndDisplayHUDMessage()`, guarded by
`SaveIsBlockedDueToRestoreGame()` and `SaveGameSystem.IsAsyncSaveRunning()`, with a cooldown. The
cooldown is deliberately **not** stamped when a save fails, so a failed press retries at once rather
than being told to wait for a save that never happened.

## 2026-09-08 - the 6.9 GB log, which was not ours

`MelonLoader\Latest.log` had reached **6.89 GB**. `ContainerRespawnTweaker` threw a
`FileNotFoundException` for `ModSettings, Version=2.2.5.0` on every single `Container.UpdateContainer`
call, and each one wrote a full stack trace. By count this was by far the loudest fault on the
machine and it was not in this mod at all.

Fixed by installing the library it wanted: **ModSettings 2.2.5** from
`DigitalzombieTLD/ModSettings` (the maintained fork; the original `zeobviouslyfakeacc` repo is
archived at 1.9.0). It is itself a MelonMod, so it goes in `Mods\`, not `UserLibs\`. Confirmed:
four mods load, no errors, log back to kilobytes.

---

## 2026-09-08 - the Cheats tab, and two bugs the log caught within a minute of each other

He asked for testing cheats: speed up and down, instant harvest, unlimited carrying weight,
unlimited ammo in a firearm, fires that never go out, and rate dials for cold, tiredness, thirst,
food and stamina. They live in `src/Cheats.cs`, their own preferences category and their own tab,
deliberately fenced off from the rule the rest of the mod follows. Everything is off or neutral by
default, everything is reversible, and the diagnostic report carries a `[cheats]` line while any of
them is on.

### `GUILayout.TextField` cannot be used in this game

The window drew its tabs but the Items tab threw, and the stack named the cause exactly:

    System.NotSupportedException: Method unstripping failed
       at UnityEngine.TextEditor.SaveBackup()
       at UnityEngine.GUI.DoTextField(...)
       at LDPickupDoctor.Ui.ItemsTab()

Hinterland's build has `UnityEngine.TextEditor` stripped and Il2CppInterop cannot unstrip it. Every
text field is now built from a `Button` plus raw key events, both of which survive. Less capable -
no selection, no clipboard - and the only kind that works here. **Do not reintroduce `TextField`.**

Known limit: while a field has focus, the keystrokes still reach the game as well, because the game
polls legacy input rather than reading IMGUI events. The Items tab has add and remove buttons for
exactly that reason.

### The speed cheat re-baselined from its own write

Two lines, seconds apart, in the first run with cheats:

    speed cheat on - the game's own acceleration is 0.0300, now scaled by 2.29x
    speed cheat on - the game's own acceleration is 0.0687, now scaled by 2.29x

0.0300 x 2.29 = 0.0687. The controller handle went stale, the code re-looked it up, and re-captured
the "original" from the value it had itself written - so each re-lookup multiplied the speed again,
and the value it would have restored was wrong too. Baselines are now keyed by controller instance
id, so an object already seen is never re-baselined.

The same trap is handled explicitly in `Cheats.Scale`, used by the five rate dials: on the way back
to `1.00` the stored baseline is *restored* before tracking resumes, because the live value at that
moment is still our own write. That branch carries a comment saying so; do not simplify it away.

---

## 2026-09-08 - the window pauses the game

F10 now pauses; Escape (or F10 again) resumes. `Interface.PauseGame`, on by default.

It sets `GameManager.m_IsPaused`, the game's own flag, read by its own `UpdatePaused` /
`UpdateNotPaused` split - the honest way to pause, rather than freezing the clock under a game that
does not know it stopped. The flag is **re-asserted every frame and then read back**, because
setting a flag the game also writes is not evidence that anything is paused. Five frames of the game
clearing it is treated as the game winning: the mod escalates to `Time.timeScale = 0`, says so in
the log, and undoes both on the way out.

**Escape reaches the game as well as us.** The game polls Escape through its own input layer, which
no IMGUI `Event.Use` can intercept, so the press that closes our window opens its pause menu a frame
later. `Ui.SwallowPauseMenu` closes that menu - but only within 0.35s of an Escape that closed our
window, so an Escape he meant for the game is left alone.

### The carry cheat had the same re-baseline bug as the speed cheat

Caught in the log, again, two lines apart:

    carry cheat on - the game's cap is 30.0 kg, raised to 500.0 kg
    carry cheat on - the game's cap is 500.0 kg, raised to 500.0 kg

`ForgetScene` cleared the "we have the original" flag on every scene initialise, so the next sweep
re-captured, and what it captured was our own 500. Turning the cheat off would then have "restored"
500 kg as though the game had always allowed it.

**The rule this produced, and the reason `ForgetScene` now carries a comment about it:** per-scene
objects (fires, carcasses, harvestables) have their maps cleared on a scene change, because their
ids mean nothing afterwards. Session-long singletons - `Encumber`, `vp_FPSController`, the five
survival components - keep their baselines. Handles are cheap to lose; a baseline is not.

Checked, so it is not a worry: `EncumberSaveDataProxy` does **not** serialise
`m_MaxCarryCapacity`, so nothing was written into a save. The cap is rebuilt from the difficulty
settings each session.

---

## 2026-09-08 - the Items tab was counting its own heartbeat

He recorded the window and said "watch how the numbers increase". Counted from the frames rather
than guessed: every row went `x120` to `x132` across three seconds. Twelve in three seconds is four
a second, and four a second is exactly `ScanIntervalSeconds` at its default of 0.25.

So the counter was counting SWEEP HITS, not items. `Candy Bar x240` did not mean 240 candy bars; it
meant one candy bar that had been looked at twice as often as the rest of the room, because two of
them were in range. The number was its own heartbeat with an item name attached.

`Sweep.SeenNames` now counts **distinct instance ids**, so the number means what anyone reading it
would assume: how many of that item this session has actually seen. The id set is never cleared on a
scene change, because Unity instance ids are unique for the life of the process - an item counted in
one cabin is not counted again in the next.

**And the sweep no longer runs while the settings window is open.** The world is paused behind it,
so a sweep can only report its own heartbeat - and worse, auto-pickup and auto-harvest were firing
behind a window he had opened to stop and think. Outlines and cheats carry on; only the
world-changing pass stops.

---

## 2026-09-08 - the game stayed paused after Escape, and why one unpause was never enough

Reported as "I cannot unpause no matter how many times I hit escape". The log had the whole
sequence, and the order is the entire bug:

1. our window sees Escape, closes, and unpauses - `m_IsPaused = false`
2. the game polls the **same** Escape, opens its pause menu, and pauses **itself**
3. `SwallowPauseMenu` closes that menu, because it was not what the key was pressed for

After step 3 the menu is gone and the flag the menu set is still true: the world is frozen with
nothing on screen to unfreeze it, and pressing Escape again simply repeats the dance. Unpausing at
step 1 was never going to be enough, because the pause that mattered had not happened yet.

`Ui.GuardUnpause` now holds the invariant **the game must never be left paused by us**: for two
seconds after an Escape that closed the window, the pause flag is checked every frame and cleared if
it comes back, and `Time.timeScale` is started again if it is zero. Two seconds, and only after our
own Escape, so a pause menu opened deliberately a moment later is untouched. Confirmed in the log -
the swallow line and the guard line now appear at the same millisecond.

A second bug in the same log: `SwallowPauseMenu` was clearing the Escape timestamp on success, which
killed the guard window before the guard could run. The timestamp is left alone now, and both the
success and failure paths carry a comment saying why - it is the "never clear the intent" rule in
miniature.

## 2026-09-08 - preferences are written once a second, not once a frame

The same log showed eight `Preferences Saved!` lines inside sixty milliseconds: dragging one slider
wrote the entire config file every frame. `Settings.SaveSoon` marks it dirty, `Settings.FlushSaves`
writes at most once a second, and closing the window forces a write. A failed write puts the dirty
flag back rather than dropping the change, and the value is live in memory the instant it changes -
only the copy on disk is deferred.

## 2026-09-08 - the mod's own prose is not addressed to anybody

Standing instruction: this mod is going to be uploaded, so nothing shipped in it may read as a note
to one person. No "you asked for it", no third-person "he" or "his". The reasoning stays - it is the
most useful thing in these files - it just stops being addressed to anybody. The Cheats tab now
opens with "This page is meant for testing. Every other page removes repetition only and leaves the
game's price exactly where it was. This page removes the price."

Addressing the reader as "you" in documentation is fine and normal. What is not fine is text that
only makes sense to the author.

---

## 2026-09-08 - fuel drain in the held item

A sixth rate dial: `Cheats.RateHeldFuel`. Below 1.00 the item in hand lasts longer, above 1.00 it
burns quicker, 1.00 is off.

Four fields, because the game measures four different things and none of them is called fuel:

    KeroseneLampItem.m_FuelBurnPerHour   litres an hour        - a RATE, multiplied
    TorchItem.m_BurnLifetimeMinutes      total minutes of life - a LIFETIME, divided
    FlareItem.m_BurnLifetimeMinutes      the same
    FlashlightItem.m_LowBeamDuration     seconds of battery    - the same, both beams

A rate and a lifetime move in opposite directions, so the dial cannot simply be multiplied through.
What is kept consistent is the only thing that matters to a reader: below 1.00 always means "lasts
longer", whichever field is behind it.

`KeroseneLampItem` lives in `Il2CppTLD.Gear`, not `Il2Cpp`, and its fuel is an
`Il2CppTLD.IntBackedUnit.ItemLiquidVolume` - `Sweep.Litres` converts it the same way `Sweep.KG`
handles `ItemWeight`.

**Held items only, deliberately.** A lantern left burning on a table is a light source somebody put
somewhere on purpose; doubling its life is a different feature that nobody switched on.

Baselines are keyed by instance id in `FuelScale`, the same discipline as `Scale` and for the same
reason. None of these fields is serialised into a save - they come back from the game's own data on
the next launch - so a value left scaled by an item that never returns to the hand costs nothing.

---

## 2026-09-08 - feats, and fuel for things that were put down

Context, in his words: he is speeding through the game to reach a part that would otherwise take a
very long time to get to, so the cheats page is a means to an end rather than the point.

### "All the positive effects" means Feats

The Long Dark calls them Feats: thirteen permanent perks - Book Smarts, Cold Fusion, Efficient
Machine, Fire Master, Free Runner, Snow Walker, Expert Trapper, Straight To Heart, Blizzard Walker,
Night Walker, Master Hunter, Settled Mind, Celestial Navigator. The game keeps every instance in the
static `FeatsManager.m_Feats` and the per-run enabled list in
`FeatEnabledTracker.m_FeatsEnabledThisSandbox`.

One switch each, **generated by walking the `FeatType` enum** rather than by typing thirteen names,
so a feat added in a future update appears on its own. `Feat.SetNormalizedProgress(1f)` unlocks;
adding the type to the tracker enables it for the run. Progress is written every sweep, not once,
because the game recomputes it as it is earned and would walk an unlocked feat back down.

**These are the only switches on the page that reach the save file**, because a feat is a permanent
unlock with save data of its own. So "reversible" here cannot mean "stops applying after a restart"
- it has to mean actively putting back what was found. Each one stores the progress it had and
whether it was already in the enabled list, and turning the switch off writes both back. The tooltip
says so.

If a switch is on and `FeatsManager` is holding nothing, that is said out loud once. An empty list
with the switches showing on is precisely the silent no-op this mod exists to refuse.

### The fuel dial now covers placed items, and reaches infinite

`Cheats.PlacedFuel` asks the scene for every `KeroseneLampItem`, `TorchItem`, `FlareItem` and
`FlashlightItem` once a second - not every frame, because none of them changes that fast and it is a
scene-wide lookup. `Interface.FuelIncludesPlaced` turns it off for held-only behaviour.

The bottom of the slider (0.06 or less) is not "very slow", it is **infinite**: lamp burn goes to
zero litres an hour and the lifetimes go to a large constant rather than to infinity, because a NaN
or an infinity inside a progress bar is a crash waiting to happen. `FuelHold` records the real value
before forcing, exactly as `FuelScale` does, so the way back is still there.

---

## 2026-09-08 - feats were the wrong "positive effects", and the right ones are the timed buffs

Feats are achievement-style permanent unlocks, and unlocking them would spoil the game rather than
speed it up. The switches were never used - the log carries **zero** feat lines, and the code only
writes when a switch is on, all of which default to false - so no save was touched. The whole
section is removed rather than left on the page: it was the only thing on it that reached a save
file, and a stray click on a switch nobody wants is not a risk worth carrying. It is in git history
if it is ever wanted.

What was actually meant is the **timed buffs**: Improved Rest, Warming Up, Reduced Fatigue, the
condition-over-time bonus, the pie bonus - the positive effects that arrive with a countdown. The
game keeps each as a pair of floats on `PlayerManager`:

    m_ConditionRestBuffHoursRemaining  / m_ConditionRestBuffHoursDuration    Improved Rest
    m_FreezingBuffHoursRemaining       / m_FreezingBuffHoursDuration         Warming Up
    m_FatigueBuffHoursRemaining        / m_FatigueBuffHoursDuration          Reduced Fatigue
    m_ConditionPerHourHoursRemaining   / m_ConditionPerHourHoursDuration     condition bonus
    m_PumpkinPieBuffHoursRemaining     / m_PumpkinPieBuffHoursDuration       pie bonus

`Cheats.BuffTimers` tops each one back up to its own duration - **but only if it is already above
zero**. A buff that is not running is left at zero, so the switch can never grant an effect that was
not earned. Stopping a countdown and handing out a buff are different things and only one was asked
for. Nothing needs restoring: a held timer is simply not decremented, and switching off resumes the
same clock.

Well Fed has no clock - it ends when the stomach empties - so it is held by re-asserting its state
flag, and only if it was already active.

## 2026-09-08 - the speed cheat wrote the right field and the wrong one was missing

"Player speed did not work", while the log said the cheat was applied. Both true. The first build
scaled `vp_FPSController.MotorAcceleration` only, which controls how quickly top speed is REACHED -
the top speed itself is `MotorVelocityMax`, and it was left alone at 0.0800. So the character
reached the same cap a fraction sooner and walked at exactly the same pace.

Both fields are scaled now. And because a number changing in a log is not evidence that anything
happened, `Cheats.Measure` samples the player's horizontal displacement and prints the fastest
ground speed it has seen every thirty seconds while the dial is off neutral, with the unmodified
figures (about 1.4 m/s walking, 3.5 sprinting) beside it for comparison.

## 2026-09-08 - the window remembers where it was put

`Interface.WindowX` / `WindowY`, written on mouse-up rather than during the drag - one write when it
is put down, not sixty a second while it is moving. Restored on the first open rather than at load,
because preferences are read before `Screen` has a size to clamp against, and a window restored onto
a monitor that is no longer there cannot be reached.

---

## 2026-09-08 - the ammo cheat stopped after two top-ups

"Ammo is still being consumed." The log had counted it, which is the only reason this took a minute
rather than an evening:

    [cheats] ... ammo(2 topups) ...        01:44
    [cheats] ... ammo(2 topups) ...        01:48

Two top-ups at the start and none in the four minutes of shooting after. No exceptions. So the code
was reaching its guard and returning, every frame.

The guard was `if (gun.m_RoundsInClip >= size) return;`. **The game does not spend a round by
decrementing that field** - it removes the round from `m_Clip`, the list that records what each
round in the clip actually is. So the count stayed full, the list emptied, and the guard blocked
every refill after the first fill.

Both are checked and both are written now, every frame, with no shortcut - two integer comparisons,
and it cannot be fooled by whichever of the two the game happens to use in a future patch. The first
three top-ups print the numbers they found, so the next time this misbehaves the log already says
which counter moved.

The lesson is the one this project keeps relearning: a counter that does not move is evidence, and
`ammo(2 topups)` sitting still through a firefight said exactly where the fault was.

---

## 2026-09-08 - no recoil

The kick lives on the shooter, not on the gun: `vp_FPSShooter.MotionPositionRecoil` and
`MotionRotationRecoil` are the shove the weapon gets when it fires, and `MotionDryFireRecoil` is the
one it gets on an empty chamber. Zeroing those three removes the recoil at its source, which means
the camera recoil spring that follows it has nothing to follow - no second switch on the camera is
needed, and no fighting between two systems.

Every weapon carries its own shooter, so the pass walks them all once a second rather than assuming
one exists, and each original vector is stored against its instance id and written back on the way
out. If the switch is on and no shooter exists yet - which is simply what a weaponless scene looks
like - it says so once and keeps looking.

**Aim sway from cold is deliberately untouched.** `vp_FPSWeapon` has its own shake system with a
`SetDisableAimShake` switch, but sway is not recoil and removing it was not what was asked for.
It is one line away if it is ever wanted.

Confirmed by testing this session: the fuel dial survives a reload, which is expected - the burn
rates are prefab data rebuilt from the game's own values on load, and the mod re-applies the dial
on the next sweep.

---

## 2026-09-08 - recoil was on the gun, not on the shooter

"Recoil is still happening even when I turned it on", and the log agreed the code had run:

    [cheats] ... noRecoil(1)

One shooter found and zeroed, and the rifle kicked anyway. So `vp_FPSShooter.MotionPositionRecoil`
and `MotionRotationRecoil` are not what this game uses - they are UFPS leftovers.

The recoil is four numbers on `GunItem`:

    m_PitchRecoilMin / m_PitchRecoilMax     the upward kick, randomised between the two
    m_YawRecoilMin   / m_YawRecoilMax       the sideways one

Every gun in the scene has them zeroed now, each original stored as a `Vector4` against its instance
id and written back on the way out. The shooter is still zeroed as well, because it costs nothing
and covers any weapon that does travel the UFPS path, and the report names both counts separately -
`noRecoil(guns=N shooters=M)` - so the next time one of them is the wrong number, the log says which.

### And a no-sway switch beside it

Sway is a third system again: the wobble while holding a sight, driven by fatigue, kept on `GunItem`
as `m_SwayValueZeroFatigue`, `m_SwayValueMaxFatigue` and `m_SwayIncreasePerSecond`. All three are
zeroed. The shake that comes from cold has the game's own switch,
`vp_FPSWeapon.SetDisableAimShake`, so that is used rather than a fourth invention of ours, and its
previous value is remembered.

Three systems, three switches, and the reason they are not one switch is that they fail
independently - this session proved it by removing one and watching the other two carry on.

**A note on tooling:** `python - <<EOF` inside a compound Bash command hung this session and swallowed
the write that should have gone here. Scripts go in a file written with the editor and are then run,
which is the standing rule on this machine for a reason.

---

## 2026-09-08 - three reports, three different halves of the same lesson

### Placed fuel: stop arguing with the burn rate, wind the clock instead

The rate was zeroed and a placed lantern kept draining. Zeroing a rate is an argument about what
the game ought to do next; refilling the tank is a fact about what it currently holds. So the
infinite position now also tops up the live resource - `m_CurrentFuelLiters` back to `m_MaxFuel`,
`m_ElapsedBurnMinutes` back to zero on torches and flares, the flashlight battery back to full - and
the placed pass runs four times a second instead of once while infinite, because a top-up is only as
good as its interval.

A measurement went in beside it, and it answered the question on the first launch:

    fuel pass: lamps/torches/flares/lights = 2/0/0/0, top-ups this session = 2,
    nearest lamp 1.000L of 1.000L, burn 0.000L/h

Two lamps found, tank full, burn rate zero. That line is worth more than any amount of reasoning
about whether the code "should" work.

### Sway and recoil were both already working - on the half that matters

His words, and they are the whole diagnosis: *"Sway is happening in animation, but not in where I am
pointing at."* Same for recoil - the shot goes straight, the weapon still kicks on screen.

So the gun-side numbers were right. What remained is `vp_FPSWeapon`, which is pure presentation:
look sway, strafe sway, fall and slope sway, the idle bob, the shake. Those are zeroed now too, and
`SetDisableAimSway` is called alongside the shake switch that was already there.

**The recoil animation is left alone, and that is a decision rather than an omission.** It is an
animation clip played on firing, not a number - the same clip that cycles the bolt and ejects the
case. Suppressing it would leave a rifle that fires without moving at all, which looks broken rather
than steady. The aim is unaffected either way.

### A curing speed dial

`EvolveItem.m_TimeToEvolveGameDays` is how long a hide, a gut, or anything else that becomes
something else takes to get there. The dial divides it, so **above 1.00 is faster** - the opposite
direction to the fuel dial, deliberately: fuel is named for how fast something drains, curing is
named for how fast a job finishes, and each reads correctly for what it is called. Each item keeps
its own original time and gets it back at 1.00.

---

## 2026-09-08 - the lag was mine, and the arithmetic was already in the log

"Something is making the game very laggy. Like, unplayable laggy."

Every cheat pass in `Cheats.cs` was asking the scene for its objects directly, with
`FindObjectsOfType`, and the fuel pass had just been raised to four times a second. Counted rather
than guessed: fuel wanted four types, recoil two, sway two, curing one, fires one, Well Fed one -
somewhere around a dozen full-scene type scans every second on a map with tens of thousands of
objects. Our own log had the multiplier sitting in it:

    fuel pass: ... top-ups this session = 10608

Ten thousand top-ups is 6.5 a second across two lamps, which is three and a half scans a second for
lamps alone.

**One shared cache now.** `Cheats.Scan<T>` refreshes each cast list on a ten second period and every
pass walks the cached list at whatever rate it likes, which costs nothing. Destroyed objects are
pruned on each walk, and a scene change drops every list. The one visible edge is that a lantern put
down is noticed by the next scan rather than instantly - the held item has its own per-frame path,
so the thing in hand is never late.

**And the cost is measured now**, in the report, because a performance fix that is not measured is a
hope:

    [scans] 55 scans, 386.7ms since the last report

Fifty-five scans a minute at about 7ms each. At the old rate of a dozen a second that same 7ms was
roughly 90ms of stall per second of play, which is what unplayable feels like from the inside.

## 2026-09-08 - the held item does not wear out

`CheatNoDegrade`: while it is on, whatever is in hand is held at full condition through the game's
own normalised setter, and topped back up the moment it is equipped. Written only when the condition
has actually slipped, so an item at full costs one comparison a frame.

Held only, for the same reason the fuel dial is: it answers "the rifle I am shooting keeps
degrading" without quietly repairing a pack full of clothing that nobody mentioned.

---

## 2026-09-08 - fifty-two screenshots on the desktop, and they were ours

"How come my desktop is full of screenshots?"

Counted first: **52 files**, named `screen_<guid>_hi.png`, about 10 MB each - **409 MB** - all created
between 00:18 and 02:33 on 2026-09-08, which is exactly the window of the previous night's testing.
None in the game folder, none in the save folder, so the desktop is where they were written.

The line that stated the cause was our own log against the file times:

    01:04:42.252   auto pickup off      (the mod's F9 toggle)
    01:04:42, 01:04:42, 01:04:43        (three screenshots)

The Long Dark binds its own high-resolution screenshot - `InputManager.TakeHighResolutionScreenshot`
- to a bare function key, and F9 was one of ours. Every press did both things, and the repeats are
the key being held for a moment.

**The fix is the class, not the key.** `Keys.NeedCtrl`, on by default, requires Ctrl with every mod
hotkey. Dodging F9 specifically would have left the next collision just as silent, and there was no
way to know which other function keys the game claims without finding out the same expensive way.
The save key's own Ctrl flag folds into it.

The 52 files were moved to `C:\Users\moham\Downloads\Claude\tld-stray-screenshots` rather than
deleted - they are screenshots of his own play and that is his call, not this mod's.

**This is the second time a hotkey collision has cost real time on this station** - the Green Hell
notes carry the first. Worth stating as a rule: a mod hotkey on a bare key is a bet that the game
does not want that key, and a mod cannot see the other side of that bet.

---

## 2026-09-08 - correcting the screenshot answer, and counting instead

The first answer given was that a hotkey collision on F9 caused all 52 screenshots. **That was wrong,
and it was wrong in the way this station has a rule about**: one coincidence was generalised into a
cause. The toggle logged ONCE all evening. One press cannot make fifty-two files, and the person
playing never pressed it deliberately.

A second claim was also wrong: `screen_` was found in `GameAssembly.dll`, but the matches were
`updateWhenOffscreen_`. A substring is not a string.

**What is actually established:**

- `_hi.png` IS a literal in the game's own metadata, alongside its screenshot pipeline messages
  ("Screenshot done, wait for saving", "Disabling rende... while in Screenshot mode") and an
  `InputManager.TakeHighResolutionScreenshot` entry point. The writer is the game.
- This mod contains no screenshot code whatsoever - `EncodeToPNG`, `ScreenCapture`,
  `CaptureScreenshot`, `WriteAllBytes`, "Screenshot": zero hits across every source file, and zero
  in the built DLL. Neither do the other three mods installed.
- What TRIGGERED fifty-two of them is unknown. The mod's Escape handling does open and close the
  game's pause menu, and one screenshot does sit on that second, but that is again one coincidence
  and it will not be treated as an answer.

**So it gets counted rather than explained.** `ShotWatch` lists the desktop every two seconds and,
when a new `screen_*.png` appears, writes a line saying what the mod last did and how long before -
including "this mod has done nothing at all this session, it is not us", which is the outcome that
would clear the mod entirely. It stops looking after a quiet hour and costs one directory listing.

`Diagnostics.WatchDesktopScreenshots` turns it off.

---

## 2026-09-08 - the screenshots, finally established

The cause, with every step now evidenced rather than inferred.

**The Long Dark binds three of its own screenshot keys, and all three write to the desktop by
design.** From the game's own community, found through Brave rather than by reading the binary:

    F8    debug screenshot
    F9    screenshot
    F10   HIGH RESOLUTION screenshot with the HUD hidden  ->  screen_<guid>_hi.png

There is no setting to redirect them. F12 is Steam's, and that one goes to Steam. Every one of the
52 files carried the `_hi` suffix, which names F10 exactly - and F10 was the mod's settings window
key, the key pressed most often in an evening of testing.

**The visible tell was there all along**: pressing F10 makes the game flick from ultrawide to a
different aspect for a fraction of a second and back. That is the high-resolution capture
re-rendering the frame at a larger size before writing the PNG.

**A modifier does not help, and that was tested rather than assumed.** The first fix required Ctrl
with every hotkey. With that build installed, a bare F10 press produced no settings window - the mod
correctly ignored it - and the game still took its screenshot. The game does not check modifiers.

**So the defaults moved off those keys entirely:**

    settings window   F10  ->  Insert
    outlines          F8   ->  Home
    auto pickup       F9   ->  End
    sweep the room    F7        (unchanged - not a screenshot key)
    report            F11       (unchanged - not a screenshot key)

`Keys.NeedCtrl` stays as an option but defaults OFF now: it was the wrong answer to this problem and
keeping it on would add friction for nothing. The existing `MelonPreferences.cfg` was rewritten too,
because changing a default does nothing to a config that already holds the old value.

**The rule this leaves behind:** before binding a hotkey in any game, find out what that game
already does with it. A mod cannot see the other side of that bet, and the cost here was 409 MB and
an hour of two people guessing.

---

## 2026-09-08 - the collision warning goes on the screen, not in a log

A log line is no use to somebody who is playing. The moment worth telling them about a bad hotkey is
the moment they press it, on the screen they are looking at.

So when `ShotWatch` sees a new `screen_*.png` within a second and a half of one of the mod's own
hotkeys, it now raises a banner across the top of the screen for fifteen seconds - fading over the
last three so it does not read as a flicker - naming the key and saying to rebind it in the Keys
tab. It draws whether or not the settings window is open, since the window is exactly what somebody
would not have open at that moment.

The Keys tab also carries a line for the rest of the session naming any key caught this way, so the
warning is still there when they go looking for it rather than only when it appeared.

This is the general shape of the answer to "a mod cannot read the game's key bindings": it cannot
know which keys are safe in advance, but it can notice the symptom in two seconds and say so where
it will be seen.

---

## 2026-09-08 - the carry cheat was half a cheat

Reported from play: the cap was at 500 kg, the pack held 66 kg, and the character was still being
told to drop something and still moving like a loaded mule.

Raising `Encumber.m_MaxCarryCapacity` bought the right to CARRY 500 kg and nothing else. The
encumbrance system does not work in fractions of the cap - it holds its own absolute weights, and
every one of them was left where it was:

    m_EncumberLowThreshold / Med / High     when it complains, and how loudly
    m_NoSprintCarryCapacity                 the weight at which sprinting stops
    m_NoWalkCarryCapacity                   the weight at which walking stops
    m_MaxCarryCapacityWhenExhausted         the tired cap

`GetEncumbranceSlowdownMultiplier` reads those same numbers, so the slowdown was real and not
imagined.

All five are now scaled by **the same ratio as the cap** rather than flattened to it. That keeps the
bands in proportion: a 500 kg cap moves the first complaint from about 30 kg to about 300 kg, and
the encumbrance system still exists and still means something. Flattening them would have deleted
encumbrance altogether, which is a different feature and nobody asked for it.

Originals are stored per `Encumber` instance and written back when the cheat goes off, same
discipline as the cap itself. The first capture logs all five of the game's own numbers, so the
scaling can be checked rather than believed.

---

## 2026-09-08 - the Cheats page is hidden by default, for release

His requirement, for a public upload: the Cheats page must be **off by default** and turned on
deliberately from the first page anybody sees.

That is a release decision rather than a taste one. The page exists because reaching a particular
part of the game to test something would otherwise cost hundreds of hours of replaying a game he has
already finished - a good reason for the person who built it, and a poor first impression for
somebody downloading a mod called Pickup Doctor. A wall of god switches, open on arrival, changes
what the mod appears to BE.

So `Interface.ShowCheatsPage` defaults to false, the tab is **appended when asked for and simply
absent otherwise** - the eight tabs before it never move either way, which is the same rule the tab
order has always followed - and the toggle sits at the top of the Items page, the first thing the
window shows.

Two details that matter:

- If the page is switched off while it is the page being looked at, the tab index would point past
  the end of the list. It steps back to Interface rather than throwing.
- **Hiding is not turning off.** Anything already running keeps running, and the Items page prints
  "Cheats currently on:" with the list whenever the page is visible and something is active. A
  switch that is on and invisible is exactly the silent state this mod refuses everywhere else.

## 2026-09-08 - three more, from play

**Fires now survive a night's sleep.** Stoves, outdoor fires, barbecues and fire drums were all out
by morning with perpetual fire on. Sleeping does not run a fire through eight hours of updates - it
fast-forwards and resolves the night in one call, so a pass that re-asserts a flag every few seconds
never gets a turn in the middle. The fuel clock itself is held now: `m_MaxOnTODSeconds` is set to a
year and `m_ElapsedOnTODSeconds` is wound back when it climbs past a tenth of that, so whatever the
fast-forward computes, it starts from a fire with a year left rather than four hours.

**Curing goes to near instant.** The dial's ceiling was 20, which turned a five day cure into six
hours - fast, and still a wait. It reaches 100 now, and the top of the dial is a named position
rather than a bigger number: at 99 or above the evolve time is set to a floor of 0.002 game days,
about two minutes. Not zero, deliberately, because a zero duration sits inside the game's own
progress arithmetic.

**Free repair, and it is the one Harmony patch in this mod.** Every other cheat writes a field on an
object it can reach, and that cannot work here: items in the pack are INACTIVE GameObjects, so
`FindObjectsOfType` returns the rifle in hand and nothing else - while the whole point is clicking an
item in the inventory. So the three questions the repair screen asks are patched at the source:
`GetNumMaterialsRequired`, `GetRequiredGearUnits` and `GetDurationMinutes` answer zero while the
switch is on and return the game's own answers when it is off. The game does the repairing; only the
price it quotes changes.
