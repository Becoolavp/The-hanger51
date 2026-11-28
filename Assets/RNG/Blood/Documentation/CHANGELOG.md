# Changelog

## 1.2.0

Everything new is off until you turn it on: without the component and with the new settings off, blood looks and behaves exactly as in 1.1.

- Blood Supply: an optional component for the blood a body holds. Smears from a dragged body spend it and get narrower, streakier and drier as it runs out; once half is gone the trail breaks into patches, and an empty body leaves a dark, matte, dry trail. The death pool of a body that bled out is smaller. `Drain`, `Refill`, `Litres`, `Fraction` and the `Emptied` event connect it to your health system, and an add-on body that holds blood of its own (`IBloodReservoir`) feeds it.
- Wet Smears (setting): a smear dragged out of a pool starts as thick, bright and glossy as the pool and thins into a normal smear over **Wet Smear Length**, so the pool and the trail read as one. Blood picked up from a pool keeps the smear full even on a body that is running dry.
- Liquid Drag (setting, needs Liquid): something dragged through liquid on the floor pushes it ahead in a thick tongue that runs into its smear.
- `Blood.Smear` takes `dryness` and `wetness` for smears of your own, and `BloodField.ReadLiquid` reads how deep the liquid floor is at a point.
- `Blood.Print` with a shape: hand, knee, hoof and tyre tread prints next to the foot.
- `Blood.Wash`: water washes blood off floors and walls. It turns pink and thin, liquid on the floor drains, and areas washed clean drop their stains. `BloodWounds.Wash` does the same for blood on a body.
- `BloodWounds.Soak`: a patch of blood on a body where it lay in a pool, with no hole and no runs.
- `BloodWounds.Stab` and `BloodWounds.Splat` for weapons: a blade coated from its tip back to the depth it went in, and a splat where a blunt weapon hit.
- `BloodField.Press` presses liquid on the floor away under a capsule.
- The liquid floor no longer paints over a body lying in it: liquid reaches 8 cm above the floor it was poured on. Raise **Liquid Floor Reach** on the Blood Stain material for pools on slopes.
- Splats no longer paint red bands on a body or a prop lying on the floor. **Stain Rise** (settings, 5 cm by default) is how high above the surface it landed a splat still paints; 0.2 paints as far as 1.1 did. Runs down a wall still turn onto the floor.
- The RNG Blood window and Copy AI Prompt include the Contact add-on when it is installed.
- The RNG Blood window reads top to bottom: project check (shown in full only when something needs fixing), then two ways to add blood (an AI coding agent or by hand, with the steps), then the demos, the Dismemberment demo included when the add-on is installed.

## 1.1.0

- Unity 2022.3 and URP 14 support.
- Smears: `Blood.Smear` and the Blood Smear component for dragged bodies, hand strokes and trails of any length. **Any Surface** smears floors and walls a body slides along, **Forward** smears the surface in front.
- Liquid floor: pools spread, stop and merge into one puddle; runs from the walls drain into it.
- Grooves: `Blood.Channels` cuts drains, gutters or letters into the liquid floor from a mask texture; blood runs into them and fills them without being created or lost.
- Showcase demo: the logo cut into a concrete slab fills with blood after a grenade or a ceiling fan.
- More than about 8000 stain slots no longer drop the last ones (the liquid floor among them): the stain mesh switches to 32-bit indices.
- Wall to floor: runs end in a puddle at the foot of the wall, pools climb the wall a little, splatter in corners continues on both surfaces.
- Blood on characters: the Blood Wounds component leaves bleeding wounds on skinned or plain meshes where they are hit.
- Quality levels High, Medium and Low. Low needs no Depth Texture and suits mobile.
- Massive mode: floor splats painted into one texture, for big battles at a constant cost.
- `Blood.Cleared` event, raised when blood is cleared, hidden or shut down.
- Max Drops goes up to 8192 for add-ons that spray a lot of blood; above about 1500 drops the drop mesh uses 32-bit indices.
- Blood Wounds holds up to 32 wounds per character instead of 16, and a hit inside an existing wound takes no new slot, so long bursts of fire no longer wipe the first wounds off the body. Each pixel of the coat only works on the wounds near it.
- `BloodWounds.Rebuild` rebuilds the blood coats after a character's meshes are swapped or cut at runtime.
- Blood on characters stays on the skin: wounds and their runs are placed in the rest space of each mesh, so they no longer slide over a body that moves or swings.
- Hit Demo: wounds on the crash test dummy, a Smear button, live stain count and frame rate, Quality, Massive and Liquid in the settings panel.
- Arena demo: a top-down brawler with five weapons, waves of dummies, ragdolls and body dragging that shows every feature at once.
- A guide for AI coding agents (`Documentation/AGENTS.md`) and buttons in the Setup window that install it as a Claude Code skill or point the project's AGENTS.md to it.
- **Copy AI Prompt**: the RNG Blood window opens once after import and copies a ready prompt with the project path and the guides (the Dismemberment guide too when the add-on is installed) for any coding agent.

## 1.0.0

First release.

- Drops in flight, splats, wall runs, puddles that merge, pools under bodies, footprints, drying and fading.
- Seven hit presets, a hit event for add-ons, censored colour mode and an off switch.
- Blood Emitter and Blood Footprints components.
- Setup window that checks and fixes the URP settings.
- Live settings: `Blood.Apply` and a settings panel that tunes every field while the game runs.
- Two demo scenes: a cinematic Crime Scene and a Hit Demo with a crash test dummy.
