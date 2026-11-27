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

### World calendar, day/night cycle, citizen schedules, and night lighting — INITIAL PLAY-MODE VISUAL TEST PASSED

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

The initial Unity integration and Play-mode visual test now show the world clock/date HUD and active day/night lighting in the standalone Development Build. The user confirmed the result looks good. Keep deeper behavioral verification open for citizen weekday/weekend transitions, night power-load behavior, full dusk/night progression, and runway OFF/AUTO/ON outage behavior.

Compile note: the first pull exposed C# definite-assignment error CS0165 in `H51CitizenBrain.cs` for the medical-appointment local variable. This was fixed in commit `97356c9b` by initializing the appointment anchor before the short-circuited schedule check.

Compile note: the next pull exposed three parser errors on one malformed Step 152 completion-log line in `Hanger51TimeCalendarAndNightLightingSetup.cs`. All three came from a missing opening quotation mark and were fixed in commit `99550e07`.

Integration note: the first Step 152 scene-build run then failed while creating the runway-light control because `H51RunwayLightSwitch` has a `RequireComponent(Collider)` contract and the setup code attempted to add the switch before its `BoxCollider`. Unity rejected the switch component, the subsequent `Configure` call hit a null reference, and Step 152 stopped before the player clock HUD/interactor were added. This was fixed in commit `80e614aa` by creating/configuring the `BoxCollider` first, then adding the runway-light switch with a null guard. Re-running Step 152 is safe: it rebuilds the generated lighting root and reuses existing runway runtime glow children.

### Farm residency, house-door, and airport runway-control cleanup — IMPLEMENTED / PENDING UNITY VALIDATION

The current world screenshots exposed three follow-up issues after the time/night pass:

- The runway-light control cabinet was outside the airport perimeter wall.
- Working farmsteads had physical farmhouses but farmers still inherited town-home assignments.
- Some house doors were unreliable and legacy door handles/knobs could float away from the moving door leaf.

A new repair pass was added:

`Hanger 51 -> World -> Current -> 154 - Repair House Doors Farm Residents And Airport Light Control`

Validation:

`Hanger 51 -> World -> Current -> 155 - Validate House Doors Farm Residents And Airport Light Control`

Step 154 does the following:

- Moves the existing runway-light control to the **inside face of the nearest airport perimeter wall**, oriented so its readable/control face points into the airport.
- Updates the Step 152 builder so future rebuilds also place the control inside the wall.
- Rebuilds every enterable home front door around a deterministic hinge pivot instead of reverse-engineering the old permanently-open door slab.
- Uses a unit-scale door-leaf hierarchy with a dedicated collider and separate scaled visual mesh.
- Parents inside/outside handles directly to the unit-scale door leaf so the handles remain physically attached throughout opening/closing.
- Doors start closed, open with **E**, and still auto-open for nearby citizens through `H51TownDoor`.
- Creates a rural farmhouse home anchor and working-farm work anchor for each generated `Working Farm XX`.
- Adds a local NavMesh volume for each farmstead.
- Creates/repairs a `H51CitizenWorkplace` on each working farm.
- Reassigns one non-essential existing citizen per farmstead as a **Farmer**, with that farm's farmhouse as home and that same farm as the work location.
- Moves the assigned farmer onto the farm NavMesh so the schedule system begins from the actual farm residence.

Important files:

- `Assets/_Project/Editor/Hanger51FarmResidenceDoorAndAirportControlRepair.cs`
- `Assets/_Project/Editor/Hanger51TimeCalendarAndNightLightingSetup.cs`
- `Assets/_Project/Scripts/NPC/H51TownDoor.cs`
- `Assets/_Project/Scripts/NPC/H51CitizenIdentity.cs`
- `Assets/_Project/Scripts/NPC/H51CitizenAnchor.cs`
- `Assets/_Project/Scripts/NPC/H51CitizenWorkplace.cs`

Unity Step 155 validation results from 2026-11-24:

- Interactive home doors: **100/100 passed**
- Detached/missing handles: **0**
- Working farmsteads: **16**
- Farm-resident farmers: **16**
- Bad farmer assignments: **0**
- Farm NavMeshes: **16/16**
- Runway control inside airport wall: **failed**

The failure was isolated to runway-control placement. The original placement selected a wall using the control's prior exterior location, which could preserve the wrong face. Commits `2bf19518` and `e5f31968` changed both Step 154 and the original Step 152 builder to choose the perimeter wall nearest the hangar/interior reference, project onto that wall's inner face, and offset the cabinet inward toward the hangar. Re-run Step 154 and Step 155 after pulling those commits.

Do not mark this cleanup fully verified until Step 155 passes and Play mode confirms at least one town-house door opens/closes with its handles attached, at least one farmer returns to the rural farmhouse/work location as the clock changes, and the runway control is reachable from the inside of the airport wall.

### Night visibility, runway control readability, visible moon, and wing-armament restoration — IMPLEMENTED / PENDING UNITY VALIDATION

The first extended Play-mode/night-flight test after Steps 152-155 exposed four important presentation/state issues:

- The runway-light control text was readable from the wrong side, appeared mirrored from the airport side, and could be seen through the wall.
- Runway bulbs were too dim/small and the real point lights were distance-limited too aggressively for an aircraft game, so the runway was not visually defined from the air.
- Town road/street lights and house/building lights existed but were not visually strong enough from flight altitude/range.
- The night cycle had moonlight but no visible moon body.
- Opening the P-51 wing armament panels no longer revealed the installed M2 guns and ammunition boxes even though the armament geometry still existed in the hierarchy.

The repair/validation path is:

`Hanger 51 -> World -> Current -> 156 - Repair Night Visibility Moon Runway And Wing Armament`

`Hanger 51 -> World -> Current -> 157 - Validate Night Visibility Moon Runway And Wing Armament`

Step 156 currently does the following:

- Corrects the runway-control TextMesh to face the airport interior.
- Runtime-hides the runway label when the camera is on the exterior/wall-back side so it cannot appear mirrored or render through the wall.
- Enlarges all runway-light lens spheres and gives every runway lens its own no-shadow point light.
- Raises runway emission strength substantially and extends real-light visibility to flight-relevant range.
- Strengthens town street lamps, airport/apron lights, and building/house exterior lights; increases their lens size and flight-relevant real-light distance while retaining grid-power/night scheduling.
- Adds an actual visible moon sphere/disc that follows the moon directional-light path across the sky and stays camera-relative at long range.
- Stores the moon material as a real asset so future Step 152 rebuilds keep the visible moon.
- Adds runtime armament hierarchy self-repair to `P51WingArmamentSystem` so stale serialized bay/gun/ammo visual references can be rediscovered from the live aircraft.
- Restores the master/current P-51 armament systems to the intended fully-equipped starting configuration: six installed M2 wing guns, six installed ammo boxes, and ammunition present.
- Reconfigures `P51WingArmamentBayPersistence` so the bay geometry remains available when panels are opened and the closed wing skin physically conceals it when panels are shut.
- Backports the stronger runway/town lighting, inward-facing runway-control label, and visible moon into the original Step 152 builder so future world regeneration does not recreate the old weak version.

Important files:

- `Assets/_Project/Editor/Hanger51NightVisibilityAndArmamentRepair.cs`
- `Assets/_Project/Editor/Hanger51TimeCalendarAndNightLightingSetup.cs`
- `Assets/_Project/Scripts/World/H51RunwayLightSwitch.cs`
- `Assets/_Project/Scripts/World/H51RunwayLightingController.cs`
- `Assets/_Project/Scripts/World/H51NightLight.cs`
- `Assets/_Project/Scripts/World/H51DayNightCycle.cs`
- `Assets/_Project/Scripts/Aircraft/P51WingArmamentSystem.cs`
- `Assets/_Project/Scripts/Aircraft/P51WingArmamentBayPersistence.cs`

Compile note after the strengthened Step 156/157 pass: the first pull exposed five C# scope errors in `Hanger51NightVisibilityAndArmamentRepair.cs`. The new validation variables `plant` and `grid` had been inserted into the Build method as duplicate locals, while the Validate method referenced them without declarations. Commit `762b36b5` removes the duplicate Build declarations and declares `plant`/`grid` in the correct Validate scope. The surrounding edited block was then rechecked for unmatched string quotes.

Development-build visual retest on 2026-11-24 found that this pass was **not yet visually verified**:

- At about 10:19 PM, the town was dark and the road/house lamps were not visibly illuminating the scene from the P-51.
- Opening the right wing armament panel still showed the intact wing surface even though the interaction prompt reported an installed gun with ammunition. This proved the authoritative armament state/service collider was correct but the installed visual presentation was still occluded.
- The underlying causes addressed in the next correction were:
  - exterior lights still depended on a grid state that could remain false/stale if the saved power plant was shut down or the grid had not yet rebound/refreshed;
  - the point-light output was still too weak for flight-altitude viewing;
  - the P-51 wing is not physically cut open, so the bay/interior visuals could remain underneath the intact wing skin even while the access panel was raised.

The same Step 156/157 repair path has now been strengthened rather than adding another duplicate setup pass:

- `H51PowerPlantController.RestoreHealthyOperationalState()` restores a healthy saved generating-station state for the current development world.
- Step 156 rebinds the utility grid to that plant and calls `RestoreAndRefreshGrid()`.
- `H51UtilityGridController.PowerAvailable` lets exterior/runway lights see healthy generation immediately instead of waiting on a stale `GridOnline` flag, while still going dark for a genuinely offline/insufficient plant.
- Grid startup now resolves the plant before its first refresh.
- Street lamps are now tuned to roughly 800 m activation distance, 45 m light range, 12 intensity, larger visible lenses, and much stronger emission.
- House/building exterior lights are now tuned to roughly 500 m activation distance, 28 m range, 7 intensity, and larger emissive lenses.
- Airport apron lights and runway lights receive similarly stronger flight-relevant settings; every runway lens retains a real no-shadow light.
- `P51WingArmamentSystem` now captures the closed bay/gun/ammo poses and, while a wing panel opens, raises the entire armament-bay presentation about 0.14 m above the uncut wing skin, then additionally lifts the gun and ammo visuals slightly for service visibility.
- Installed gun/ammo child renderers are explicitly re-enabled and `forceRenderingOff` is cleared.
- When the panel closes, the bay presentation returns to its original contained position, preserving the normal closed-wing appearance.
- Step 156 explicitly serializes these lift values onto existing P-51 armament systems.
- Step 157 now verifies the saved power plant is healthy and each P-51 armament system has the new service-visibility lift values in addition to the six installed guns/ammo boxes.

Additional armament-service bug found after the bay visuals were restored: removing a full ammunition box caused the corresponding gun barrel to glow red-hot. The heat controller estimated firing by comparing the previous ammo count to the current ammo count, so service removal from roughly 200 rounds to 0 looked like 200 rounds fired in one frame. Commit `9dd51ddf` fixes this by only treating downward ammo deltas as firing while the ammo box is still installed; ammo-box removal now resets the heat baseline and lets any real residual heat cool normally.

Visible-moon runtime bug found after night testing: the moon sphere existed, but `H51DayNightCycle.ApplyMoonVisual()` placed it along `moon.transform.forward`. For a Unity directional light, forward is the direction the light rays travel toward the world, so the visible celestial body must be placed in the opposite direction. Commit `2d4d5954` changes the moon visual direction to `-moon.transform.forward`, which also makes the existing above-horizon visibility test evaluate the actual moon position rather than the ray direction. This is a runtime code fix; no new editor setup pass is required once the moon visual has already been created by Step 156.

Do not mark this pass verified until the strengthened Step 157 passes and a Development Build confirms: runway edges are clearly visible at night from the P-51, road/house lighting is visible when flying over populated areas, the visible moon crosses the night sky, the runway-control text is readable only from inside the airport wall, and opening either wing armament panel reveals all three installed guns and three adjacent ammo boxes for that wing.

### Night-sky art, visibly lit lamp bulbs, and civilian overnight curfew — IMPLEMENTED / PENDING UNITY VALIDATION

Compile note: the next pull of Step 158 exposed a cascade of parser errors near the path helpers in `Hanger51NightImmersionAndCitizenCurfewSetup.cs`. The root cause was two malformed backslash character literals in `ToAssetPath()`: the source contained `'\'` instead of the valid escaped C# backslash literal `'\\'`. Commit `fb482a80` fixes both lines. The entire Step 158 file was then rescanned for malformed backslash char literals and unmatched string quotes; none remain.


Compile note: the first pull of the Step 158/159 night-immersion pass exposed `CS0102` because `H51DayNightCycle` already had the original `nightSkyExposure` field for dimming the normal daytime skybox at night, while the new dedicated HDRI/procedural night-sky path accidentally introduced a second field with the same name. Commit `d1529d2c` separates these concerns into `nightSkyExposure` (legacy/day-sky dimming) and `dedicatedNightSkyExposure` (the star-sky/HDRI material exposure).


A later 11:10 PM Development Build showed that the exterior point lights were finally casting some illumination onto the ground, but the lamp bulbs themselves still looked like dull brown geometry. The visible moon also still did not appear in the sky.

A new focused setup/validation pass was added:

`Hanger 51 -> World -> Current -> 158 - Install Starry Night Sky Lamp Glow And Citizen Curfew`

`Hanger 51 -> World -> Current -> 159 - Validate Starry Night Sky Lamp Glow And Citizen Curfew`

The internet research for this pass selected **ambientCG Night Sky HDRI 001** as the preferred external night panorama. ambientCG publishes the asset under **CC0**, including commercial use without attribution. Step 158 attempts to download the **2K** package directly from ambientCG into the generated project environment folder. If the download is unavailable, the repository now includes `Assets/_Project/Shaders/H51ProceduralNightSky.shader` as a deterministic procedural star/Milky-Way fallback.

Source/reference:

- Asset page: `https://ambientcg.com/view?id=NightSkyHDRI001`
- 2K download endpoint: `https://ambientcg.com/get?file=NightSkyHDRI001_2K.zip`
- License: CC0

Step 158 also:

- Creates/configures a panoramic Unity skybox material when the ambientCG HDRI downloads successfully.
- Falls back automatically to the repository's procedural starry-night shader when external download/import is unavailable.
- Configures `H51DayNightCycle` to switch to the dedicated night sky when the night factor is established.
- Reconfigures the visible moon to a nearer nominal distance and changes moon placement to respect the active camera's far clip plane while preserving apparent angular size. This addresses a likely remaining cause of the moon being absent even after the directional-light sign fix: the former 1800 m moon could be beyond the player camera's far clip distance.
- Explicitly forces moon renderers enabled / `forceRenderingOff=false`.
- Replaces town, building, airport, and runway lamp-lens materials with bright **unlit** materials so a powered fixture visibly looks illuminated even when Bloom is unavailable. Real point lights continue to provide actual ground illumination.
- Adds a hard **9:30 PM civilian curfew** for non-essential citizens and a 6:30 AM morning release. This home rule takes priority over shopping, leisure, and ordinary appointments. Essential rotating-shift workers remain exempt while actually scheduled for work.
- Serializes the curfew values onto existing citizen brains.
- Writes a local `SOURCE_LICENSE.txt` next to the downloaded/generated night-sky assets documenting the source and CC0 license.

Runtime files changed:

- `Assets/_Project/Scripts/NPC/H51CitizenBrain.cs`
- `Assets/_Project/Scripts/World/H51DayNightCycle.cs`
- `Assets/_Project/Scripts/World/H51NightLight.cs`
- `Assets/_Project/Shaders/H51ProceduralNightSky.shader`
- `Assets/_Project/Editor/Hanger51NightImmersionAndCitizenCurfewSetup.cs`

Do not mark this pass verified until Step 159 passes and a Development Build confirms: lamp bulbs themselves visibly glow at night, non-essential citizens route home after 9:30 PM, the sky contains stars instead of a flat black background, and the moon is visibly rendered above the horizon during its nighttime arc.

### Smooth twilight sky blending and overnight citizen recovery — IMPLEMENTED / PENDING UNITY VALIDATION

The next Development Build visually confirmed that the ambientCG night sky is working and looks substantially better than the previous black sky. It also exposed two follow-up issues:

- The sky still transitioned too abruptly because `H51DayNightCycle` was swapping between the normal day skybox and the dedicated night sky once a threshold was crossed.
- At roughly 4:40-5:00 AM on Saturday, two citizens were seen standing inside the regional hospital while one reported an at-home activity. This was traced to citizen update ordering: an already-active activity could keep running before the routine scheduler got a chance to react to curfew/shift changes. A genuinely stalled NavMesh route could also leave a citizen away from home overnight.

The existing Step 158/159 workflow has been strengthened rather than adding another duplicate numbered pass.

Sky changes:

- Added `Assets/_Project/Shaders/H51BlendedDayNightSky.shader`.
- The preferred ambientCG Night Sky HDRI 001 panorama is now sampled by a **single blended day/night skybox material** rather than being swapped in abruptly.
- The same shader generates the daytime zenith/horizon gradient, a twilight color band, and a sun disc.
- `H51DayNightCycle` drives a continuous `_NightBlend` value from the existing daylight/night factors, allowing stars/HDRI detail to fade in through dusk and fade back out through dawn.
- If an older sky material is encountered, the old threshold-switch behavior remains as a compatibility fallback.
- Step 159 now verifies that `H51_NightSky.mat` uses `Hanger51/Skybox/Blended Day Night` and exposes the `_NightBlend` control.

Citizen changes:

- `H51CitizenBrain` now treats doctor, physician, surgeon, nurse, hospital, and medical occupations as essential rotating-shift roles in addition to the existing emergency/power roles.
- A curfew or shift change can interrupt an existing activity immediately when that activity belongs to the wrong destination. A citizen can no longer remain at a hospital/leisure activity solely because the activity timer has not expired yet.
- Non-essential citizens still use the 9:30 PM-6:30 AM hard home period.
- Off-shift essential workers still route home under the existing overnight-home rule.
- Overnight citizens track actual NavMesh movement toward home. If they remain away from home and make essentially no progress for about 12 real seconds, the system samples the home NavMesh and recovers them there rather than leaving them stranded indefinitely.
- The dialogue state therefore stays aligned with the citizen's actual scheduled destination instead of saying an NPC is "at home" while a stale hospital activity remains active.
- Step 158 explicitly serializes the 12-second overnight recovery window onto existing citizen brains, and Step 159 validates it.

Important files:

- `Assets/_Project/Shaders/H51BlendedDayNightSky.shader`
- `Assets/_Project/Scripts/World/H51DayNightCycle.cs`
- `Assets/_Project/Scripts/NPC/H51CitizenBrain.cs`
- `Assets/_Project/Editor/Hanger51NightImmersionAndCitizenCurfewSetup.cs`

Re-run:

`Hanger 51 -> World -> Current -> 158 - Install Starry Night Sky Lamp Glow And Citizen Curfew`

then:

`Hanger 51 -> World -> Current -> 159 - Validate Starry Night Sky Lamp Glow And Citizen Curfew`

Do not mark this refinement fully verified until a Development Build confirms a gradual star-sky fade across dusk/dawn and verifies that overnight civilians are actually at their homes while legitimate on-shift hospital staff remain at the hospital.

### Runway sign occlusion, obsolete fixed gun stubs, radiator leftover, and spawned-aircraft tow-bar targeting — IMPLEMENTED / PENDING UNITY VALIDATION

A later Development Build exposed three independent cleanup issues:

- The runway-light controller's TextMesh could still be visible through the airport/hangar wall.
- Six obsolete fixed gun cylinders from the original P-51 visual builder were still present in addition to the real serviceable M2 system. The exact legacy names are `Left Wing Gun 1-3` and `Right Wing Gun 1-3`.
- The portable tow bar remained bound to the first `P51FlightController` it found. With a spawned P-51 nearby, the interaction could still measure the distance to the original/master aircraft tailwheel (for example reporting roughly 19 m even while the tow-bar fork was beside the spawned tailwheel).
- The older obsolete radiator transition object `Radiator Scoop Transition Fairing` is also removed by this cleanup because it is known to overlap the newer functional radiator/belly geometry if it survives an older build pass.

A new repair/validation path was added:

`Hanger 51 -> World -> Current -> 160 - Repair Runway Sign Legacy Guns And Spawned Tow Bar`

`Hanger 51 -> World -> Current -> 161 - Validate Runway Sign Legacy Guns And Spawned Tow Bar`

Runtime changes:

- `P51TowBarController` now resolves the **nearest active P-51 tailwheel** whenever the tow bar is loose. It updates the flight controller, landing-gear controller, rigidbody, and tailwheel anchor together and locks those references once attached.
- The tow-bar interaction text also refreshes nearest-aircraft targeting, so distance prompts should refer to the airplane physically nearest the fork instead of the original master.
- `H51RunwayLightSwitch` now requires three conditions before showing its TextMesh: the player must be on the readable side, within the configured nearby range, and have a clear physics line of sight to the label. A wall between the camera and the switch therefore hides the sign even if the old facing test would have shown it.
- `P51WingArmamentSystem` defensively disables the six exact obsolete fixed-gun objects at runtime, so cloned/spawned aircraft cannot visibly resurrect those placeholders.

Step 160 permanently cleans the saved scene by:

- destroying all six exact legacy fixed-gun objects on every scene P-51;
- destroying any remaining `Radiator Scoop Transition Fairing` objects;
- setting the runway-light label viewing distance to 8 m;
- preserving the real serviceable `Installed M2 Wing Gun` visuals and the existing tow-bar object.

Step 161 verifies:

- zero obsolete fixed wing-gun stubs remain;
- zero obsolete radiator transition fairings remain;
- at least six real `Installed M2 Wing Gun` visuals still exist;
- the runway sign nearby/occlusion setting is present;
- the tow bar remains configured for runtime nearest-aircraft targeting.

Important files:

- `Assets/_Project/Scripts/Aircraft/P51TowBarController.cs`
- `Assets/_Project/Scripts/Aircraft/P51WingArmamentSystem.cs`
- `Assets/_Project/Scripts/World/H51RunwayLightSwitch.cs`
- `Assets/_Project/Editor/Hanger51RunwaySignLegacyGunAndTowBarRepair.cs`

Do not mark this pass verified until Step 161 passes and a Development Build confirms that the runway-light sign cannot be seen through the wall, no old fixed gun cylinders remain outside the real serviceable M2 system, the obsolete radiator transition fairing is absent, and the tow bar can connect to a newly spawned P-51 rather than only the master aircraft.

### BoZo citizen visual migration — IMPLEMENTED / PENDING UNITY VALIDATION

Development Build follow-up: the first BoZo migration rendered the new citizens as solid black silhouettes, and some citizens produced a huge black spike/wedge extending from the face/head.

Root cause and correction:

- Hanger 51 had configured BoZo with `cleanAfterMerge = true`.
- In BoZo's own load sequence, `MergeCharacter()` runs before the final material/texture pass. With `cleanAfterMerge` enabled, the modular outfit sources are removed during that merge.
- BoZo then performs a final `MergeTextures()` pass. Because the source outfits are already gone, the atlas is cleared but has nothing left to composite, producing the solid-black character textures seen in the player build.
- Hanger 51 now forces `cleanAfterMerge = false` both in Step 162 and in `H51BoZoCitizenVisual`, so BoZo retains the modular source data until its full runtime load/texture-bake sequence is complete.
- The BoZo Anime material's toon outline is disabled for Hanger 51 NPCs, and the runtime adapter also removes `_USEOUTLINE` / sets outline distance to zero after the character finishes loading. The extreme black face/head spikes were consistent with the outline pass exaggerating a malformed/skinned vertex, and Hanger 51 does not need the thick anime outline on world citizens.
- Step 162 explicitly adds the dynamically discovered runtime-bake shaders `Hidden/TextureLayerer` and `Hidden/BoZo_Decal` to Unity Graphics Settings -> Always Included Shaders. The Anime toon shader is **not** forced into that list because it has many variants; its normal Resources/material-package reference keeps it in the build without needlessly inflating build time/size.
- The shader-retention code uses Unity 6's Graphics Settings asset through `GraphicsSettings.GetGraphicsSettings()` / serialized `m_AlwaysIncludedShaders`, rather than relying on older API assumptions.
- `H51BoZoCitizenVisual` subscribes to BoZo's `OnCharacterLoaded` event and performs a post-load material repair: outline off, zero outline distance, sane brightness, and ambient lighting enabled.
- Step 163 now fails if any citizen still has premature `cleanAfterMerge`, if the hidden runtime-bake shaders are not retained / the Anime material no longer references `BoZo/BMAC_Toon`, or if the source Anime outline is still enabled.

Re-run the same migration after pulling this fix:

`Hanger 51 -> World -> Current -> 162 - Replace Citizen Bodies With BoZo Characters`

then:

`Hanger 51 -> World -> Current -> 163 - Validate BoZo Citizen Migration`

A new Development Build must confirm that citizens render with their intended skin/hair/clothing colors and that no long black outline/spike geometry remains.


The purchased **BoZo Stylized Modular Characters - Anime Pack** has been imported under:

`Assets/BoZo_StylizedModularCharacters/`

The existing Hanger 51 NPC simulation is retained. This migration replaces only the visible citizen body/Animator presentation while preserving each citizen's existing:

- `H51CitizenIdentity` / persistent citizen ID
- display name and age
- home/work assignments and occupation
- `H51CitizenBrain` schedule/curfew/navigation behavior
- NavMeshAgent and collision root
- dialogue, memories, relationships, medical visits, vehicles, and emergency/civic logic

A new migration/validation path was added:

`Hanger 51 -> World -> Current -> 162 - Replace Citizen Bodies With BoZo Characters`

`Hanger 51 -> World -> Current -> 163 - Validate BoZo Citizen Migration`

Gender/presentation rules are intentionally strict:

- Hanger 51 already has `H51CitizenPresentation.Unknown / Male / Female` on `H51CitizenIdentity`.
- Existing citizens created before that field was populated are assigned a presentation once during Step 162 using the existing first-name set plus a deterministic persistent-seed fallback for uncommon names. The chosen value is then stored on the identity.
- Step 162 now uses **only `Default_Boy` for male identities and `Default_Girl` for female identities** as the body/face starting point. BoZo showcase presets such as Jackal, Kenji, Zell, Glover, Hana, and Jayda are no longer used by the population migration because several include fantasy styling, incomplete clothing, or presentation choices that do not fit Hanger 51's civilian world.
- Step 164 then creates a dedicated Hanger 51 civilian preset per citizen from that correct male/female base and applies a human-only wardrobe, natural skin/hair/eye palette, and occupation-aware clothing.
- `H51BoZoCitizenVisual` includes a runtime presentation guard: if a stored identity presentation and configured visual ever disagree, the mismatched BoZo visual is disabled and an error is logged rather than silently cross-mixing the models.

Animation integration:

- BoZo's `BodyRig.fbx` is imported as a valid Unity Humanoid Avatar.
- Hanger 51 generates separate male/female AnimatorControllers using BoZo's own looping `BMAC_M_Idle` / `BMAC_M_Walk` and `BMAC_F_Idle` / `BMAC_F_Walk` clips.
- Both generated controllers retain the `Speed`, `Wave`, and `Interact` parameters expected by `H51CitizenBrain`; idle/walk movement is driven by the existing citizen NavMesh velocity.
- Root motion remains disabled because Hanger 51 navigation remains authoritative.

Step 162 performs the migration in-place:

- removes an earlier `H51 BoZo Citizen Visual` child if the pass is rerun;
- hides all legacy citizen body renderers and sets `forceRenderingOff` on them;
- removes the legacy citizen Animator components so `H51CitizenBrain` finds the new BoZo Animator instead;
- disables `H51CitizenVisualVariation` so the old material-property randomizer does not overwrite BoZo's authored skin/hair/outfit look;
- instantiates `BSMC_CharacterBase.prefab` underneath the existing citizen simulation root;
- assigns the correct sex-specific human base preset (`Default_Boy` or `Default_Girl`) before the civilian wardrobe pass;
- configures BoZo `OutfitSystem` for OnStart loading, synchronous resource lookup, 512 texture size, and `cleanAfterMerge = false` so BoZo's final runtime texture bake remains valid;
- adds a Humanoid Animator using the imported BoZo body Avatar and the correct male/female Hanger 51 controller;
- adds/configures `H51BoZoCitizenVisual` as the bridge/guard component.

Generated controller location:

`Assets/_Project/Characters/H51Citizens/BoZoGenerated/`

Important files:

- `Assets/_Project/Scripts/NPC/H51BoZoCitizenVisual.cs`
- `Assets/_Project/Editor/Hanger51BoZoCitizenMigration.cs`
- `Assets/BoZo_StylizedModularCharacters/Prefabs/BSMC_CharacterBase.prefab`
- `Assets/BoZo_StylizedModularCharacters/Outfits/Common/Body/BodyRig.fbx`
- `Assets/BoZo_StylizedModularCharacters/CustomCharacters/Resources/Anime/`

Step 163 verifies that every scene citizen:

- has a persisted Male or Female presentation (zero Unknown);
- has exactly the Hanger 51 BoZo visual bridge/root;
- has a BoZo `OutfitSystem`;
- has a valid Human Avatar and generated runtime AnimatorController;
- uses a preset from the correct male/female whitelist;
- has no still-visible legacy body renderer outside the BoZo visual;
- has the old `H51CitizenVisualVariation` disabled.

Do not mark this migration verified until Step 163 passes and a Development Build confirms that male/female citizens load as the intended sex-specific BoZo characters, walk/idle correctly, remain grounded on the NavMesh, still talk/respond normally, and retain their previous names/jobs/homes/schedules.

The occupation-specific wardrobe pass is now implemented under Steps 164/165. Do not leave citizens on the raw Step-162 base appearance for a player build.

### Human-only BoZo civilian wardrobe pass — IMPLEMENTED / PENDING DEVELOPMENT BUILD VALIDATION

After the texture-bake fix, the first Development Build proved that BoZo meshes/materials were loading, but several citizens still looked wrong for Hanger 51: some were in underwear only, one inherited a fantasy red-skin/devil-horn showcase preset, and another male identity visually inherited a more feminine showcase character. A small black prop/cube was also seen near one citizen and may have contributed to movement obstruction.

The next Development Build exposed a second regression after Step 164: citizens could collapse to a floating black head with the body/clothing apparently gone. Root cause was BoZo save-version handling. `Default_Boy` and `Default_Girl` are version-3 saves; BoZo's `Update4` patch converts every outfit layer to `alpha=1`. Hanger 51 was cloning those saves, stamping the generated preset directly to version 4, but retaining `alpha=0`, so BoZo correctly skipped its updater and loaded invalid v4 outfit layers. Step 164 now writes `alpha=1` for every retained and generated outfit layer. `H51BoZoCitizenVisual` also repairs stale bad v4 presets before BoZo's Start-time load so already-serialized scenes recover safely.

The population pipeline no longer uses BoZo's showcase character saves as final citizen appearances.

Wardrobes are now schedule-aware. Step 164 generates one occupation/work preset plus four deterministic off-duty presets per citizen. The citizen brain requests the work preset while the NPC is at work or traveling toward work; home, shopping, leisure, appointments, lunch, and non-work travel use an off-duty preset selected by game day. This gives each citizen consistent identity/hair/skin while rotating real civilian clothing instead of locking everyone into one outfit forever.

Vehicle occupancy is also hardened against floating BoZo fragments. The old commute code only hid renderers cached during `H51CitizenBrain.Awake()`, while BoZo can instantiate additional merged/outfit renderers later. `H51BoZoCitizenVisual` now force-suppresses every current and newly created BoZo renderer for the entire time the citizen is riding in a vehicle, then restores rendering after exit. This targets the intermittent floating black box/head/mesh seen above citizen cars.

Run:

`Hanger 51 -> World -> Current -> 164 - Apply Human Civilian BoZo Wardrobes`

then:

`Hanger 51 -> World -> Current -> 165 - Validate Human Civilian BoZo Wardrobes`

Step 164 creates one generated `CharacterObject` per scene citizen under:

`Assets/_Project/Characters/H51Citizens/BoZoGenerated/CivilianPresets/`

Rules:

- Male citizens are built from `Default_Boy`; female citizens are built from `Default_Girl`. No cross-presentation demo preset pool is used.
- The generated data strips showcase/fantasy/accessory categories before rebuilding the final appearance: no `HeadAcc`, no devil horns, alien antennae, kitty ears, fantasy pupils, demo gloves, eyepatches, or other inherited showcase accessories.
- Pupils are forced to `Pupil/Pupil_Round`.
- Hair is selected from separate conservative male/female hairstyle pools.
- Hair colors are restricted to natural black/brown/blond/auburn/gray palettes.
- Skin is rebuilt from a human skin-tone palette rather than inheriting fantasy/demo skin colors.
- Eye colors are restricted to natural brown/blue/green/hazel/gray values.
- Every generated citizen is required to have **Top + Bottom + Feet**. Underwear may remain underneath as the base-layer system expects, but no citizen should appear publicly in underwear-only state.
- Clothing is occupation-aware. Current wardrobe roles are Casual, Farmer, Worker, Medical, Business, PublicSafety, and Professional.
- Clothing colors are mapped to each prefab's actual channel semantics rather than applied generically. For example, overalls get separate shirt/denim/buckle/cuff colors; suits get separate suit/button/shirt/tie colors; boots get leather/lace/sole colors; and dress pants get separate belt/buckle channels.
- Farmer examples use overalls/khakis/work boots.
- Mechanic, utility, power-plant, airport, construction, and other worker roles use hard jackets or heavy shirts, belted work pants, and work boots.
- Hospital/medical roles use restrained blue/teal professional clothing and proper shoes.
- Business/office roles use suits, dress shirts/cardigans, dress pants/skirts, and office shoes.
- Police/security/fire roles use dark workwear and boots.
- Teachers/instructors use professional civilian clothing.
- Ordinary residents get varied but grounded T-shirts, hoodies, sweaters, jeans, khakis, skirts, sneakers, flats, or loafers depending on presentation and persistent appearance seed.
- All visual choices remain deterministic from the existing citizen appearance seed, so the same citizen should keep the same look across reruns.

The runtime bridge also treats BoZo as **visual-only**. `H51BoZoCitizenVisual` disables child Colliders and CharacterControllers and makes child Rigidbodies kinematic/non-colliding after load. The existing Hanger 51 citizen root/NavMeshAgent remains authoritative for movement. This is intended to prevent imported clothing/accessory physics or stray modular props from pinning a citizen against walls or blocking pathing.

Step 165 validates:

- every citizen has a generated Hanger 51 work preset plus four generated off-duty presets rather than BoZo demo presets;
- at least three distinct Top/Bottom/Feet combinations exist across each citizen's off-duty set;
- generated preset names match the stored male/female presentation;
- every work and off-duty preset has a Top, Bottom, and Feet item;
- no fantasy/demo-only part remains;
- no non-round fantasy pupil remains;
- every generated preset is BoZo save version 4 or newer;
- every outfit layer has the version-4-required `alpha=1`;
- every outfit path resolves through BoZo's Resources folders;
- `cleanAfterMerge` is still disabled for the BoZo texture pipeline.

Important files:

- `Assets/_Project/Editor/Hanger51BoZoCivilianWardrobeRepair.cs`
- `Assets/_Project/Scripts/NPC/H51BoZoCitizenVisual.cs`

For a fresh scene migration, the intended order is now:

1. Step 162 - Replace Citizen Bodies With BoZo Characters
2. Step 163 - Validate BoZo Citizen Migration
3. Step 164 - Apply Human Civilian BoZo Wardrobes
4. Step 165 - Validate Human Civilian BoZo Wardrobes
5. Development Build and visually inspect citizens, clothing, movement, and occupation fit.

### Current World reset, roadside repair, and civic interior grass cleanup — IMPLEMENTED / PENDING UNITY VALIDATION

The World editor menu is now reorganized automatically. Completed numbered commands through Step 165 are moved from `Hanger 51 -> World -> Current` into:

`Hanger 51 -> World -> Archive -> Steps 096-165`

No tools are deleted; this only keeps the Current menu short. New work resumes at Step 166.

Run:

`Hanger 51 -> World -> Current -> 166 - Repair Bus Stops And Interior Grass`

then:

`Hanger 51 -> World -> Current -> 167 - Validate Bus Stops And Interior Grass`

Step 166 re-snaps every generated roadside bus shelter against the **final** road-surface mesh, places it safely beyond the road edge/shoulder, keeps the long shelter axis parallel to the road, and points the open side toward traffic. It also finds the finished theater/cinema/hospital/medical `Interior Floor` footprints and removes both Terrain detail grass and spawned grass-prefab instances from those footprints so vegetation cannot clip through indoor floors.

The earlier intermittent black cube above citizen cars also has an explicit fix: `H51CitizenEmergencyState` creates a dark runtime cube named `Emergency Phone`. That visual is now travel-suppressed whenever a citizen enters a car, in addition to the BoZo renderer suppression already used for vehicle occupants.

### Anatomical player/NPC health system — FIRST SYSTEMIC PASS IMPLEMENTED / PENDING UNITY VALIDATION

Run:

`Hanger 51 -> World -> Current -> 168 - Install Anatomical Health System`

then:

`Hanger 51 -> World -> Current -> 169 - Validate Anatomical Health System`

The same `H51AnatomicalHealth` model is installed on the player and every citizen. It currently contains:

- nine damageable body regions: head, neck, chest, abdomen, pelvis, both arms, and both legs;
- ten independently damageable anatomy targets: brain, heart, both lungs, liver, stomach, intestines, both kidneys, and spine;
- circulating blood volume in liters;
- separate external and internal bleeding rates;
- oxygen saturation, pulse, systolic pressure, pain, shock, adrenaline, and consciousness;
- fractures and disabled limbs;
- organ-specific critical states and death from catastrophic brain/heart injury, exsanguination, or prolonged hypoxia;
- point-based damage routing for future projectiles/weapons via `ApplyDamage(worldPoint, amount, type, direction, penetration)`;
- NPC medical-emergency triggering from anatomical trauma;
- citizen vehicle collision trauma routed into the anatomical model;
- ambulance/on-scene treatment that stabilizes bleeding without magically restoring destroyed organs.

The player receives `H51BodyInspectorUI`. Press **H** in Play Mode/Development Build to open it. It intentionally follows the same broad body-inspector concept as the supplied reference: body regions on the left, selected anatomy and organs in the center, and physiological bars/readouts on the right. Development builds also expose test controls for blunt trauma, penetrating trauma, stabilization, and full reset so the physiology can be exercised before weapons and medical items are fully connected.

Important files:

- `Assets/_Project/Scripts/Health/H51AnatomicalHealth.cs`
- `Assets/_Project/Scripts/Health/H51BodyInspectorUI.cs`
- `Assets/_Project/Editor/Hanger51AnatomicalHealthSetup.cs`
- `Assets/_Project/Editor/Hanger51WorldCurrentCleanup166.cs`
- `Assets/_Project/Editor/Hanger51WorldMenuOrganizer.cs`

### LowPolyWeapons purchase, combat, ballistics, emergency and trauma-treatment pass — IMPLEMENTED / PENDING UNITY VALIDATION

The imported `Assets/LowPolyWeapons` pack is now connected to Hanger 51 through the existing commerce/inventory stack rather than a separate weapon-only inventory.

Run:

`Hanger 51 -> World -> Current -> 170 - Install Low Poly Weapons Combat System`

then:

`Hanger 51 -> World -> Current -> 171 - Validate Low Poly Weapons Combat System`

Step 170 keeps the existing aviation-parts catalog and appends twelve combat products. The first playable set is intentionally representative rather than wiring every model variant at once:

- 5.56 service rifle using `AssaultRifle1_01`;
- 9mm service pistol using `Pistol1_01`;
- 7.62 precision rifle using `SniperRifle1_01`;
- matching 5.56×45, 9×19 and 7.62×51 ammunition cases;
- compact red-dot and magnified optics;
- rifle suppressor and vertical foregrip;
- fragmentation grenade;
- trauma medical kit.

Purchases use the normal parts-computer -> shipment-bay -> animated crate -> unbox -> pickup -> inventory pipeline. Player inventory capacity is raised to at least 20 slots so combat items can coexist with aircraft/engine parts.

Runtime controls in a Development Build:

- `LMB`: fire equipped firearm / throw equipped grenade;
- `RMB`: aim down sights;
- `R`: reload from matching ammunition stacks in inventory;
- `C`: open/close weapon customization while a firearm is equipped;
- `F`: open the trauma-treatment panel for the aimed injured NPC (or self if no patient is targeted) while a trauma kit is equipped;
- `H`: existing Body Inspector.

Weapon customization transfers owned attachments out of inventory while installed and returns them when removed or swapped. Optics change the actual ADS field of view. The magnified optic uses a circular scope blackout/reticle. The suppressor and foregrip carry configured velocity/recoil modifiers rather than being visual-only.

Ballistics are projectile-based, not hitscan. The initial cartridge profiles use representative projectile mass/muzzle-velocity values for 9×19, 5.56×45 and 7.62×51. `H51BallisticProjectile` advances under gravity plus a lightweight drag model, calculates impact kinetic energy and momentum at the collision point, then routes the hit into `H51AnatomicalHealth`. NPCs receive a separate trigger-only anatomical hitbox rig for head, neck, chest, abdomen, pelvis, both arms and both legs while retaining their original navigation/collision capsule.

Projectile momentum also feeds `H51BallisticImpactReaction`. Reactions are deliberately restrained: ordinary bullets should create a believable flinch/displacement rather than unrealistically throwing a person across the room. Fatal injuries stop the NPC routine and leave the body/state in the world for testing instead of deleting the citizen.

Gunshot and grenade injuries route into the existing emergency system. Injured citizens request ambulance + police response. A fatal victim remains in-world and cannot resume its schedule when emergency callbacks finish.

The trauma kit is injury-driven instead of a generic HP refill. It consumes one kit when treatment begins and exposes context-sensitive stabilization actions for external hemorrhage, severe limb bleeding/tourniquet use, chest/breathing injury, airway/breathing support, and shock support. It warns when major blood loss or critical internal-organ damage exceeds what the field kit can correct. These actions stabilize physiology but do not regenerate destroyed organs or replace lost blood.

Important first-pass limitations:

- only the representative firearms/attachments listed above are cataloged so far; the same data-driven system is intended to absorb the pack's SMGs, shotguns, LMGs, heavy weapons, additional optics, mines/C4/Molotovs and remaining variants later;
- police/ambulance dispatch responds to shootings, but armed police combat AI is not yet implemented;
- firearm audio, muzzle flash, casing ejection and more advanced weapon animation are still future polish;
- magnified optics currently use FOV zoom + scope mask/reticle rather than a separate render-to-texture scope camera;
- attachment state is currently stored per weapon definition during the runtime session rather than as fully serialized per-instance gun condition.

Important files:

- `Assets/_Project/Scripts/Weapons/H51CombatItemDefinition.cs`
- `Assets/_Project/Scripts/Weapons/H51PlayerCombatController.cs`
- `Assets/_Project/Scripts/Weapons/H51BallisticProjectile.cs`
- `Assets/_Project/Scripts/Weapons/H51BallisticImpactReaction.cs`
- `Assets/_Project/Scripts/Weapons/H51GrenadeProjectile.cs`
- `Assets/_Project/Scripts/Weapons/H51MedicalTreatmentUI.cs`
- `Assets/_Project/Scripts/Health/H51AnatomicalHitboxRig.cs`
- `Assets/_Project/Editor/Hanger51LowPolyWeaponsCombatSetup.cs`

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
- Apply and validate World Steps 158/159 for the CC0/procedural starry night sky, visibly glowing lamp bulbs, clip-safe visible moon, and civilian overnight curfew.
- Apply and validate World Steps 162/163 for the BoZo citizen body migration and strict male/female visual separation.
- Apply and validate World Steps 160/161 for runway-sign occlusion, obsolete fixed-gun/radiator cleanup, and spawned-aircraft tow-bar targeting.
- Continue regression testing World Steps 156/157 for runway visibility, runway-control readability, and restored wing-armament bay visuals.
- Recheck Step 155 after the night/control repair if the runway-control placement is changed again.
- Continue testing the time system at dawn, daytime, dusk, and full night.
- Verify ordinary citizens stop following weekday work routines on weekends.
- Verify power-plant and emergency workers maintain rotating overnight coverage.
- Verify town/building lights turn on at night only when the utility grid is powered.
- Verify runway OFF/AUTO/ON behavior and loss of runway lighting during a grid outage.
- Continue expanding aircraft systems, cockpit systems, engine maintenance, armament service, and flight behavior without regressing existing maintenance interactions.

## Repository History Note

The original README described the repository as a basic first-person movement prototype. That milestone has long since been surpassed. The current project is now a substantially larger aviation maintenance/flight prototype centered on the serviceable P-51 and its associated aircraft, engine, inventory, condition, commerce, and flight systems.
