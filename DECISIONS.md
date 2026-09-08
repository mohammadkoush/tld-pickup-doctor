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
