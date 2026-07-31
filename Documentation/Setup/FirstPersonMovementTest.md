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
3. Unity creates and saves:
   - `Assets/_Project/Scenes/FirstPersonMovementTest.unity`
4. The generated `Player` object is selected automatically.

### 3. Update an existing test scene

After pulling controller improvements, update the existing player without rebuilding the scene:

1. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
2. Exit Play mode.
3. Select **Hanger 51 > Setup > Apply First-Person Smoothing Defaults**.
4. Save the scene with **File > Save** or `Ctrl+S`.

This command corrects the test player's starting height, applies the recommended Character Controller values, and applies the current movement-smoothing and ground-probe values.

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
3. Start and stop repeatedly. Confirm speed eases in and out without feeling delayed.
4. Change direction quickly and confirm there is no sharp visual snap.
5. Move diagonally and confirm it is not faster than straight movement.
6. Hold **Left Shift** and confirm sprinting is faster than walking.
7. Jump several times while standing still.
8. Jump while walking and while sprinting.
9. Confirm the jump arc rises and falls smoothly with no repeated vertical shaking.
10. Walk off the low step without jumping and confirm the player falls cleanly.
11. Walk into every outer wall and confirm the player cannot pass through it.
12. Walk onto the low step and confirm the Character Controller climbs it.
13. Walk between the two narrow-passage walls and confirm collision feels consistent.
14. Look fully up and down and confirm the camera stops before flipping upside down.
15. Press **Escape**, then left-click the Game view to recapture the mouse.
16. Stop Play mode and check the Console for errors or warnings.

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

#### Movement Smoothing

- Ground Acceleration: `30`
- Ground Deceleration: `40`
- Air Acceleration: `10`
- Air Deceleration: `4`

#### Jump and Gravity

- Jump Height: `1.2`
- Gravity: `-24`
- Ground Stick Velocity: `-1.5`
- Terminal Velocity: `50`

#### Ground Probe

- Ground Layers: `Everything`
- Ground Probe Distance: `0.12`
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

### The mouse is trapped in the Game view

Press **Escape** to release it.

### The player falls through the floor

Confirm that the generated `Floor` object still has its `Box Collider` and that the `Player` still has its `Character Controller`.

### Movement feels too slow to respond

Increase **Ground Acceleration** in small increments, such as `30` to `35`. Higher values respond faster. Lower values feel softer.

### Movement stops too slowly

Increase **Ground Deceleration** in small increments, such as `40` to `45`.

### Jumping still appears jittery

Test the jump while completely stationary on the center of the flat floor. If it is smooth there but rough near the low step, the remaining problem is step collision rather than the jump arc. Also confirm the Game view is focused and the Console is not rapidly printing messages every frame.

## Recommended next step

Do not add interaction or aircraft systems until walking, stopping, sprinting, and jumping feel consistently smooth on the flat floor and around the low step.
