# First-Person Movement Test

## Goal

Create a small enclosed test area where the player can walk, sprint, look around, jump repeatedly, collide with walls, climb a low step, and produce a working standalone build.

## Assumptions

- The project uses Unity 6.3 LTS unless the project version file states otherwise.
- The Unity Input System package is installed.
- Keyboard and mouse input are read from `MonoBehaviour.Update()`.
- Placeholder cubes are acceptable until gameplay is proven.
- Movement and the standalone build must be tested before the pull request is merged.

## Files created or changed

- `Assets/_Project/Scripts/Player/FirstPersonController.cs`
- `Assets/_Project/Scripts/Player/FirstPersonCameraSmoother.cs`
- `Assets/_Project/Scripts/System/FramePacingController.cs`
- `Assets/_Project/Editor/FirstPersonTestAreaBuilder.cs`
- `Documentation/Architecture/ArchitectureDecisions.md`
- `Documentation/Setup/FirstPersonMovementTest.md`

## Numbered Unity setup

Run the setup commands in this exact order. Each menu item begins with the same number shown below.

### Step 1 — Create or recreate the test area

1. Wait until Unity finishes compiling.
2. Open **Window > General > Console**.
3. Confirm there are no red compiler errors.
4. Select **Hanger 51 > Setup > 1 - Create or Recreate Test Area**.
5. Unity creates and saves `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
6. This command also configures input timing and adds the scene to the build.

Use Step 1 only when creating the scene for the first time or when intentionally replacing the entire test scene.

### Step 2 — Apply the movement and camera fix

1. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
2. Exit Play mode.
3. Select **Hanger 51 > Setup > 2 - Apply Movement and Camera Fix**.
4. Press `Ctrl+S` to save the scene.

This updates an existing Player without deleting the environment.

### Step 3 — Configure input and frame pacing

1. Select **Hanger 51 > Setup > 3 - Configure Input and Frame Pacing**.
2. Open the Console.
3. Confirm the Console reports that Dynamic Update and VSync are enabled.

This sets the Input System to **Process Events In Dynamic Update** and enables VSync for the active quality level.

### Step 4 — Add the test scene to the build

1. Select **Hanger 51 > Setup > 4 - Add Test Scene to Build**.
2. Open **File > Build Profiles**.
3. Confirm `FirstPersonMovementTest` is included and enabled.
4. Confirm it is the first scene in the build list.

A build with no enabled scene can open to an empty or unusable result. Step 4 makes the test scene the startup scene.

### Step 5 — Validate all setup

1. Keep `FirstPersonMovementTest` open.
2. Select **Hanger 51 > Setup > 5 - Validate All Setup**.
3. Open the Console.
4. Confirm the message begins with **Step 5 passed**.
5. If Unity is not version `6000.3.x`, the validator prints a version warning.

Do not create a build until Step 5 passes.

## Controls

| Control | Action |
|---|---|
| W, A, S, D | Move |
| Mouse | Look around |
| Left Shift | Sprint |
| Space | Jump |
| Escape | Release the mouse cursor |
| Left click | Lock the mouse cursor again |

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
- Skin Width: `0.08`
- Min Move Distance: `0`

### First Person Controller

#### Movement

- Walk Speed: `5`
- Sprint Speed: `8`
- Jump Height: `1.2`
- Gravity: `-24`
- Terminal Velocity: `50`

#### Ground Detection

- Ground Layers: `Everything`
- Ground Probe Distance: `0.12`
- Ground Probe Start Offset: `0.05`
- Ground Probe Radius Inset: `0.04`

#### Mouse Look

- Player Camera: `Player Camera`
- Mouse Sensitivity: `0.12`
- Vertical Look Limit: `85`
- Lock Cursor On Start: enabled

### Player Camera

The `Player Camera` must have `First Person Camera Smoother`.

- Follow Target: `Player`
- Player Controller: `Player`
- Eye Offset: `(0, 1.65, 0)`
- Position Smooth Time: `0.025`

### Game Systems

The scene must contain a root GameObject named `Game Systems` with `Frame Pacing Controller`.

- Enable VSync: enabled
- Fallback Target Frame Rate: `120`

## Movement test

1. Run Step 5 and confirm validation passes.
2. Press Play.
3. Hold W for ten seconds.
4. Add Left Shift and sprint for ten seconds.
5. Release all keys and confirm movement stops immediately.
6. Alternate A and D rapidly for ten seconds.
7. Jump five times while stationary.
8. Jump while walking.
9. Jump while sprinting.
10. Walk onto and off the low step.
11. Stop Play mode and check the Console for errors.

## Standalone build test

1. Run Step 4 again immediately before building.
2. Run Step 5 and confirm it passes.
3. Open **File > Build Profiles**.
4. Select the Windows profile.
5. Click **Build and Run**.
6. Confirm the enclosed test area appears.
7. Confirm the Player spawns inside the area.
8. Test walking, sprinting, A/D direction changes, and five jumps.
9. Press Escape to release the cursor before closing the build.

## Common problems

### The build opens but nothing appears

Run Step 4. Then open **File > Build Profiles** and confirm `FirstPersonMovementTest` is the first enabled scene.

### Movement still jitters

Run Steps 2, 3, and 5 again. Confirm the Player Camera has `First Person Camera Smoother` and the scene contains `Game Systems` with `Frame Pacing Controller`.

### The player jumps only once

Run Step 2 and save the scene. The current controller uses a stable ground probe and does not depend only on `CharacterController.isGrounded` after landing.

### The setup menu does not appear

Open **Window > General > Console** and resolve the first red compiler error. Unity does not load Editor menu scripts while compilation is failing.

### Step 5 prints a Unity version warning

The running Unity Editor is not `6000.3.x`. Open Unity Hub and check the Editor version used to open this project before continuing package or movement debugging.

## Recommended next step

Do not add aircraft interaction until Step 5 passes, the movement test is smooth, repeated jumping works, and the standalone build starts in the test scene.
