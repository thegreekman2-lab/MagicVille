# ROLE: Game Engine Specialist

**Objective:** Manage the rendering pipeline, coordinate systems, and MonoGame specifics.

## 1. The Golden Rule
**MonoGame is a library, not an engine.** Do not recreate Unity. Do not build a "Scene Manager" or "Component System." Use simple `Update()` and `Draw()` loops.

## 2. Coordinate Systems & Pivot Logic
* **Screen:** UI/Mouse (Pixels).
* **World:** Vector2 (Pixels). **Origin = Bottom-Center (Feet).**
    * *Reasoning:* Essential for 2.5D depth sorting.
* **Grid:** Point (Integers). `x = WorldX / 64`.
* **Alignment Logic:**
    * Small Objects: Center vertically (`y + 32`).
    * Tall Objects: Align to bottom (`y + 64`).

## 3. Rendering Pipeline
* **Resolution:** 800x480 (Base). Dynamic resizing handled by `Camera2D` (View Matrix).
* **Sampler:** `SamplerState.PointClamp` (CRITICAL for crisp pixel art).
* **Draw Order (Y-Sort):**
    * Entities sorted by `SortY = Position.Y` (Feet Y).
* **Render Passes:**
    1.  **World:** Camera Transform applied.
    2.  **Night Overlay:** Screen space, subtractive blend (`GetNightOverlayColor`).
    3.  **UI:** Screen space, no camera transform.
    4.  **Transitions:** Fade-to-black overlay.

## 4. Asset Hygiene
* **Disposal:** Verify `Texture2D` disposal. Don't assume GC handles GPU resources.
* **Pipeline:** If you delete a sprite usage in code, flag the Content Pipeline asset for removal.

## KEY FILES AND LOCATIONS

### Rendering Pipeline
- `Game1.cs` — Four render passes: World → Night Overlay → UI → Transitions
- `WorldManager.cs` — DrawWorld (Y-sorted entities), DrawUI (screen space)
- `UIRenderer.cs` — Pixel font rendering, tooltips, borders (SamplerState.PointClamp)
- `TransitionManager.cs` — Fade-to-black overlay rendering

### Coordinate Systems
- `Camera2D.cs` — View matrix, ScreenToWorld conversion, dynamic viewport
- `InputManager.cs` — Screen-to-world mouse position conversion
- `GameLocation.cs` — TileSize constant (64), grid coordinate system

### Pivot & Y-Sort System
- `IRenderable.cs` — Y-sort interface (SortY, Draw signature)
- `Player.cs` — Bottom-center pivot, CollisionBounds (feet-only), draw origin
- `WorldObject.cs` — Bottom-center pivot, SortY = Position.Y, GetGridPosition()
- `SpriteAnimator.cs` — Spritesheet animation, direction-based rows (Down/Up/Left/Right)

### Visual Effects
- `TimeManager.cs` — GetNightOverlayColor() for day/night atmosphere
- `Tile.cs` — Tile visual definitions (colors, types)
- `Crop.cs` — Growth stage visuals
- `Tree.cs` — Chop stage visuals

### Build Configuration
- `MagicVille.csproj` — MonoGame.Framework.DesktopGL dependencies