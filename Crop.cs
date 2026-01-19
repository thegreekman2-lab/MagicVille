#nullable enable
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MagicVille;

/// <summary>
/// Growth stages for crops.
/// </summary>
public enum CropStage
{
    Seed = 0,
    Sprout = 1,
    Growing = 2,
    Mature = 3,
    Dead = 4
}

/// <summary>
/// A crop planted in tilled soil. Grows through stages over multiple days.
/// Requires watered soil to advance. Becomes harvestable at Mature stage.
///
/// COORDINATE SYSTEM:
/// - Position (Vector2): World space pixels with smart alignment offset
/// - GridX/GridY: Computed tile coordinates from GetGridPosition()
/// - TileX/TileY: Legacy stored coordinates (kept for save compatibility)
///
/// For tile lookups, ALWAYS use GetGridPosition() to bridge the gap between
/// the "Visual World" (pixels) and the "Data World" (grid/tiles).
/// </summary>
public class Crop : WorldObject
{
    /// <summary>Crop type identifier (e.g., "corn", "tomato", "potato").</summary>
    public string CropType { get; set; } = "corn";

    /// <summary>Current growth stage.</summary>
    public CropStage Stage { get; set; } = CropStage.Seed;

    /// <summary>Days spent at current stage (resets on stage advance).</summary>
    public int DaysAtStage { get; set; }

    /// <summary>Days required at each stage before advancing.</summary>
    public int DaysPerStage { get; set; } = 2;

    /// <summary>Days without water before crop dies.</summary>
    public int DaysWithoutWater { get; set; }

    /// <summary>Max days without water before death.</summary>
    public int MaxDaysWithoutWater { get; set; } = 3;

    /// <summary>Whether this crop was watered today (set by player action).</summary>
    public bool WasWateredToday { get; set; }

    /// <summary>Whether this crop is ready for harvest (mature stage).</summary>
    public bool ReadyToHarvest => Stage == CropStage.Mature;

    /// <summary>
    /// Item ID to spawn when harvested (e.g., "corn", "tomato").
    /// Used by GetHarvestDrop() to create the reward item.
    /// </summary>
    public string HarvestItemId { get; set; } = "";

    /// <summary>
    /// Whether this crop regrows after harvest (e.g., tomatoes, peppers).
    /// If true, crop resets to HarvestResetStage instead of being removed.
    /// </summary>
    public bool Regrows { get; set; } = false;

    /// <summary>
    /// Stage to reset to after harvest (only used if Regrows = true).
    /// Default is Growing (stage 2), so crop needs 1 more growth cycle.
    /// </summary>
    public CropStage HarvestResetStage { get; set; } = CropStage.Growing;

    /// <summary>
    /// Base quantity of items dropped on harvest.
    /// </summary>
    public int HarvestQuantity { get; set; } = 1;

    /// <summary>
    /// Legacy tile coordinate X. Prefer using GridX (computed from position).
    /// Kept for save/load compatibility.
    /// </summary>
    public int TileX { get; set; }

    /// <summary>
    /// Legacy tile coordinate Y. Prefer using GridY (computed from position).
    /// Kept for save/load compatibility.
    /// </summary>
    public int TileY { get; set; }

    public Crop() : base()
    {
        Name = "crop";
        Width = 32;
        Height = 32;
        IsCollidable = false; // Walk over crops
        UpdateVisuals();
    }

    public Crop(string cropType, Vector2 position, int tileX, int tileY) : base()
    {
        CropType = cropType;
        Position = position;
        TileX = tileX;
        TileY = tileY;
        Name = "crop";
        Width = 32;
        Height = 32;
        IsCollidable = false;
        UpdateVisuals();
    }

    /// <summary>
    /// Process daily growth. Called at start of new day.
    ///
    /// CRITICAL ORDER OF OPERATIONS:
    /// This method is called BEFORE tiles are dried (evaporation phase).
    /// It checks if the underlying tile is WetDirt to determine watering status.
    ///
    /// Uses GetGridPosition() for tile lookups to handle the coordinate mismatch
    /// between smart-aligned world position and grid tiles.
    /// </summary>
    /// <param name="location">The location containing this crop (for tile state checks).</param>
    /// <returns>True if crop should be removed (dead).</returns>
    public override bool OnNewDay(GameLocation? location = null)
    {
        // Get grid position using the helper (bridges Visual World → Data World)
        Point gridPos = GetGridPosition();

        // === TILE STATE CHECK ===
        // Check if the underlying tile is wet (WetDirt)
        // This is the PRIMARY method for determining if crop was watered
        string tileState = "Unknown";
        bool tileIsWet = false;

        if (location != null)
        {
            Tile tile = location.GetTile(gridPos.X, gridPos.Y);
            tileIsWet = (tile.Id == Tile.WetDirt.Id);
            tileState = tileIsWet ? "WET" : "DRY";

            // If tile is wet, mark crop as watered (even if Water() wasn't called directly)
            // This supports sprinklers, rain, and other indirect watering methods
            if (tileIsWet)
            {
                WasWateredToday = true;
            }
        }

        // === LOUD DEBUG LOGGING ===
        Debug.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Debug.WriteLine($"║ [CROP DEBUG] {CropType.ToUpper()} at Grid Position: ({gridPos.X}, {gridPos.Y})");
        Debug.WriteLine($"║ [CROP DEBUG] Tile State: {tileState}");
        Debug.WriteLine($"║ [CROP DEBUG] WasWateredToday: {WasWateredToday}");
        Debug.WriteLine($"║ [CROP DEBUG] Growing? {WasWateredToday} (requires wet tile or manual watering)");
        Debug.WriteLine($"║ [CROP DEBUG] Current Stage: {Stage}, Days at Stage: {DaysAtStage}/{DaysPerStage}");
        Debug.WriteLine($"║ [CROP DEBUG] Days Without Water: {DaysWithoutWater}/{MaxDaysWithoutWater}");
        Debug.WriteLine("╚══════════════════════════════════════════════════════════════╝");

        // Dead crops stay dead
        if (Stage == CropStage.Dead)
        {
            Debug.WriteLine($"[Crop] {CropType} at ({gridPos.X}, {gridPos.Y}) is dead, removing");
            return true; // Remove dead crops
        }

        // Check if watered (via tile state OR direct Water() call)
        if (!WasWateredToday)
        {
            DaysWithoutWater++;
            Debug.WriteLine($"[Crop] {CropType} at ({gridPos.X}, {gridPos.Y}) NOT watered (tile was {tileState}), days without: {DaysWithoutWater}");

            if (DaysWithoutWater >= MaxDaysWithoutWater)
            {
                Stage = CropStage.Dead;
                UpdateVisuals();
                Debug.WriteLine($"[Crop] {CropType} at ({gridPos.X}, {gridPos.Y}) DIED from lack of water!");
                return false; // Keep dead crop visible for one day
            }
        }
        else
        {
            DaysWithoutWater = 0;
            Debug.WriteLine($"[Crop] {CropType} at ({gridPos.X}, {gridPos.Y}) was watered (tile was {tileState}), drought counter reset");
        }

        // Mature crops don't grow further
        if (Stage == CropStage.Mature)
        {
            WasWateredToday = false;
            Debug.WriteLine($"[Crop] {CropType} at ({gridPos.X}, {gridPos.Y}) is mature, no further growth needed");
            return false;
        }

        // Advance growth if watered
        if (WasWateredToday)
        {
            DaysAtStage++;
            Debug.WriteLine($"[Crop] {CropType} at ({gridPos.X}, {gridPos.Y}) growing! Days at stage: {DaysAtStage}/{DaysPerStage}");

            if (DaysAtStage >= DaysPerStage)
            {
                DaysAtStage = 0;
                CropStage oldStage = Stage;
                Stage = (CropStage)((int)Stage + 1);
                Debug.WriteLine($"[Crop] {CropType} at ({gridPos.X}, {gridPos.Y}) ★ STAGE ADVANCED: {oldStage} -> {Stage} ★");
            }
        }

        // Reset watered flag for new day
        WasWateredToday = false;
        UpdateVisuals();
        return false;
    }

    /// <summary>
    /// Sync legacy TileX/TileY with computed grid position.
    /// Call this after loading or if position changes.
    /// </summary>
    public void SyncTileCoordinates()
    {
        Point gridPos = GetGridPosition();
        TileX = gridPos.X;
        TileY = gridPos.Y;
    }

    /// <summary>
    /// Mark this crop as watered. Called when player uses watering can on it.
    /// </summary>
    public void Water()
    {
        WasWateredToday = true;
    }

    /// <summary>
    /// Get the item to drop when this crop is harvested.
    /// Creates a Material item based on HarvestItemId and HarvestQuantity.
    /// </summary>
    /// <returns>A new Material item, or null if HarvestItemId is not set.</returns>
    public Item? GetHarvestDrop()
    {
        // If no harvest item configured, return null
        if (string.IsNullOrEmpty(HarvestItemId))
        {
            Debug.WriteLine($"[Crop] WARNING: {CropType} has no HarvestItemId set!");
            return null;
        }

        // TODO: In v3, replace with Item Database lookup
        // For now, create a Material directly with the harvest item ID
        string displayName = char.ToUpper(HarvestItemId[0]) + HarvestItemId[1..];
        return new Material(
            registryKey: HarvestItemId,
            name: displayName,
            description: $"Fresh {displayName} from the farm.",
            quantity: HarvestQuantity,
            maxStack: 99
        );
    }

    /// <summary>
    /// Harvest the crop (only valid at Mature stage).
    /// Note: This method only validates and handles crop state.
    /// Caller (WorldManager) is responsible for inventory management.
    /// </summary>
    /// <returns>True if crop is ready and can be harvested.</returns>
    public bool TryHarvest()
    {
        if (Stage != CropStage.Mature)
            return false;

        return true;
    }

    /// <summary>
    /// Reset crop for regrowth after harvest.
    /// Only call this if Regrows == true and harvest was successful.
    /// </summary>
    public void ResetForRegrowth()
    {
        Stage = HarvestResetStage;
        DaysAtStage = 0;
        WasWateredToday = false;
        UpdateVisuals();
        Debug.WriteLine($"[Crop] {CropType} reset to {Stage} for regrowth");
    }

    /// <summary>
    /// Update visual appearance based on stage.
    /// </summary>
    private void UpdateVisuals()
    {
        (Color, Height) = Stage switch
        {
            CropStage.Seed => (new Color(139, 90, 43), 16),      // Brown (barely visible)
            CropStage.Sprout => (new Color(100, 180, 80), 24),   // Light green
            CropStage.Growing => (new Color(60, 140, 60), 40),   // Medium green
            CropStage.Mature => (GetMatureColor(), 48),          // Crop-specific color
            CropStage.Dead => (new Color(80, 60, 40), 32),       // Brown/dead
            _ => (Color.Green, 32)
        };
    }

    /// <summary>
    /// Get the mature color based on crop type.
    /// </summary>
    private Color GetMatureColor() => CropType switch
    {
        "corn" => new Color(255, 220, 80),      // Yellow
        "tomato" => new Color(220, 50, 50),     // Red
        "potato" => new Color(180, 140, 80),    // Tan
        "carrot" => new Color(255, 140, 0),     // Orange
        "wheat" => new Color(220, 190, 100),    // Golden
        _ => new Color(100, 200, 100)           // Default green
    };

    /// <summary>
    /// Create a corn seed crop.
    /// Corn does NOT regrow - must replant after harvest.
    /// </summary>
    public static Crop CreateCornSeed(Vector2 position, int tileX, int tileY)
    {
        return new Crop("corn", position, tileX, tileY)
        {
            DaysPerStage = 2,         // 6 days total to mature (2 days x 3 stages)
            HarvestItemId = "corn",   // Drops corn material
            HarvestQuantity = 2,      // 2 ears per plant
            Regrows = false           // Must replant
        };
    }

    /// <summary>
    /// Create a tomato seed crop.
    /// Tomatoes REGROW after harvest - can harvest multiple times.
    /// </summary>
    public static Crop CreateTomatoSeed(Vector2 position, int tileX, int tileY)
    {
        return new Crop("tomato", position, tileX, tileY)
        {
            DaysPerStage = 2,                          // 6 days to first harvest
            HarvestItemId = "tomato",                  // Drops tomato material
            HarvestQuantity = 3,                       // 3 tomatoes per harvest
            Regrows = true,                            // Regrows after harvest
            HarvestResetStage = CropStage.Growing     // Resets to Growing stage
        };
    }

    /// <summary>
    /// Create a potato seed crop.
    /// Potatoes do NOT regrow - must replant after harvest.
    /// </summary>
    public static Crop CreatePotatoSeed(Vector2 position, int tileX, int tileY)
    {
        return new Crop("potato", position, tileX, tileY)
        {
            DaysPerStage = 3,          // 9 days total to mature (slower)
            HarvestItemId = "potato",
            HarvestQuantity = 4,       // Good yield
            Regrows = false
        };
    }
}
