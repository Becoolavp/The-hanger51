# Merlin V-1650 Assembly Architecture

## Status

Prototype implementation on `agent/merlin-engine-assembly`.

## Purpose

Establish the first complete aircraft-maintenance assembly loop using the existing first-person controller and inventory system.

The player can:

1. Pick up a major engine assembly.
2. Carry installable parts as inventory data.
3. approach a compatible maintenance stand;
4. select a part in the inventory;
5. use a context-sensitive Install action;
6. see the installed state change visibly;
7. continue through a required assembly sequence.

## Decisions

### Installation state belongs to the station

`EngineAssemblyStation` owns:

- required item references;
- engine-block installed state;
- cylinder-cover count;
- spark-plug count;
- installed visual activation;
- sequence validation;
- progress and prompt text.

The inventory UI does not own engine state. It only forwards the selected slot to the active station.

### The existing inventory remains the source of item quantities

Installation consumes one unit through `PlayerInventory.TryRemoveFromSlot`.

This prevents the engine feature from maintaining a second, conflicting inventory count.

### The interaction ray establishes installation context

`InventoryInteractor` now recognizes both:

- `InventoryPickup`;
- `EngineAssemblyStation`.

Aiming at a station before opening inventory establishes the active installation target. The player remains input-blocked while the inventory is open, so that target remains stable during the UI action.

### Install-only items use item metadata

`InventoryItemDefinition` now includes:

- `CanEquip`;
- `WorldPrefab`.

The engine block and cylinder covers are marked install-only. Spark plugs remain equippable.

### Item-specific world models are reusable

`InventoryItemDropper` uses `WorldPrefab` when available. This means generated aircraft parts retain their recognizable model when dropped and repicked instead of reverting to colored cubes.

`EquippedItemView` also uses the world prefab for equippable items, scaled for first-person viewing.

### The assembly order is intentionally constrained

The current prototype sequence is:

1. engine block;
2. two cylinder-bank covers;
3. 24 spark plugs.

This sequence is a gameplay teaching path rather than a claim that every real maintenance procedure must occur in exactly this order.

### Art is procedural for the prototype

The editor setup tool generates the V-1650-style parts from Unity primitives and reusable materials.

The engine includes recognizable high-level features:

- long liquid-cooled V-12 proportions;
- two six-cylinder banks at approximately 60 degrees total bank angle;
- crankcase and sump;
- front reduction-gear housing and output shaft;
- rear supercharger and intake mass;
- intake runners;
- exhaust stubs;
- ignition rails;
- coolant crossover;
- magnetos and visible fasteners.

The procedural models are placeholders for future authored meshes, but they are detailed enough to validate scale, recognition, interaction, UI, inventory, installation, and standalone builds.

## Generated assets

The editor setup creates assets under:

- `Assets/_Project/EngineAssembly/Prefabs`
- `Assets/_Project/EngineAssembly/Materials`
- `Assets/_Project/Inventory/Items`

The generated assets should be committed after Unity creates them and their `.meta` files.

## Build requirement

Every completed test pass must include:

1. Merlin assembly validation;
2. Build Step 1;
3. Build Step 2;
4. Build Step 3;
5. standalone pickup and installation testing.

## Future extensions

Likely next steps after this prototype passes:

- authored Blender or CAD-derived engine-part meshes;
- installation animations and hand/tool motion;
- fastener-specific actions;
- torque values and tightening patterns;
- tool requirements;
- part condition and wear;
- incorrect-part and incorrect-sequence feedback;
- removal/disassembly flow;
- persistent save data for station state;
- reusable assembly recipes for other aircraft systems.
