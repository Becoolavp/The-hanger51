# RNG Blood: guide for AI coding agents

This file is for coding agents (Claude Code, Codex, Cursor, Copilot and others) working in a project that uses RNG Blood. Read it before you add, change or debug anything that involves blood. The human documentation is `README.md` next to this file.

## What it is

RNG Blood is a procedural blood system for the Universal Render Pipeline. One static call per hit sprays drops that fly on a ballistic arc, splat on floors and walls, run down walls, merge into puddles, pool under bodies, leave bloody footprints and dry from glossy red to dark brown. All drops are one draw call, all stains a second one. There are no textures, no decal projectors and no runtime allocations.

- Namespace: `Rng.Blood`. Everything a game needs is on the static class `Blood`.
- Requirements: Unity 2022.3 or newer, URP 14 or newer, **Depth Texture** on in the URP asset, **Depth Texture Mode** set to **After Opaques** on the Universal Renderer. Low quality needs no Depth Texture.
- Assemblies: `Rng.Blood` (runtime), `Rng.Blood.Editor` (Setup window), `Rng.Blood.Demo` (demo only). Game code references `Rng.Blood` only. If the game uses assembly definitions, add `Rng.Blood` to its references.

| Path (inside the package folder) | Contents |
| --- | --- |
| `Runtime/Blood.cs` | the public API |
| `Runtime/BloodSettings.cs` | every tunable value, with tooltips |
| `Runtime/Resources/BloodSettings.asset` | default settings, loaded when nothing else is passed |
| `Runtime/BloodEmitter.cs`, `Runtime/BloodFootprints.cs`, `Runtime/BloodSmear.cs`, `Runtime/BloodWounds.cs` | ready components |
| `Runtime/BloodHit.cs` | `BloodHitKind` presets and the `BloodHit` event data |
| `Materials/Blood Drop.mat`, `Materials/Blood Stain.mat` | the look of fresh blood (gloss, bevel, reflections) |
| `Demo/` | Crime Scene, Hit Demo and Arena scenes, the crash test dummy, the runtime settings panel |

## Rules

1. Call `Blood.Spawn` from the code that already knows about a hit. Do not create the blood object yourself: the first call creates it.
2. Drops land only on layers in **Surfaces** (settings). Floors and walls must be on those layers and have colliders. Character colliders must **not** be on those layers, or blood splats on the character that was hit.
3. Pick a `BloodHitKind` that matches the weapon. Use `intensity` (0 to 2, default 1) to scale a hit, not a custom burst.
4. Pass the direction the shot or swing **travelled**, not the surface normal. Blood flies on with the shot.
5. For your own settings, duplicate `BloodSettings.asset` (or **Create > RNG > Blood Settings**) and call `Blood.Initialize(mySettings)` once, for example when a level loads. Do not edit the default asset for one level.
6. To change settings while the game runs, edit a copy (`Object.Instantiate(Blood.Settings)`) and pass it to `Blood.Apply(copy)`. Never write to the settings asset itself in play mode: in the editor the change stays in the asset.
7. Do not call `Blood.Shutdown` or `Blood.Initialize` per hit. Use `Blood.Clear()` on a level restart.
8. Do not modify the shaders. Tune the look on the two materials and the behaviour in the settings.
9. Do not use the `Rng.Blood.Demo` assembly from game code. Copy what you need from it instead.

## API

```csharp
using Rng.Blood;

Blood.Spawn(RaycastHit hit, Vector3 direction, float intensity = 1f, BloodHitKind kind = BloodHitKind.Bullet);
Blood.Spawn(Vector3 position, Vector3 normal, Vector3 direction, float intensity = 1f, BloodHitKind kind = BloodHitKind.Bullet, Transform bone = null);
Blood.Death(Vector3 feet, Vector3 direction, Transform body = null, BloodHitKind kind = BloodHitKind.Bullet, float poolScale = 1f);
Blood.Drip(Vector3 origin);

Blood.Emit(BloodBurst burst, Vector3 origin, Vector3 direction, float strength = 1f);
Blood.Fountain(Transform source, Vector3 axis, bool local, float seconds, float interval, BloodBurst burst);
Blood.Pool(Vector3 point, Vector3 normal, float radius, float growSeconds, float lifetime);
Blood.PoolUnder(Transform body, Vector3 fallback, float delay, float radius, float growSeconds, float lifetime);
Blood.Mark(Vector3 origin, Vector3 direction, BloodStainStyle style, float halfWidth, float stretch = 1f);
Blood.Smear(Vector3 from, Vector3 to, float width = 0.3f, float strength = 1f);
Blood.Smear(Vector3 from, Vector3 to, Vector3 normal, float width = 0.3f, float strength = 1f, bool start = true, bool end = true);
Blood.Smear(Vector3 from, Vector3 to, Vector3 normal, float width, float strength, bool start, bool end, float dryness, float wetness = 0f);
Blood.Print(Vector3 point, Vector3 normal, Vector3 heading, BloodPrintShape shape, float length, float width, bool left = false, float strength = 1f, float lifetime = 0f, float seed = 0f);
Blood.Wash(Vector3 from, Vector3 to, float radius, float amount);
bool Blood.Wet(Vector3 point, float margin = 0f);
Blood.Channels(Texture mask, Vector3 centre, Vector2 size, float depth = 1f);
Blood.Walk(Object walker, Vector3 position, Vector3 heading, bool stride);
Blood.Forget(Object walker);

Blood.Initialize(BloodSettings settings = null, Transform parent = null);
Blood.Apply(BloodSettings settings = null);
Blood.Show(bool shown, bool alternate = false);
Blood.Recolour(Color colour, Color alternateColour);
Blood.Clear();
Blood.Shutdown();
Blood.Tick(float time);

event Action<BloodHit> Blood.Hit;
event Action Blood.Cleared;
BloodSettings Blood.Settings { get; }
bool Blood.Shown { get; }
BloodField Blood.Field { get; }
```

| Call | Use it for |
| --- | --- |
| `Spawn` | one hit on a body. Blood sprays from the hit point along `direction`. At intensity 1 it also leaves a mark on the floor below. |
| `Death` | a body going down. Sprays from chest height above `feet`, then grows a pool under `body` that follows it while it exists and fades when it is destroyed. `poolScale = 0` means no pool. |
| `Drip` | one or two drops from a wound. Call it about every 0.5 s for a bleeding wound. |
| `Emit` | any `BloodBurst`, yours or one from the settings (`Blood.Settings.Shotgun`, `.Headshot`, ...). |
| `Fountain` | a burst repeated every `interval` for `seconds`, getting weaker, from a moving transform. Severed arteries. |
| `Pool`, `PoolUnder` | a growing pool at a point, or under a body after a delay. |
| `Mark` | one stain of a given style on the floor under a point. |
| `Smear` | a smear from one point to another. The short form finds the floor below the two points; pass `normal` for a wall. Width in metres; `strength` below 1 is thinner and streakier. `start` / `end` round the ends; turn them off to chain pieces into one trail. `dryness` 1 is dark, matte and broken, `wetness` 1 is as thick and glossy as a pool; wetness wins, and a chained trail blends between calls. |
| `Print` | a print of any `BloodPrintShape`: `Hand` (`left` mirrors), `Knee`, `Hoof`, `Foot`, `Tread`. `length` along `heading`; for tread pass the distance along the track as `seed`. Shares Max Footprints. |
| `Wash` | water over a capsule: call every frame it runs with `amount = Time.deltaTime / seconds`. Blood turns pink, liquid drains, clean areas drop their stains. 16 areas at once. |
| `Wet` | whether a point stands in wet blood (footsteps, sounds, slipping). |
| `Channels` | grooves in the liquid floor (needs **Liquid** on): white texels of `mask` are drains, gutters or engraved letters that pools run into and fill. The mask lies flat, `size` metres across, centred on `centre`, top of the texture towards +z. `null` removes them. Call it once the blood object exists (after `Initialize` or the first hit) and again after `Initialize` or an `Apply` that rebuilds it. |
| `Walk`, `Forget` | footprints for walkers you move yourself. Most games use the `BloodFootprints` component instead. |
| `Initialize` | switch to another settings asset. Clears all blood. |
| `Apply` | changed settings while running. Colours, drying, pace, merging, pools, footprints and bursts change at once and blood on screen stays; a changed limit or material rebuilds the blood object and clears it. |
| `Show(false)` | gore off: hides and clears all blood. `Show(true, alternate: true)` swaps to the alternate (censored) colour. |
| `Recolour` | change the blood colour of everything, including stains already on the ground. |
| `Clear` | remove all blood, for a level restart. |
| `Tick` | drive blood with your own clock (replays, cutscenes, pausing blood apart from `Time.timeScale`). Without it blood follows `Time.time`. |
| `Hit` | raised by every `Spawn` before drawing, even while blood is hidden. Data: `Position`, `Normal`, `Direction`, `Force`, `Kind`, `Bone`. Use it for dismemberment, sounds or hit marks. |
| `Cleared` | raised when blood is cleared, hidden or shut down. Clear your own blood effects here. |

Components:

| Component | Use |
| --- | --- |
| `BloodWounds` | on a character root. Each `Spawn` whose `bone` (or `RaycastHit.transform`) is inside this character leaves a wound that bleeds down the body, dries and fades. Works on `SkinnedMeshRenderer` and `MeshRenderer` without touching the character's materials. `Add(point, kind, intensity, bone)` places one by hand. 32 wounds per character; a hit inside an existing wound adds none. |
| `BloodSmear` | on something that gets dragged, near the floor. Smears after moving through a wet pool or after `Soak(metres)`; `Bleeding = true` smears wherever it moves. `Width` in metres. `AnySurface = true` smears the floor or a wall it slides along (ragdoll chest); `Forward = true` smears the surface in front of its blue axis (palm on a wall). |
| `BloodSupply` | optional, on the root of a body with a `BloodSmear` under it: litres of blood the smears spend, so the trail thins, dries and breaks as it runs out, and the `Death` pool shrinks. `Drain(litres)`, `Refill(litres = -1)`, `Litres`, `Fraction`, `Emptied`. Reads and drains an `IBloodReservoir` on the same object (the Dismemberment `BloodBody`) instead of its own count. Without it nothing changes. |
| `BloodFootprints` | on walkers, at the feet. |
| `BloodWounds.Soak(point, radius, depth, bone)` / `BloodWounds.Wash(amount)` | a patch of blood where a body lay in a pool, no hole or runs; washing blood off a body (1 = clean). |
| `BloodWounds.Stab(tip, entry, bone)` / `BloodWounds.Splat(point, radius, bone)` | for weapons: a blade coated from its tip back to where it went in; a splat on a blunt weapon where it hit, running the way down was then. The Contact `BloodBlade` calls them. |
| `BloodEmitter` | a wound point or weapon tip, for UnityEvents and Animation Events. |

Settings that change how blood is drawn:

| Setting | Effect |
| --- | --- |
| `Quality` | `High`: projected stains with wet bevelled edges. `Medium`: no bevel, cheaper. `Low`: flat stains, low-poly drops, no Depth Texture needed. For mobile use `Low`. |
| `Massive`, `MassiveSize`, `MassiveResolution` | paints floor splats into one texture that follows the camera, so thousands cost the same as one. For big battles and top-down games. Needs High or Medium. Fine spatter below one texel blurs. |
| `Liquid` | floor pools become one liquid in a texture of `MassiveSize` that follows the camera: they spread, stop when thin, merge, and wall runs drain into them. Needs High or Medium. Keep off on mobile or set `MassiveResolution` to 512. |
| `LiquidDrag`, `LiquidDragStrength` | needs `Liquid`: a `BloodSmear` dragged through liquid pushes it ahead in a tongue that runs into its smear. Off by default. |
| `WetSmears`, `WetSmearLength` | a smear leaving a pool starts as thick and glossy as the pool and thins into a normal smear over that many metres. Off by default. |

`BloodHitKind`: `Bullet`, `BulletClose` (point blank, adds back spatter and mist), `Shotgun`, `Headshot`, `Blunt`, `Sharp` (blade, long floor streak), `Explosion` (pass the feet, it sprays from chest height).

`BloodStainStyle`: `Splash`, `Rays`, `Streak`, `Heavy`, `Run`.

`BloodBurst` constructor: `new BloodBurst(countMin, countMax, coneDegrees, speedMin, speedMax, sizeMin, sizeMax, stainScale, lift, style = Splash, markStyle = Splash, markSize = 0, markStretch = 1)`. Sizes are drop radii in metres (0.01 to 0.05 is typical). `stainScale` 0 makes drops vanish in the air (mist).

## Recipes

### Hitscan weapon

```csharp
if (Physics.Raycast(ray, out RaycastHit hit, range, hitMask) && hit.collider.GetComponentInParent<Health>() != null)
    Blood.Spawn(hit, ray.direction, 1f, BloodHitKind.Bullet);
```

Pick the kind in this order: `Headshot` if the head was hit (it wins over distance), otherwise `BulletClose` under about 3 m, otherwise `Bullet`.

### Shotgun

Call `Spawn` **once per enemy hit**, with `BloodHitKind.Shotgun` and intensity 1, at the first pellet that hit that enemy. The `Shotgun` preset already is the spray of a whole shot. Do not call it once per pellet and do not add extra spawns for the other pellets.

```csharp
var hitEnemies = new HashSet<Health>();
foreach (RaycastHit pellet in pelletHits)
{
    Health enemy = pellet.collider.GetComponentInParent<Health>();
    if (enemy != null && hitEnemies.Add(enemy)) Blood.Spawn(pellet, shotDirection, 1f, BloodHitKind.Shotgun);
}
```

### Projectile

```csharp
private void OnCollisionEnter(Collision collision)
{
    if (collision.collider.GetComponentInParent<Health>() == null) return;

    ContactPoint contact = collision.GetContact(0);
    Blood.Spawn(contact.point, contact.normal, transform.forward, 1f, BloodHitKind.Bullet, collision.transform);
}
```

### Melee swing from an animation, no code

Add **Blood Emitter** (`Add Component > RNG > Blood Emitter`) to a child of the weapon at the blade, rotate it so its blue axis points the way blood should fly, set **Kind** to `Sharp` or `Blunt`, and call `Spawn()` from an Animation Event on the impact frame. `SpawnKind(int)` takes the kind as a number for events.

### Death

```csharp
Blood.Spawn(hit, shotDirection, 1f, kind);
Blood.Death(feet, shotDirection, transform, kind);
```

On the killing hit call both: `Spawn` is the wound, `Death` is the body going down (its own spray from chest height plus the pool). `feet` is the ground point under the character: `transform.position` when the pivot is at the feet, otherwise for example `new Vector3(pos.x, collider.bounds.min.y, pos.z)`. Pass the root that stays with the corpse as `body`. If the corpse is destroyed or pooled, the pool fades out.

### Bleeding wound

```csharp
private IEnumerator Bleed(Transform wound, float seconds)
{
    for (float t = 0f; t < seconds; t += 0.5f)
    {
        Blood.Drip(wound.position);
        yield return new WaitForSeconds(0.5f);
    }
}
```

For a spurting artery: `Blood.Fountain(wound, Vector3.forward, true, 3f, 0.15f, Blood.Settings.Gout);`

### Footprints

Add **Blood Footprints** to anything that walks, on a transform at its feet. Walkers that step into a wet pool leave fading prints. Nothing else to call.

### Wounds on characters

Add **Blood Wounds** to the character root once. With `Blood.Spawn(hit, direction, ...)` on the character's own collider, the wound appears by itself. If your `Spawn` uses a point inside the body or at the exit, add the entry wound yourself:

```csharp
wounds.Add(hit.point, BloodHitKind.Bullet, 1f, hit.transform);
```

### Dragged body and hand smears

```csharp
var smear = corpse.hips.gameObject.AddComponent<BloodSmear>();
smear.Bleeding = true;

Blood.Smear(handTop, handBottom, wallHit.normal, 0.04f, 0.9f);
```

For a hand print, call `Smear` four times with 0.04 m wide strokes 5 cm apart.

A body that bleeds out as it is dragged, with the pool and the trail blending:

```csharp
var supply = corpse.root.gameObject.AddComponent<BloodSupply>(); // 5 litres by default
supply.Emptied += () => Debug.Log("bled out");
var settings = Object.Instantiate(Blood.Settings);
settings.Liquid = true;
settings.LiquidDrag = true;
settings.WetSmears = true;
Blood.Apply(settings);
```

### Mobile

Set `Quality` to `Low` in the settings used on mobile (for example a second settings asset passed to `Blood.Initialize` on mobile builds), lower `MaxSplats` to about 200 and `MaxPools` to about 40. Depth Texture can then stay off.

### Big battles

Turn on `Massive`. Set `MassiveSize` to the area the camera shows plus a margin (40 m to 80 m) and `MassiveResolution` to 2048. Raise `MaxDrops`; `MaxSplats` matters only for walls then.

### Gore option in a settings menu

```csharp
Blood.Show(goreSetting != Gore.Off, alternate: goreSetting == Gore.Censored);
```

Apply it at startup and whenever the option changes. While hidden, `Spawn` still raises `Hit`.

### Different blood for one enemy type

Blood colour is global. For an alien level, call `Blood.Recolour(alienGreen, Blood.Settings.AlternateColour)` when the level loads and `Blood.Recolour(Blood.Settings.Colour, Blood.Settings.AlternateColour)` when it ends.

### Tuning while the game runs

The Hit Demo has a runtime panel with every setting (`Demo/BloodTuner.cs`, press Tab). Add the `BloodTuner` component to any scene and call `Show(true)` to tune in the game itself; **Save as asset** writes a new settings asset in the editor. In code:

```csharp
BloodSettings tuned = Object.Instantiate(Blood.Settings);
tuned.DropScale = 1.6f;
tuned.StainLifetime = 120f;
Blood.Apply(tuned);
```

### Dismemberment or other add-ons

```csharp
Blood.Hit += hit =>
{
    if (hit.Kind == BloodHitKind.Explosion && hit.Bone != null) Sever(hit.Bone, hit.Direction * hit.Force);
};
```

Spray the stump with `Blood.Fountain(stump, Vector3.up, true, 4f, 0.2f, Blood.Settings.Gout)` and leave a pool with `Blood.PoolUnder`.

## Behaviour details

- The blood object is created by the first call that needs it (`Spawn`, `Death`, `Show`, `Settings`, ...). It lives on a `DontDestroyOnLoad` object, so blood, settings and the shown state survive scene loads. Call `Blood.Clear()` when a level restarts if old blood should go.
- `Blood.Initialize` and `Blood.Shutdown` reset the shown state to visible red. Apply your gore option again after calling either.
- `Show(true)` (alternate false) returns from the censored colour to normal blood.
- While blood is hidden, `Spawn`, `Death` and `Drip` draw nothing; `Spawn` still raises `Hit`.
- `intensity` scales the drop count and speed. A burst leaves its floor mark only at intensity 1 or above, and only presets with a mark have one (`Headshot` rays, `Sharp` streak, `Explosion`).
- `Spawn(RaycastHit, ...)` passes `hit.transform` as the bone, so `BloodHit.Bone` is the transform that was hit.
- The hit point may be right on the character's collider: drops never land on the character as long as its colliders are not on a **Surfaces** layer. The same goes for `Drip`.
- Pool radius after `Death` comes from the settings per kind: **Pool Radius Bullet** (also `BulletClose` and `Shotgun`), **Headshot**, **Sharp**, **Blunt**, **Explosion**, times `poolScale`.
- A tag used with `CompareTag` must exist in the Tag Manager. For headshots either add a `Head` tag or check the hit collider or bone name.

## Mobile and low-end targets

Use **Quality** `Low`, lower **Max Splats** (about 200) and **Max Pools** (about 40), shorten **Stain Lifetime**, keep **Max Drops** at 256 to 512. With Low the Depth Texture is not needed, which removes the biggest fixed cost.

## Checking your work

- In the editor, `Rng.Blood.EditorTools.BloodSetup.Check()` returns the setup problems; `BloodSetup.FixAll()` fixes what it can. The same checks are in **Tools > RNG Blood > Setup**.
- At runtime `Blood.Field` has counters: `DropsEmitted`, `DropsLanded`, `StainsPlaced`, `WallStains`, `PoolsAlive`, `PuddlesAlive`, `PrintsPlaced`, `LastLanding`. If `DropsEmitted` grows and `DropsLanded` stays 0, the floor is not in **Surfaces** or has no collider.

## Troubleshooting

| Symptom | Cause and fix |
| --- | --- |
| Drops fly but leave no stains | Depth Texture off, Depth Texture Mode not After Opaques, or a camera overrides Depth Texture to Off. Run Setup, Fix All. |
| Drops fall through the floor | Floor not on a **Surfaces** layer or no collider. |
| Blood splats on the character that was hit | Character colliders are on a **Surfaces** layer. |
| Red bands with straight edges on a body lying on the floor | Floor splats painting it from below. Lower **Stain Rise** in the settings (5 cm by default); raise it if splats should climb bumpy ground. |
| No pool after `Death` | The pool searches up to 3 m below a point 0.6 m above the body. Floor must be in **Surfaces**. |
| Nothing at all, error "No BloodSettings with both materials" | The settings asset lost its materials. Assign `Materials/Blood Drop` and `Materials/Blood Stain`. |
| Blood is too bright or orange in a bright scene | Lower **Environment Reflections** on the materials or lower **Colour** in the settings. |
