#nullable enable
using System.Collections.Generic;

namespace MagicVille;

// ════════════════════════════════════════════════════════════════════════════
// ITEM DTO - Flat serialization for polymorphic Items
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Flat DTO for Item serialization.
/// Avoids polymorphic serialization issues by storing type explicitly.
///
/// RESTORATION STRATEGY:
/// - Tools: Recreated from RegistryKey using tool factory (GiveStarterItems pattern)
/// - Materials: Created with RegistryKey, Name, Quantity, SellPrice
/// </summary>
public class ItemData
{
    /// <summary>Item type discriminator: "tool" or "material".</summary>
    public string Type { get; set; } = "";

    /// <summary>Registry key for item identification.</summary>
    public string RegistryKey { get; set; } = "";

    /// <summary>Display name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Description text.</summary>
    public string Description { get; set; } = "";

    /// <summary>Stack quantity (Materials only).</summary>
    public int Quantity { get; set; } = 1;

    /// <summary>Max stack size (Materials only).</summary>
    public int MaxStack { get; set; } = 99;

    /// <summary>Sell price (-1 = unsellable).</summary>
    public int SellPrice { get; set; } = -1;

    // ═══════════════════════════════════════════════════════════════════════
    // TOOL-SPECIFIC PROPERTIES (only used when Type == "tool")
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Whether this is a weapon (combat tool).</summary>
    public bool IsWeapon { get; set; }

    /// <summary>Attack style: None, Melee, Projectile, Raycast.</summary>
    public string AttackStyle { get; set; } = "None";

    /// <summary>Base damage for weapons.</summary>
    public int Damage { get; set; }

    /// <summary>Attack range in pixels.</summary>
    public float Range { get; set; }

    /// <summary>Projectile speed (Projectile weapons only).</summary>
    public float ProjectileSpeed { get; set; }

    /// <summary>Attack cooldown in seconds.</summary>
    public float Cooldown { get; set; }

    /// <summary>Stamina cost per use.</summary>
    public float StaminaCost { get; set; }

    /// <summary>Resource cost (legacy mana cost).</summary>
    public float ResourceCost { get; set; }

    /// <summary>Tool power level.</summary>
    public int PowerLevel { get; set; }

    /// <summary>Whether tool affects tile through objects.</summary>
    public bool AffectsTileThroughObjects { get; set; }

    /// <summary>Projectile color (packed RGBA).</summary>
    public uint ProjectileColorPacked { get; set; }

    /// <summary>Melee hitbox width.</summary>
    public int HitboxWidth { get; set; }

    /// <summary>Melee hitbox height.</summary>
    public int HitboxHeight { get; set; }
}

// ════════════════════════════════════════════════════════════════════════════
// WORLD OBJECT DTO - Flat serialization for polymorphic WorldObjects
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Flat DTO for WorldObject serialization.
/// Stores all possible fields for all subclasses, using Type to determine which apply.
///
/// SUPPORTED TYPES:
/// - "base": Generic WorldObject (rock, bush, fence)
/// - "crop": Crop with growth stage
/// - "tree": Tree with growth stage
/// - "mana_node": ManaNode with charge state
/// - "bed": Bed furniture
/// - "shipping_bin": ShippingBin with manifest
/// - "sign": Sign with text
/// </summary>
public class WorldObjectData
{
    // ═══════════════════════════════════════════════════════════════════════
    // COMMON FIELDS (all types)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Type discriminator for object recreation.</summary>
    public string Type { get; set; } = "base";

    /// <summary>Object name (e.g., "rock", "tree", "crop").</summary>
    public string Name { get; set; } = "";

    /// <summary>World position X.</summary>
    public float X { get; set; }

    /// <summary>World position Y.</summary>
    public float Y { get; set; }

    /// <summary>Visual width in pixels.</summary>
    public int Width { get; set; }

    /// <summary>Visual height in pixels.</summary>
    public int Height { get; set; }

    /// <summary>Color packed as RGBA uint.</summary>
    public uint ColorPacked { get; set; }

    /// <summary>Whether object blocks movement.</summary>
    public bool IsCollidable { get; set; } = true;

    // ═══════════════════════════════════════════════════════════════════════
    // CROP-SPECIFIC FIELDS (Type == "crop")
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Crop type identifier (e.g., "corn", "tomato").</summary>
    public string CropType { get; set; } = "";

    /// <summary>Current growth stage (0-4).</summary>
    public int CropStage { get; set; }

    /// <summary>Days spent at current stage.</summary>
    public int DaysAtStage { get; set; }

    /// <summary>Days required per stage.</summary>
    public int DaysPerStage { get; set; } = 2;

    /// <summary>Whether crop was watered today.</summary>
    public bool WasWateredToday { get; set; }

    /// <summary>Days without water (for death tracking).</summary>
    public int DaysWithoutWater { get; set; }

    /// <summary>Max days without water before death.</summary>
    public int MaxDaysWithoutWater { get; set; } = 3;

    /// <summary>Item ID dropped on harvest.</summary>
    public string HarvestItemId { get; set; } = "";

    /// <summary>Whether crop regrows after harvest.</summary>
    public bool Regrows { get; set; }

    /// <summary>Stage to reset to after harvest (if Regrows).</summary>
    public int HarvestResetStage { get; set; }

    /// <summary>Quantity of items dropped on harvest.</summary>
    public int HarvestQuantity { get; set; } = 1;

    // ═══════════════════════════════════════════════════════════════════════
    // TREE-SPECIFIC FIELDS (Type == "tree")
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Tree type identifier (e.g., "oak", "pine").</summary>
    public string TreeType { get; set; } = "";

    /// <summary>Tree growth stage (0-3).</summary>
    public int TreeStage { get; set; }

    /// <summary>Days to reach young stage.</summary>
    public int DaysToYoung { get; set; } = 3;

    /// <summary>Days to reach mature stage.</summary>
    public int DaysToMature { get; set; } = 5;

    /// <summary>Days for stump to regrow.</summary>
    public int DaysToRegrow { get; set; } = 7;

    // ═══════════════════════════════════════════════════════════════════════
    // MANA NODE-SPECIFIC FIELDS (Type == "mana_node")
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Crystal type (e.g., "arcane", "fire").</summary>
    public string CrystalType { get; set; } = "";

    /// <summary>Current charge level.</summary>
    public int CurrentCharge { get; set; }

    /// <summary>Maximum charge capacity.</summary>
    public int MaxCharge { get; set; } = 3;

    /// <summary>Charge restored per day.</summary>
    public int RechargePerDay { get; set; } = 1;

    // ═══════════════════════════════════════════════════════════════════════
    // BED-SPECIFIC FIELDS (Type == "bed")
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Bed style (e.g., "wooden", "fancy").</summary>
    public string BedStyle { get; set; } = "";

    /// <summary>Whether bed can be used for sleeping.</summary>
    public bool CanSleep { get; set; } = true;

    // ═══════════════════════════════════════════════════════════════════════
    // SIGN-SPECIFIC FIELDS (Type == "sign")
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Text displayed when reading the sign.</summary>
    public string SignText { get; set; } = "";

    // ═══════════════════════════════════════════════════════════════════════
    // SHIPPING BIN-SPECIFIC FIELDS (Type == "shipping_bin")
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>The last shipped item in buffer slot (can be retrieved).</summary>
    public ItemData? LastShippedItem { get; set; }

    /// <summary>Committed items in shipping manifest.</summary>
    public List<ShippingBin.ShippedItem> ShippingManifest { get; set; } = new();
}

// ════════════════════════════════════════════════════════════════════════════
// LOCATION SAVE DATA - Per-location state
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Saves the state of a single location (tiles + objects).
/// Uses flat DTOs to avoid polymorphic serialization issues.
/// </summary>
public class LocationSaveData
{
    /// <summary>Location name (e.g., "Farm", "Cabin").</summary>
    public string Name { get; set; } = "";

    /// <summary>Modified tiles that differ from the default generated map.</summary>
    public List<TileSaveData> ModifiedTiles { get; set; } = new();

    /// <summary>World objects in this location (flat DTOs).</summary>
    public List<WorldObjectData> Objects { get; set; } = new();
}

// ════════════════════════════════════════════════════════════════════════════
// MAIN SAVE DATA - Complete game state
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Pure data transfer object for serialization.
/// Contains all game state needed to save/load.
///
/// v2.16: Uses flat DTOs (ItemData, WorldObjectData) to avoid polymorphic
/// serialization failures that were stripping subclass data.
/// </summary>
public class SaveData
{
    // Player state
    public float PlayerPositionX { get; set; }
    public float PlayerPositionY { get; set; }
    public string PlayerName { get; set; } = "Farmer";
    public int PlayerGold { get; set; } = 500;
    public float PlayerStamina { get; set; } = 100f;
    public int PlayerHP { get; set; } = 10;

    // World state
    public string CurrentLocationName { get; set; } = "Farm";
    public int WorldSeed { get; set; }

    // Time state
    public int Day { get; set; } = 1;
    public int TimeOfDay { get; set; } = 600;

    // Inventory - flat DTO list (v2.16: replaces polymorphic List<Item?>)
    public List<ItemData?> InventorySlots { get; set; } = new();
    public int ActiveHotbarSlot { get; set; } = 0;

    // All locations with their modified tiles and objects
    public List<LocationSaveData> Locations { get; set; } = new();
}
