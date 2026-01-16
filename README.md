# MagicVille

A 2D top-down farming RPG inspired by Stardew Valley, built with C# and MonoGame.

## Version 0 - Foundation

This is the initial engine foundation with:
- Tile-based world rendering (20x20 test map)
- Player movement (WASD/Arrow keys)
- Camera system that follows the player
- Save/Load system (JSON-based)

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
| K | Save game |
| L | Load game |

## Project Structure

```
MagicVille/
├── Program.cs          # Entry point
├── Game1.cs            # MonoGame Game class (delegates to WorldManager)
├── WorldManager.cs     # Orchestrates game state, update, and draw
├── GameLocation.cs     # Tile map container
├── Tile.cs             # Tile data structure
├── Player.cs           # Player entity with movement
├── Camera2D.cs         # 2D camera with transform matrix
├── SaveData.cs         # Serializable game state DTO
├── SaveManager.cs      # JSON save/load utilities
└── Saves/              # Save files (created at runtime)
```

## Architecture

### State-Driven Design
- Game state is explicit and serializable
- Clear separation between "Live" objects (Player, GameLocation) and "Data" objects (SaveData)
- WorldManager acts as the central orchestrator

### Rendering
- Placeholder colored rectangles (no external assets required)
- 64x64 pixel tiles
- SpriteBatch with PointClamp sampling for crisp pixels

### Save System
- JSON serialization via System.Text.Json
- Saves stored in `Saves/` folder alongside the executable
- Clean Live↔Data conversion pattern

## What's NOT Built Yet (Future Versions)

- Tile collision
- Sprite animations
- Inventory system
- Tools and farming
- NPCs and dialogue
- Time/day cycle
- Multiple locations

## License

MIT
