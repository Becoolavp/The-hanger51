# The Hanger 51

The Hanger 51 is a first-person aviation maintenance and flight game built in Unity. The project is aiming for a hands-on, Car Mechanic Simulator-style aircraft experience: inspect aircraft, remove and replace physical parts, preserve part condition, service systems, purchase/install equipment, and then fly the aircraft with those maintenance states affecting operation.

> **Project handoff rule:** This README is the persistent cross-chat briefing for the project. After meaningful development milestones, bug fixes, architecture changes, or workflow changes, update the **Current Development Handoff** section before ending the work session. A new ChatGPT development chat should read this README and inspect the latest commits on the active branch before making changes.

## Current Development Handoff

**Last updated:** 2026-11-24  
**Active development branch:** `agent/merlin-engine-assembly`  
**Primary scene:** `Assets/_Project/Scenes/FirstPersonMovementTest.unity`  
**Current focus:** playable/serviceable P-51D Mustang, aircraft maintenance interactions, world time/calendar, citizen schedules, day/night lighting, regional utilities/power demand, airport lighting, cockpit systems, Merlin engine service, and expanding aircraft systems.

### Latest verified milestone

The complete P-51 wheel removal/reinstallation loop is now working.

Verified player flow:

1. Remove the complete wheel assembly from its original landing-gear strut.
2. Pick up the complete tire-and-rim assembly.
3. Carry it back to the compatible highlighted axle.
4. Hold **E** to animate the wheel back onto the aircraft.
5. The installed tire and rim remain visible and installed after the temporary carried-wheel object is destroyed.

The disappearing-wheel bug was **not** caused by an incomplete wheel assembly. Diagnostics proved the carried wheel was:

- `IsCarried=True`
- `IsComplete=True`
- `CanInstall=True`
- correctly associated with its original wheel station

The actual install transaction initially wrote `tireInstalled=true` and `rimInstalled=true`, but re-enabling the permanent tire hierarchy also re-enabled its child `P51LandingGearServiceTarget`. Its `OnEnable -> UpdateInstallHighlight -> EnsureOriginAxleEmptyForCarriedWheel` path still saw the temporary source wheel as `CurrentCarried` and incorrectly "self-healed" the newly installed axle back to empty.

The fix transfers ownership before the visual handoff:

`validate carried wheel -> copy tire/rim condition -> mark tire/rim installed -> clear CurrentCarried ownership -> re-enable/rebind permanent wheel visuals -> destroy temporary handoff wheel`

Key fix commits immediately before this README update:

- `f78eb330` — Release carried wheel ownership before install visual handoff.
- `2ad4481d` — Prevent wheel self-heal from undoing committed install.

### World calendar, day/night cycle, citizen schedules, and night lighting — IMPLEMENTED / PENDING UNITY VALIDATION

A new shared world-time architecture has been added on top of the existing `H51WorldClock`.

Editor setup:

`Hanger 51 -> World -> Current -> 152 - Add Calendar Day-Night Cycle And Night Lighting`

Validation:

`Hanger 51 -> World -> Current -> 153 - Validate Calendar Day-Night And Night Lighting`

The current implementation adds:

- Calendar-aware time with year/month/day/day-of-week.
- Player-readable clock/calendar HUD. Press **K** to expand/collapse the calendar details.
- Seasonal sunrise/sunset timing.
- Continuous sun/moon day-night lighting and ambient/sky/fog transitions.
- Weekday/weekend citizen routines.
- Rotating eight-hour shifts for essential workers such as power-plant operators and emergency-service occupations.
- Grid-powered town street lamps and building/house exterior night lights.
- Proximity-limited real point lights with emissive fixtures remaining visible farther away for performance.
- Hanger 51 airport/apron night lighting.
- Grid-powered runway fixtures with **OFF / AUTO / ON** modes.
- A physical runway-lighting control cabinet near the airport end of the runway; aim at it and press **E** to cycle the mode.
- Night-lighting and runway demand included in `H51UtilityGridController` load, so the regional power station's displayed grid demand increases when nighttime lighting is requested.

Important new files:

- `Assets/_Project/Scripts/NPC/H51WorldClock.cs`
- `Assets/_Project/Scripts/NPC/H51CitizenBrain.cs`
- `Assets/_Project/Scripts/World/H51DayNightCycle.cs`
- `Assets/_Project/Scripts/World/H51NightLight.cs`
- `Assets/_Project/Scripts/World/H51WorldTimeHUD.cs`
- `Assets/_Project/Scripts/World/H51RunwayLightingController.cs`
- `Assets/_Project/Scripts/World/H51RunwayLightSwitch.cs`
- `Assets/_Project/Scripts/World/H51FacilityLightingPlayerInteractor.cs`
- `Assets/_Project/Scripts/World/H51UtilityGridController.cs`
- `Assets/_Project/Editor/Hanger51TimeCalendarAndNightLightingSetup.cs`

The default configured calendar start is **June 1, 2026 at 6:30 AM**, with the existing `1.5 game minutes per real second` progression retained. The date is configuration data, not a hard-coded historical dependency, and can be changed later without rewriting citizen schedules.

Do not mark this milestone verified until Step 152 has been applied, Step 153 passes, and a Play-mode/build test confirms daylight progression, nighttime lighting, citizen schedule transitions, grid demand, and runway-light switching.

Compile note: the first pull exposed C# definite-assignment error CS0165 in `H51CitizenBrain.cs` for the medical-appointment local variable. This was fixed in commit `97356c9b` by initializing the appointment anchor before the short-circuited schedule check.

Compile note: the next pull exposed three parser errors on one malformed Step 152 completion-log line in `Hanger51TimeCalendarAndNightLightingSetup.cs`. All three came from a missing opening quotation mark and were fixed in commit `99550e07`.

Integration note: the first Step 152 scene-build run then failed while creating the runway-light control because `H51RunwayLightSwitch` has a `RequireComponent(Collider)` contract and the setup code attempted to add the switch before its `BoxCollider`. Unity rejected the switch component, the subsequent `Configure` call hit a null reference, and Step 152 stopped before the player clock HUD/interactor were added. This was fixed in commit `80e614aa` by creating/configuring the `BoxCollider` first, then adding the runway-light switch with a null guard. Re-running Step 152 is safe: it rebuilds the generated lighting root and reuses existing runway runtime glow children.

### Wheel-install diagnostics

A dedicated runtime recorder remains in the project:

`Assets/_Project/Scripts/Aircraft/P51WheelInstallDiagnostics.cs`

It records the wheel-install transaction, including:

- visual preview start/end
- carried/complete/compatible state
- tire/rim/gear state transitions
- permanent tire and rim hierarchy paths
- renderer/activity state
- transform positions and physics-proxy distances
- wheel-state reconciler changes
- snapshots immediately after install and over the following second

For Windows standalone builds, the easiest trace is written beside the game executable as:

`Hanger51_WheelInstallTrace.txt`

A second copy is also written under Unity's `Application.persistentDataPath`.

Keep this diagnostic system until the landing-gear/wheel workflow has had enough regression testing to justify removing or reducing it.

## Current Project State

The project is far beyond the original movement-test milestone. Major systems currently present include:

- First-person player movement and interaction.
- Inventory pickup/drop/equip workflows.
- P-51D Mustang aircraft interaction and flight systems.
- Merlin engine assembly/service workflows.
- Physical aircraft service targets and maintenance animations.
- Retractable/serviceable landing gear using raycast-supported ground physics.
- Complete removable wheel assemblies with tire/rim condition and pressure preservation.
- Tire wear, pressure, nitrogen servicing, failure behavior, and replacement workflows.
- Wheel-install highlights and animated reinstall feedback.
- Live-master P-51 spawning so newly spawned aircraft inherit current master-aircraft features.
- Landing-gear service attachment followers so service targets travel with moving/retracting gear.
- Cockpit gauges and current cockpit usability repairs.
- Wing armament service systems, including gun/ammo installation work already present in the branch.
- Aircraft/engine condition systems intended to support Car Mechanic Simulator-style part wear and replacement.

## Important Recent Fixes

### Complete wheel reinstall race condition — RESOLVED

The installed wheel used to animate onto the axle and then disappear. The authoritative state was being reset during the visual handoff because the source wheel remained globally marked as carried while the permanent tire hierarchy re-enabled its service target.

The install transaction now calls `P51LooseWheelAssembly.BeginAircraftInstallationHandoff()` before restoring the permanent wheel hierarchy. This clears `CurrentCarried` without destroying the temporary wheel immediately, allowing a clean visual handoff.

Relevant files:

- `Assets/_Project/Scripts/Aircraft/P51LooseWheelAssembly.cs`
- `Assets/_Project/Scripts/Aircraft/P51LandingGearInventoryBridge.cs`
- `Assets/_Project/Scripts/Aircraft/P51LandingGearServiceTarget.cs`
- `Assets/_Project/Scripts/Aircraft/P51WheelInstallStateReconciler.cs`
- `Assets/_Project/Scripts/Aircraft/P51LandingGearMaintenanceController.cs`
- `Assets/_Project/Scripts/Aircraft/P51WheelInstallationVisualFeedback.cs`
- `Assets/_Project/Scripts/Aircraft/P51WheelInstallDiagnostics.cs`

### Spawned-aircraft wheel initialization — REPAIRED

Live-spawned P-51s are created after the normal scene-load bootstrap has already run. Wheel visual/runtime configuration is now explicitly applied to spawned aircraft.

The spawn reset also restores both tire and rim state together. Previously a clone could receive `tireInstalled=true` while inheriting stale `rimInstalled=false`, which the wheel-state reconciler correctly interpreted as an impossible/incomplete wheel.

Relevant files:

- `Assets/_Project/Scripts/Commerce/HangarAircraftSpawnConsole.cs`
- `Assets/_Project/Scripts/Aircraft/P51LandingGearVisualRuntimeBootstrap.cs`
- `Assets/_Project/Scripts/Aircraft/P51LandingGearMaintenanceController.cs`

### Particle velocity curves console error — RESOLVED

The previous `Particle Velocity Curves Must All Be in the Same Mode` error has been eliminated. Do not reopen or rewrite that system without a new reproduced failure.

### Cockpit gauges

The recent cockpit repair work restored/improved the P-51 gauges and added validation under:

`Hanger 51 -> P-51 Mustang -> Current -> Repair Cockpit Gauges And Wheel Reinstall`

and:

`Hanger 51 -> P-51 Mustang -> Current -> Validate Cockpit Gauges And Wheel Reinstall`

## Important Architecture Notes

### Live master aircraft

The hangar spawn console is intended to use the live master P-51 as its aircraft source so current aircraft features propagate into newly spawned aircraft instead of relying on an old frozen prefab/snapshot.

Relevant system:

`Assets/_Project/Scripts/Commerce/HangarAircraftSpawnConsole.cs`

### Landing-gear state ownership

Do not treat visual state as authoritative.

Authoritative wheel state is split between:

- `P51LandingGearMaintenanceController` for tire/gear state, tire condition, pressure, failure, and gear deployment.
- `P51LandingGearInventoryBridge` for rim installation/condition and complete-wheel inventory/service coordination.
- `P51LooseWheelAssembly` for a removed complete wheel and its preserved tire/rim condition while off-aircraft.

A complete installed wheel requires the tire and rim states to agree. The `P51WheelInstallStateReconciler` exists to normalize impossible legacy disagreement, but it must not be allowed to interfere with a valid in-progress install transaction.

### Visual handoff rule

When installing a carried physical part, especially the complete wheel:

1. Validate the physical carried object.
2. Capture/preserve its condition.
3. Commit authoritative installed state.
4. Transfer/release temporary carry ownership.
5. Activate/rebind permanent aircraft visuals.
6. Destroy the temporary carried/handoff object last.

Do not reactivate service-target hierarchies while a source object is still globally marked as carried if those targets contain self-healing or origin-state logic.

## Development Workflow

- Unity project is developed primarily through GitHub feature branches.
- Current active work is on `agent/merlin-engine-assembly`, not `main`.
- Before changing code in a new chat:
  1. Read this README.
  2. Confirm GitHub access.
  3. Inspect the active branch HEAD and recent commits.
  4. Inspect the exact source files involved before modifying them.
  5. Preserve working systems unless a regression is reproduced.
- Keep changes small enough to test in Unity.
- Use runtime diagnostics/logging when a bug cannot be proven from code inspection.
- Do not repeatedly layer visual workarounds over unknown state bugs; instrument the authoritative state transition first.
- After a meaningful verified milestone, update this README so the next chat has an accurate handoff.

## Development Baseline

- Unity 6.x
- PC/Windows first
- Universal Render Pipeline
- Unity Input System
- GitHub feature-branch workflow
- Development builds are commonly used for runtime testing and diagnostics

## Near-Term Priorities

Continue expanding the P-51 as a deeply serviceable aircraft while protecting currently verified workflows. In particular:

- Regression-test complete wheel removal/reinstallation on left main, right main, and tailwheel.
- Regression-test the same wheel flow on newly spawned P-51 aircraft.
- Continue improving maintenance animations and physical part visibility.
- Continue the Car Mechanic Simulator-style replaceable-part/wear/condition architecture.
- Keep live-spawn inheritance synchronized with the master aircraft.
- Apply and validate World Steps 152/153, then test at least dawn, daytime, dusk, and full night.
- Verify ordinary citizens stop following weekday work routines on weekends.
- Verify power-plant and emergency workers maintain rotating overnight coverage.
- Verify town/building lights turn on at night only when the utility grid is powered.
- Verify runway OFF/AUTO/ON behavior and loss of runway lighting during a grid outage.
- Continue expanding aircraft systems, cockpit systems, engine maintenance, armament service, and flight behavior without regressing existing maintenance interactions.

## Repository History Note

The original README described the repository as a basic first-person movement prototype. That milestone has long since been surpassed. The current project is now a substantially larger aviation maintenance/flight prototype centered on the serviceable P-51 and its associated aircraft, engine, inventory, condition, commerce, and flight systems.
