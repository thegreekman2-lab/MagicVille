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
- **Game1.cs**: MonoGame Game class, handles window resize events
- **WorldManager.cs**: Central orchestrator - manages game state, update loop, rendering, and tool interaction

### World & Tiles
- **GameLocation.cs**: Tile map container with random generation (30x17 tiles for 1920x1080)
- **Tile.cs**: Tile struct with ID and walkability. Types: Grass, Dirt, Water, Stone, WetDirt, Tilled
- **Camera2D.cs**: 2D camera with dynamic viewport support for window resizing

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
- **Tool.cs**: Tools with ResourceCost and PowerLevel (Hoe, Axe, Pickaxe, Watering Can, Scythe, Earth Wand, Hydro Wand)
- **Material.cs**: Stackable items with Quantity and MaxStack (Wood, Stone, etc.)
- **Inventory.cs**: 10-slot hotbar with stacking logic and slot selection

### Input & Interaction
- **InputManager.cs**: Mouse/keyboard state tracking, screen-to-world coordinate conversion
- Tool interaction uses range checking (96px) with visual reticle feedback

### Save System
- **SaveData.cs**: DTO for serializable game state
- **TileSaveData.cs**: DTO for modified tile positions and IDs
- **SaveManager.cs**: JSON save/load with polymorphic Item support

## Key Patterns

### Tool Interaction Flow
1. `InputManager.GetMouseTilePosition(camera)` converts screen → tile coordinates
2. Range check against player position (96px / ~1.5 tiles)
3. `WorldManager.InteractWithTile()` applies tool effect based on `tool.RegistryKey`
4. Tile type is modified (e.g., Grass → Tilled)

### Coordinate Conversion
- Camera stores `GraphicsDevice` reference (not Viewport) to support dynamic window resizing
- `Camera2D.ScreenToWorld()` uses inverted transform matrix
- Always reads `GraphicsDevice.Viewport` for current dimensions

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
- Two render passes: world (with camera transform) and UI (screen space)

## Debug Controls

| Key | Action |
|-----|--------|
| K | Save game to `debug_save.json` |
| L | Load game from `debug_save.json` |
| F3 | Toggle collision box visualization (red borders) |
