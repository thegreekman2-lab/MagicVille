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
- **Pickaxe** - Breaks stone into dirt, harvests mana from crystals
- **Watering Can** - Waters tilled soil and crops (pass-through)
- **Axe** - Chops trees into stumps, removes stumps
- **Scythe** - Harvests mature crops into inventory items

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
| E / Tab | Open/close inventory menu |
| Escape | Close inventory menu |
| K | Save game |
| L | Load game |
| F3 | Toggle collision debug visualization |
| T | Fast forward time by 1 hour |
| P | Pause/unpause time |

## Project Structure

```
MagicVille/
├── Program.cs          # Entry point
├── Game1.cs            # MonoGame Game class with FSM state management
├── WorldManager.cs     # Orchestrates game state, update, draw, tool interaction
├── InventoryMenu.cs    # Inventory UI with drag-and-drop
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
├── TransitionManager.cs # Location transition fade effects
├── Warp.cs             # Warp point data structure
├── WorldObject.cs      # Base world object with polymorphic OnNewDay()
├── Crop.cs             # Crop subclass with growth stages
├── Tree.cs             # Tree subclass with growth/stump mechanics
├── ManaNode.cs         # Mana crystal with daily recharge
├── Bed.cs              # Furniture for sleeping and day advancement
├── ShippingBin.cs      # Shipping bin for selling items overnight
├── ShippingMenu.cs     # Shipping bin UI (Stardew-style drag-and-drop)
├── Sign.cs             # Readable sign world object
├── DialogueSystem.cs   # Static dialogue manager with typewriter effect
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

### v2.11 - Dialogue System (Current)

**Narrative Layer**
- `GameState.Dialogue`: World paused while reading text
- Event-driven architecture for UI decoupling

**DialogueSystem.cs** - Static Manager
- **Typewriter Effect**: Characters appear at 0.03s intervals
- **Input Handling**:
  - During typing: Click/Space/Enter/E → Instant finish
  - After typing: Click/Space/Enter/E → Close dialogue
- **Input Consumption**: Prevents click-through to game world
- RPG-style box at bottom (80% width, dark blue, white border)
- Word wrapping for long text

**Sign.cs** - Readable World Object
- WorldObject subclass with `Text` property
- Click to read, opens dialogue box
- JSON serialization support

**Integration**
- Welcome sign spawned at tile (12, 5) on Farm
- `OnOpenDialogue` event from WorldManager to Game1

### v2.10 - Stamina System

**Player Energy**
- **MaxStamina**: 100 energy capacity (upgradeable in future)
- **CurrentStamina**: Depletes with tool use, persisted across saves
- **TryUseStamina(cost)**: Gate check - returns false if too tired
- **Recovery**: Full restore on sleep/new day

**Tool Stamina Costs**
| Tool | Cost | Notes |
|------|------|-------|
| Pickaxe | 4 | Heavy work |
| Axe | 4 | Heavy work |
| Hoe | 3 | Moderate |
| Watering Can | 2 | Light work |
| Earth Wand | 1 | Magic efficiency |
| Hydro Wand | 1 | Magic efficiency |
| Scythe | 0 | Free harvesting |

**Stamina Bar UI**
- Vertical bar in bottom-right corner (20x100px)
- Color gradient: Green > 50%, Yellow 20-50%, Red < 20%
- Hover tooltip shows "Energy: X/100"

**Anti-Save-Scum**
- Stamina persisted in `SaveData.PlayerStamina`
- Cannot reload to restore energy

### v2.9 - Economy Loop

**Player Economy**
- **Gold System**: Player starts with 500g, saved/loaded with game state
- **Item Prices**: Items have `SellPrice` property (-1 = unsellable)
- Basic materials: Wood = 2g, Stone = 2g

**Shipping Bin** (`ShippingBin.cs`)
- Interactive world object spawned on Farm (tile 2,2)
- Click to open Shipping Menu UI
- Items in manifest are sold overnight during `OnDayPassed`
- Total earnings added to player gold with debug report

**Shipping Menu** (`ShippingMenu.cs`) - Stardew-style UI
- **Bin Slot Buffer**: Large center slot acts as "last item" undo buffer
- **Push System**: Dropping new item auto-finalizes previous item to manifest
- **Undo**: Drag item from bin slot back to inventory anytime
- **On Close**: Any item in bin slot is finalized to shipping manifest
- Shows pending gold value and current player gold

**New Game State**
- `GameState.Shipping`: World paused, shipping menu active
- Transition: Click ShippingBin → Open menu, E/Tab/Esc → Close and finalize

**Event System**
- `WorldManager.OnOpenShippingMenu` event for UI decoupling
- Game1 subscribes to event to handle state transitions

### v2.8 - Inventory UI & Game States

**Game State FSM** (`Game1.cs`)
- `GameState` enum: `Playing`, `Inventory`
- FSM-based Update loop routes input to active system only
- `Playing`: World/player update, E/Tab opens inventory
- `Inventory`: World paused (frozen background), UI-only updates

**Inventory Menu** (`InventoryMenu.cs`)
- **View-Model Separation**: References `Player.Inventory`, no data duplication
- **Drag-and-Drop**: Pick up items, swap between slots, snap-back on invalid drop
- **Input Mode Controller**: Prevents click-through to world while menu open
- Render order: Background → Slots → Items → Tooltip → Held Item (at cursor)

**Visual Polish**
- Pulsing "PAUSED" indicator (Yellow, 3x scale, sine wave animation)
- Dimmed background overlay (semi-transparent black)
- Slot hover highlights (light blue) and selection indicators (white border)
- Tooltips with full item names following mouse cursor
- Scaled pixel font (2x) for readability

**Pixel Font System**
- Full uppercase A-Z, lowercase a-z, digits 0-9
- Punctuation: `.,:;!?-+='"/()[]`
- Scalable rendering (1x for slots, 2x for tooltips, 3x for PAUSED)

### v2.7 - Harvest System

**Data-Driven Crop Harvesting**
- `Crop.HarvestItemId`: Defines which item drops on harvest (e.g., "tomato")
- `Crop.HarvestQuantity`: How many items drop per harvest
- `Crop.Regrows`: Whether crop regrows after harvest (tomatoes) or must be replanted (corn)
- `Crop.HarvestResetStage`: Which growth stage to reset to after harvest (for regrowable crops)
- `Crop.GetHarvestDrop()`: Factory method creates Material item from harvest data

**Harvest Transaction System** (`WorldManager.TryHarvestCrop`)
1. Check `crop.ReadyToHarvest` (must be Mature stage)
2. Create reward item via `GetHarvestDrop()`
3. Try to add to inventory: `Player.Inventory.AddItem(reward)`
4. **Success**: Remove crop OR reset for regrowth
5. **Fail (Inventory Full)**: Do NOT modify crop, player can try again

**Crop Types**
| Crop | Days to Mature | Harvest Qty | Regrows? |
|------|----------------|-------------|----------|
| Corn | 6 days | 2 | No |
| Tomato | 6 days | 3 | Yes (→ Growing) |
| Potato | 9 days | 4 | No |

**Hotbar Item Colors**
- Harvest items display with crop-specific colors (yellow corn, red tomato, tan potato)

### v2.6 - Smart Alignment & Crop Fix

**Smart Object Alignment System**
- Objects now use intelligent tile placement based on height
- Small/flat objects (Height ≤ 64px): Centered vertically on tile
- Tall objects (Height > 64px): Aligned to bottom of tile
- `GetAlignedPosition(tileX, tileY, width, height)` helper in WorldManager

**Coordinate System Bridge**
- `WorldObject.GetGridPosition()`: Converts world pixels → tile grid coordinates
- Essential for bridging "Visual World" (pixels) and "Data World" (tile lookups)
- `GridX` and `GridY` convenience properties on all WorldObjects

**Tool Pass-Through System**
- `Tool.AffectsTileThroughObjects` property for scalable tool behavior
- Watering Can/Hydro Wand: `true` - waters crop AND wets underlying tile
- Pickaxe/Axe/Scythe: `false` - stops at object, doesn't affect tile
- Future-proof: Sprinklers and magic staffs just set this flag

**Critical Bug Fix: Crop Growth**
- Fixed order of operations in `OnDayPassed()` (Two-Pass System):
  - **Pass 1 - Growth Phase**: Process objects while tiles are STILL WET
  - **Pass 2 - Evaporation Phase**: Dry tiles AFTER objects processed
- Crops now check underlying tile state (`WetDirt`) in addition to `WasWateredToday` flag
- Verbose debug logging for crop state tracking

**Wet Tile Rendering**
- Tiles drawn in `WorldManager.DrawTiles()` with wet state visual tint
- `WetDirt` tiles get LightSteelBlue color overlay (40% blend)
- Clear visual feedback when soil is watered

### v2.5 - New Day Simulation
- **Polymorphic WorldObject**: Virtual `OnNewDay()` method for daily updates
- **Crop System**: Seeds grow through stages (Seed -> Sprout -> Growing -> Mature)
  - Requires daily watering to grow, dies after 3 days without water
  - Harvest mature crops with Scythe
- **Tree System**: Saplings grow to Young -> Mature over days
  - Axe chops mature trees into stumps, stumps can regrow
- **ManaNode System**: Magical crystals with daily recharge
  - Harvest with Pickaxe, depleted nodes recharge 1 charge per day
  - Types: Arcane (blue), Fire (red), Nature (green)
- **Bed & Sleep**: Click bed in Cabin to save game and advance to next day
- **Global State Reset**: Wet soil dries overnight -> tilled soil
- **Time Persistence**: Day and TimeOfDay saved/loaded
- **Object Persistence**: World objects serialized with polymorphic JSON

### v2.4 - Multi-Location System
- **Location Dictionary**: Multiple maps kept in memory for state persistence
- **Warp System**: Step on trigger zones to teleport between locations
- **Fade Transitions**: Screen fades to black during location changes
- **Farm** (20x20): Outdoor area with grass, dirt, pond, and cabin entrance
- **Cabin** (10x10): Indoor wood floor room with wall borders
- **New Tiles**: Wood floor, Wall (impassable)

### v2.3 - Time System & Day/Night Cycle
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

- Seed items and planting system (currently crops spawn pre-planted)
- Item drops from trees (wood) and rocks (stone)
- Item Database for centralized item definitions
- Player energy/stamina system
- NPCs and dialogue
- Sound effects and music
- RenderTarget-based lighting (v3)

## License

MIT
