# MagicVille

A 2D top-down farming RPG inspired by Stardew Valley, built with C# and MonoGame.

## Version 1 - Complete Gameplay Foundation

Building on the v0 engine foundation, v1 adds:

### Items & Inventory
- Polymorphic Item system (Tool, Material) with JSON serialization
- 10-slot hotbar with scroll wheel and number key selection (1-0)
- Stackable materials with quantity tracking

### Tools & Interaction
- **Hoe** - Tills grass/dirt into farmable soil
- **Pickaxe** - Breaks stone into dirt
- **Watering Can** - Waters tilled soil
- **Axe** - (Future: chop trees)
- **Scythe** - (Future: harvest crops)

### Magic Wands
- **Earth Wand** - Magically tills soil (like Hoe, lower resource cost)
- **Hydro Wand** - Conjures water (like Watering Can, lower resource cost)

### World & Rendering
- Mouse-based tile targeting with range-checking reticle (green/red)
- Dynamic window resizing with proper coordinate conversion
- Random 30x17 tile map generation (1920x1080 at 64px tiles)
- Sprite animation system with direction-based spritesheets
- New tile types: Stone, WetDirt, Tilled

### Technical
- Camera reads viewport dynamically for accurate mouse-to-world conversion
- Polymorphic JSON serialization for inventory save/load
- Input manager with mouse and keyboard state tracking

## Requirements

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- MonoGame 3.8.1 (restored via NuGet)

## Build & Run

```bash
dotnet restore    # Restore NuGet packages
dotnet build      # Build the project
dotnet run        # Run the game
```

## Controls

| Key | Action |
|-----|--------|
| W / Up Arrow | Move up |
| A / Left Arrow | Move left |
| S / Down Arrow | Move down |
| D / Right Arrow | Move right |
| 1-9, 0 | Select hotbar slot 1-10 |
| Scroll Wheel | Cycle hotbar slots |
| Left Click | Use selected tool on tile |
| K | Save game |
| L | Load game |
| F3 | Toggle collision debug visualization |
| T | Fast forward time by 1 hour |
| P | Pause/unpause time |

## Project Structure

```
MagicVille/
├── Program.cs          # Entry point
├── Game1.cs            # MonoGame Game class (window resize handling)
├── WorldManager.cs     # Orchestrates game state, update, draw, tool interaction
├── GameLocation.cs     # Tile map with random generation
├── Tile.cs             # Tile types (Grass, Dirt, Water, Stone, WetDirt, Tilled)
├── Player.cs           # Player entity with movement and sprite animation
├── Camera2D.cs         # 2D camera with dynamic viewport support
├── InputManager.cs     # Mouse/keyboard input and coordinate conversion
├── Inventory.cs        # 10-slot hotbar with item management
├── Item.cs             # Base item class with polymorphic serialization
├── Tool.cs             # Tool items (Hoe, Axe, Pickaxe, etc.)
├── Material.cs         # Stackable material items (Wood, Stone, etc.)
├── SpriteAnimator.cs   # Sprite animation with direction rows
├── TimeManager.cs      # Global time system and day/night cycle
├── WorldObject.cs      # World objects (rocks, trees) with collision
├── IRenderable.cs      # Interface for Y-sortable entities
├── SaveData.cs         # Serializable game state DTO
├── TileSaveData.cs     # Serializable modified tile data
├── SaveManager.cs      # JSON save/load with polymorphic support
└── Saves/              # Save files (created at runtime)
```

## Architecture

### Item System
- Abstract `Item` base class with `[JsonPolymorphic]` attributes
- `Tool` subclass: ResourceCost, PowerLevel properties
- `Material` subclass: Quantity, MaxStack, stackable behavior
- Inventory handles stacking logic for materials automatically

### Tool Interaction
- InputManager converts screen coordinates to world/tile coordinates
- Range checking (96px / ~1.5 tiles) determines valid targets
- Visual reticle feedback: green = in range, red = out of range
- Tool effects modify tile types (e.g., Hoe: Grass → Tilled)

### Rendering
- Placeholder colored rectangles (no external assets required)
- 64x64 pixel tiles
- SpriteBatch with PointClamp sampling for crisp pixels
- Camera transform matrix for world rendering
- Separate UI pass for hotbar (screen space)

### Save System
- JSON serialization via System.Text.Json
- Polymorphic type discriminator for Item subclasses
- Saves stored in `Saves/` folder alongside the executable
- Inventory state persisted with item types and quantities
- **World persistence**: Modified tiles saved as delta from seeded default map

## Version History

### v2.3 - Time System & Day/Night Cycle (Current)
- **TimeManager**: Global time system (7 real seconds = 10 in-game minutes)
- **Day/Night cycle**: Visual atmosphere with sunset/dusk/night overlays
- **Clock UI**: Pixel-rendered clock showing "Day X - HH:MM AM/PM"
- **Map boundaries**: Player clamped to map edges (no walking into void)
- **Time math fix**: Proper minute rollover (no more 65-minute hours)
- **Debug keys**: T = advance 1 hour, P = pause/unpause time

> **Note**: Sunset colors are placeholder and need artistic tuning. v3 will implement a RenderTarget-based lighting system for proper color grading and dynamic lights.

### v2.2 - Bottom-Center Pivot System
- **Rendering refactor**: All sprites use bottom-center origin (Position = feet)
- **Feet-only collision**: Collision boxes cover ~20% height at base for proper 2.5D overlap
- **Y-sorting**: Objects sorted by feet position (Position.Y) for correct depth
- **F3 debug toggle**: Visualize collision rectangles with red borders

### v2.1 - Object Layer & Collision
- WorldObject class (rocks, trees, bushes, mana nodes)
- IRenderable interface for Y-sorted depth rendering
- Collision detection with slide movement
- Object interaction priority (objects checked before tiles)
- Seeded object spawning for reproducible placement

### v2.0 - World Persistence
- **Bug fix**: Map tiles now properly save and load
- Seeded map generation for deterministic world recreation
- TileSaveData stores only modified tiles (delta compression)
- Load resets map to default, then applies saved modifications

### v1 - Complete Gameplay Foundation
- Item/Tool/Material system with polymorphic serialization
- 10-slot hotbar inventory
- Mouse-based tool interaction with reticle
- Magic wands (Earth Wand, Hydro Wand)
- Dynamic window resizing
- Sprite animation system
- Random map generation

### v0 - Foundation
- MonoGame engine setup
- Tile-based world rendering
- Player movement
- Camera system
- JSON save/load

## What's NOT Built Yet (Future Versions)

- Crops and farming cycle
- NPCs and dialogue
- Multiple locations
- Sound effects and music
- RenderTarget-based lighting (v3)

## License

MIT
