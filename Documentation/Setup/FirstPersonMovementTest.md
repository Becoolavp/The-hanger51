# First-Person Movement Test

## Goal

Create a small enclosed test area where the player can walk, sprint, look around, jump repeatedly, collide with walls, climb a low step, and test narrow spaces.

## Assumptions

- The project uses the Unity Input System.
- Keyboard and mouse input is read from `MonoBehaviour.Update()`.
- Placeholder cubes are acceptable until gameplay is proven.
- Movement must be playtested before the pull request is merged.

## Files created or changed

- `Assets/_Project/Scripts/Player/FirstPersonController.cs`
- `Assets/_Project/Editor/FirstPersonTestAreaBuilder.cs`
- `Documentation/Architecture/ArchitectureDecisions.md`
- `Documentation/Setup/FirstPersonMovementTest.md`

## Unity Editor setup

### 1. Confirm the Input System update mode

1. Open **Edit > Project Settings**.
2. Select **Input System Package**.
3. Set **Update Mode** to **Process Events In Dynamic Update**.
4. Do not use **Process Events In Fixed Update** with this controller because the controller reads keyboard and mouse state in `Update()`.

You can validate the setting with:

**Hanger 51 > Setup > Validate First-Person Project Settings**

A successful validation prints the actual Unity Editor version and the selected Input System update mode to the Console.

### 2. Create the test scene

1. Wait until Unity finishes compiling with no red Console errors.
2. Select **Hanger 51 > Setup > Create First-Person Test Area**.
3. Unity creates and saves:
   - `Assets/_Project/Scenes/FirstPersonMovementTest.unity`
4. The generated `Player` object is selected automatically.

### 3. Update an existing test player

1. Open `Assets/_Project/Scenes/FirstPersonMovementTest.unity`.
2. Exit Play mode.
3. Select **Hanger 51 > Setup > Apply First-Person Controller Defaults**.
4. Save the scene with **File > Save** or `Ctrl+S`.

The older **Apply First-Person Smoothing Defaults** menu remains as a temporary compatibility shortcut and performs the same action.

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
- Skin Width: `0.04`
- Min Move Distance: `0`

### First Person Controller

- Walk Speed: `5`
- Sprint Speed: `8`
- Jump Height: `1.2`
- Gravity: `-24`
- Grounded Velocity: `-2`
- Terminal Velocity: `50`
- Player Camera: `Player Camera`
- Mouse Sensitivity: `0.12`
- Vertical Look Limit: `85`
- Lock Cursor On Start: enabled

## How to test it

1. Run **Hanger 51 > Setup > Validate First-Person Project Settings**.
2. Confirm the Console reports **Process Events In Dynamic Update**.
3. Open the movement test scene and press Play.
4. Hold W, then release it. Movement should start and stop immediately without sliding.
5. Sprint for ten seconds and confirm the view does not become progressively jittery.
6. Alternate A and D rapidly and confirm direction changes are immediate.
7. Jump at least five times while stationary.
8. Jump while walking and sprinting.
9. Walk onto the low step and through the narrow passage.
10. Stop Play mode and check the Console for errors or warnings.

## Common problems

### Movement jitters and jump presses are missed

Check **Edit > Project Settings > Input System Package > Update Mode**. It must be **Process Events In Dynamic Update** for this controller.

### Movement feels like ice

Run **Hanger 51 > Setup > Apply First-Person Controller Defaults**. The current controller intentionally has no acceleration or deceleration system.

### The player jumps only once

Confirm the latest controller is compiled and that **Grounded Velocity** is `-2`. Also validate the Input System update mode.

### The validation command reports the wrong input mode

Change the setting manually in **Edit > Project Settings > Input System Package**, save the project, and run the validation command again.

### The Hanger 51 menu does not appear

Open **Window > General > Console** and resolve the first red compiler error. Unity does not load Editor menu scripts when compilation fails.

## Recommended next step

Do not add aircraft interaction until sustained sprinting, rapid A/D direction changes, and at least five repeated jumps are smooth and reliable.
