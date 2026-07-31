# First-Person Movement Test

## Goal

Create a small enclosed test area where the player can walk, sprint, look around, jump, collide with walls, climb a low step, and test narrow spaces.

## Assumptions

- The Unity project is opened in Unity 6.3 LTS.
- This is a PC-first prototype using keyboard and mouse.
- The Unity Input System package is installed.
- Placeholder cubes are acceptable until gameplay is proven.
- This feature has not yet been validated inside the Unity Editor. It must be playtested before merging.

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

### 3. Save Unity-generated metadata

Unity will create `.meta` files for the new folders, scripts, and scene. Commit those files to GitHub with the scene after the test succeeds.

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
3. Move forward, backward, left, and right.
4. Move diagonally and confirm it is not faster than straight movement.
5. Hold **Left Shift** and confirm sprinting is faster than walking.
6. Press **Space** while grounded and confirm the player jumps once.
7. Walk into every outer wall and confirm the player cannot pass through it.
8. Walk onto the low step and confirm the Character Controller climbs it.
9. Walk between the two narrow-passage walls and confirm collision feels consistent.
10. Look fully up and down and confirm the camera stops before flipping upside down.
11. Press **Escape**, then left-click the Game view to recapture the mouse.
12. Stop Play mode and check the Console for errors or warnings.

## Expected Inspector settings

Select the `Player` object.

### Character Controller

- Height: `2`
- Radius: `0.35`
- Center: `(0, 1, 0)`
- Step Offset: `0.3`
- Slope Limit: `45`
- Skin Width: `0.08`

### First Person Controller

- Player Camera: `Player Camera`
- Walk Speed: `5`
- Sprint Speed: `8`
- Jump Height: `1.2`
- Gravity: `-20`
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

### The mouse is trapped in the Game view

Press **Escape** to release it.

### The player falls through the floor

Confirm that the generated `Floor` object still has its `Box Collider` and that the `Player` still has its `Character Controller`.

### Movement feels too fast, slow, or sensitive

Select the `Player` and adjust the serialized values on `First Person Controller`. Change one value at a time and retest.

## Recommended next step

After this scene passes the full test, commit the Unity-generated scene and `.meta` files. The next small feature should be a visible interaction prompt and a single test object that the player can inspect or pick up.
