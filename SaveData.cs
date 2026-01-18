#nullable enable
using System.Collections.Generic;

namespace MagicVille;

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

    // Inventory - polymorphic list (Tool and Material subtypes preserved)
    public List<Item?> InventorySlots { get; set; } = new();
    public int ActiveHotbarSlot { get; set; } = 0;

    // Map state - only stores tiles that differ from the default generated map
    public List<TileSaveData> ModifiedTiles { get; set; } = new();
}
