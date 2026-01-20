# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
dotnet restore    # Restore NuGet packages
dotnet build      # Build the project
dotnet run        # Build and run the game
```

## Project Overview

MagicVille is a 2D farming RPG built with MonoGame targeting .NET 8.0 (DesktopGL/OpenGL backend). Inspired by Stardew Valley.

## Architecture

### Core Game Loop
- **Program.cs**: Entry point
- **Game1.cs**: MonoGame Game class with FSM state management (Playing/Inventory/Shipping)
- **WorldManager.cs**: Central orchestrator - manages game state, update loop, rendering, and tool interaction
- **InventoryMenu.cs**: Inventory UI with drag-and-drop (View-Model pattern)
- **ShippingMenu.cs**: Shipping bin UI for selling items (Stardew-style, v2.9)

### World & Tiles
- **GameLocation.cs**: Tile map container with name, tiles, and warp points
- **Tile.cs**: Tile struct with ID and walkability. Types: Grass, Dirt, Water, Stone, WetDirt, Tilled, Wood, Wall
- **Camera2D.cs**: 2D camera with dynamic viewport support for window resizing

### Multi-Location System (v2.4)
- **WorldManager.Locations**: `Dictionary<string, GameLocation>` keeps all maps in memory
- **Warp.cs**: Defines trigger zones and target positions for location transitions
- **TransitionManager.cs**: State machine for fade-to-black transitions
- Locations: "Farm" (20x20 outdoor), "Cabin" (10x10 indoor)
- State preserved when switching locations (no save/load needed for doors)

### Player & Animation
- **Player.cs**: Player entity with WASD movement, sprite animation, feet-based position
- **SpriteAnimator.cs**: Handles spritesheet animation with direction-based rows (Down, Up, Left, Right)

### World Objects & Rendering
- **WorldObject.cs**: Physical objects (rocks, trees, bushes, mana nodes) with collision
- **IRenderable.cs**: Interface for Y-sortable entities (Player, WorldObject)
- All sprites use **bottom-center origin** (Position = feet location)
- **Feet-only collision**: Collision boxes cover ~20% of sprite height for 2.5D overlap
- **Y-sorting**: Entities sorted by `SortY` (feet Y position) for correct depth ordering

### Item System (Polymorphic)
- **Item.cs**: Abstract base class with `[JsonPolymorphic]` for serialization
- **Tool.cs**: Tools with ResourceCost, PowerLevel, AffectsTileThroughObjects (Hoe, Axe, Pickaxe, Watering Can, Scythe, Earth Wand, Hydro Wand)
- **Material.cs**: Stackable items with Quantity and MaxStack (Wood, Stone, Corn, Tomato, etc.)
- **Inventory.cs**: 10-slot hotbar with stacking logic, slot selection, and `AddItem()` returns bool for full inventory handling

### Input & Interaction
- **InputManager.cs**: Mouse/keyboard state tracking, screen-to-world coordinate conversion
- Tool interaction uses range checking (96px) with visual reticle feedback

### Save System
- **SaveData.cs**: DTO for serializable game state
- **TileSaveData.cs**: DTO for modified tile positions and IDs
- **SaveManager.cs**: JSON save/load with polymorphic Item support

### Time System (v2.3)
- **TimeManager.cs**: Static class managing global game time
- Time format: Military (0600 = 6 AM, 2400 = Midnight, 2600 = 2 AM end of day)
- Tick rate: 7 real seconds = 10 in-game minutes
- `OnTenMinutesPassed` event for crop growth / machine processing
- Day/night overlay colors via `GetNightOverlayColor()`

> **TODO**: Sunset colors need artistic tuning. v3 will use RenderTarget-based lighting for proper color grading.

## Key Patterns

### Game State FSM (v2.8+)
High-level game flow control using Finite State Machine:
```csharp
public enum GameState { Playing, Inventory, Shipping }
public GameState CurrentState { get; private set; }

protected override void Update(GameTime gameTime)
{
    switch (CurrentState)
    {
        case GameState.Playing:
            // World updates, player moves, time passes
            // E/Tab → transition to Inventory
            // Click ShippingBin → transition to Shipping
            _world.Update(gameTime);
            break;

        case GameState.Inventory:
            // World PAUSED (frozen background)
            // Only UI updates (drag-and-drop)
            // E/Tab/Esc → transition to Playing
            _inventoryMenu.Update(Mouse.GetState());
            break;

        case GameState.Shipping:
            // World PAUSED, shipping menu active
            // Drag items to bin slot to sell
            // E/Tab/Esc → finalize and return to Playing
            _shippingMenu.Update(Mouse.GetState());
            break;
    }
}
```

### Inventory UI - View-Model Pattern (v2.8)
```csharp
public class InventoryMenu
{
    // VIEW: References MODEL, doesn't store its own copy
    private readonly Player _player;
    private Inventory PlayerInventory => _player.Inventory;

    // Drag-and-drop state
    private Item? _heldItem;
    private int _sourceIndex = -1;

    // OnMouseDown: Pick up item
    _heldItem = PlayerInventory.GetSlot(slotIndex);
    PlayerInventory.SetSlot(slotIndex, null);

    // OnMouseUp: Place or snap-back
    if (targetIndex >= 0)
        PlayerInventory.SetSlot(targetIndex, _heldItem); // Swap
    else
        PlayerInventory.SetSlot(_sourceIndex, _heldItem); // Snap-back
}
```

### Tool Interaction Flow
1. `InputManager.GetMouseTilePosition(camera)` converts screen → tile coordinates
2. Range check against player position (96px / ~1.5 tiles)
3. `WorldManager.InteractWithTile()` checks for objects first, then tiles
4. Object interaction based on tool type (polymorphic dispatch)
5. **Critical Check**: `tool.AffectsTileThroughObjects`
   - `false` (default): Tool stops at object (Pickaxe, Axe, Scythe)
   - `true`: Tool continues to modify tile (Watering Can, Hydro Wand)
6. Tile type is modified (e.g., Grass → Tilled, Tilled → WetDirt)

### Tool Pass-Through Property (v2.6)
```csharp
public class Tool : Item
{
    // If true, tool effect passes through objects to also affect the tile
    public bool AffectsTileThroughObjects { get; init; } = false;
}

// Example: Watering Can waters crop AND wets soil
new Tool("watering_can", ..., affectsTileThroughObjects: true);
```

### Coordinate Conversion
- Camera stores `GraphicsDevice` reference (not Viewport) to support dynamic window resizing
- `Camera2D.ScreenToWorld()` uses inverted transform matrix
- Always reads `GraphicsDevice.Viewport` for current dimensions

### World ↔ Grid Coordinate Bridge (v2.6)
Objects use "smart alignment" with pixel offsets, but tile lookups need grid coordinates:
```csharp
// WorldObject.GetGridPosition() bridges Visual World → Data World
public Point GetGridPosition()
{
    const int TileSize = GameLocation.TileSize; // 64
    float centerX = Position.X;                  // Already at horizontal center (bottom-center pivot)
    float centerY = Position.Y - (Height / 2f);  // Vertical center of object
    return new Point((int)(centerX / TileSize), (int)(centerY / TileSize));
}

// Usage in Crop.OnNewDay:
Point gridPos = GetGridPosition();
Tile tile = location.GetTile(gridPos.X, gridPos.Y);
bool tileIsWet = (tile.Id == Tile.WetDirt.Id);
```

### Smart Object Alignment (v2.6)
```csharp
// WorldManager.GetAlignedPosition(tileX, tileY, objWidth, objHeight)
// Uses height to determine alignment:
if (objHeight <= TileSize)
{
    // Small objects: center vertically (e.g., rocks, seeds, bushes)
    y = (tileY * TileSize) + (TileSize / 2f) + (objHeight / 2f);
}
else
{
    // Tall objects: align to bottom (e.g., trees, buildings)
    y = (tileY * TileSize) + TileSize;
}
// X is always centered: (tileX * TileSize) + (TileSize / 2f)
```

### Two-Pass Day Processing (v2.6)
**CRITICAL**: Order of operations matters for crop growth:
```csharp
private void OnDayPassed(int newDay)
{
    // PASS 1: GROWTH PHASE - Tiles still wet
    foreach (var (locationName, location) in Locations)
    {
        if (LocationObjects.TryGetValue(locationName, out var objects))
        {
            ProcessObjectsNewDay(location, objects);
            // Crops check tile.Id == Tile.WetDirt.Id here
        }
    }

    // PASS 2: EVAPORATION PHASE - Dry tiles after processing
    foreach (var (locationName, location) in Locations)
    {
        ResetLocationTiles(location); // WetDirt → Tilled
    }
}
```

### Harvest Transaction System (v2.7)
Data-driven crop harvesting with inventory management:

```csharp
// Crop.cs - Harvest Properties
public string HarvestItemId { get; set; }        // "tomato", "corn", etc.
public int HarvestQuantity { get; set; } = 1;   // Items per harvest
public bool Regrows { get; set; } = false;       // Regrows after harvest?
public CropStage HarvestResetStage { get; set; } // Stage to reset to if Regrows

// Creates harvest item from configured data
public Item? GetHarvestDrop()
{
    // TODO: Replace with Item Database lookup in v3
    return new Material(
        registryKey: HarvestItemId,
        name: DisplayName,
        description: $"Fresh {DisplayName} from the farm.",
        quantity: HarvestQuantity
    );
}
```

```csharp
// WorldManager.TryHarvestCrop() - Transaction Flow
private bool TryHarvestCrop(Crop crop, WorldObject obj)
{
    // 1. Validate crop is ready
    if (!crop.ReadyToHarvest) return true; // Handled but not harvestable

    // 2. Create harvest item
    Item? reward = crop.GetHarvestDrop();

    // 3. Try adding to inventory (returns bool)
    bool success = Player.Inventory.AddItem(reward);

    // 4. Handle outcome
    if (success)
    {
        if (crop.Regrows)
            crop.ResetForRegrowth();  // Reset stage, keep crop
        else
            Objects.Remove(obj);      // Remove from world
    }
    // If !success: Inventory full - do NOT modify crop

    return true;
}
```

### Economy System (v2.9)
**Player Gold & Item Prices**
```csharp
// Player.cs
public int Gold { get; set; } = 500;

// Item.cs - Base class
public int SellPrice { get; init; } = -1;  // -1 = unsellable
public bool IsSellable => SellPrice > 0;

// Material.cs constructor
new Material("wood", "Wood", "...", quantity: 25, sellPrice: 2);
```

**Shipping Bin** (`ShippingBin.cs`)
- World object spawned on Farm at tile (2, 2)
- Click to open `ShippingMenu` UI (via `WorldManager.OnOpenShippingMenu` event)
- Manifest processed overnight in `OnDayPassed`:

```csharp
// WorldManager.ProcessNightlyShipments()
foreach (var bin in allShippingBins)
{
    int earnings = bin.ProcessNightlyShipment(); // Clears manifest
    Player.Gold += earnings;
}
```

**Shipping Menu** (`ShippingMenu.cs`) - Stardew-style UI
- **Bin Slot**: Single large slot acts as "undo buffer"
- **Push System**: Dropping new item → auto-finalizes previous item to manifest
- **Undo**: Drag item from bin slot back to inventory before close
- **On Close**: Bin slot item finalized, state returns to Playing

```csharp
// Drop into bin slot
if (_binSlotItem != null)
    _activeBin.ShipItem(_binSlotItem);  // Finalize old item
_binSlotItem = _heldItem;               // New item in buffer

// On menu close
if (_binSlotItem != null)
    _activeBin.ShipItem(_binSlotItem);  // Finalize last item
```

### Item Serialization
```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Tool), "tool")]
[JsonDerivedType(typeof(Material), "material")]
public abstract class Item { ... }
```

### Bottom-Center Pivot System (v2.2)
All sprites use bottom-center origin for proper 2.5D depth:
```csharp
// Position represents feet (bottom-center of sprite)
public Vector2 Position { get; set; }

// Collision box covers only feet area (~20% of height)
public Rectangle CollisionBounds => new Rectangle(
    (int)(Position.X - Width * 0.35f),
    (int)(Position.Y - Height / 5),
    (int)(Width * 0.7f),
    Height / 5
);

// Y-sorting uses feet position directly
public float SortY => Position.Y;

// Draw with bottom-center origin
var origin = new Vector2(Width / 2f, Height);
spriteBatch.Draw(texture, Position, sourceRect, color, 0f, origin, scale, SpriteEffects.None, 0f);
```

### World Persistence (v2.0)
- `GameLocation.CreateTestMap(seed)` generates deterministic maps from seed
- `WorldManager.GetModifiedTiles()` compares current map vs freshly generated default
- Only tiles that differ from default are saved (delta compression)
- On load: regenerate default map from seed, then apply saved modifications

## MonoGame Specifics

- Default window: 800x480 pixels (resizable)
- Tile size: 64x64 pixels
- Uses `SpriteBatch` with `SamplerState.PointClamp` for crisp pixel art
- Four render passes:
  1. World (with camera transform)
  2. Night overlay (screen space, alpha blend)
  3. UI (screen space, unaffected by night filter)
  4. Transition fade (screen space, covers everything during warp)

## Controls

| Key | Action |
|-----|--------|
| E / Tab | Open/close inventory menu |
| Escape | Close inventory menu |
| K | Save game to `debug_save.json` |
| L | Load game from `debug_save.json` |
| F3 | Toggle collision box visualization (red borders) |
| T | Fast forward time by 1 hour |
| P | Pause/unpause time |
