#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MagicVille;

public class WorldManager
{
    // All loaded locations (persisted in memory for state preservation)
    public Dictionary<string, GameLocation> Locations { get; private set; } = new();

    // Current active location
    public GameLocation CurrentLocation { get; private set; } = null!;

    // Live game objects (initialized in Initialize())
    public Camera2D Camera { get; private set; } = null!;
    public Player Player { get; private set; } = null!;
    public InputManager Input { get; private set; } = null!;
    public TransitionManager Transition { get; private set; } = null!;

    // Objects are per-location (stored in CurrentLocation context)
    public List<WorldObject> Objects { get; private set; } = new();

    // World metadata
    public string CurrentLocationName => CurrentLocation?.Name ?? "Unknown";
    public int WorldSeed { get; private set; }

    // Save file name for debug saves
    private const string DebugSaveFile = "debug_save.json";

    // Tool interaction range (in pixels, ~1.5 tiles)
    private const float InteractionRange = 96f;

    // Current targeting state
    private Point _targetTile;
    private bool _targetInRange;

    // Debug visualization (F3 toggle)
    public bool ShowDebug { get; private set; }

    // Graphics resources (initialized in Initialize())
    private Texture2D _pixel = null!;
    private Texture2D _playerSpritesheet = null!;
    private GraphicsDevice _graphicsDevice = null!;

    // Spritesheet frame dimensions
    private const int SpriteFrameWidth = 32;
    private const int SpriteFrameHeight = 32;

    public WorldManager()
    {
    }

    public void Initialize(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;

        // Create input manager
        Input = new InputManager();

        // Create transition manager and subscribe to events
        Transition = new TransitionManager();
        Transition.OnSwapMap += HandleMapSwap;

        // Create 1x1 white pixel texture for placeholder rendering
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        // Create placeholder spritesheet for player animation
        _playerSpritesheet = SpriteAnimator.CreatePlaceholderSpritesheet(
            graphicsDevice, SpriteFrameWidth, SpriteFrameHeight);

        // Create camera (uses GraphicsDevice for dynamic viewport)
        Camera = new Camera2D(graphicsDevice);

        // Generate world seed
        WorldSeed = Environment.TickCount;

        // Initialize all locations (kept in memory for state persistence)
        Locations["Farm"] = GameLocation.CreateFarm(WorldSeed);
        Locations["Cabin"] = GameLocation.CreateCabin();

        // Start on the Farm
        CurrentLocation = Locations["Farm"];

        // Spawn player at tile (7, 7) on the Farm
        var startPosition = new Vector2(
            7 * GameLocation.TileSize + GameLocation.TileSize / 2f,
            7 * GameLocation.TileSize + GameLocation.TileSize / 2f
        );
        Player = new Player(startPosition);

        // Initialize player spritesheet
        Player.SetSpritesheet(_playerSpritesheet, SpriteFrameWidth, SpriteFrameHeight);

        // Spawn world objects for the Farm
        SpawnFarmObjects();

        Debug.WriteLine($"[WorldManager] Initialized with {Locations.Count} locations (seed: {WorldSeed})");
    }

    /// <summary>
    /// Handle the map swap when transition screen is fully black.
    /// </summary>
    private void HandleMapSwap(string targetLocation, Vector2 targetPosition)
    {
        if (!Locations.TryGetValue(targetLocation, out var newLocation))
        {
            Debug.WriteLine($"[Warp] ERROR: Location '{targetLocation}' not found!");
            return;
        }

        // Switch to the new location
        CurrentLocation = newLocation;
        Player.Position = targetPosition;

        // Clear/reload objects for the new location
        Objects.Clear();
        if (targetLocation == "Farm")
        {
            SpawnFarmObjects();
        }
        // Cabin has no objects for now

        // Snap camera to new player position
        Camera.CenterOn(Player.Center);

        Debug.WriteLine($"[Warp] Teleported to {targetLocation} at ({targetPosition.X}, {targetPosition.Y})");
    }

    /// <summary>
    /// Spawn world objects for the Farm location.
    /// Uses WorldSeed for reproducible placement.
    /// </summary>
    private void SpawnFarmObjects()
    {
        Objects.Clear();

        // Use world seed for reproducible object placement
        var random = new Random(WorldSeed + 1); // +1 to differ from map generation

        int mapWidth = CurrentLocation.Width;
        int mapHeight = CurrentLocation.Height;

        // Helper to get a valid spawn position (not on water, not too close to spawn)
        Vector2 GetValidPosition()
        {
            for (int attempts = 0; attempts < 50; attempts++)
            {
                int tileX = random.Next(mapWidth);
                int tileY = random.Next(mapHeight);

                // Skip player spawn area (around 7,7)
                if (Math.Abs(tileX - 7) < 3 && Math.Abs(tileY - 7) < 3)
                    continue;

                // Skip water tiles
                var tile = CurrentLocation.GetTile(tileX, tileY);
                if (tile.Id == Tile.Water.Id)
                    continue;

                return new Vector2(tileX * 64, tileY * 64);
            }
            return new Vector2(10 * 64, 10 * 64); // Fallback
        }

        // Spawn 5 rocks (breakable with pickaxe)
        for (int i = 0; i < 5; i++)
        {
            Objects.Add(WorldObject.CreateRock(GetValidPosition()));
        }

        // Spawn 5 mana nodes (blue rocks, breakable with pickaxe)
        for (int i = 0; i < 5; i++)
        {
            Objects.Add(WorldObject.CreateManaNode(GetValidPosition()));
        }

        // Spawn 4 trees
        for (int i = 0; i < 4; i++)
        {
            Objects.Add(WorldObject.CreateTree(GetValidPosition()));
        }

        // Spawn 3 bushes
        for (int i = 0; i < 3; i++)
        {
            Objects.Add(WorldObject.CreateBush(GetValidPosition()));
        }

        Debug.WriteLine($"[WorldManager] Spawned {Objects.Count} world objects (seed: {WorldSeed})");
    }

    public void Update(GameTime gameTime)
    {
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Update input state first
        Input.Update();

        // Update transition (always, even during gameplay)
        Transition.Update(deltaTime);

        // Skip gameplay updates during transition
        if (Transition.IsTransitioning)
            return;

        // Debug Save/Load hotkeys: K = Save, L = Load
        if (Input.IsKeyPressed(Keys.K))
        {
            Save();
        }
        if (Input.IsKeyPressed(Keys.L))
        {
            Load();
        }

        // F3: Toggle debug collision visualization
        if (Input.IsKeyPressed(Keys.F3))
        {
            ShowDebug = !ShowDebug;
            Debug.WriteLine($"[Debug] Collision visualization: {(ShowDebug ? "ON" : "OFF")}");
        }

        // T: Fast forward time by 1 hour
        if (Input.IsKeyPressed(Keys.T))
        {
            TimeManager.AdvanceHour();
            Debug.WriteLine($"[Time] Advanced to {TimeManager.GetFormattedTime()}");
        }

        // P: Pause/Unpause time
        if (Input.IsKeyPressed(Keys.P))
        {
            TimeManager.IsPaused = !TimeManager.IsPaused;
            Debug.WriteLine($"[Time] {(TimeManager.IsPaused ? "PAUSED" : "RESUMED")}");
        }

        // Update global time
        TimeManager.Update(deltaTime);

        // Update player movement with collision checking
        int mapPixelWidth = CurrentLocation.Width * GameLocation.TileSize;
        int mapPixelHeight = CurrentLocation.Height * GameLocation.TileSize;
        Player.Update(gameTime, Input.GetKeyboardState(), CanMove, mapPixelWidth, mapPixelHeight);

        // Check for warp triggers after movement
        CheckWarpTriggers();

        // Update inventory (slot selection via scroll/number keys)
        Player.Inventory.Update(Input.GetKeyboardState(), Input.GetMouseState());

        // Camera follows player
        Camera.CenterOn(Player.Center);

        // Update targeting
        UpdateTargeting();

        // Handle tool use on left click
        if (Input.IsLeftMousePressed())
        {
            TryUseTool();
        }
    }

    /// <summary>
    /// Check if player is standing on a warp trigger and initiate transition.
    /// </summary>
    private void CheckWarpTriggers()
    {
        var playerBounds = Player.CollisionBounds;

        foreach (var warp in CurrentLocation.Warps)
        {
            if (playerBounds.Intersects(warp.TriggerZone))
            {
                Debug.WriteLine($"[Warp] Triggered: {CurrentLocationName} -> {warp.TargetLocationName}");
                Transition.StartTransition(warp.TargetLocationName, warp.TargetPlayerPosition);
                return; // Only trigger one warp at a time
            }
        }
    }

    #region Collision Detection

    /// <summary>
    /// Check if a bounding box can move to a position without colliding.
    /// Checks both tile walkability and world object collision.
    /// </summary>
    private bool CanMove(Rectangle bounds)
    {
        // Check tile collisions (all four corners of the bounding box)
        if (!IsTileWalkable(bounds.Left, bounds.Top) ||
            !IsTileWalkable(bounds.Right - 1, bounds.Top) ||
            !IsTileWalkable(bounds.Left, bounds.Bottom - 1) ||
            !IsTileWalkable(bounds.Right - 1, bounds.Bottom - 1))
        {
            return false;
        }

        // Check world object collisions
        foreach (var obj in Objects)
        {
            if (obj.IsCollidable && bounds.Intersects(obj.BoundingBox))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Check if a world position is on a walkable tile.
    /// </summary>
    private bool IsTileWalkable(int worldX, int worldY)
    {
        int tileX = worldX / GameLocation.TileSize;
        int tileY = worldY / GameLocation.TileSize;

        // Out of bounds = not walkable
        if (tileX < 0 || tileX >= CurrentLocation.Width ||
            tileY < 0 || tileY >= CurrentLocation.Height)
        {
            return false;
        }

        return CurrentLocation.GetTile(tileX, tileY).Walkable;
    }

    /// <summary>
    /// Get the world object at a specific tile coordinate, if any.
    /// </summary>
    private WorldObject? GetObjectAtTile(Point tileCoords)
    {
        var tileRect = new Rectangle(
            tileCoords.X * GameLocation.TileSize,
            tileCoords.Y * GameLocation.TileSize,
            GameLocation.TileSize,
            GameLocation.TileSize
        );

        foreach (var obj in Objects)
        {
            if (obj.BoundingBox.Intersects(tileRect))
            {
                return obj;
            }
        }

        return null;
    }

    #endregion

    #region Targeting & Tool Interaction

    private void UpdateTargeting()
    {
        // Get tile under mouse cursor
        _targetTile = Input.GetMouseTilePosition(Camera);

        // Calculate distance from player to target tile center
        var tileCenterWorld = InputManager.GetTileCenterWorld(_targetTile);
        float distance = Vector2.Distance(Player.Center, tileCenterWorld);

        _targetInRange = distance <= InteractionRange;
    }

    private void TryUseTool()
    {
        // Get active item
        var activeItem = Player.Inventory.GetActiveItem();
        if (activeItem is not Tool tool)
        {
            Debug.WriteLine("[Tool] No tool selected");
            return;
        }

        // Range check
        if (!_targetInRange)
        {
            Debug.WriteLine("[Tool] Target out of range");
            return;
        }

        // Validate tile is in bounds
        if (_targetTile.X < 0 || _targetTile.X >= CurrentLocation.Width ||
            _targetTile.Y < 0 || _targetTile.Y >= CurrentLocation.Height)
        {
            Debug.WriteLine("[Tool] Target out of bounds");
            return;
        }

        // Use the tool
        InteractWithTile(_targetTile, tool);
    }

    /// <summary>
    /// Apply tool effect to a tile based on tool type.
    /// PRIORITY: Check for world objects first before tile interactions.
    /// </summary>
    private void InteractWithTile(Point tileCoords, Tool tool)
    {
        // PRIORITY CHECK: Check for world object at this tile first
        var targetObject = GetObjectAtTile(tileCoords);
        if (targetObject != null)
        {
            // Handle object interaction based on tool and object type
            if (InteractWithObject(targetObject, tool))
            {
                return; // Object handled the interaction, don't process tile
            }
        }

        // No object interaction, proceed with tile logic
        var currentTile = CurrentLocation.GetTile(tileCoords.X, tileCoords.Y);

        switch (tool.RegistryKey)
        {
            case "hoe":
            case "earth_wand":
                // Hoe/Earth Wand: Grass/Dirt → Tilled
                if (currentTile.Id == Tile.Grass.Id)
                {
                    CurrentLocation.SetTile(tileCoords.X, tileCoords.Y, Tile.Tilled);
                    Debug.WriteLine($"[{tool.Name}] Tilled grass at ({tileCoords.X}, {tileCoords.Y})");
                }
                else if (currentTile.Id == Tile.Dirt.Id)
                {
                    CurrentLocation.SetTile(tileCoords.X, tileCoords.Y, Tile.Tilled);
                    Debug.WriteLine($"[{tool.Name}] Tilled dirt at ({tileCoords.X}, {tileCoords.Y})");
                }
                else
                {
                    Debug.WriteLine($"[{tool.Name}] Can't till this tile (ID: {currentTile.Id})");
                }
                break;

            case "pickaxe":
                // Pickaxe: Stone tile → Dirt
                if (currentTile.Id == Tile.Stone.Id)
                {
                    CurrentLocation.SetTile(tileCoords.X, tileCoords.Y, Tile.Dirt);
                    Debug.WriteLine($"[Pickaxe] Broke stone tile at ({tileCoords.X}, {tileCoords.Y})");
                }
                else
                {
                    Debug.WriteLine($"[Pickaxe] Nothing to break at ({tileCoords.X}, {tileCoords.Y})");
                }
                break;

            case "watering_can":
            case "hydro_wand":
                // Watering Can/Hydro Wand: Tilled/Dirt → WetDirt
                if (currentTile.Id == Tile.Tilled.Id || currentTile.Id == Tile.Dirt.Id)
                {
                    CurrentLocation.SetTile(tileCoords.X, tileCoords.Y, Tile.WetDirt);
                    Debug.WriteLine($"[{tool.Name}] Watered soil at ({tileCoords.X}, {tileCoords.Y})");
                }
                else
                {
                    Debug.WriteLine($"[{tool.Name}] Can't water this tile (ID: {currentTile.Id})");
                }
                break;

            case "axe":
                // Axe: (future - chop trees/stumps)
                Debug.WriteLine($"[Axe] Nothing to chop at ({tileCoords.X}, {tileCoords.Y})");
                break;

            case "scythe":
                // Scythe: (future - harvest crops)
                Debug.WriteLine($"[Scythe] Nothing to harvest at ({tileCoords.X}, {tileCoords.Y})");
                break;

            default:
                Debug.WriteLine($"[Tool] Unknown tool: {tool.RegistryKey}");
                break;
        }
    }

    /// <summary>
    /// Handle interaction between a tool and a world object.
    /// Returns true if the interaction was handled (object consumed the action).
    /// </summary>
    private bool InteractWithObject(WorldObject obj, Tool tool)
    {
        switch (obj.Name)
        {
            case "rock":
                // Pickaxe destroys rocks
                if (tool.RegistryKey == "pickaxe")
                {
                    Objects.Remove(obj);
                    Debug.WriteLine($"[Pickaxe] Destroyed rock at ({obj.Position.X / 64}, {obj.Position.Y / 64})");
                    // TODO: Drop stone material
                    return true;
                }
                Debug.WriteLine($"[{tool.Name}] Can't interact with rock");
                return true; // Block other tools from affecting tile under rock

            case "mana_node":
                // Pickaxe destroys mana nodes
                if (tool.RegistryKey == "pickaxe")
                {
                    Objects.Remove(obj);
                    Debug.WriteLine($"[Pickaxe] Harvested mana node at ({obj.Position.X / 64}, {obj.Position.Y / 64})");
                    // TODO: Give mana or mana crystal
                    return true;
                }
                Debug.WriteLine($"[{tool.Name}] Can't interact with mana node");
                return true; // Block other tools

            case "tree":
                // Axe chops trees
                if (tool.RegistryKey == "axe")
                {
                    Objects.Remove(obj);
                    Debug.WriteLine($"[Axe] Chopped tree at ({obj.Position.X / 64}, {obj.Position.Y / 64})");
                    // TODO: Drop wood material
                    return true;
                }
                Debug.WriteLine($"[{tool.Name}] Can't interact with tree");
                return true; // Block other tools

            case "bush":
                // Scythe cuts bushes
                if (tool.RegistryKey == "scythe")
                {
                    Objects.Remove(obj);
                    Debug.WriteLine($"[Scythe] Cut bush at ({obj.Position.X / 64}, {obj.Position.Y / 64})");
                    // TODO: Drop fiber
                    return true;
                }
                // Other tools pass through bushes
                return false;

            case "fence":
                // Axe breaks fences
                if (tool.RegistryKey == "axe")
                {
                    Objects.Remove(obj);
                    Debug.WriteLine($"[Axe] Broke fence at ({obj.Position.X / 64}, {obj.Position.Y / 64})");
                    return true;
                }
                Debug.WriteLine($"[{tool.Name}] Can't interact with fence");
                return true; // Block other tools

            default:
                // Unknown object type, don't block tile interaction
                return false;
        }
    }

    #endregion

    #region Drawing

    /// <summary>
    /// Get the pixel texture for external rendering (overlays, etc.).
    /// </summary>
    public Texture2D GetPixelTexture() => _pixel;

    /// <summary>
    /// Draw world layer (tiles, objects, player) with camera transform.
    /// Call this first in the render pipeline.
    /// </summary>
    public void DrawWorld(SpriteBatch spriteBatch)
    {
        spriteBatch.Begin(
            sortMode: SpriteSortMode.Deferred,
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.PointClamp,
            transformMatrix: Camera.GetTransformMatrix()
        );

        // Layer 1: Tiles (always background)
        CurrentLocation.Draw(spriteBatch, _pixel, Camera);

        // Layer 2: Reticle (on top of tiles, below objects)
        DrawReticle(spriteBatch);

        // Layer 3: Y-sorted objects and player
        DrawSorted(spriteBatch);

        spriteBatch.End();
    }

    /// <summary>
    /// Draw UI layer (hotbar, clock) in screen space.
    /// Call this last in the render pipeline, after any overlays.
    /// </summary>
    public void DrawUI(SpriteBatch spriteBatch)
    {
        spriteBatch.Begin(
            sortMode: SpriteSortMode.Deferred,
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.PointClamp
        );

        DrawHotbar(spriteBatch);
        DrawClock(spriteBatch);

        spriteBatch.End();
    }

    /// <summary>
    /// Legacy combined draw method. Use DrawWorld + DrawUI for proper layering.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch)
    {
        DrawWorld(spriteBatch);
        DrawUI(spriteBatch);
    }

    /// <summary>
    /// Draw all world objects and player with proper Y-sorting (depth ordering).
    /// Objects with lower Y are drawn first (behind), higher Y drawn last (in front).
    /// </summary>
    private void DrawSorted(SpriteBatch spriteBatch)
    {
        // Build list of all renderables
        var renderables = new List<IRenderable>(Objects.Count + 1);

        // Add player
        renderables.Add(Player);

        // Add all world objects
        foreach (var obj in Objects)
        {
            renderables.Add(obj);
        }

        // Sort by Y position (ascending = back to front)
        renderables.Sort((a, b) => a.SortY.CompareTo(b.SortY));

        // Draw in sorted order
        foreach (var renderable in renderables)
        {
            renderable.Draw(spriteBatch, _pixel);
        }

        // Draw debug collision boxes if enabled
        if (ShowDebug)
        {
            DrawDebugCollisions(spriteBatch);
        }
    }

    /// <summary>
    /// Draw 1px red borders around collision rectangles for debugging.
    /// Shows Player.CollisionBounds and all WorldObject.BoundingBox.
    /// </summary>
    private void DrawDebugCollisions(SpriteBatch spriteBatch)
    {
        Color debugColor = Color.Red;

        // Draw player collision bounds
        DrawRectBorder(spriteBatch, Player.CollisionBounds, debugColor);

        // Draw world object bounding boxes
        foreach (var obj in Objects)
        {
            if (obj.IsCollidable)
            {
                DrawRectBorder(spriteBatch, obj.BoundingBox, debugColor);
            }
        }
    }

    /// <summary>
    /// Draw a 1px border around a rectangle.
    /// </summary>
    private void DrawRectBorder(SpriteBatch spriteBatch, Rectangle rect, Color color)
    {
        // Top
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), color);
        // Bottom
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), color);
        // Left
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), color);
        // Right
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), color);
    }

    /// <summary>
    /// Draw targeting reticle over the selected tile.
    /// Green = in range, Red = out of range.
    /// </summary>
    private void DrawReticle(SpriteBatch spriteBatch)
    {
        // Only show reticle if a tool is selected
        var activeItem = Player.Inventory.GetActiveItem();
        if (activeItem is not Tool)
            return;

        // Validate tile is in map bounds
        if (_targetTile.X < 0 || _targetTile.X >= CurrentLocation.Width ||
            _targetTile.Y < 0 || _targetTile.Y >= CurrentLocation.Height)
            return;

        // Calculate tile rectangle in world coordinates
        var tileRect = new Rectangle(
            _targetTile.X * GameLocation.TileSize,
            _targetTile.Y * GameLocation.TileSize,
            GameLocation.TileSize,
            GameLocation.TileSize
        );

        // Color based on range: Green = in range, Red = out of range
        Color reticleColor = _targetInRange
            ? new Color(0, 255, 0, 100)   // Semi-transparent green
            : new Color(255, 0, 0, 100);  // Semi-transparent red

        // Draw filled rectangle
        spriteBatch.Draw(_pixel, tileRect, reticleColor);

        // Draw white border for visibility
        int borderWidth = 2;
        Color borderColor = new Color(255, 255, 255, 150);

        // Top border
        spriteBatch.Draw(_pixel, new Rectangle(tileRect.X, tileRect.Y, tileRect.Width, borderWidth), borderColor);
        // Bottom border
        spriteBatch.Draw(_pixel, new Rectangle(tileRect.X, tileRect.Bottom - borderWidth, tileRect.Width, borderWidth), borderColor);
        // Left border
        spriteBatch.Draw(_pixel, new Rectangle(tileRect.X, tileRect.Y, borderWidth, tileRect.Height), borderColor);
        // Right border
        spriteBatch.Draw(_pixel, new Rectangle(tileRect.Right - borderWidth, tileRect.Y, borderWidth, tileRect.Height), borderColor);
    }

    #endregion

    #region HUD Drawing

    private const int SlotSize = 50;
    private const int SlotPadding = 4;
    private const int HotbarY = 10;

    private void DrawHotbar(SpriteBatch spriteBatch)
    {
        var viewport = _graphicsDevice.Viewport;
        int totalWidth = Inventory.HotbarSize * (SlotSize + SlotPadding) - SlotPadding;
        int startX = (viewport.Width - totalWidth) / 2;
        int startY = viewport.Height - SlotSize - HotbarY;

        for (int i = 0; i < Inventory.HotbarSize; i++)
        {
            int x = startX + i * (SlotSize + SlotPadding);
            var slotRect = new Rectangle(x, startY, SlotSize, SlotSize);

            bool isSelected = i == Player.Inventory.ActiveSlotIndex;
            var item = Player.Inventory.GetSlot(i);

            // Draw slot background (white border for selected)
            Color borderColor = isSelected ? Color.White : new Color(60, 60, 60);
            spriteBatch.Draw(_pixel, slotRect, borderColor);

            // Draw inner area
            var innerRect = new Rectangle(x + 3, startY + 3, SlotSize - 6, SlotSize - 6);
            Color innerColor = isSelected ? new Color(80, 80, 80) : new Color(40, 40, 40);
            spriteBatch.Draw(_pixel, innerRect, innerColor);

            // Draw item indicator
            if (item != null)
            {
                Color itemColor = GetItemColor(item);
                var itemRect = new Rectangle(x + 8, startY + 8, SlotSize - 16, SlotSize - 16);
                spriteBatch.Draw(_pixel, itemRect, itemColor);

                // Quantity dots for materials
                if (item is Material mat && mat.Quantity > 1)
                {
                    int dots = Math.Min(mat.Quantity / 10, 5);
                    for (int d = 0; d < dots; d++)
                    {
                        var dotRect = new Rectangle(x + 6 + d * 6, startY + SlotSize - 10, 4, 4);
                        spriteBatch.Draw(_pixel, dotRect, Color.White);
                    }
                }
            }

            // Slot number indicator
            var numRect = new Rectangle(x + SlotSize - 12, startY + 2, 10, 10);
            Color numColor = isSelected ? Color.Black : new Color(150, 150, 150);
            spriteBatch.Draw(_pixel, numRect, numColor);
        }
    }

    /// <summary>
    /// Draw the clock UI in the top-right corner.
    /// Shows "Day X" and time with AM/PM indicator.
    /// </summary>
    private void DrawClock(SpriteBatch spriteBatch)
    {
        var viewport = _graphicsDevice.Viewport;

        // Clock box dimensions
        const int boxWidth = 140;
        const int boxHeight = 50;
        const int margin = 10;

        int boxX = viewport.Width - boxWidth - margin;
        int boxY = margin;

        // Draw clock background
        var boxRect = new Rectangle(boxX, boxY, boxWidth, boxHeight);
        spriteBatch.Draw(_pixel, boxRect, new Color(40, 40, 40, 200));

        // Draw border
        DrawRectBorder(spriteBatch, boxRect, new Color(100, 100, 100));

        // Get time components
        int timeOfDay = TimeManager.TimeOfDay;
        int hours = timeOfDay / 100;
        int minutes = timeOfDay % 100;

        // Handle times past midnight (2400+)
        if (hours >= 24)
            hours -= 24;

        // Convert to 12-hour format
        bool isPM = hours >= 12;
        int displayHour = hours % 12;
        if (displayHour == 0)
            displayHour = 12;

        // Draw day indicator (left side)
        int dayX = boxX + 8;
        int dayY = boxY + 8;
        DrawPixelText(spriteBatch, $"Day {TimeManager.Day}", dayX, dayY, Color.White);

        // Draw time (bottom, larger)
        int timeX = boxX + 8;
        int timeY = boxY + 26;
        string timeStr = $"{displayHour}:{minutes:D2}";
        DrawPixelText(spriteBatch, timeStr, timeX, timeY, Color.White);

        // Draw AM/PM indicator
        int ampmX = boxX + 70;
        int ampmY = boxY + 26;
        Color ampmColor = isPM ? new Color(255, 200, 100) : new Color(150, 200, 255);
        DrawPixelText(spriteBatch, isPM ? "PM" : "AM", ampmX, ampmY, ampmColor);

        // Draw pause indicator if paused
        if (TimeManager.IsPaused)
        {
            int pauseX = boxX + boxWidth - 30;
            int pauseY = boxY + 8;
            spriteBatch.Draw(_pixel, new Rectangle(pauseX, pauseY, 4, 12), Color.Red);
            spriteBatch.Draw(_pixel, new Rectangle(pauseX + 7, pauseY, 4, 12), Color.Red);
        }
    }

    /// <summary>
    /// Simple pixel-based text rendering for digits and basic characters.
    /// Each character is 5x7 pixels with 1px spacing.
    /// </summary>
    private void DrawPixelText(SpriteBatch spriteBatch, string text, int x, int y, Color color)
    {
        int cursorX = x;
        const int charWidth = 5;
        const int spacing = 1;

        foreach (char c in text)
        {
            DrawPixelChar(spriteBatch, c, cursorX, y, color);
            cursorX += charWidth + spacing;
        }
    }

    /// <summary>
    /// Draw a single character using pixel patterns.
    /// Supports 0-9, A-Z, colon, and space.
    /// </summary>
    private void DrawPixelChar(SpriteBatch spriteBatch, char c, int x, int y, Color color)
    {
        // 5x7 pixel patterns for characters (1 = filled, 0 = empty)
        // Each string is a row, 5 chars wide, 7 rows
        string[] pattern = c switch
        {
            '0' => new[] { " ### ", "#   #", "#  ##", "# # #", "##  #", "#   #", " ### " },
            '1' => new[] { "  #  ", " ##  ", "  #  ", "  #  ", "  #  ", "  #  ", " ### " },
            '2' => new[] { " ### ", "#   #", "    #", "  ## ", " #   ", "#    ", "#####" },
            '3' => new[] { " ### ", "#   #", "    #", "  ## ", "    #", "#   #", " ### " },
            '4' => new[] { "   # ", "  ## ", " # # ", "#  # ", "#####", "   # ", "   # " },
            '5' => new[] { "#####", "#    ", "#### ", "    #", "    #", "#   #", " ### " },
            '6' => new[] { " ### ", "#    ", "#### ", "#   #", "#   #", "#   #", " ### " },
            '7' => new[] { "#####", "    #", "   # ", "  #  ", " #   ", " #   ", " #   " },
            '8' => new[] { " ### ", "#   #", "#   #", " ### ", "#   #", "#   #", " ### " },
            '9' => new[] { " ### ", "#   #", "#   #", " ####", "    #", "   # ", " ##  " },
            ':' => new[] { "     ", "  #  ", "  #  ", "     ", "  #  ", "  #  ", "     " },
            ' ' => new[] { "     ", "     ", "     ", "     ", "     ", "     ", "     " },
            'D' => new[] { "#### ", "#   #", "#   #", "#   #", "#   #", "#   #", "#### " },
            'a' => new[] { "     ", "     ", " ### ", "    #", " ####", "#   #", " ####" },
            'y' => new[] { "     ", "     ", "#   #", "#   #", " ####", "    #", " ### " },
            'A' => new[] { " ### ", "#   #", "#   #", "#####", "#   #", "#   #", "#   #" },
            'M' => new[] { "#   #", "## ##", "# # #", "#   #", "#   #", "#   #", "#   #" },
            'P' => new[] { "#### ", "#   #", "#   #", "#### ", "#    ", "#    ", "#    " },
            _ => new[] { "     ", "     ", "     ", "     ", "     ", "     ", "     " }
        };

        for (int row = 0; row < 7; row++)
        {
            for (int col = 0; col < 5; col++)
            {
                if (col < pattern[row].Length && pattern[row][col] == '#')
                {
                    spriteBatch.Draw(_pixel, new Rectangle(x + col, y + row, 1, 1), color);
                }
            }
        }
    }

    private static Color GetItemColor(Item item)
    {
        return item.RegistryKey switch
        {
            // Standard Tools
            "hoe" => new Color(139, 90, 43),
            "axe" => new Color(100, 100, 100),
            "pickaxe" => new Color(120, 120, 140),
            "watering_can" => new Color(80, 130, 200),
            "scythe" => new Color(180, 180, 100),
            // Magic Wands
            "earth_wand" => new Color(180, 140, 60),   // Golden brown (magical earth)
            "hydro_wand" => new Color(60, 180, 255),   // Bright cyan (magical water)
            // Materials
            "wood" => new Color(139, 90, 43),
            "stone" => new Color(128, 128, 128),
            "fiber" => new Color(34, 139, 34),
            "coal" => new Color(30, 30, 30),
            "copper_ore" => new Color(184, 115, 51),
            _ => item switch
            {
                Tool => new Color(100, 150, 255),
                Material => new Color(180, 160, 140),
                _ => new Color(200, 200, 200)
            }
        };
    }

    #endregion

    #region Save/Load

    /// <summary>
    /// Get all tiles that differ from the default generated map.
    /// Compares current state against a freshly generated map with same seed.
    /// </summary>
    private List<TileSaveData> GetModifiedTiles()
    {
        var modifiedTiles = new List<TileSaveData>();

        // Generate a fresh "default" map with the same seed
        var defaultMap = GameLocation.CreateTestMap(WorldSeed);

        // Compare every tile
        for (int y = 0; y < CurrentLocation.Height; y++)
        {
            for (int x = 0; x < CurrentLocation.Width; x++)
            {
                var currentTile = CurrentLocation.GetTile(x, y);
                var defaultTile = defaultMap.GetTile(x, y);

                // If tile differs from default, save it
                if (currentTile.Id != defaultTile.Id)
                {
                    modifiedTiles.Add(new TileSaveData(x, y, currentTile.Id));
                }
            }
        }

        return modifiedTiles;
    }

    public SaveData CreateSaveData()
    {
        return new SaveData
        {
            PlayerPositionX = Player.Position.X,
            PlayerPositionY = Player.Position.Y,
            PlayerName = Player.Name,
            CurrentLocationName = CurrentLocationName,
            WorldSeed = WorldSeed,
            InventorySlots = Player.Inventory.ToSaveList(),
            ActiveHotbarSlot = Player.Inventory.ActiveSlotIndex,
            ModifiedTiles = GetModifiedTiles()
        };
    }

    public void ApplySaveData(SaveData data)
    {
        // Update world seed
        WorldSeed = data.WorldSeed;

        // Reinitialize locations with saved seed
        Locations["Farm"] = GameLocation.CreateFarm(WorldSeed);
        Locations["Cabin"] = GameLocation.CreateCabin();

        // Switch to saved location (default to Farm if not found)
        if (Locations.TryGetValue(data.CurrentLocationName, out var savedLocation))
        {
            CurrentLocation = savedLocation;
        }
        else
        {
            CurrentLocation = Locations["Farm"];
        }
        Debug.WriteLine($"[WorldManager] Loaded location: {CurrentLocationName} (seed: {WorldSeed})");

        // Respawn objects for current location
        Objects.Clear();
        if (CurrentLocationName == "Farm")
        {
            SpawnFarmObjects();
        }

        // STEP 3: Apply modified tiles from save data
        foreach (var tileSave in data.ModifiedTiles)
        {
            var tile = GetTileById(tileSave.TileId);
            CurrentLocation.SetTile(tileSave.X, tileSave.Y, tile);
        }
        Debug.WriteLine($"[WorldManager] Applied {data.ModifiedTiles.Count} modified tiles");

        // Restore player state
        Player.Position = new Vector2(data.PlayerPositionX, data.PlayerPositionY);
        Player.Name = data.PlayerName;
        Player.Inventory.LoadFromSaveList(data.InventorySlots);
        Player.Inventory.SelectSlot(data.ActiveHotbarSlot);
    }

    /// <summary>
    /// Get a Tile by its ID.
    /// </summary>
    private static Tile GetTileById(int id) => id switch
    {
        0 => Tile.Grass,
        1 => Tile.Dirt,
        2 => Tile.Water,
        3 => Tile.Stone,
        4 => Tile.WetDirt,
        5 => Tile.Tilled,
        _ => Tile.Grass // Default fallback
    };

    public void Save()
    {
        Debug.WriteLine("[WorldManager] Saving game...");
        var data = CreateSaveData();
        SaveManager.Save(DebugSaveFile, data);
        Debug.WriteLine($"[WorldManager] Saved {data.ModifiedTiles.Count} modified tiles");
    }

    public void Load()
    {
        Debug.WriteLine("[WorldManager] Loading game...");
        var data = SaveManager.Load(DebugSaveFile);
        if (data != null)
        {
            ApplySaveData(data);
            Debug.WriteLine($"[WorldManager] Restored player at ({data.PlayerPositionX}, {data.PlayerPositionY})");
        }
    }

    #endregion
}
