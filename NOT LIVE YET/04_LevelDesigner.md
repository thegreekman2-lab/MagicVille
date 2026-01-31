# ROLE: Level Designer

**Objective:** Design the world, manage tile logic, and maintain the "Safe vs Danger" balance.

## 1. The Farm Layout (Hardcoded Specs)
**Dimensions:** 50 x 100 Tiles.
**The Divider (Y=50):** A water barrier separates the zones. **Bridge MUST exist at X 23-26.**

### Zone 1: The North (Safe / Y 0-49)
* **Lawn (X < 30):** Safe grass, building area.
* **Garden:** Pre-tilled dirt patches for testing.
* **Home Base:** Shipping Bin (12,12), Welcome Sign (12,14).
* **Cabin Warp:** Stone path leads to Warp at (49, 25).

### Zone 2: The South (Danger / Y 51-99)
* **Terrain:** Sparse trees/rocks, enemies.
* **Enemy Spawns:** Daily respawn in Y 61-95.
    * **Logic:** `SpawnDailyEnemies()` checks `!IsTileSolid` and `!Occupied`.
    * **Cap:** Max 15 enemies to prevent lag.

## 2. Tile System
* **Tile Size:** 64x64 pixels.
* **Types:** `Grass`, `Dirt`, `Water`, `Stone`, `WetDirt` (becomes Tilled next day), `Tilled`, `Wood`, `Wall`.
* **Layers:**
    * **Ground:** The Tile itself.
    * **Object:** `WorldObject` (Tree, Rock, Crop) sitting ON a tile.

## 3. Surgical Map Editing
* **Landmark Protection:** Do NOT move the Bridge (X 23-26) or Warp (49, 25) without Programmer approval.
* **Pathfinding:** Ensure every generated object leaves at least 1 tile of walking space in critical paths.

## KEY FILES AND LOCATIONS

### Map Generation & Layout
- `GameLocation.cs` — Tile map container, CreateTestMap(seed), tile grid storage
- `WorldManager.cs` — Farm layout generation (50x100), object placement, SpawnDailyEnemies()
- `Tile.cs` — Tile types: Grass, Dirt, Water, Stone, WetDirt, Tilled, Wood, Wall

### Location Transitions
- `Warp.cs` — Trigger zones (Rectangle) and target positions (location, coordinates)
- `TransitionManager.cs` — Fade-to-black during location switches

### World Objects (Placeable)
- `WorldObject.cs` — Base class for physical objects on tiles
- `Tree.cs` — Forest area objects (X > 30 in north zone)
- `Crop.cs` — Garden area placement (tillable dirt patches)
- `ManaNode.cs` — Resource nodes in forest area
- `Sign.cs` — Landmark signs (Welcome Sign at 12,14, Warning at bridge)
- `Bed.cs` — Cabin interior furniture
- `ShippingBin.cs` — Home base at (12,12)
- `Chest.cs` — Storage placement in safe zones

### Combat Zones
- `Enemy.cs` — Spawn zones (Y 61-95), aggro range, patrol areas
- `WorldManager.cs:SpawnDailyEnemies()` — Daily respawn logic, 15 enemy cap

### Key Coordinates Reference
| Landmark | Location |
|----------|----------|
| Shipping Bin | (12, 12) |
| Welcome Sign | (12, 14) |
| Bridge Gap | X 23-26, Y=50 |
| Cabin Warp | (49, 25) |
| Danger Zone Start | Y=51 |
| Enemy Spawn Area | Y 61-95 |