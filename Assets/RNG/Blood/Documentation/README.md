# RNG Blood

Procedural GPU blood for the Universal Render Pipeline. Drops fly, splat on floors and walls, run down walls, merge into puddles, pool under bodies, leave bloody footprints and dry out over time. No textures, two draw calls for all the blood in the scene and no garbage collection at runtime.

- Drops fly on a ballistic arc in the vertex shader, stretch with speed and wobble.
- Where a drop lands it leaves a splat, rays, a streak or a heavy blot, stretched by the angle it hit at.
- Small drops that hit a wall stay as spatter; larger ones leave runs that drip down over the next seconds.
- Fresh splats that land close together merge into one puddle that keeps growing.
- A body leaves a pool that grows under it and follows it for as long as it exists.
- Everything dries: glossy red turns dark, matte and brown, then fades out.
- A walker that steps into a wet pool leaves bloody footprints that fade over eight steps.
- Smears: a body dragged through blood leaves a streaked trail, a hand leaves finger strokes on a wall, a body knocked against a wall smears the wall. Trails of any length stay seamless.
- Wall to floor: runs that reach the floor end in a puddle at the foot of the wall, pools that meet a wall climb its foot, splatter in a corner continues on both surfaces.
- Liquid floor: pools spread, stop where they get thin and merge into one puddle, and runs from the walls drain into them.
- Grooves: cut drains, gutters or letters into the liquid floor with one mask texture, and blood runs into them and fills them.
- Characters get bloody where they are hit, with blood running down from each wound.
- Stains are projected onto the depth buffer, so they wrap over any floor, ramp or wall without decal projectors.
- Stains on glass panes are drawn on the pane itself.
- Seven hit presets (bullet, close bullet, shotgun, headshot, blunt, blade, explosion), all tunable.
- Three quality levels, from projected stains with wet bevelled edges to flat stains that need no depth texture, for mobile.
- Massive mode for big battles: floor splats are painted into one texture, so thousands cost the same as one.
- A green censored mode and an off switch for a gore setting.
- A public hit event for add-ons such as dismemberment.

Requirements: Unity 2022.3 or newer with the Universal Render Pipeline (URP 14 or newer). Nothing else.

## Quick start

1. Import the package. The **RNG Blood** window opens once: it checks your project (press **Fix All** if it lists anything) and has the **Copy AI Prompt** button described below. You can open it again any time from **Tools > RNG Blood > Setup**.
2. Make sure floors and walls are on the layers in **Surfaces** of the settings (the Default layer out of the box) and characters are not.
3. Call one line from your hit code:

```csharp
using Rng.Blood;

Blood.Spawn(hit, ray.direction);
```

That is all. The first call creates the blood object with the default settings from `Runtime/Resources/BloodSettings.asset`.

Four demo scenes are included:

- **Crime Scene** (**Tools > RNG Blood > Open Crime Scene Demo**): a tiled room with cinematic lighting, depth of field and grading. It starts with blood that already dried a minute ago, then a fresh impact sprays a trail across the floor and runs down the wall. Keys 1 to 6 switch cameras, a click on a wall or the floor sprays blood there, R replays. The scene shows how to drive blood with your own clock (`Blood.Tick`) to age it instantly. It uses its own settings asset (`Crime Scene Blood Settings`) with longer lifetimes so dried and fresh blood are on screen together. Film grain needs the Post Process Data asset assigned on your Universal Renderer, which new URP projects have by default.
- **Hit Demo** (**Tools > RNG Blood > Open Hit Demo**): the same room with a crash test dummy. Press HIT or Space and every hit comes from a new angle; pick a weapon, switch cameras, turn on slow motion or auto fire, open the settings panel with Tab, and switch the blood between six colours (crimson, alien green, coolant blue, oil, venom purple and the glowing toxic colour from **Alternate Colour**). The **Crash Dummy** prefab (`Demo/Dummy`) is built from simple shapes and reacts to hits with springs on its joints, no animation files. Its `Take(kind, direction)` method shows how to tie `Blood.Spawn` to the body part that was hit.
- **Arena** (**Tools > RNG Blood > Open Arena Demo**): a small top-down brawler. Move with WASD, aim with the mouse, attack with the left button and switch weapons with 1 to 5 (bat, knife, pistol, shotgun, grenade). Dummies come in waves; the dead fall as physics ragdolls you can kick, shoot or blow across the room. Hold the right button on a body to drag it. Every feature is on screen at once: hit sprays, wounds, pools, wall runs that drain into the liquid floor, footprints and smears on floors and walls. Tab opens the settings panel. The scene uses its own settings asset (`Arena Blood Settings`) with **Liquid** on and stronger bursts.
- **Showcase** (**Tools > RNG Blood > Open Showcase Demo**): the RNG BLOOD logo cut into a concrete slab, seen from above. Press 1 for a grenade that blows a row of crash test dummies apart, 2 for a dummy that drops into a ceiling fan; Space replays. The blood that lands spreads as liquid and runs into the cut letters until the logo stands in blood. It shows `Blood.Channels` (see **Grooves** below) with its own settings asset (`Showcase Blood Settings`).

## AI coding agents

`Documentation/AGENTS.md` is written for coding agents such as Claude Code, Codex, Cursor and Copilot: the API, the rules that keep blood working and ready recipes (hitscan, projectiles, melee, deaths, bleeding, gore option, add-ons).

The quickest way: press **Copy AI Prompt** in the RNG Blood window (or **Tools > RNG Blood > Copy AI Prompt**). It copies a prompt with your project's path and the path of the guide, plus the add-on guide when RNG Blood: Dismemberment is installed. Paste it into your coding agent, write what you want after "What I want", and the agent reads the guide and wires the blood into your weapons and characters.

Two more buttons in the same window:

- **Install Claude Skill** writes it as a Claude Code skill to `.claude/skills/rng-blood/SKILL.md` in your project, so Claude loads it by itself whenever a task involves blood.
- **Add Note To AGENTS.md** adds one line to the `AGENTS.md` in your project root (it is created if missing) that points other agents to the guide.

Both ask before writing and touch nothing else. After a package update, press **Update Claude Skill** to refresh the skill.

## What the Setup window checks

| Check | Why |
| --- | --- |
| The project uses URP | The shaders are URP shaders. |
| **Depth Texture** is on in every URP asset in use | Stains, pools and footprints read the depth buffer to wrap over surfaces. Skipped when the default settings use Low quality. |
| **Depth Texture Mode** is **After Opaques** on every Universal Renderer | Stains are drawn with the transparents and need depth before that. |
| The default settings asset exists and has both materials | `Blood` loads it when you do not pass your own settings. |
| **Surfaces** is not empty | Drops land only on these layers. |

Stains are projected, so they can paint things lying on the floor. **Stain Rise** in the settings is how high above the surface it landed a splat still paints (5 cm by default): lower keeps bodies and props cleaner, higher lets splats climb bumpy ground or the foot of a wall.

A camera with its own **Depth Texture** override set to Off shows no stains either; leave that override on **Use settings from Render Pipeline Asset** or set it to On.

## Scripting

Everything is on the static class `Rng.Blood.Blood`.

### Hits and deaths

```csharp
Blood.Spawn(hit, direction, intensity = 1f, kind = BloodHitKind.Bullet);
Blood.Spawn(position, normal, direction, intensity = 1f, kind = BloodHitKind.Bullet, bone = null);
Blood.Death(feet, direction, body = null, kind = BloodHitKind.Bullet, poolScale = 1f);
Blood.Drip(origin);
```

- `Spawn` is one hit on a body. Pass the `RaycastHit` of your shot and the direction it travelled, or the hit point, surface normal and direction yourself. Blood flies on with the shot. `intensity` scales count and speed of every burst; at 1 the hit also leaves a mark on the floor under it. `kind` picks the preset:

| Kind | Bursts |
| --- | --- |
| `Bullet` | gout and a bullet spray |
| `BulletClose` | gout, a wider spray, back spatter against the shot and mist |
| `Shotgun` | gout, a wide spray and mist |
| `Headshot` | gout, a big spray with a rays mark on the floor and mist |
| `Blunt` | gout and heavy blots |
| `Sharp` | gout and a long streak on the floor |
| `Explosion` | gout and a blast from chest height above `position`, so pass the feet |

- `Death` is a body going down. `feet` is where it stands. It sprays from chest height and, after a short delay, grows a pool under `body` whose size depends on `kind`. The pool lives while `body` exists and fades out when it is destroyed. Pass `poolScale = 0` for no pool.
- `Drip` lets one or two drops fall from a wound. Call it every half second or so for a bleeding wound.

### Components

- **Blood Emitter** (`Add Component > RNG > Blood Emitter`). Put it on a wound point or a weapon tip, aim its blue axis the way blood should fly and call `Spawn()`, `SpawnKind(int)`, `Death()` or `Drip()` from a UnityEvent, an Animation Event, Timeline or code. No scripting needed.
- **Blood Wounds** (`Add Component > RNG > Blood Wounds`). Put it on the root of a character. Every `Blood.Spawn` that hits one of its bones (the `bone` argument, or the hit transform of a `RaycastHit`) leaves a wound on the body that bleeds down, dries and fades with the rest of the blood. It works with skinned and plain meshes and leaves your materials alone: it draws a thin coat over the body with its own material. Call `Add(point, kind, intensity, bone)` to place a wound yourself, for example at the entry point when your `Spawn` sprays from the exit. Up to 32 wounds per character; the oldest is reused, and a hit inside a wound that is already there does not take a new one.
- **Blood Smear** (`Add Component > RNG > Blood Smear`). Put it on something that gets dragged, at floor level: a corpse's hips, a sack, a hand. After it moves through a wet pool, or after you call `Soak()`, it leaves a smear that fades over **Reach** metres. Tick **Bleeding** for a bleeding body that smears wherever it goes. Tick **Any Surface** to smear whatever it touches, the floor below it or a wall it slides along, for example on the chest of a ragdoll. **Forward** smears the surface in front of its blue axis instead, for a palm on a wall.
- **Blood Supply** (`Add Component > RNG > Blood Supply`), optional. Put it on the root of a body that has a Blood Smear under it. The body holds **Capacity** litres; its smears spend them, so the trail gets narrower, streakier and drier as the blood runs out. Drying starts once a quarter is gone, the trail breaks into patches once half is gone, and an empty body leaves a dark, matte, broken trail. The pool from `Blood.Death` shrinks with what is left. **Smear Cost** sets how fast a smear spends blood: with the defaults a body dragged with a 0.35 m smear runs dry after about 12 m. The *When Empty* fields set how the trail looks at the end. From code: `Drain(litres)` for wounds of your own, `Refill()` on respawn, `Litres` and `Fraction` to read it, `Emptied` (event and UnityEvent) once it runs out. Without the component smears are as before.
- **Blood Footprints** (`Add Component > RNG > Blood Footprints`). Put it on anything that walks, at the feet. After the walker steps into a wet pool it leaves left and right prints that get fainter. If you already track steps yourself, call `Blood.Walk(walker, position, heading, stride)` every frame the walker moves (`stride` true once per `StrideLength`) and `Blood.Forget(walker)` when it is gone.

### Hook for add-ons

```csharp
Blood.Hit += hit => { /* hit.Position, hit.Normal, hit.Direction, hit.Force, hit.Kind, hit.Bone */ };
```

Every `Spawn` raises `Hit` before any blood is drawn, even while blood is hidden. `Force` is the intensity passed to `Spawn`, `Bone` the transform that was hit. Use it for dismemberment, sounds, hit marks or your own effects.

### Lower level

```csharp
Blood.Emit(burst, origin, direction, strength = 1f);
Blood.Fountain(source, axis, local, seconds, interval, burst);
Blood.Pool(point, normal, radius, growSeconds, lifetime);
Blood.PoolUnder(body, fallback, delay, radius, growSeconds, lifetime);
Blood.Mark(origin, direction, style, halfWidth, stretch = 1f);
Blood.Smear(from, to, width = 0.3f, strength = 1f);
Blood.Smear(from, to, normal, width = 0.3f, strength = 1f, start = true, end = true);
Blood.Smear(from, to, normal, width, strength, start, end, dryness, wetness = 0f);
Blood.Print(point, normal, heading, shape, length, width, left = false, strength = 1f, lifetime = 0f, seed = 0f);
Blood.Wash(from, to, radius, amount);
Blood.Wet(point, margin = 0f);
```

- `Emit` fires any `BloodBurst` (count, cone, speed, size, stain scale, lift, stain style, floor mark). Make your own with `new BloodBurst(...)` or use the ones in the settings.
- `Fountain` re-emits a burst from a moving `source` every `interval` seconds, weaker over time, along `axis` (in the local space of `source` when `local` is true). Good for a severed artery.
- `Pool` puts a growing pool at a point. `PoolUnder` finds the floor under a body after `delay` and keeps the pool tied to that body.
- `Mark` puts one stain of a given style on the floor under `origin`.
- `Smear` draws a smear from `from` to `to`. The short form finds the floor below; pass a `normal` for walls and ceilings. `strength` below 1 leaves a thinner, streakier smear. `start` and `end` round the two ends; turn them off to chain smears into one long trail, as the Blood Smear component does. `dryness` 1 lays the smear down already dry (dark, matte, broken); `wetness` 1 makes it as thick and glossy as a pool. Along a chained trail the look blends from one call to the next.
- `Print` with a `BloodPrintShape` leaves a hand (`left` mirrors it), knee, hoof, foot or a piece of tyre tread. `length` runs along `heading`. For tread, pass the distance along the track as `seed` so pieces laid one after another line up. Prints share the **Max Footprints** slots.
- `Wash` washes blood off in a capsule from `from` to `to`: rain, a hose or a mop. Call it every frame the water runs with `amount` the share washed off in that frame (`Time.deltaTime / seconds`). Blood turns pink and thin, liquid on the floor drains, and once an area is clean its stains are gone for good. Up to 16 areas at once. `BloodWounds.Wash(amount)` washes the blood on a body, and `BloodWounds.Soak(point, radius, depth, bone)` puts a patch of blood on the side of a body that lay in a pool. For weapons, `BloodWounds.Stab(tip, entry, bone)` coats a blade from its tip back to where it went in and `BloodWounds.Splat(point, radius, bone)` leaves a splat where a blunt weapon hit. The Contact add-on drives all of these from components.
- `Wet` tells whether a point stands in a wet pool.

### Visibility, colour and settings

```csharp
Blood.Initialize(mySettings);
Blood.Show(false);
Blood.Show(true, alternate: true);
Blood.Recolour(colour, alternateColour);
Blood.Apply(changedSettings);
Blood.Clear();
Blood.Shutdown();
```

- `Initialize` switches to your own settings asset. Duplicate `BloodSettings.asset` or use **Create > RNG > Blood Settings**.
- `Show(false)` hides and clears all blood, for a gore-off option. `alternate: true` swaps to the alternate colour (green by default) for a censored mode.
- `Apply` takes changed settings while the game runs. Edit a copy (`Instantiate(Blood.Settings)`) rather than the asset itself, since changes to an asset in play mode stay in the editor. Colours, drying, pace, merging, pools, footprints and bursts change at once and the blood already on screen stays. A changed limit or material rebuilds the blood object, which clears it.
- `Clear` removes everything on the ground and in the air, for example on a level restart.
- `Shutdown` destroys the blood object. The next call creates it again.

### Driving time yourself

`Blood.Tick(time)` switches blood to a manual clock and advances it to `time` seconds. Use it to render previews in the editor, to replay deterministically or to pause blood separately from `Time.timeScale`. Without it blood follows `Time.time`, so it pauses with `Time.timeScale = 0`.

### Diagnostics

`Blood.Field` exposes counters: `DropsEmitted`, `DropsLanded`, `StainsPlaced`, `WallStains`, `GlassStains`, `PoolsAlive`, `PuddlesAlive`, `PuddlesFormed`, `PrintsPlaced`, `LastLanding`, `LastPoolPoint`.

## Settings

Select `Runtime/Resources/BloodSettings.asset` (or **Tools > RNG Blood > Select Default Settings**). Every field has a tooltip.

To tune by eye, open the Hit Demo, press Play and press Tab (or **Blood settings**). The panel lists every setting with a slider, grouped like the asset, and the next hit shows the change. Point at a setting to read what it does, **Reset** goes back to the asset, and **Save as asset** (in the editor) writes your values to a new settings asset. The panel is the `Blood Tuner` component in `Demo`; add it to any scene and call `Show(true)` to tune blood in your own game.

| Group | What it controls |
| --- | --- |
| Rendering | materials, quality, surface layers, colour, alternate colour, stain lifetime, glass keyword |
| Massive | paint floor splats into one texture, the area it covers and its resolution, the liquid floor, liquid drag, wet smears |
| Limits | drops in flight, splats, pools and footprints at once |
| Drying | when drying starts and ends, when fading starts, dry brightness, brown tint and gloss |
| Amount | overall drop size, the heavy gout every hit adds |
| Pace | flight speed, launch speed, reach, cone spread |
| Stains That Run Together | how close and how fresh splats must be to merge, how big a puddle can get |
| Walls | how large a splat on a wall must be to run down |
| Body | chest height used for deaths and explosions |
| Pool Under A Body | delay, grow time, lifetime and radius per hit kind |
| Wounds | the drip burst |
| Footprints | prints per soaked walker, print size, foot spacing, soak margin, stride length |
| Bursts | every hit preset |

The look of fresh blood (gloss, specular, edge bevel, environment reflections, splat speed, wobble of flying drops) lives on the two materials, `Materials/Blood Drop` and `Materials/Blood Stain`. Lower **Environment Reflections** keeps blood dark red in bright scenes; raise it for a wetter, more mirror-like look.

## Performance

All drops are one mesh and one draw call, all stains are a second one. Flight, drying and fading run on the GPU. The CPU only casts the flight path of each new drop and places stains when drops land. There is no garbage collection at runtime.

Measured in the editor on a GeForce RTX 5060 at 3840 x 2160. At 1920 x 1080 the GPU cost is roughly a quarter.

| Situation | GPU per frame |
| --- | --- |
| Idle, all blood dry or gone | 0.1 ms |
| Start of a fight, about 60 stains, top-down camera | 0.7 ms |
| Stress test, about 1000 stains alive, top-down camera | 2.9 ms |
| Stress test, about 1000 stains alive, camera right above them | 5.0 ms |

CPU: about 0.2 ms per hit on the main thread, 0 bytes allocated.

### Quality levels

**Quality** in the settings trades looks for speed:

| Quality | Stains | Needs Depth Texture | For |
| --- | --- | --- | --- |
| High | projected onto any surface, wet bevelled edges | yes | PC and consoles |
| Medium | projected onto any surface, no bevel, about a third of the shading cost | yes | mid-range hardware |
| Low | flat on the plane they landed on, no bevel, low-poly drops | no | mobile and low-end |

Low needs no depth copy at all, which is the biggest single saving on mobile. On curved or uneven ground its stains stay flat.

### Massive

With **Massive** on, floor splats are painted into one floor texture that follows the camera instead of each being its own stain. A thousand splats then cost the same as one. Walls, pools, puddles, footprints and smears stay normal stains. Use it for big battles, top-down and RTS games. Splats smaller than a texel of the texture blur into their neighbours: 2048 pixels over 40 m is about 2 cm per pixel. Massive needs High or Medium quality.

### Liquid floor

With **Liquid** on, pools on the floor are one liquid instead of separate stains. Blood is kept as a layer in a texture that follows the camera (the **Massive Size** area, at most 1024 pixels across) and the GPU spreads it a few times every frame. A pool grows while it is thicker than a thin film and stops when it gets thin, so its size comes from how much blood it holds. Pools that touch run together into one puddle with one edge, runs that reach the floor drain into it, and many drops landing in one place gather into a puddle of their own. The liquid dries and leaves a dry stain that fades with the rest of the blood. Footprints and smears pick up blood from it. Liquid needs High or Medium quality.

It costs one full-texture pass per spreading step: about three steps per frame at 60 frames per second, on a 1024 texture. On mobile keep it off or lower **Massive Resolution** to 512.

#### Grooves

`Blood.Channels(mask, centre, size, depth)` cuts grooves into the liquid floor: drains, gutters, cracks or letters engraved in concrete. The mask is a texture laid flat on the floor, `size` metres across and centred on `centre`, with the top of the texture towards +z. White texels are grooves. Liquid that reaches a groove runs into it, flows along it many times faster than over the floor and fills it before it spills back out; what lies in a groove is drawn only where the groove is. Blood is never created or lost on the way, so a small pool fills a short groove and a big one fills a long one. Pass `null` to remove the grooves. Without a call nothing changes: the liquid floor behaves exactly as without grooves. Import the mask without compression (a single-channel PNG works best) so the groove edges stay clean. Call it once the blood object exists, and again after `Initialize` or an `Apply` that rebuilds it. The Showcase demo uses it for the logo.

#### Dragging through pools

Two settings in the Massive group make dragged bodies and pools read as one. Both are off by default.

- **Liquid Drag** (needs Liquid): a Blood Smear dragged through liquid on the floor pushes it ahead in a thick tongue, and the pool closes in behind. **Liquid Drag Strength** sets how much of the liquid moves.
- **Wet Smears**: a smear dragged out of a pool starts as thick, bright and glossy as the pool and turns into a normal smear over **Wet Smear Length** metres. Blood picked up from a pool is not the body's own, so a Blood Supply does not thin or break it. With Liquid on, the smear asks the GPU where the liquid really lies, so the wet part starts at the visible edge of the pool.

### Wall to floor

Stains near the line where a floor meets a wall continue across it. A pool that meets a wall climbs its foot by about 3 cm, splatter that lands in a corner continues on both surfaces, and a run that reaches the floor ends in a puddle that grows while blood runs down. This works on High and Medium quality and needs nothing to set up.

The cost grows with the number of stains on screen and how much of the screen they cover. On mobile and other low-end targets lower **Max Splats** (for example to 200) and **Max Pools**, shorten **Stain Lifetime**, and keep **Depth Texture** on only if you can afford the depth copy.

## Limits

- Universal Render Pipeline only. The Built-in pipeline and HDRP are not supported.
- VR single pass instanced rendering is not supported in this version.
- Stain boxes are 0.25 m deep around the surface they landed on and skip surfaces steeper than about 70 degrees off the stain.
- Surfaces that write no depth (transparent materials) get no stains, except glass panes (next point).
- Stains on glass: when a drop hits a collider whose renderers use a material with **Glass Material Keyword** in its name, the stain is drawn on the pane itself instead of on what is behind it.
- When a limit is reached the oldest drop, splat, pool or footprint is reused.
- Massive keeps one floor height per point of its texture: on stacked floors (a balcony over a hall) the upper floor's blood wins where both overlap. Blood outside the area it covers is drawn as normal stains.
- The liquid floor spreads on flat floors. It does not flow down ramps or stairs, and blood that reaches a wall stops at the wall line. Pools outside its area are drawn as normal pools.

## Troubleshooting

**I see flying drops but no stains.** Depth Texture is off or the Depth Texture Mode is After Transparents. Run **Tools > RNG Blood > Setup** and press **Fix All**. Check that the camera does not override Depth Texture to Off.

**Drops fly through the floor.** The floor is not on a layer in **Surfaces**, or it has no collider.

**Blood splats on the character that was hit.** The character is on a layer in **Surfaces**. Move its colliders to another layer or remove that layer from **Surfaces**.

**Pools do not appear after Death.** After the delay the pool looks for the floor up to 3 m below a point 0.6 m above the body (or above `feet` when no body is passed). Make sure the floor is in **Surfaces** and the body is not far above it.

**No footprints.** Footprints start after the walker stands in a wet pool, near its edge or inside. The Blood Footprints component must sit at the feet, and **Print Count** must be above 0.

## Support

Questions and bug reports: see the publisher page on the Asset Store.
