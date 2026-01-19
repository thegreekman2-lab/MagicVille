#nullable enable
using System.Collections.Generic;

namespace MagicVille;

/// <summary>
/// Saves the state of a single location (tiles + objects).
/// </summary>
public class LocationSaveData
{
    /// <summary>Location name (e.g., "Farm", "Cabin").</summary>
    public string Name { get; set; } = "";

    /// <summary>Modified tiles that differ from the default generated map.</summary>
    public List<TileSaveData> ModifiedTiles { get; set; } = new();

    /// <summary>World objects in this location (polymorphic).</summary>
    public List<WorldObject> Objects { get; set; } = new();
}

/// <summary>
/// Pure data transfer object for serialization.
/// Contains all game state needed to save/load.
/// </summary>
public class SaveData
{
    // Player state
    public float PlayerPositionX { get; set; }
    public float PlayerPositionY { get; set; }
    public string PlayerName { get; set; } = "Farmer";

    // World state
    public string CurrentLocationName { get; set; } = "Farm";
    public int WorldSeed { get; set; }

    // Time state
    public int Day { get; set; } = 1;
    public int TimeOfDay { get; set; } = 600;

    // Inventory - polymorphic list (Tool and Material subtypes preserved)
    public List<Item?> InventorySlots { get; set; } = new();
    public int ActiveHotbarSlot { get; set; } = 0;

    // All locations with their modified tiles and objects
    public List<LocationSaveData> Locations { get; set; } = new();
}
