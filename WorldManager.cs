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

    // Enemies stored per-location
    public Dictionary<string, List<Enemy>> LocationEnemies { get; private set; } = new();

    // Projectiles in current location (not persisted - cleared on location change)
    public List<Projectile> Projectiles { get; private set; } = new();

    // Debug visualization data
    private Rectangle? _debugMeleeHitbox;
    private float _debugMeleeHitboxTimer;
    private Vector2? _debugRayStart;
    private Vector2? _debugRayEnd;
    private float _debugRayTimer;
    private const float DebugHitboxDisplayTime = 0.2f;
    private const float DebugRayDisplayTime = 0.1f;

    // Convenience accessor for current location's objects
    public List<WorldObject> Objects => LocationObjects.TryGetValue(CurrentLocationName, out var objs) ? objs : new();

    // Convenience accessor for current location's enemies
    public List<Enemy> Enemies => LocationEnemies.TryGetValue(CurrentLocationName, out var enemies) ? enemies : new();

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

        // ═══════════════════════════════════════════════════════════════════
        // PASS 3: THE HORDE RETURNS
        // Spawn new enemies in the Danger Zone
        // ═══════════════════════════════════════════════════════════════════
        Debug.WriteLine("[WorldManager] ▶ PASS 3: Enemy Respawn Phase");
        SpawnDailyEnemies();

        Debug.WriteLine($"[WorldManager] Day {newDay} processing complete");
    }

    /// <summary>
    /// Spawn a new wave of enemies in the Danger Zone (South Zone, Y > 60).
    /// Called each morning to ensure sustainable combat.
    ///
    /// RULES:
    /// - Only spawns on Farm location
    /// - Spawns in South Zone (Y > 60) away from bridge
    /// - Safety checks: No spawning on solid tiles or occupied positions
    /// - Spawns a mix of enemy types based on difficulty
    /// </summary>
    private void SpawnDailyEnemies()
    {
        // Only spawn on Farm
        if (!LocationEnemies.TryGetValue("Farm", out var farmEnemies))
        {
            farmEnemies = new List<Enemy>();
            LocationEnemies["Farm"] = farmEnemies;
        }

        // Don't spawn if too many enemies already (cap at 15)
        if (farmEnemies.Count >= 15)
        {
            Debug.WriteLine("[Spawn] Enemy cap reached (15), skipping daily spawn");
            return;
        }

        // Get Farm location for tile checks
        if (!Locations.TryGetValue("Farm", out var farm))
            return;

        var random = new Random();
        int spawned = 0;
        int targetSpawns = 5; // Spawn 5 enemies per day
        int maxAttempts = 20; // Prevent infinite loop
        int attempts = 0;

        while (spawned < targetSpawns && attempts < maxAttempts)
        {
            attempts++;

            // Random position in Danger Zone (Y 61-95, X 5-45)
            int tileX = random.Next(5, 46);
            int tileY = random.Next(61, 96);

            // Safety Check 1: Is tile walkable?
            var tile = farm.GetTile(tileX, tileY);
            if (!tile.Walkable)
            {
                continue; // Retry - can't spawn on water/walls
            }

            // Convert to world position (center of tile)
            Vector2 worldPos = new Vector2(
                tileX * TileSize + TileSize / 2f,
                tileY * TileSize + TileSize
            );

            // Safety Check 2: Is position occupied by another enemy?
            bool occupied = false;
            foreach (var existing in farmEnemies)
            {
                if (Vector2.Distance(existing.Position, worldPos) < 64)
                {
                    occupied = true;
                    break;
                }
            }
            if (occupied) continue;

            // Safety Check 3: Is position occupied by a world object?
            if (LocationObjects.TryGetValue("Farm", out var farmObjects))
            {
                foreach (var obj in farmObjects)
                {
                    if (obj.IsCollidable && obj.BoundingBox.Contains(worldPos.ToPoint()))
                    {
                        occupied = true;
                        break;
                    }
                }
            }
            if (occupied) continue;

            // ═══════════════════════════════════════════════════════════════════
            // SPAWN! Pick enemy type based on weighted random
            // ═══════════════════════════════════════════════════════════════════
            Enemy newEnemy;
            int roll = random.Next(100);

            if (roll < 50)
            {
                // 50% Goblin (common)
                newEnemy = Enemy.CreateGoblin(worldPos);
            }
            else if (roll < 80)
            {
                // 30% Slime (common, weaker)
                newEnemy = Enemy.CreateSlime(worldPos);
            }
            else
            {
                // 20% Skeleton (rare, stronger)
                newEnemy = Enemy.CreateSkeleton(worldPos);
            }

            farmEnemies.Add(newEnemy);
            spawned++;
            Debug.WriteLine($"[Spawn] {newEnemy.Name} spawned at tile ({tileX}, {tileY})");
        }

        Debug.WriteLine($"[Spawn] Daily spawn complete: {spawned} enemies added (Total: {farmEnemies.Count})");
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
    /// Spawn world objects for the Farm location (fixed 50x50 layout).
    ///
    /// ZONES:
    /// - Home Base (10-14, 10-14): ShippingBin, Sign - safe area
    /// - Garden (15-25, 10-20): Crops spawn here (tillable dirt)
    /// - Forest (X > 30): Trees and rocks for stamina testing
    /// - Lawn (X less than 30, not Garden): Safe grass for zero-cost testing
    /// </summary>
    private void SpawnFarmObjects()
    {
        // Ensure Farm has an object list
        if (!LocationObjects.ContainsKey("Farm"))
            LocationObjects["Farm"] = new List<WorldObject>();
        LocationObjects["Farm"].Clear();

        var farmObjects = LocationObjects["Farm"];

        // ═══════════════════════════════════════════════════════════════════
        // HOME BASE AREA (around 10,10)
        // ═══════════════════════════════════════════════════════════════════

        // Shipping Bin at (12, 12) - easy access
        farmObjects.Add(ShippingBin.Create(12, 12));
        Debug.WriteLine("[WorldManager] Spawned ShippingBin at tile (12, 12)");

        // Welcome Sign at (12, 14)
        farmObjects.Add(Sign.CreateAtTile(12, 14, "Welcome to the Farm! Press E to open your inventory. Click objects to interact with them."));
        Debug.WriteLine("[WorldManager] Spawned Sign at tile (12, 14)");

        // ═══════════════════════════════════════════════════════════════════
        // GARDEN AREA (15-25, 10-20) - Test crops in tillable dirt
        // ═══════════════════════════════════════════════════════════════════

        // Pre-plant some test crops in the garden
        // Corn at (17, 12)
        CurrentLocation.SetTile(17, 12, Tile.Tilled);
        farmObjects.Add(Crop.CreateCornSeed(GetAlignedPosition(17, 12, 32, 16), 17, 12));

        // Tomato at (19, 12)
        CurrentLocation.SetTile(19, 12, Tile.Tilled);
        farmObjects.Add(Crop.CreateTomatoSeed(GetAlignedPosition(19, 12, 32, 16), 19, 12));

        // Potato at (21, 12)
        CurrentLocation.SetTile(21, 12, Tile.Tilled);
        farmObjects.Add(Crop.CreatePotatoSeed(GetAlignedPosition(21, 12, 32, 16), 21, 12));

        // ═══════════════════════════════════════════════════════════════════
        // FOREST AREA (X > 30) - Trees and rocks for stamina testing
        // ═══════════════════════════════════════════════════════════════════

        // Trees (64x96, tall so bottom-aligned)
        farmObjects.Add(Tree.CreateMatureOak(GetAlignedPosition(32, 10, 64, 96)));
        farmObjects.Add(Tree.CreateMatureOak(GetAlignedPosition(35, 12, 64, 96)));
        farmObjects.Add(Tree.CreateMatureOak(GetAlignedPosition(38, 8, 64, 96)));
        farmObjects.Add(Tree.CreateMatureOak(GetAlignedPosition(33, 18, 64, 96)));
        farmObjects.Add(Tree.CreateMatureOak(GetAlignedPosition(40, 15, 64, 96)));

        // Saplings (will grow over time)
        farmObjects.Add(Tree.CreateOakSapling(GetAlignedPosition(36, 20, 32, 48)));
        farmObjects.Add(Tree.CreatePineSapling(GetAlignedPosition(42, 12, 32, 48)));

        // Rocks (48x40, small so centered) - breakable with pickaxe
        farmObjects.Add(WorldObject.CreateRock(GetAlignedPosition(34, 15, 48, 40)));
        farmObjects.Add(WorldObject.CreateRock(GetAlignedPosition(37, 10, 48, 40)));
        farmObjects.Add(WorldObject.CreateRock(GetAlignedPosition(41, 18, 48, 40)));
        farmObjects.Add(WorldObject.CreateRock(GetAlignedPosition(45, 14, 48, 40)));
        farmObjects.Add(WorldObject.CreateRock(GetAlignedPosition(43, 22, 48, 40)));

        // Mana Nodes (40x48) - in forest area
        farmObjects.Add(ManaNode.CreateArcaneCrystal(GetAlignedPosition(35, 25, 40, 48)));
        farmObjects.Add(ManaNode.CreateFireCrystal(GetAlignedPosition(40, 28, 40, 48)));
        farmObjects.Add(ManaNode.CreateNatureCrystal(GetAlignedPosition(45, 20, 40, 48)));

        // ═══════════════════════════════════════════════════════════════════
        // DECORATIVE (Lawn area - X < 30, outside garden)
        // ═══════════════════════════════════════════════════════════════════

        // A few bushes scattered on the lawn
        farmObjects.Add(WorldObject.CreateBush(GetAlignedPosition(5, 8, 40, 32)));
        farmObjects.Add(WorldObject.CreateBush(GetAlignedPosition(8, 18, 40, 32)));
        farmObjects.Add(WorldObject.CreateBush(GetAlignedPosition(3, 25, 40, 32)));

        // ═══════════════════════════════════════════════════════════════════
        // DANGER ZONE (Y > 50) - South Zone with enemies
        // ═══════════════════════════════════════════════════════════════════

        // Warning sign at bridge
        farmObjects.Add(Sign.CreateAtTile(24, 48, "WARNING: Danger Zone ahead! Hostile creatures roam the southern lands."));

        // Some rocks and trees in danger zone for cover
        farmObjects.Add(WorldObject.CreateRock(GetAlignedPosition(15, 60, 48, 40)));
        farmObjects.Add(WorldObject.CreateRock(GetAlignedPosition(30, 65, 48, 40)));
        farmObjects.Add(WorldObject.CreateRock(GetAlignedPosition(10, 75, 48, 40)));
        farmObjects.Add(WorldObject.CreateRock(GetAlignedPosition(40, 70, 48, 40)));

        farmObjects.Add(Tree.CreateMatureOak(GetAlignedPosition(8, 58, 64, 96)));
        farmObjects.Add(Tree.CreateMatureOak(GetAlignedPosition(42, 62, 64, 96)));
        farmObjects.Add(Tree.CreateMatureOak(GetAlignedPosition(20, 80, 64, 96)));

        Debug.WriteLine($"[WorldManager] Spawned {farmObjects.Count} world objects (fixed layout)");

        // ═══════════════════════════════════════════════════════════════════
        // ENEMIES - Spawn in Danger Zone
        // ═══════════════════════════════════════════════════════════════════
        SpawnFarmEnemies();
    }

    /// <summary>
    /// Spawn enemies for the Farm location (Danger Zone only).
    /// </summary>
    private void SpawnFarmEnemies()
    {
        // Ensure Farm has an enemy list
        if (!LocationEnemies.ContainsKey("Farm"))
            LocationEnemies["Farm"] = new List<Enemy>();
        LocationEnemies["Farm"].Clear();

        var farmEnemies = LocationEnemies["Farm"];

        // Spawn enemies in the Danger Zone (Y > 50)
        // Goblin at (25, 75) - main test enemy
        farmEnemies.Add(Enemy.CreateGoblin(GetAlignedPosition(25, 75, 36, 40)));
        Debug.WriteLine("[WorldManager] Spawned Goblin at tile (25, 75)");

        // Additional enemies for variety
        farmEnemies.Add(Enemy.CreateSlime(GetAlignedPosition(15, 65, 32, 28)));
        farmEnemies.Add(Enemy.CreateSlime(GetAlignedPosition(35, 70, 32, 28)));
        farmEnemies.Add(Enemy.CreateGoblin(GetAlignedPosition(20, 85, 36, 40)));
        farmEnemies.Add(Enemy.CreateSkeleton(GetAlignedPosition(40, 80, 40, 48)));

        Debug.WriteLine($"[WorldManager] Spawned {farmEnemies.Count} enemies in Danger Zone");
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

        // Update enemies
        UpdateEnemies(deltaTime);

        // Update projectiles
        UpdateProjectiles(deltaTime);

        // Update debug visualization timers
        UpdateDebugTimers(deltaTime);

        // Handle tool use on left click
        if (Input.IsLeftMousePressed())
        {
            TryUseTool();
        }
    }

    /// <summary>
    /// Update all projectiles in the current location.
    /// </summary>
    private void UpdateProjectiles(float deltaTime)
    {
        var toRemove = new List<Projectile>();

        foreach (var projectile in Projectiles)
        {
            bool shouldRemove = projectile.Update(deltaTime, Enemies, IsTileSolidForProjectile);

            if (shouldRemove || !projectile.IsActive)
            {
                toRemove.Add(projectile);
            }
        }

        foreach (var projectile in toRemove)
        {
            Projectiles.Remove(projectile);
        }
    }

    /// <summary>
    /// Check if a world position is solid (for projectile/raycast collision).
    /// Checks BOTH terrain tiles AND world objects (trees, rocks, etc.).
    /// </summary>
    private bool IsTileSolidForProjectile(Vector2 worldPos)
    {
        int tileX = (int)(worldPos.X / TileSize);
        int tileY = (int)(worldPos.Y / TileSize);

        // Out of bounds = solid
        if (tileX < 0 || tileX >= CurrentLocation.Width ||
            tileY < 0 || tileY >= CurrentLocation.Height)
        {
            return true;
        }

        // Check 1: Terrain - Non-walkable tiles are solid (walls, water)
        var tile = CurrentLocation.GetTile(tileX, tileY);
        if (!tile.Walkable)
        {
            return true;
        }

        // Check 2: World Objects - Trees, rocks, and other collidable objects
        if (IsPositionBlockedByObject(worldPos))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Check if a world position intersects with any collidable world object.
    /// Used for projectile and raycast collision with trees, rocks, etc.
    /// </summary>
    private bool IsPositionBlockedByObject(Vector2 worldPos)
    {
        // Create a small hitbox at the position (4x4 pixel point)
        var pointRect = new Rectangle(
            (int)worldPos.X - 2,
            (int)worldPos.Y - 2,
            4,
            4
        );

        foreach (var obj in Objects)
        {
            // Skip non-collidable objects (crops, etc.)
            if (!obj.IsCollidable)
                continue;

            // Skip objects that projectiles should pass through
            // (e.g., bushes - small and soft)
            if (obj.Name == "bush")
                continue;

            // Check if the point intersects the object's bounding box
            if (pointRect.Intersects(obj.BoundingBox))
            {
                Debug.WriteLine($"[Collision] Projectile blocked by {obj.Name} at ({worldPos.X:F0}, {worldPos.Y:F0})");
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Update debug visualization timers.
    /// </summary>
    private void UpdateDebugTimers(float deltaTime)
    {
        if (_debugMeleeHitboxTimer > 0)
        {
            _debugMeleeHitboxTimer -= deltaTime;
            if (_debugMeleeHitboxTimer <= 0)
                _debugMeleeHitbox = null;
        }

        if (_debugRayTimer > 0)
        {
            _debugRayTimer -= deltaTime;
            if (_debugRayTimer <= 0)
            {
                _debugRayStart = null;
                _debugRayEnd = null;
            }
        }
    }

    /// <summary>
    /// Update all enemies in the current location.
    /// Handles AI, movement, and collision with player.
    /// </summary>
    private void UpdateEnemies(float deltaTime)
    {
        if (!LocationEnemies.TryGetValue(CurrentLocationName, out var enemies))
            return;

        var deadEnemies = new List<Enemy>();

        foreach (var enemy in enemies)
        {
            // Update enemy AI (chasing, contact damage)
            enemy.Update(Player, deltaTime, CanMoveEnemy);

            // Track dead enemies for removal
            if (enemy.IsDead)
            {
                deadEnemies.Add(enemy);
            }
        }

        // Remove dead enemies and spawn loot
        foreach (var dead in deadEnemies)
        {
            enemies.Remove(dead);
            SpawnEnemyLoot(dead);
            Debug.WriteLine($"[WorldManager] {dead.Name} removed from world");
        }
    }

    /// <summary>
    /// Check if an enemy can move to a position.
    /// </summary>
    private bool CanMoveEnemy(Rectangle bounds)
    {
        // Check tile collisions
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
    /// Spawn loot when an enemy dies.
    /// </summary>
    private void SpawnEnemyLoot(Enemy enemy)
    {
        // Random chance to drop loot
        var random = new Random();
        if (random.NextDouble() < 0.5) // 50% chance
        {
            // For now, add gold directly
            int goldDrop = random.Next(1, 5) * enemy.MaxHP;
            Player.Gold += goldDrop;
            Debug.WriteLine($"[Loot] {enemy.Name} dropped {goldDrop}g!");

            // TODO: Spawn actual loot items on ground
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
        // ═══════════════════════════════════════════════════════════════════
        // SMART TARGETING: Eliminates pixel hunting for farming tools
        //
        // If mouse is within range: Use precise mouse tile (player has control)
        // If mouse is out of range: Snap to adjacent tile in mouse direction
        //
        // This means clicking far to the right targets the tile to your right,
        // no matter how far away your mouse cursor is!
        // ═══════════════════════════════════════════════════════════════════

        // Get mouse position in world coordinates
        Vector2 mouseWorld = Input.GetMouseWorldPosition(Camera);

        // Use smart targeting to get the target tile
        _targetTile = Player.GetSmartTile(mouseWorld, InteractionRange, TileSize);

        // Check if target tile is within map bounds
        bool inBounds = _targetTile.X >= 0 && _targetTile.X < CurrentLocation.Width &&
                        _targetTile.Y >= 0 && _targetTile.Y < CurrentLocation.Height;

        // Check distance to the SMART tile (not mouse position)
        var tileCenterWorld = InputManager.GetTileCenterWorld(_targetTile);
        float distanceToTile = Vector2.Distance(Player.Center, tileCenterWorld);

        // Smart tile is always "in range" if it's adjacent (within ~1.5 tiles)
        // This is because GetSmartTile guarantees adjacent tile when out of range
        _targetInRange = inBounds && distanceToTile <= InteractionRange + TileSize;
    }

    private void TryUseTool()
    {
        // ═══════════════════════════════════════════════════════════════════
        // PRIORITY CHECK: WEAPONS BYPASS RANGE/TILE VALIDATION
        // Combat weapons (Sword, Wand, Staff) use mouse aiming with clamped
        // range - they don't need valid tile targeting. Fire toward cursor!
        // ═══════════════════════════════════════════════════════════════════
        var activeItem = Player.Inventory.GetActiveItem();
        if (activeItem is Tool weapon && weapon.IsWeapon)
        {
            // Weapons work regardless of cursor color/range
            PerformWeaponAttack(weapon);
            return;
        }

        // ═══════════════════════════════════════════════════════════════════
        // FARMING TOOLS: Require valid tile targeting (range + bounds check)
        // ═══════════════════════════════════════════════════════════════════
        if (!_targetInRange)
        {
            Debug.WriteLine("[Interact] Target out of range");
            return;
        }

        if (_targetTile.X < 0 || _targetTile.X >= CurrentLocation.Width ||
            _targetTile.Y < 0 || _targetTile.Y >= CurrentLocation.Height)
        {
            Debug.WriteLine("[Interact] Target out of bounds");
            return;
        }

        // ═══════════════════════════════════════════════════════════════════
        // STEP 1: CHECK FOR INTERACTIVE OBJECTS (Priority over tool use!)
        // Objects like ShippingBin, Sign, Bed should work regardless of held item.
        // No stamina cost - just interact and return.
        // ═══════════════════════════════════════════════════════════════════
        var targetObject = GetObjectAtTile(_targetTile);
        if (targetObject != null && TryInteractWithObject(targetObject))
        {
            return; // Interaction handled, no tool use needed
        }

        // ═══════════════════════════════════════════════════════════════════
        // STEP 2: CHECK FOR HARVESTABLE CROPS (Free action, any item or empty hand)
        // Mature crops can be harvested without tools and without stamina cost.
        // ═══════════════════════════════════════════════════════════════════
        if (targetObject is Crop crop && crop.ReadyToHarvest)
        {
            TryHarvestCrop(crop, targetObject);
            return; // Harvest handled
        }

        // ═══════════════════════════════════════════════════════════════════
        // STEP 3: FARMING TOOL USE (Requires holding a farming tool)
        // ═══════════════════════════════════════════════════════════════════
        if (activeItem is not Tool tool)
        {
            Debug.WriteLine("[Interact] No tool selected");
            return;
        }

        // Standard tool use (farming tools only reach here)
        InteractWithTile(_targetTile, tool);
    }

    /// <summary>
    /// Try to interact with an object that has "free" interaction (no stamina cost).
    /// Returns true if the object was an interactive type (ShippingBin, Sign, Bed).
    /// </summary>
    private bool TryInteractWithObject(WorldObject obj)
    {
        switch (obj)
        {
            case ShippingBin bin:
                // Open the shipping menu UI (Game1 handles this via event)
                OnOpenShippingMenu?.Invoke(bin);
                Debug.WriteLine("[Interact] Opening shipping menu");
                return true;

            case Sign sign:
                // Open dialogue box with sign text (Game1 handles this via event)
                OnOpenDialogue?.Invoke(sign.Text);
                Debug.WriteLine($"[Interact] Reading sign: \"{sign.Text}\"");
                return true;

            case Bed bed:
                // Trigger sleep transition
                if (bed.TrySleep())
                {
                    Debug.WriteLine("[Interact] Player sleeping...");
                    Transition.StartSleepTransition();
                }
                else
                {
                    Debug.WriteLine("[Interact] Cannot use this bed");
                }
                return true;

            default:
                // Not an interactive object - requires tool use
                return false;
        }
    }

    /// <summary>
    /// Perform a weapon attack based on the weapon's AttackStyle.
    ///
    /// ATTACK STYLES:
    /// - MELEE: Creates hitbox in front of player, checks intersection with enemies.
    /// - PROJECTILE: Spawns a moving projectile in facing direction.
    /// - RAYCAST: Instant hit along a line, finds first enemy.
    /// </summary>
    private void PerformWeaponAttack(Tool weapon)
    {
        // ═══════════════════════════════════════════════════════════════════
        // PRE-CHECKS: Cooldown and Stamina
        // ═══════════════════════════════════════════════════════════════════

        // Check attack cooldown
        if (!Player.CanAttack)
        {
            Debug.WriteLine($"[{weapon.Name}] On cooldown!");
            return;
        }

        // Check stamina
        if (weapon.StaminaCost > 0f && Player.CurrentStamina < weapon.StaminaCost)
        {
            Debug.WriteLine($"[{weapon.Name}] Too tired to attack!");
            return;
        }

        // ═══════════════════════════════════════════════════════════════════
        // COMMITMENT: Deduct stamina and start cooldown
        // ═══════════════════════════════════════════════════════════════════

        if (weapon.StaminaCost > 0f)
        {
            Player.CurrentStamina -= weapon.StaminaCost;
            Debug.WriteLine($"[Stamina] Spent {weapon.StaminaCost} ({Player.CurrentStamina:F1}/{Player.MaxStamina} remaining)");
        }

        Player.StartAttackCooldown(weapon.Cooldown);

        // ═══════════════════════════════════════════════════════════════════
        // ATTACK STYLE SWITCH
        // ═══════════════════════════════════════════════════════════════════

        switch (weapon.Style)
        {
            case AttackStyle.Melee:
                PerformMeleeAttack(weapon);
                break;

            case AttackStyle.Projectile:
                PerformProjectileAttack(weapon);
                break;

            case AttackStyle.Raycast:
                PerformRaycastAttack(weapon);
                break;

            default:
                // Fallback to melee for unspecified weapons
                Debug.WriteLine($"[{weapon.Name}] Unknown attack style, defaulting to melee");
                PerformMeleeAttack(weapon);
                break;
        }
    }

    /// <summary>
    /// Perform a melee attack with hitbox collision.
    /// </summary>
    private void PerformMeleeAttack(Tool weapon)
    {
        // Get attack hitbox based on weapon settings
        Rectangle attackHitbox = Player.GetAttackHitbox(weapon.HitboxWidth, weapon.HitboxHeight);
        bool hitSomething = false;

        // Store for debug visualization
        _debugMeleeHitbox = attackHitbox;
        _debugMeleeHitboxTimer = DebugHitboxDisplayTime;

        // Check enemies in current location
        if (LocationEnemies.TryGetValue(CurrentLocationName, out var enemies))
        {
            foreach (var enemy in enemies)
            {
                if (enemy.IsDead) continue;

                if (attackHitbox.Intersects(enemy.BoundingBox))
                {
                    enemy.TakeDamage(weapon.Damage, Player.Center);
                    hitSomething = true;
                    Debug.WriteLine($"[Melee] {weapon.Name} hit {enemy.Name} for {weapon.Damage} damage!");
                }
            }
        }

        // Feedback
        if (hitSomething)
        {
            Debug.WriteLine($"[Audio] {weapon.Name}: *SLASH*");
        }
        else
        {
            Debug.WriteLine($"[Audio] {weapon.Name}: *woosh* (miss)");
        }
    }

    /// <summary>
    /// Perform a projectile attack by spawning a projectile.
    /// Uses SMART AIMING: Player aims at mouse position, clamped to max range.
    /// </summary>
    private void PerformProjectileAttack(Tool weapon)
    {
        // Get spawn point
        Vector2 spawnPoint = Player.GetProjectileSpawnPoint();

        // SMART AIMING: Get mouse position in world, clamp to weapon range
        Vector2 mouseWorld = Input.GetMouseWorldPosition(Camera);
        Vector2 clampedTarget = Player.GetClampedTarget(mouseWorld, weapon.Range > 0 ? weapon.Range : 500f);
        Vector2 direction = Player.GetDirectionTo(clampedTarget);

        // Create projectile with weapon properties
        var projectile = new Projectile
        {
            Position = spawnPoint,
            Velocity = direction * weapon.ProjectileSpeed,
            Damage = weapon.Damage,
            Size = 12,
            Color = new Color(weapon.ProjectileColorPacked), // Unpack ARGB color
            MaxLifetime = 5f,
            OwnerId = "player"
        };

        Projectiles.Add(projectile);

        Debug.WriteLine($"[Projectile] {weapon.Name} fired toward ({clampedTarget.X:F0}, {clampedTarget.Y:F0})");
    }

    /// <summary>
    /// Perform a raycast attack (instant line hit).
    /// Uses SMART AIMING: Player aims at mouse position, clamped to max range.
    /// Checks WALLS FIRST - beam stops at solid tiles before checking enemies.
    /// </summary>
    private void PerformRaycastAttack(Tool weapon)
    {
        Vector2 rayStart = Player.Center;

        // SMART AIMING: Get mouse position in world, clamp to weapon range
        Vector2 mouseWorld = Input.GetMouseWorldPosition(Camera);
        Vector2 clampedTarget = Player.GetClampedTarget(mouseWorld, weapon.Range);
        Vector2 rayDirection = Player.GetDirectionTo(clampedTarget);

        // ═══════════════════════════════════════════════════════════════════
        // WALL CHECK: Walk the ray in steps to find first solid tile
        // ═══════════════════════════════════════════════════════════════════
        float maxDistance = weapon.Range;
        float wallHitDistance = maxDistance;
        bool hitWall = false;

        const float RayStepSize = 10f; // Check every 10 pixels
        int steps = (int)MathF.Ceiling(maxDistance / RayStepSize);

        for (int i = 1; i <= steps; i++)
        {
            float checkDist = MathF.Min(i * RayStepSize, maxDistance);
            Vector2 checkPos = rayStart + rayDirection * checkDist;

            if (IsTileSolidForProjectile(checkPos))
            {
                wallHitDistance = checkDist;
                hitWall = true;
                Debug.WriteLine($"[Raycast] Wall hit at distance {wallHitDistance:F0}");
                break;
            }
        }

        // Ray can only reach up to the wall
        float effectiveRange = wallHitDistance;
        Vector2 rayEnd = rayStart + rayDirection * effectiveRange;

        // Store for debug visualization
        _debugRayStart = rayStart;
        _debugRayEnd = rayEnd;
        _debugRayTimer = DebugRayDisplayTime;

        // ═══════════════════════════════════════════════════════════════════
        // ENEMY CHECK: Find closest enemy within the effective range
        // ═══════════════════════════════════════════════════════════════════
        Enemy? hitEnemy = null;
        float closestDistance = effectiveRange;

        if (LocationEnemies.TryGetValue(CurrentLocationName, out var enemies))
        {
            foreach (var enemy in enemies)
            {
                if (enemy.IsDead) continue;

                // Check if enemy is along the ray (within effective range)
                if (RayIntersectsRectangle(rayStart, rayDirection, effectiveRange, enemy.BoundingBox, out float distance))
                {
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        hitEnemy = enemy;
                    }
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // APPLY DAMAGE OR FEEDBACK
        // ═══════════════════════════════════════════════════════════════════
        if (hitEnemy != null)
        {
            // Update ray end to enemy hit point
            _debugRayEnd = rayStart + rayDirection * closestDistance;

            hitEnemy.TakeDamage(weapon.Damage, rayStart);
            Debug.WriteLine($"[Raycast] {weapon.Name} zapped {hitEnemy.Name} for {weapon.Damage} damage!");
            Debug.WriteLine($"[Audio] {weapon.Name}: *CRACK* (lightning)");
        }
        else if (hitWall)
        {
            Debug.WriteLine($"[Raycast] {weapon.Name} hit wall at distance {wallHitDistance:F0}");
            Debug.WriteLine($"[Audio] {weapon.Name}: *FIZZLE* (wall)");
        }
        else
        {
            Debug.WriteLine($"[Raycast] {weapon.Name} fired but hit nothing");
            Debug.WriteLine($"[Audio] {weapon.Name}: *ZAP* (miss)");
        }
    }

    /// <summary>
    /// Check if a ray intersects a rectangle (simple AABB test).
    /// </summary>
    private static bool RayIntersectsRectangle(Vector2 rayOrigin, Vector2 rayDir, float maxDistance, Rectangle rect, out float distance)
    {
        distance = 0;

        // Expand ray direction to avoid division by zero
        float dirX = rayDir.X == 0 ? 0.0001f : rayDir.X;
        float dirY = rayDir.Y == 0 ? 0.0001f : rayDir.Y;

        // Calculate intersection times for each axis
        float txMin = (rect.Left - rayOrigin.X) / dirX;
        float txMax = (rect.Right - rayOrigin.X) / dirX;
        float tyMin = (rect.Top - rayOrigin.Y) / dirY;
        float tyMax = (rect.Bottom - rayOrigin.Y) / dirY;

        // Ensure min < max for each axis
        if (txMin > txMax) (txMin, txMax) = (txMax, txMin);
        if (tyMin > tyMax) (tyMin, tyMax) = (tyMax, tyMin);

        // Check for overlap
        float tMin = MathF.Max(txMin, tyMin);
        float tMax = MathF.Min(txMax, tyMax);

        // No intersection if ranges don't overlap
        if (tMin > tMax)
            return false;

        // Check if intersection is within ray range and in front
        if (tMin < 0)
            tMin = tMax; // Ray starts inside, use exit point

        if (tMin < 0 || tMin > maxDistance)
            return false;

        distance = tMin;
        return true;
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
        // ═══════════════════════════════════════════════════════════════════
        // PAY-TO-SWING STAMINA SYSTEM (Stardew Style)
        // ═══════════════════════════════════════════════════════════════════
        // Philosophy: Swinging costs energy regardless of whether you hit anything.
        // This creates resource management tension - don't spam clicks carelessly!
        //
        // Flow:
        // 1. PRE-CHECK: Block if exhausted (can't even swing)
        // 2. COMMITMENT: Deduct stamina immediately (you swung!)
        // 3. APPLICATION: Try to affect world (object or tile)
        // 4. FEEDBACK: Track hit/miss for sound effects
        // ═══════════════════════════════════════════════════════════════════

        // STEP 1: PRE-CHECK - Block if too tired to swing
        if (tool.StaminaCost > 0f && Player.CurrentStamina < tool.StaminaCost)
        {
            Debug.WriteLine($"[{tool.Name}] Too tired! Need {tool.StaminaCost} stamina (have {Player.CurrentStamina:F1})");
            // TODO: Play "error/buzz" sound
            return;
        }

        // STEP 2: COMMITMENT - Deduct stamina immediately (you're swinging!)
        if (tool.StaminaCost > 0f)
        {
            Player.CurrentStamina -= tool.StaminaCost;
            Debug.WriteLine($"[Stamina] Spent {tool.StaminaCost} ({Player.CurrentStamina:F1}/{Player.MaxStamina} remaining)");
        }

        // TODO: STEP 3: ANIMATION - Trigger tool swing animation
        // Player.PlayToolAnimation(tool.RegistryKey);

        bool hitSomething = false; // Track for sound feedback

        // STEP 4: APPLICATION - Check for world object at this tile
        var targetObject = GetObjectAtTile(tileCoords);

        // If object exists, interact with it
        if (targetObject != null)
        {
            bool objectHandled = InteractWithObject(targetObject, tool);
            if (objectHandled)
                hitSomething = true;

            // Does this tool affect tiles through objects?
            if (!tool.AffectsTileThroughObjects)
            {
                // Tool effect stops at the object (Pickaxe, Axe, Scythe, etc.)
                PlayToolFeedback(tool, hitSomething);
                return;
            }

            // Tool passes through - continue to tile logic (Watering Can, Hydro Wand, etc.)
            if (objectHandled)
            {
                Debug.WriteLine($"[{tool.Name}] Pass-through: Also affecting tile at ({tileCoords.X}, {tileCoords.Y})");
            }
        }

        // STEP 5: TILE MODIFICATION
        var currentTile = CurrentLocation.GetTile(tileCoords.X, tileCoords.Y);
        bool tileChanged = false;

        switch (tool.RegistryKey)
        {
            case "hoe":
            case "earth_wand":
                // Hoe/Earth Wand: Grass/Dirt → Tilled
                if (currentTile.Id == Tile.Grass.Id || currentTile.Id == Tile.Dirt.Id)
                {
                    CurrentLocation.SetTile(tileCoords.X, tileCoords.Y, Tile.Tilled);
                    Debug.WriteLine($"[{tool.Name}] Tilled at ({tileCoords.X}, {tileCoords.Y})");
                    tileChanged = true;
                }
                break;

            case "pickaxe":
                // Pickaxe: Stone tile → Dirt
                if (currentTile.Id == Tile.Stone.Id)
                {
                    CurrentLocation.SetTile(tileCoords.X, tileCoords.Y, Tile.Dirt);
                    Debug.WriteLine($"[Pickaxe] Broke stone tile at ({tileCoords.X}, {tileCoords.Y})");
                    tileChanged = true;
                }
                break;

            case "watering_can":
            case "hydro_wand":
                // Watering Can/Hydro Wand: Tilled → WetDirt
                if (currentTile.Id == Tile.Tilled.Id)
                {
                    CurrentLocation.SetTile(tileCoords.X, tileCoords.Y, Tile.WetDirt);
                    Debug.WriteLine($"[{tool.Name}] Watered soil at ({tileCoords.X}, {tileCoords.Y})");
                    tileChanged = true;
                }
                break;

            case "axe":
            case "scythe":
                // These only work on objects, no tile effect
                break;
        }

        if (tileChanged)
            hitSomething = true;

        // STEP 6: FEEDBACK - Play appropriate sound
        PlayToolFeedback(tool, hitSomething);
    }

    /// <summary>
    /// Play audio feedback based on whether the tool hit something.
    /// </summary>
    private void PlayToolFeedback(Tool tool, bool hitSomething)
    {
        if (hitSomething)
        {
            // TODO: Play "impact/work" sound based on tool type
            Debug.WriteLine($"[Audio] {tool.Name}: *IMPACT*");
        }
        else
        {
            // TODO: Play "woosh/miss" sound
            Debug.WriteLine($"[Audio] {tool.Name}: *woosh* (miss)");
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

                    // AUTO-LOOT: Add Wood to inventory (every chop drops wood)
                    var wood = new Material("wood", "Wood", "A piece of wood.", 1);
                    if (Player.Inventory.AddItem(wood))
                    {
                        Debug.WriteLine($"[Loot] +1 Wood added to inventory");
                    }
                    else
                    {
                        Debug.WriteLine($"[Loot] Inventory full! Wood lost.");
                        // TODO: Spawn dropped item on ground
                    }
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

            // NOTE: Bed, ShippingBin, and Sign are handled by TryInteractWithObject()
            // which runs BEFORE tool stamina is deducted. They no longer need handling here.
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

                    // AUTO-LOOT: Add Stone to inventory
                    var stone = new Material("stone", "Stone", "A chunk of stone.", 1);
                    if (Player.Inventory.AddItem(stone))
                    {
                        Debug.WriteLine($"[Loot] +1 Stone added to inventory");
                    }
                    else
                    {
                        Debug.WriteLine($"[Loot] Inventory full! Stone lost.");
                        // TODO: Spawn dropped item on ground
                    }
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
        // Build list of all renderables (Objects + Enemies + Projectiles + Player)
        var renderables = new List<IRenderable>(Objects.Count + Enemies.Count + Projectiles.Count + 1);

        // Add player
        renderables.Add(Player);

        // Add all world objects
        foreach (var obj in Objects)
        {
            renderables.Add(obj);
        }

        // Add all enemies
        foreach (var enemy in Enemies)
        {
            renderables.Add(enemy);
        }

        // Add all projectiles
        foreach (var projectile in Projectiles)
        {
            renderables.Add(projectile);
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

        // Draw attack debug visualization (always on for now, shows active attacks)
        DrawAttackDebug(spriteBatch);
    }

    /// <summary>
    /// Draw 1px red borders around collision rectangles for debugging.
    /// Shows Player.CollisionBounds, WorldObject.BoundingBox, Enemy.BoundingBox, and attack hitbox.
    /// </summary>
    private void DrawDebugCollisions(SpriteBatch spriteBatch)
    {
        Color debugColor = Color.Red;
        Color enemyColor = Color.Orange;
        Color attackColor = Color.Yellow;

        // Draw player collision bounds
        DrawRectBorder(spriteBatch, Player.CollisionBounds, debugColor);

        // Draw player attack hitbox (if holding a weapon)
        var activeItem = Player.Inventory.GetActiveItem();
        if (activeItem is Tool tool && tool.IsWeapon)
        {
            Rectangle attackHitbox = Player.GetAttackHitbox();
            DrawRectBorder(spriteBatch, attackHitbox, attackColor);
        }

        // Draw world object bounding boxes
        foreach (var obj in Objects)
        {
            if (obj.IsCollidable)
            {
                DrawRectBorder(spriteBatch, obj.BoundingBox, debugColor);
            }
        }

        // Draw enemy bounding boxes
        foreach (var enemy in Enemies)
        {
            DrawRectBorder(spriteBatch, enemy.BoundingBox, enemyColor);
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
    /// Draw debug visualization for active attacks (melee hitbox, raycast line).
    /// Shows for a brief duration after attack.
    /// </summary>
    private void DrawAttackDebug(SpriteBatch spriteBatch)
    {
        // Draw melee hitbox (red filled rectangle)
        if (_debugMeleeHitbox.HasValue)
        {
            // Semi-transparent red fill
            spriteBatch.Draw(_pixel, _debugMeleeHitbox.Value, new Color(255, 0, 0, 100));
            // Solid red border
            DrawRectBorder(spriteBatch, _debugMeleeHitbox.Value, Color.Red);
        }

        // Draw raycast line (lightning bolt effect)
        if (_debugRayStart.HasValue && _debugRayEnd.HasValue)
        {
            DrawLine(spriteBatch, _debugRayStart.Value, _debugRayEnd.Value, Color.Cyan, 3);
            // Draw impact point
            var impactRect = new Rectangle(
                (int)_debugRayEnd.Value.X - 6,
                (int)_debugRayEnd.Value.Y - 6,
                12,
                12
            );
            spriteBatch.Draw(_pixel, impactRect, Color.White);
        }
    }

    /// <summary>
    /// Draw a line between two points.
    /// </summary>
    private void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, int thickness = 1)
    {
        Vector2 edge = end - start;
        float angle = MathF.Atan2(edge.Y, edge.X);
        float length = edge.Length();

        spriteBatch.Draw(
            _pixel,
            new Rectangle((int)start.X, (int)start.Y, (int)length, thickness),
            null,
            color,
            angle,
            Vector2.Zero,
            SpriteEffects.None,
            0
        );
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
            // Weapons
            "sword" => new Color(200, 200, 220),    // Steel gray
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
    /// v2.16: Uses flat DTOs to avoid polymorphic serialization failures.
    /// </summary>
    public SaveData CreateSaveData()
    {
        // Save ALL locations (tiles + objects as DTOs)
        var locationSaveData = new List<LocationSaveData>();

        foreach (var (locationName, location) in Locations)
        {
            var objectDtos = new List<WorldObjectData>();

            if (LocationObjects.TryGetValue(locationName, out var objs))
            {
                foreach (var obj in objs)
                {
                    objectDtos.Add(WorldObjectToData(obj));
                }
            }

            var locData = new LocationSaveData
            {
                Name = locationName,
                ModifiedTiles = GetModifiedTilesForLocation(location),
                Objects = objectDtos
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
            PlayerHP = Player.CurrentHP,
            CurrentLocationName = CurrentLocationName,
            WorldSeed = WorldSeed,
            Day = TimeManager.Day,
            TimeOfDay = TimeManager.TimeOfDay,
            InventorySlots = Player.Inventory.ToSaveData(),
            ActiveHotbarSlot = Player.Inventory.ActiveSlotIndex,
            Locations = locationSaveData
        };
    }

    /// <summary>
    /// Convert a WorldObject to flat DTO for serialization.
    /// </summary>
    private static WorldObjectData WorldObjectToData(WorldObject obj)
    {
        var data = new WorldObjectData
        {
            Name = obj.Name,
            X = obj.Position.X,
            Y = obj.Position.Y,
            Width = obj.Width,
            Height = obj.Height,
            ColorPacked = obj.Color.PackedValue,
            IsCollidable = obj.IsCollidable
        };

        switch (obj)
        {
            case Crop crop:
                data.Type = "crop";
                data.CropType = crop.CropType;
                data.CropStage = (int)crop.Stage;
                data.DaysAtStage = crop.DaysAtStage;
                data.DaysPerStage = crop.DaysPerStage;
                data.WasWateredToday = crop.WasWateredToday;
                data.DaysWithoutWater = crop.DaysWithoutWater;
                data.MaxDaysWithoutWater = crop.MaxDaysWithoutWater;
                data.HarvestItemId = crop.HarvestItemId;
                data.Regrows = crop.Regrows;
                data.HarvestResetStage = (int)crop.HarvestResetStage;
                data.HarvestQuantity = crop.HarvestQuantity;
                break;

            case Tree tree:
                data.Type = "tree";
                data.TreeType = tree.TreeType;
                data.TreeStage = (int)tree.Stage;
                data.DaysAtStage = tree.DaysAtStage;
                data.DaysToYoung = tree.DaysToYoung;
                data.DaysToMature = tree.DaysToMature;
                data.DaysToRegrow = tree.DaysToRegrow;
                break;

            case ManaNode node:
                data.Type = "mana_node";
                data.CrystalType = node.CrystalType;
                data.CurrentCharge = node.CurrentCharge;
                data.MaxCharge = node.MaxCharge;
                data.RechargePerDay = node.RechargePerDay;
                break;

            case Bed bed:
                data.Type = "bed";
                data.BedStyle = bed.BedStyle;
                data.CanSleep = bed.CanSleep;
                break;

            case Sign sign:
                data.Type = "sign";
                data.SignText = sign.Text;
                break;

            case ShippingBin bin:
                data.Type = "shipping_bin";
                data.LastShippedItem = Inventory.ItemToData(bin.LastShippedItem);
                data.ShippingManifest = new List<ShippingBin.ShippedItem>(bin.ShippingManifest);
                break;

            default:
                data.Type = "base";
                break;
        }

        return data;
    }

    /// <summary>
    /// Restores ALL locations from save data (global persistence).
    /// v2.16: Uses flat DTOs to properly restore polymorphic objects.
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

            // Restore objects from DTOs (or spawn defaults if none saved)
            if (locData.Objects.Count > 0)
            {
                var objects = new List<WorldObject>();
                foreach (var dto in locData.Objects)
                {
                    var obj = DataToWorldObject(dto);
                    if (obj != null)
                    {
                        objects.Add(obj);
                    }
                }
                LocationObjects[locData.Name] = objects;
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
        Player.CurrentHP = data.PlayerHP > 0 ? data.PlayerHP : Player.MaxHP;
        Player.Inventory.LoadFromData(data.InventorySlots);
        Player.Inventory.SelectSlot(data.ActiveHotbarSlot);

        Debug.WriteLine($"[WorldManager] Loaded location: {CurrentLocationName}");
        Debug.WriteLine($"[WorldManager] Restored player gold: {Player.Gold}g, stamina: {Player.CurrentStamina:F1}");
    }

    /// <summary>
    /// Convert flat DTO back to a WorldObject.
    /// </summary>
    private static WorldObject? DataToWorldObject(WorldObjectData data)
    {
        var position = new Vector2(data.X, data.Y);

        switch (data.Type)
        {
            case "crop":
                return new Crop
                {
                    Name = data.Name,
                    Position = position,
                    Width = data.Width,
                    Height = data.Height,
                    Color = new Microsoft.Xna.Framework.Color(data.ColorPacked),
                    IsCollidable = data.IsCollidable,
                    CropType = data.CropType,
                    Stage = (CropStage)data.CropStage,
                    DaysAtStage = data.DaysAtStage,
                    DaysPerStage = data.DaysPerStage,
                    WasWateredToday = data.WasWateredToday,
                    DaysWithoutWater = data.DaysWithoutWater,
                    MaxDaysWithoutWater = data.MaxDaysWithoutWater,
                    HarvestItemId = data.HarvestItemId,
                    Regrows = data.Regrows,
                    HarvestResetStage = (CropStage)data.HarvestResetStage,
                    HarvestQuantity = data.HarvestQuantity
                };

            case "tree":
                return new Tree
                {
                    Name = data.Name,
                    Position = position,
                    TreeType = data.TreeType,
                    Stage = (TreeStage)data.TreeStage,
                    DaysAtStage = data.DaysAtStage,
                    DaysToYoung = data.DaysToYoung,
                    DaysToMature = data.DaysToMature,
                    DaysToRegrow = data.DaysToRegrow
                };

            case "mana_node":
                return new ManaNode
                {
                    Name = data.Name,
                    Position = position,
                    CrystalType = data.CrystalType,
                    CurrentCharge = data.CurrentCharge,
                    MaxCharge = data.MaxCharge,
                    RechargePerDay = data.RechargePerDay
                };

            case "bed":
                return new Bed
                {
                    Name = data.Name,
                    Position = position,
                    BedStyle = data.BedStyle,
                    CanSleep = data.CanSleep
                };

            case "sign":
                return new Sign
                {
                    Name = data.Name,
                    Position = position,
                    Text = data.SignText
                };

            case "shipping_bin":
                var bin = new ShippingBin
                {
                    Name = data.Name,
                    Position = position
                };
                bin.LastShippedItem = Inventory.DataToItem(data.LastShippedItem);
                bin.ShippingManifest = new List<ShippingBin.ShippedItem>(data.ShippingManifest);
                return bin;

            case "base":
            default:
                return new WorldObject
                {
                    Name = data.Name,
                    Position = position,
                    Width = data.Width,
                    Height = data.Height,
                    Color = new Microsoft.Xna.Framework.Color(data.ColorPacked),
                    IsCollidable = data.IsCollidable
                };
        }
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
