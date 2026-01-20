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

    // Objects stored per-location for global persistence
    public Dictionary<string, List<WorldObject>> LocationObjects { get; private set; } = new();

    // Convenience accessor for current location's objects
    public List<WorldObject> Objects => LocationObjects.TryGetValue(CurrentLocationName, out var objs) ? objs : new();

    // World metadata
    public string CurrentLocationName => CurrentLocation?.Name ?? "Unknown";
    public int WorldSeed { get; private set; }

    // Save file name for debug saves
    private const string DebugSaveFile = "debug_save.json";

    // Tool interaction range (in pixels, ~1.5 tiles)
    private const float InteractionRange = 96f;

    // Tile size constant for alignment calculations
    private const int TileSize = GameLocation.TileSize;

    // Current targeting state
    private Point _targetTile;
    private bool _targetInRange;

    // Debug visualization (F3 toggle)
    public bool ShowDebug { get; private set; }

    // Event: Request to open shipping menu (Game1 subscribes to this)
    public event Action<ShippingBin>? OnOpenShippingMenu;

    // Event: Request to open dialogue (Game1 subscribes to this)
    public event Action<string>? OnOpenDialogue;

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
        Transition.OnSleep += HandleSleep;

        // Subscribe to day change events for daily simulation
        TimeManager.OnDayChanged += OnDayPassed;

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
    /// Objects persist per-location - only spawn on first visit.
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

        // Only spawn objects if this location hasn't been visited yet
        if (!LocationObjects.ContainsKey(targetLocation))
        {
            if (targetLocation == "Farm")
            {
                SpawnFarmObjects();
            }
            else if (targetLocation == "Cabin")
            {
                SpawnCabinObjects();
            }
        }

        // Snap camera to new player position
        Camera.CenterOn(Player.Center);

        Debug.WriteLine($"[Warp] Teleported to {targetLocation} at ({targetPosition.X}, {targetPosition.Y})");
    }

    /// <summary>
    /// Handle the sleep action when screen is fully black.
    /// Saves game and advances to next day.
    /// </summary>
    private void HandleSleep()
    {
        Debug.WriteLine("[Sleep] Screen black - saving and advancing day...");
        Save();
        TimeManager.StartNewDay();
        Debug.WriteLine($"[Sleep] Now Day {TimeManager.Day}, {TimeManager.GetFormattedTime()}");
    }

    /// <summary>
    /// Handle the start of a new day.
    ///
    /// CRITICAL ORDER OF OPERATIONS (Two-Pass System):
    ///
    /// PASS 1 - THE GROWTH PHASE:
    ///   Process OnNewDay() for all world objects while tiles are STILL WET.
    ///   Crops check tile state and grow if soil is wet.
    ///
    /// PASS 2 - THE EVAPORATION PHASE:
    ///   Dry out wet tiles AFTER all objects have processed.
    ///   This ensures crops see wet soil when they wake up.
    ///
    /// BUG FIX: Previously tiles were dried BEFORE objects processed,
    /// causing crops to always see dry soil and never grow.
    /// </summary>
    private void OnDayPassed(int newDay)
    {
        Debug.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Debug.WriteLine($"║ [WorldManager] ═══ NEW DAY {newDay} ═══");
        Debug.WriteLine("╚════════════════════════════════════════════════════════════════╝");

        // ═══════════════════════════════════════════════════════════════════
        // PAYDAY: Process nightly shipments BEFORE anything else
        // This ensures player receives gold before save
        // ═══════════════════════════════════════════════════════════════════
        ProcessNightlyShipments();

        // ═══════════════════════════════════════════════════════════════════
        // RECOVERY: Player wakes up fully rested
        // ═══════════════════════════════════════════════════════════════════
        float oldStamina = Player.CurrentStamina;
        Player.RestoreStamina();
        Debug.WriteLine($"[WorldManager] Stamina restored: {oldStamina:F1} → {Player.CurrentStamina:F1}");

        // ═══════════════════════════════════════════════════════════════════
        // PASS 1: THE GROWTH PHASE
        // Process all objects WHILE TILES ARE STILL WET
        // Crops can check tile state and grow accordingly
        // ═══════════════════════════════════════════════════════════════════
        Debug.WriteLine("[WorldManager] ▶ PASS 1: Growth Phase (tiles still wet)");

        foreach (var (locationName, location) in Locations)
        {
            // Get objects for this location
            if (LocationObjects.TryGetValue(locationName, out var objects))
            {
                ProcessObjectsNewDay(location, objects);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // PASS 2: THE EVAPORATION PHASE
        // Dry out tiles AFTER objects have processed
        // ═══════════════════════════════════════════════════════════════════
        Debug.WriteLine("[WorldManager] ▶ PASS 2: Evaporation Phase (drying tiles)");

        foreach (var (locationName, location) in Locations)
        {
            ResetLocationTiles(location);
        }

        Debug.WriteLine($"[WorldManager] Day {newDay} processing complete");
    }

    /// <summary>
    /// Process all shipping bins and pay the player for shipped items.
    /// Called at start of OnDayPassed, before save.
    /// </summary>
    private void ProcessNightlyShipments()
    {
        int totalEarnings = 0;

        // Find all shipping bins across all locations
        foreach (var (locationName, objects) in LocationObjects)
        {
            foreach (var obj in objects)
            {
                if (obj is ShippingBin bin)
                {
                    int earnings = bin.ProcessNightlyShipment();
                    totalEarnings += earnings;
                }
            }
        }

        // Pay the player
        if (totalEarnings > 0)
        {
            Player.Gold += totalEarnings;
            Debug.WriteLine($"[WorldManager] ★ PAYDAY: Player earned {totalEarnings}g (Total: {Player.Gold}g)");
        }
    }

    /// <summary>
    /// Reset tile states for a location at start of day.
    /// - Wet soil dries out -> becomes tilled soil
    /// </summary>
    private void ResetLocationTiles(GameLocation location)
    {
        int driedCount = 0;

        for (int y = 0; y < location.Height; y++)
        {
            for (int x = 0; x < location.Width; x++)
            {
                var tile = location.GetTile(x, y);

                // WetDirt dries out overnight -> Tilled
                if (tile.Id == Tile.WetDirt.Id)
                {
                    location.SetTile(x, y, Tile.Tilled);
                    driedCount++;
                }
            }
        }

        if (driedCount > 0)
        {
            Debug.WriteLine($"[NewDay] {location.Name}: {driedCount} wet tiles dried out");
        }
    }

    /// <summary>
    /// Process OnNewDay() for all world objects in a specific location.
    /// Passes the location to each object so they can check tile states.
    /// Removes objects that return true (dead/depleted).
    /// </summary>
    /// <param name="location">The location containing these objects (for tile state checks).</param>
    /// <param name="objects">The list of objects to process.</param>
    private void ProcessObjectsNewDay(GameLocation location, List<WorldObject> objects)
    {
        Debug.WriteLine($"[NewDay] Processing {objects.Count} objects in {location.Name}...");

        var objectsToRemove = new List<WorldObject>();

        foreach (var obj in objects)
        {
            // Pass location to OnNewDay so objects can check tile states
            bool shouldRemove = obj.OnNewDay(location);
            if (shouldRemove)
            {
                objectsToRemove.Add(obj);
            }
        }

        // Remove dead/depleted objects
        foreach (var obj in objectsToRemove)
        {
            objects.Remove(obj);
            Point gridPos = obj.GetGridPosition();
            Debug.WriteLine($"[NewDay] Removed {obj.Name} at grid ({gridPos.X}, {gridPos.Y})");
        }

        Debug.WriteLine($"[NewDay] {location.Name}: Processed {objects.Count} objects, removed {objectsToRemove.Count}");
    }

    #region Smart Object Alignment

    /// <summary>
    /// Calculate aligned position for an object on a tile using the Hybrid Approach:
    /// - Small/Flat Objects (Height <= TileSize): Centered vertically on tile
    /// - Tall/Standing Objects (Height > TileSize): Aligned to bottom of tile
    /// - X-Axis: Always centered horizontally
    ///
    /// Note: Returns position for BOTTOM-CENTER pivot system used by WorldObject.
    /// </summary>
    /// <param name="tileX">Tile X coordinate</param>
    /// <param name="tileY">Tile Y coordinate</param>
    /// <param name="objWidth">Object width in pixels</param>
    /// <param name="objHeight">Object height in pixels</param>
    /// <returns>World position for object spawn (bottom-center pivot)</returns>
    private static Vector2 GetAlignedPosition(int tileX, int tileY, int objWidth, int objHeight)
    {
        // X-Axis: Always center horizontally
        // For bottom-center pivot, X is the center of the object
        float x = (tileX * TileSize) + (TileSize / 2f);

        float y;
        if (objHeight <= TileSize)
        {
            // Case A: Small/Flat Objects - Center vertically on tile
            // Visual center of tile, adjusted for bottom-center pivot
            // Tile center Y + half of object height (since pivot is at bottom)
            y = (tileY * TileSize) + (TileSize / 2f) + (objHeight / 2f);
        }
        else
        {
            // Case B: Tall/Standing Objects - Align to bottom of tile
            // Object's feet touch the bottom edge of the tile
            y = (tileY * TileSize) + TileSize;
        }

        return new Vector2(x, y);
    }

    /// <summary>
    /// Get aligned position for a specific object type.
    /// Uses the object's dimensions to determine alignment.
    /// </summary>
    private static Vector2 GetAlignedPositionForObject(int tileX, int tileY, WorldObject obj)
    {
        return GetAlignedPosition(tileX, tileY, obj.Width, obj.Height);
    }

    #endregion

    /// <summary>
    /// Spawn world objects for the Farm location.
    /// Uses WorldSeed for reproducible placement.
    /// Includes polymorphic objects: Trees, ManaNodes, Crops.
    /// </summary>
    private void SpawnFarmObjects()
    {
        // Ensure Farm has an object list
        if (!LocationObjects.ContainsKey("Farm"))
            LocationObjects["Farm"] = new List<WorldObject>();
        LocationObjects["Farm"].Clear();

        // Use world seed for reproducible object placement
        var random = new Random(WorldSeed + 1); // +1 to differ from map generation

        int mapWidth = CurrentLocation.Width;
        int mapHeight = CurrentLocation.Height;

        // Helper to get valid tile coordinates (not on water, not too close to spawn, not on edges)
        (int tileX, int tileY) GetValidTile()
        {
            for (int attempts = 0; attempts < 50; attempts++)
            {
                // Spawn within safe bounds (1 to Width-2, 1 to Height-2) to avoid edges
                int tileX = random.Next(1, mapWidth - 1);
                int tileY = random.Next(1, mapHeight - 1);

                // Skip player spawn area (around 7,7)
                if (Math.Abs(tileX - 7) < 3 && Math.Abs(tileY - 7) < 3)
                    continue;

                // Skip water tiles
                var tile = CurrentLocation.GetTile(tileX, tileY);
                if (tile.Id == Tile.Water.Id)
                    continue;

                return (tileX, tileY);
            }
            return (10, 10); // Fallback
        }

        var farmObjects = LocationObjects["Farm"];

        // Spawn 5 rocks (breakable with pickaxe) - 48x40, small so centered
        for (int i = 0; i < 5; i++)
        {
            var (tx, ty) = GetValidTile();
            var pos = GetAlignedPosition(tx, ty, 48, 40); // Rock dimensions
            farmObjects.Add(WorldObject.CreateRock(pos));
        }

        // Spawn 3 ManaNode crystals (40x48, small-ish so centered)
        {
            var (tx, ty) = GetValidTile();
            farmObjects.Add(ManaNode.CreateArcaneCrystal(GetAlignedPosition(tx, ty, 40, 48)));
        }
        {
            var (tx, ty) = GetValidTile();
            farmObjects.Add(ManaNode.CreateFireCrystal(GetAlignedPosition(tx, ty, 40, 48)));
        }
        {
            var (tx, ty) = GetValidTile();
            farmObjects.Add(ManaNode.CreateNatureCrystal(GetAlignedPosition(tx, ty, 40, 48)));
        }

        // Spawn 2 mature trees (64x96, TALL so bottom-aligned) + 2 saplings (32x48)
        {
            var (tx, ty) = GetValidTile();
            farmObjects.Add(Tree.CreateMatureOak(GetAlignedPosition(tx, ty, 64, 96)));
        }
        {
            var (tx, ty) = GetValidTile();
            farmObjects.Add(Tree.CreateMatureOak(GetAlignedPosition(tx, ty, 64, 96)));
        }
        {
            var (tx, ty) = GetValidTile();
            farmObjects.Add(Tree.CreateOakSapling(GetAlignedPosition(tx, ty, 32, 48)));
        }
        {
            var (tx, ty) = GetValidTile();
            farmObjects.Add(Tree.CreatePineSapling(GetAlignedPosition(tx, ty, 32, 48)));
        }

        // Spawn 3 bushes (40x32, small so centered)
        for (int i = 0; i < 3; i++)
        {
            var (tx, ty) = GetValidTile();
            var pos = GetAlignedPosition(tx, ty, 40, 32); // Bush dimensions
            farmObjects.Add(WorldObject.CreateBush(pos));
        }

        // Spawn 2 test crops on tilled soil (32x16-48 depending on stage, small so centered)
        // Crops start as seeds (32x16)
        {
            var (tileX, tileY) = GetValidTile();
            CurrentLocation.SetTile(tileX, tileY, Tile.Tilled);
            var pos = GetAlignedPosition(tileX, tileY, 32, 16); // Seed dimensions
            farmObjects.Add(Crop.CreateCornSeed(pos, tileX, tileY));
        }
        {
            var (tileX, tileY) = GetValidTile();
            CurrentLocation.SetTile(tileX, tileY, Tile.Tilled);
            var pos = GetAlignedPosition(tileX, tileY, 32, 16); // Seed dimensions
            farmObjects.Add(Crop.CreateTomatoSeed(pos, tileX, tileY));
        }

        // Spawn Shipping Bin at fixed location near farm edge (easy to find)
        // Position: tile (2, 2) - top-left area, accessible
        farmObjects.Add(ShippingBin.Create(2, 2));
        Debug.WriteLine("[WorldManager] Spawned ShippingBin at tile (2, 2)");

        // Spawn welcome Sign near player spawn area
        // Position: tile (12, 5) - easily visible at game start
        farmObjects.Add(Sign.CreateAtTile(12, 5, "Welcome to the Farm! Press E to open your inventory. Click objects to interact with them."));
        Debug.WriteLine("[WorldManager] Spawned Sign at tile (12, 5)");

        Debug.WriteLine($"[WorldManager] Spawned {farmObjects.Count} world objects (seed: {WorldSeed})");
    }

    /// <summary>
    /// Spawn world objects for the Cabin location.
    /// Includes a bed for sleeping.
    /// </summary>
    private void SpawnCabinObjects()
    {
        // Ensure Cabin has an object list
        if (!LocationObjects.ContainsKey("Cabin"))
            LocationObjects["Cabin"] = new List<WorldObject>();
        var cabinObjects = LocationObjects["Cabin"];
        cabinObjects.Clear();

        // Place bed in the upper-left area of the cabin (away from door)
        // Cabin is 10x10 with walls, so interior is roughly 1-8 x 1-8
        // Bed dimensions: 64x48 (tall enough to bottom-align)
        var bedPosition = GetAlignedPosition(2, 3, 64, 48);
        cabinObjects.Add(Bed.CreateWoodenBed(bedPosition));

        Debug.WriteLine($"[WorldManager] Spawned {cabinObjects.Count} cabin objects");
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
    ///
    /// LOGIC FLOW (using AffectsTileThroughObjects):
    /// 1. Check for WorldObject at target tile
    /// 2. IF Object exists:
    ///    - Interact with Object (e.g., Scythe harvesting, Pickaxe breaking)
    ///    - CRITICAL: if (!tool.AffectsTileThroughObjects) return;
    /// 3. If we passed that check (or no object exists), proceed to modify the Tile
    ///
    /// Examples:
    /// - Pickaxe (false): Hits rock and STOPS (doesn't affect tile under it)
    /// - Watering Can (true): Waters crop AND CONTINUES to wet the tile underneath
    /// </summary>
    private void InteractWithTile(Point tileCoords, Tool tool)
    {
        // STEP 0: Check stamina cost (skip free actions)
        if (tool.StaminaCost > 0f)
        {
            if (!Player.TryUseStamina(tool.StaminaCost))
            {
                Debug.WriteLine($"[{tool.Name}] Too tired! Need {tool.StaminaCost} stamina (have {Player.CurrentStamina:F1})");
                // TODO: Play "buzzer/error" sound effect
                return;
            }
            Debug.WriteLine($"[Stamina] Used {tool.StaminaCost} ({Player.CurrentStamina:F1}/{Player.MaxStamina} remaining)");
        }

        // STEP 1: Check for world object at this tile
        var targetObject = GetObjectAtTile(tileCoords);

        // STEP 2: If object exists, interact with it
        if (targetObject != null)
        {
            bool objectHandled = InteractWithObject(targetObject, tool);

            // CRITICAL CHECK: Does this tool affect tiles through objects?
            if (!tool.AffectsTileThroughObjects)
            {
                // Tool effect stops at the object (Pickaxe, Axe, Scythe, etc.)
                return;
            }

            // Tool passes through - continue to tile logic (Watering Can, Hydro Wand, etc.)
            if (objectHandled)
            {
                Debug.WriteLine($"[{tool.Name}] Pass-through: Also affecting tile at ({tileCoords.X}, {tileCoords.Y})");
            }
        }

        // STEP 3: Proceed with tile modification
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
        // Handle polymorphic object types first
        switch (obj)
        {
            case ManaNode manaNode:
                // Pickaxe harvests mana (doesn't destroy node)
                if (tool.RegistryKey == "pickaxe")
                {
                    int mana = manaNode.TryHarvest();
                    if (mana > 0)
                    {
                        Debug.WriteLine($"[Pickaxe] Harvested {mana} mana from node at ({obj.Position.X / 64}, {obj.Position.Y / 64})");
                        // TODO: Add mana to player
                    }
                    else
                    {
                        Debug.WriteLine($"[Pickaxe] Mana node is depleted at ({obj.Position.X / 64}, {obj.Position.Y / 64})");
                    }
                    return true;
                }
                Debug.WriteLine($"[{tool.Name}] Can't interact with mana node");
                return true;

            case Tree tree:
                // Axe chops trees
                if (tool.RegistryKey == "axe")
                {
                    bool remove = tree.TryChop();
                    if (remove)
                    {
                        Objects.Remove(obj);
                        Debug.WriteLine($"[Axe] Removed tree/stump at ({obj.Position.X / 64}, {obj.Position.Y / 64})");
                    }
                    else
                    {
                        Debug.WriteLine($"[Axe] Chopped tree to stump at ({obj.Position.X / 64}, {obj.Position.Y / 64})");
                    }
                    // TODO: Drop wood material
                    return true;
                }
                Debug.WriteLine($"[{tool.Name}] Can't interact with tree");
                return true;

            case Crop crop:
                // Watering can waters crops
                if (tool.RegistryKey == "watering_can" || tool.RegistryKey == "hydro_wand")
                {
                    crop.Water();
                    Debug.WriteLine($"[{tool.Name}] Watered crop at ({obj.Position.X / 64}, {obj.Position.Y / 64})");
                    return true;
                }
                // Scythe (or empty hand in future) harvests mature crops
                if (tool.RegistryKey == "scythe")
                {
                    return TryHarvestCrop(crop, obj);
                }
                // Other tools pass through crops
                return false;

            case Bed bed:
                // Any tool click on bed triggers sleep transition
                if (bed.TrySleep())
                {
                    Debug.WriteLine("[Bed] Player sleeping... Starting sleep transition");
                    Transition.StartSleepTransition();
                    return true;
                }
                Debug.WriteLine("[Bed] Cannot use this bed");
                return true;

            case ShippingBin bin:
                // Open the shipping menu UI (Game1 handles this via event)
                OnOpenShippingMenu?.Invoke(bin);
                Debug.WriteLine("[WorldManager] Opening shipping menu for bin");
                return true; // Always consume the interaction

            case Sign sign:
                // Open dialogue box with sign text (Game1 handles this via event)
                OnOpenDialogue?.Invoke(sign.Text);
                Debug.WriteLine($"[WorldManager] Reading sign: \"{sign.Text}\"");
                return true; // Always consume the interaction
        }

        // Fall back to name-based interaction for base WorldObjects
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

    /// <summary>
    /// Attempt to harvest a crop with full inventory transaction logic.
    ///
    /// HARVEST TRANSACTION FLOW:
    /// 1. Check if crop is ready (Mature stage)
    /// 2. Create harvest item via GetHarvestDrop()
    /// 3. Try to add item to player inventory
    /// 4. IF success:
    ///    - IF crop.Regrows: Reset crop to HarvestResetStage
    ///    - ELSE: Remove crop from world
    /// 5. IF fail (inventory full):
    ///    - Show "Inventory Full" message
    ///    - Do NOT remove or modify the crop
    /// </summary>
    /// <param name="crop">The crop to harvest.</param>
    /// <param name="obj">The WorldObject reference (for removal).</param>
    /// <returns>True - interaction was handled (even if harvest failed).</returns>
    private bool TryHarvestCrop(Crop crop, WorldObject obj)
    {
        Point gridPos = crop.GetGridPosition();

        // Step 1: Check if crop is ready to harvest
        if (!crop.ReadyToHarvest)
        {
            Debug.WriteLine($"[Harvest] Crop not ready ({crop.Stage}) at ({gridPos.X}, {gridPos.Y})");
            // TODO: Play "error" sound
            return true; // Interaction handled, but harvest not possible
        }

        // Step 2: Create the harvest reward item
        Item? reward = crop.GetHarvestDrop();
        if (reward == null)
        {
            Debug.WriteLine($"[Harvest] ERROR: {crop.CropType} has no harvest drop configured!");
            return true;
        }

        // Step 3: Try to add item to player inventory
        bool addedToInventory = Player.Inventory.AddItem(reward);

        // Step 4/5: Handle outcome
        if (addedToInventory)
        {
            // SUCCESS: Item added to inventory
            Debug.WriteLine($"[Harvest] ★ SUCCESS ★ Harvested {reward.Name} x{(reward is Material m ? m.Quantity : 1)} from {crop.CropType}");
            // TODO: Play "pluck" sound

            if (crop.Regrows)
            {
                // Regrowable crop: Reset to earlier stage
                crop.ResetForRegrowth();
                Debug.WriteLine($"[Harvest] {crop.CropType} will regrow from {crop.Stage}");
            }
            else
            {
                // Non-regrowable crop: Remove from world
                Objects.Remove(obj);
                Debug.WriteLine($"[Harvest] Removed {crop.CropType} at ({gridPos.X}, {gridPos.Y})");
            }
        }
        else
        {
            // FAIL: Inventory full - do NOT modify crop
            Debug.WriteLine($"[Harvest] FAILED - Inventory full! {crop.CropType} NOT harvested.");
            // TODO: Play "error" sound
            // TODO: Show "Inventory Full" notification on screen
        }

        return true; // Interaction was handled
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

        // Layer 1: Tiles with wet state visual feedback
        DrawTiles(spriteBatch);

        // Layer 2: Reticle (on top of tiles, below objects)
        DrawReticle(spriteBatch);

        // Layer 3: Y-sorted objects and player
        DrawSorted(spriteBatch);

        spriteBatch.End();
    }

    /// <summary>
    /// Draw all visible tiles with wet state visual tint.
    /// Wet tiles (WetDirt) are rendered with a LightSteelBlue tint overlay.
    /// </summary>
    private void DrawTiles(SpriteBatch spriteBatch)
    {
        // Calculate visible tile range based on camera
        var viewBounds = Camera.GetVisibleBounds();
        int startX = Math.Max(0, (int)(viewBounds.Left / TileSize));
        int startY = Math.Max(0, (int)(viewBounds.Top / TileSize));
        int endX = Math.Min(CurrentLocation.Width, (int)(viewBounds.Right / TileSize) + 1);
        int endY = Math.Min(CurrentLocation.Height, (int)(viewBounds.Bottom / TileSize) + 1);

        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                var tile = CurrentLocation.GetTile(x, y);
                var baseColor = GetTileColor(tile.Id);
                var rect = new Rectangle(x * TileSize, y * TileSize, TileSize, TileSize);

                // Check for wet state and apply visual tint
                Color drawColor = baseColor;
                if (tile.Id == Tile.WetDirt.Id)
                {
                    // Wet tiles get a LightSteelBlue tint for visual feedback
                    drawColor = Color.Lerp(baseColor, Color.LightSteelBlue, 0.4f);
                }

                spriteBatch.Draw(_pixel, rect, drawColor);
            }
        }
    }

    /// <summary>
    /// Get the base color for a tile ID.
    /// </summary>
    private static Color GetTileColor(int tileId) => tileId switch
    {
        0 => new Color(34, 139, 34),   // Grass - forest green
        1 => new Color(139, 90, 43),   // Dirt - brown
        2 => new Color(30, 144, 255),  // Water - blue
        3 => new Color(128, 128, 128), // Stone - gray
        4 => new Color(90, 70, 50),    // WetDirt - dark soil base (tint applied in DrawTiles)
        5 => new Color(160, 110, 60),  // Tilled - light brown with furrows
        6 => new Color(139, 90, 60),   // Wood - wood floor
        7 => new Color(80, 80, 90),    // Wall - dark gray stone wall
        _ => Color.Magenta             // Unknown - debug pink
    };

    /// <summary>
    /// Draw UI layer (hotbar, clock, stamina) in screen space.
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
        DrawStaminaBar(spriteBatch);

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
        // Using 0.8f alpha (204/255) for better visibility
        Color reticleColor = _targetInRange
            ? new Color(0, 255, 0, 204)   // Bright green (80% opacity)
            : new Color(255, 0, 0, 204);  // Bright red (80% opacity)

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
    /// Draw the stamina/energy bar in the bottom-right corner.
    /// Vertical bar with color gradient: Green > 50%, Yellow 20-50%, Red < 20%.
    /// </summary>
    private void DrawStaminaBar(SpriteBatch spriteBatch)
    {
        var viewport = _graphicsDevice.Viewport;

        // Bar dimensions and position
        const int barWidth = 20;
        const int barHeight = 100;
        const int margin = 20;
        const int borderWidth = 2;

        int barX = viewport.Width - barWidth - margin;
        int barY = viewport.Height - barHeight - margin;

        // Background (black border)
        var borderRect = new Rectangle(barX - borderWidth, barY - borderWidth, barWidth + borderWidth * 2, barHeight + borderWidth * 2);
        spriteBatch.Draw(_pixel, borderRect, new Color(20, 20, 20));

        // Inner background (dark gray)
        var bgRect = new Rectangle(barX, barY, barWidth, barHeight);
        spriteBatch.Draw(_pixel, bgRect, new Color(40, 40, 50));

        // Calculate fill percentage
        float staminaPercent = Player.CurrentStamina / Player.MaxStamina;
        int fillHeight = (int)(barHeight * staminaPercent);

        // Color based on percentage: Green > 50%, Yellow 20-50%, Red < 20%
        Color fillColor;
        if (staminaPercent > 0.5f)
            fillColor = new Color(50, 200, 50);   // Green - healthy
        else if (staminaPercent > 0.2f)
            fillColor = new Color(220, 200, 50);  // Yellow - caution
        else
            fillColor = new Color(200, 50, 50);   // Red - exhausted

        // Draw fill (bottom-up)
        if (fillHeight > 0)
        {
            var fillRect = new Rectangle(barX, barY + (barHeight - fillHeight), barWidth, fillHeight);
            spriteBatch.Draw(_pixel, fillRect, fillColor);
        }

        // Draw border
        DrawRectBorder(spriteBatch, borderRect, new Color(100, 100, 100));

        // Tooltip on hover
        var mouseState = Input.GetMouseState();
        if (borderRect.Contains(mouseState.Position))
        {
            string tooltipText = $"Energy: {(int)Player.CurrentStamina}/{(int)Player.MaxStamina}";
            int tooltipWidth = tooltipText.Length * 6 + 10;
            int tooltipX = barX - tooltipWidth - 5;
            int tooltipY = barY + barHeight / 2 - 10;

            // Tooltip background
            var tooltipRect = new Rectangle(tooltipX, tooltipY, tooltipWidth, 16);
            spriteBatch.Draw(_pixel, tooltipRect, new Color(0, 0, 0, 200));
            DrawRectBorder(spriteBatch, tooltipRect, new Color(100, 100, 100));

            // Tooltip text
            DrawPixelText(spriteBatch, tooltipText, tooltipX + 5, tooltipY + 4, Color.White);
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
            // Harvest Items (Crops)
            "corn" => new Color(255, 220, 80),      // Yellow
            "tomato" => new Color(220, 50, 50),     // Red
            "potato" => new Color(180, 140, 80),    // Tan
            "carrot" => new Color(255, 140, 0),     // Orange
            "wheat" => new Color(220, 190, 100),    // Golden
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
    /// Get the default (freshly generated) version of a location for tile comparison.
    /// </summary>
    private GameLocation GetDefaultLocation(string locationName)
    {
        return locationName switch
        {
            "Farm" => GameLocation.CreateFarm(WorldSeed),
            "Cabin" => GameLocation.CreateCabin(),
            _ => GameLocation.CreateFarm(WorldSeed) // Fallback
        };
    }

    /// <summary>
    /// Get modified tiles for a specific location by comparing against its default.
    /// </summary>
    private List<TileSaveData> GetModifiedTilesForLocation(GameLocation location)
    {
        var modifiedTiles = new List<TileSaveData>();
        var defaultMap = GetDefaultLocation(location.Name);

        for (int y = 0; y < location.Height; y++)
        {
            for (int x = 0; x < location.Width; x++)
            {
                var currentTile = location.GetTile(x, y);
                var defaultTile = defaultMap.GetTile(x, y);

                if (currentTile.Id != defaultTile.Id)
                {
                    modifiedTiles.Add(new TileSaveData(x, y, currentTile.Id));
                }
            }
        }

        return modifiedTiles;
    }

    /// <summary>
    /// Creates save data for ALL locations (global persistence).
    /// </summary>
    public SaveData CreateSaveData()
    {
        // Save ALL locations (tiles + objects)
        var locationSaveData = new List<LocationSaveData>();

        foreach (var (locationName, location) in Locations)
        {
            var locData = new LocationSaveData
            {
                Name = locationName,
                ModifiedTiles = GetModifiedTilesForLocation(location),
                Objects = LocationObjects.TryGetValue(locationName, out var objs)
                    ? new List<WorldObject>(objs)
                    : new List<WorldObject>()
            };
            locationSaveData.Add(locData);
            Debug.WriteLine($"[Save] {locationName}: {locData.ModifiedTiles.Count} tiles, {locData.Objects.Count} objects");
        }

        return new SaveData
        {
            PlayerPositionX = Player.Position.X,
            PlayerPositionY = Player.Position.Y,
            PlayerName = Player.Name,
            PlayerGold = Player.Gold,
            PlayerStamina = Player.CurrentStamina,
            CurrentLocationName = CurrentLocationName,
            WorldSeed = WorldSeed,
            Day = TimeManager.Day,
            TimeOfDay = TimeManager.TimeOfDay,
            InventorySlots = Player.Inventory.ToSaveList(),
            ActiveHotbarSlot = Player.Inventory.ActiveSlotIndex,
            Locations = locationSaveData
        };
    }

    /// <summary>
    /// Restores ALL locations from save data (global persistence).
    /// </summary>
    public void ApplySaveData(SaveData data)
    {
        // Update world seed
        WorldSeed = data.WorldSeed;

        // Restore time state (without triggering OnDayChanged)
        TimeManager.SetTime(data.Day, data.TimeOfDay);
        Debug.WriteLine($"[WorldManager] Restored time: Day {data.Day}, {TimeManager.GetFormattedTime()}");

        // Reinitialize locations with saved seed (creates default tiles)
        Locations["Farm"] = GameLocation.CreateFarm(WorldSeed);
        Locations["Cabin"] = GameLocation.CreateCabin();

        // Clear all location objects
        LocationObjects.Clear();

        // Restore ALL locations from save data
        foreach (var locData in data.Locations)
        {
            // Find the location
            if (!Locations.TryGetValue(locData.Name, out var location))
            {
                Debug.WriteLine($"[Load] WARNING: Unknown location '{locData.Name}' in save");
                continue;
            }

            // Apply modified tiles
            foreach (var tileSave in locData.ModifiedTiles)
            {
                var tile = GetTileById(tileSave.TileId);
                location.SetTile(tileSave.X, tileSave.Y, tile);
            }

            // Restore objects (or spawn defaults if none saved)
            if (locData.Objects.Count > 0)
            {
                LocationObjects[locData.Name] = new List<WorldObject>(locData.Objects);
            }
            else
            {
                // No saved objects - spawn defaults
                if (locData.Name == "Farm")
                    SpawnFarmObjects();
                else if (locData.Name == "Cabin")
                    SpawnCabinObjects();
            }

            Debug.WriteLine($"[Load] {locData.Name}: {locData.ModifiedTiles.Count} tiles, {LocationObjects.GetValueOrDefault(locData.Name)?.Count ?? 0} objects");
        }

        // Switch to saved location (default to Farm if not found)
        if (Locations.TryGetValue(data.CurrentLocationName, out var savedLocation))
        {
            CurrentLocation = savedLocation;
        }
        else
        {
            CurrentLocation = Locations["Farm"];
        }

        // Restore player state
        Player.Position = new Vector2(data.PlayerPositionX, data.PlayerPositionY);
        Player.Name = data.PlayerName;
        Player.Gold = data.PlayerGold;
        Player.CurrentStamina = data.PlayerStamina;
        Player.Inventory.LoadFromSaveList(data.InventorySlots);
        Player.Inventory.SelectSlot(data.ActiveHotbarSlot);

        Debug.WriteLine($"[WorldManager] Loaded location: {CurrentLocationName}");
        Debug.WriteLine($"[WorldManager] Restored player gold: {Player.Gold}g, stamina: {Player.CurrentStamina:F1}");
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
        6 => Tile.Wood,
        7 => Tile.Wall,
        _ => Tile.Grass // Default fallback
    };

    public void Save()
    {
        Debug.WriteLine("[WorldManager] Saving game...");
        var data = CreateSaveData();
        Debug.WriteLine($"[WorldManager] Saving time: Day {data.Day}, Time {data.TimeOfDay} ({TimeManager.GetFormattedTime()})");
        SaveManager.Save(DebugSaveFile, data);
        Debug.WriteLine($"[WorldManager] Saved {data.Locations.Count} locations");
    }

    public void Load()
    {
        Debug.WriteLine("[WorldManager] Loading game...");
        var data = SaveManager.Load(DebugSaveFile);
        if (data != null)
        {
            Debug.WriteLine($"[WorldManager] Loading time from save: Day {data.Day}, Time {data.TimeOfDay}");
            ApplySaveData(data);
            Debug.WriteLine($"[WorldManager] After ApplySaveData: Day {TimeManager.Day}, Time {TimeManager.TimeOfDay}");
            Debug.WriteLine($"[WorldManager] Restored player at ({data.PlayerPositionX}, {data.PlayerPositionY})");
        }
    }

    #endregion
}
