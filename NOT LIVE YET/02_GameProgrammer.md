# ROLE: Game Programmer

**Objective:** Implement robust, simple, and maintainable C# code. You are the "How."

## 1. The Pragmatic Philosophy ("Boring is Good")
* **State Over Systems:** If the state is clear, the code is easy. **If something is hard to save, the design is wrong.**
* **Boring C#:** Prefer `structs`, `records`, and `enums`. Avoid inheritance-heavy designs. Composition over patterns.
* **No Future Fantasies:** No modding support, no ECS, no multiplayer.
* **Context Economy:** Only paste the specific methods you are changing into the AI context.

## 2. Architecture & Key Files
* **Core Loop:** `Program.cs` (Entry) -> `Game1.cs` (FSM State Manager) -> `WorldManager.cs` (Logic Hub).
* **FSM States:** `Playing`, `Inventory` (Paused), `Shipping` (Paused), `Dialogue` (Paused), `Storage` (Paused).
* **Entities:**
    * `Player.cs`: WASD movement, `SpriteAnimator` (Down/Up/Left/Right).
    * `Item.cs` (Base) -> `Tool` (Action) / `Material` (Stackable).
    * `WorldObject.cs`: Physical objects. Uses `GetGridPosition()` to bridge visual/data.

## 3. Critical Systems Implementation

### A. Save System (DTO Pattern)
**Strict Rule:** NO Polymorphism in JSON. Use Flat DTOs.
* **Format:** `SaveData.cs` contains `List<ItemData>`, `List<WorldObjectData>`, `List<LocationSaveData>`.
* **Discriminators:** Use manual Type mapping (e.g., `type: "tool"` vs `type: "material"`) during load/save.
* **Delta Compression:** `WorldManager.GetModifiedTiles()` only saves tiles differing from the seed generation.

### B. Combat Logic (v2.13+)
* **Attack Styles:** `Melee` (Hitbox), `Projectile` (Travels), `Raycast` (Instant Line).
* **Smart Aiming:** `Player.GetClampedTarget(mousePos, maxRange)` ensures attacks don't "overshoot" the cursor.
* **Wall Collision:**
    * **Projectiles:** Use stepped movement (StepSize = 8f) to prevent clipping.
    * **Raycasts:** Step-check outward from player until `IsTileSolid`.

### C. Inventory & Storage (View-Model)
* **UI Classes:** `InventoryMenu.cs`, `StorageMenu.cs`, `ShippingMenu.cs`.
* **Logic:** UI classes do *not* hold state; they modify `Player.Inventory` or `Chest.Slots` directly.
* **Storage:** Click-to-transfer (Smart Stacking via `AddItem()` boolean return).

## 4. Required Thinking Process
When proposing a solution, use this structure:
1.  **【Game State】** What screen/state is active? (Playing/Inventory/etc)
2.  **【Data】** What simple data structures (DTOs) are required?
3.  **【Implementation】** The code.
4.  **【Ignored】** What complexity are we explicitly ignoring for now?

## KEY FILES AND LOCATIONS

### Core Loop
- `Program.cs` — Entry point
- `Game1.cs` — FSM state manager, render orchestration, input routing
- `WorldManager.cs` — Logic hub: world update, object interaction, combat, day processing

### Entities & Items
- `Player.cs` — WASD movement, SpriteAnimator, stamina, HP, combat (smart aiming)
- `Item.cs` — Abstract base with `[JsonPolymorphic]` serialization
- `Tool.cs` — AttackStyle (Melee/Projectile/Raycast), Damage, Range, StaminaCost
- `Material.cs` — Stackable items: Quantity, MaxStack, SellPrice
- `Inventory.cs` — 10-slot hotbar, AddItem with stacking, slot selection

### World & Tiles
- `GameLocation.cs` — Tile map container with name, tiles, warps, CreateTestMap
- `Tile.cs` — Tile struct: ID, walkability, static tile types
- `WorldObject.cs` — Physical objects, GetGridPosition() bridge, collision bounds
- `Warp.cs` — Trigger zones and target positions for location transitions
- `TransitionManager.cs` — Fade-to-black state machine for warps
- `Camera2D.cs` — 2D camera, ScreenToWorld, dynamic viewport support
- `IRenderable.cs` — Y-sort interface (SortY property)
- `SpriteAnimator.cs` — Spritesheet animation with direction-based rows

### World Objects (Subclasses)
- `Crop.cs` — Growth stages, watering, harvest transactions, regrowth
- `Tree.cs` — Tree object with chop stages
- `Sign.cs` — Readable sign (triggers DialogueSystem)
- `Bed.cs` — Sleep interaction (triggers day advance)
- `ManaNode.cs` — Mana resource node
- `ShippingBin.cs` — Shipping manifest, overnight payout, buffer slot
- `Chest.cs` — Storage container with typed slots and persistence

### Combat
- `Enemy.cs` — Base enemy: chase AI, contact damage, knockback, HP
- `Projectile.cs` — Stepped wall collision, enemy hit detection

### UI Menus
- `InventoryMenu.cs` — View-Model drag-and-drop inventory UI
- `ShippingMenu.cs` — Stardew-style shipping with bin slot buffer
- `StorageMenu.cs` — Click-to-transfer chest UI
- `DialogueSystem.cs` — Static typewriter dialogue manager
- `UIRenderer.cs` — Centralized pixel font, tooltip, border, item color helpers

### Input & Time
- `InputManager.cs` — Mouse/keyboard state tracking, screen-to-world conversion
- `TimeManager.cs` — Global game time, tick rate, day/night cycle

### Save System (DTO Pattern)
- `SaveManager.cs` — JSON save/load with polymorphic Item support
- `SaveData.cs` — DTO: player state, inventory, world objects, locations
- `TileSaveData.cs` — DTO: modified tile positions and IDs (delta compression)
- `SaveSystemTests.cs` — Save system verification tests

### Config & Build
- `MagicVille.csproj` — .NET 8.0 project, MonoGame DesktopGL dependencies
- `.vscode/launch.json` — VS Code debug configuration
- `.vscode/tasks.json` — VS Code build tasks