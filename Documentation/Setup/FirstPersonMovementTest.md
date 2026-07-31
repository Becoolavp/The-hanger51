# First-Person Movement Test

## Goal

Create a small enclosed test area where the player can walk, sprint, look around, jump, collide with walls, climb a low step, and test narrow spaces.

## Assumptions

- The Unity project is opened in Unity 6.3 LTS.
- This is a PC-first prototype using keyboard and mouse.
- The Unity Input System package is installed.
- Placeholder cubes are acceptable until gameplay is proven.
- Movement feel must be validated inside Unity before the pull request is merged.

## Files created or changed

- `Assets/_Project/Scripts/Player/FirstPersonController.cs`
- `Assets/_Project/Editor/FirstPersonTestAreaBuilder.cs`
- `Documentation/Architecture/ArchitectureDecisions.md`
- `Documentation/Setup/FirstPersonMovementTest.md`

## Unity Editor setup

### 1. Install the Input System

1. Open Unity.
2. Select **Window > Package Management > Package Manager**.
3. Change the package source to **Unity Registry**.
4. Search for **Input System**.
5. Select it and click **Install**.
6. If Unity asks to enable the new input back end and restart, accept the restart.

After restarting, confirm this setting:

1. Select **Edit > Project Settings > Player**.
2. Expand **Other Settings > Configuration**.
3. Set **Active Input Handling** to **Input System Package (New)**.

### 2. Create the test scene

1. Wait until Unity finishes compiling with no red Console errors.
2. Select **Hanger 51 > Setup > Create First-Person Test Area**.
3. Unity creates and saves `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
4. The generated `Player` object is selected automatically.

### 3. Update an existing test scene

After pulling controller improvements, update the existing player without rebuilding the scene:

1. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
2. Exit Play mode.
3. Select **Hanger 51 > Setup > Apply First-Person Smoothing Defaults**.
4. Save the scene with **File > Save** or `Ctrl+S`.

This command corrects the test player's starting height and applies the current Character Controller, movement-response, jump, and ground-probe values.

### 4. Save Unity-generated metadata

Unity creates `.meta` files for the folders, scripts, and scene. Commit those files to GitHub with the scene after the test succeeds.

## Controls

| Control | Action |
|---|---|
| W, A, S, D | Move |
| Mouse | Look around |
| Left Shift | Sprint |
| Space | Jump |
| Escape | Release the mouse cursor |
| Left click | Lock the mouse cursor again |

## How to test it

1. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
2. Press **Play**.
3. Stand still on the center of the flat floor and jump five times.
4. Confirm every jump rises and falls in one continuous arc.
5. Hold W for ten seconds at walking speed.
6. Continue holding W and add Left Shift for ten seconds.
7. Confirm the view does not become progressively more jittery.
8. Release all movement keys and confirm the player comes to a smooth stop.
9. Hold A for one second, then D for one second, and repeat for at least ten direction changes.
10. Confirm each reversal is smooth and does not create a periodic camera shake.
11. Move diagonally and confirm it is not faster than straight movement.
12. Jump while walking and while sprinting.
13. Walk off the low step without jumping and confirm the player falls cleanly.
14. Walk into every outer wall and confirm the player cannot pass through it.
15. Walk onto the low step and confirm the Character Controller climbs it.
16. Walk between the two narrow-passage walls and confirm collision feels consistent.
17. Look fully up and down and confirm the camera stops before flipping upside down.
18. Stop Play mode and check the Console for errors or warnings.

## Expected Inspector settings

Select the `Player` object.

### Transform

- Position Y in the generated test scene: `0.02`

### Character Controller

- Height: `2`
- Radius: `0.35`
- Center: `(0, 1, 0)`
- Step Offset: `0.3`
- Slope Limit: `45`
- Skin Width: `0.04`
- Min Move Distance: `0`

### First Person Controller

#### Movement Speed

- Walk Speed: `5`
- Sprint Speed: `8`

#### Movement Response

- Ground Acceleration: `24`
- Ground Deceleration: `30`
- Direction Change Acceleration: `28`
- Air Acceleration: `8`
- Air Deceleration: `3`

#### Jump and Gravity

- Jump Height: `1.2`
- Gravity: `-24`
- Terminal Velocity: `50`

#### Ground Probe

- Ground Layers: `Everything`
- Ground Probe Distance: `0.06`
- Ground Probe Start Offset: `0.05`
- Ground Probe Radius Inset: `0.03`

#### Mouse Look

- Player Camera: `Player Camera`
- Mouse Sensitivity: `0.12`
- Vertical Look Limit: `85`
- Lock Cursor On Start: enabled

## Common problems

### `The type or namespace name 'InputSystem' could not be found`

The Input System package is not installed. Follow the package installation steps above.

### The player does not respond to keyboard or mouse input

Check **Edit > Project Settings > Player > Other Settings > Configuration > Active Input Handling**. It must include the new Input System.

### The Hanger 51 menu does not appear

Open **Window > General > Console** and resolve all red compiler errors. Unity does not load Editor scripts when compilation fails.

### The smoothing-default command cannot find the Player

The active scene must contain a GameObject named exactly `Player` with:

- `Character Controller`
- `First Person Controller`
- A child Camera

### Movement still becomes progressively jittery

Open the Game view's **Stats** overlay and note the FPS while the jitter occurs. Also check whether the Player Transform Y value visibly changes while strafing on the flat floor. Those two observations separate frame-pacing problems from controller-height corrections.

### Direction changes feel too sharp

Lower **Direction Change Acceleration** in small increments, such as `28` to `24`.

### Direction changes feel too sluggish

Raise **Direction Change Acceleration** in small increments, such as `28` to `32`.

### Jumping still appears jittery

Test completely stationary on the center of the flat floor. If the jump is smooth there but rough near the low step, the remaining problem is step collision rather than the jump arc. Confirm that **Ground Probe Distance** is `0.06`, not the older `0.12` value.

## Recommended next step

Do not add interaction or aircraft systems until walking, stopping, sprinting, repeated A/D direction changes, and jumping remain consistently smooth for at least thirty seconds.
