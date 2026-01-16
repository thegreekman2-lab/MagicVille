namespace MagicVille;

/// <summary>
/// Pure data transfer object for serialization.
/// Contains ONLY primitive/serializable types - no MonoGame objects.
/// This is the "Data" layer, separate from "Live" game objects.
/// </summary>
public class SaveData
{
    // Player state (Vector2 split into primitives for JSON compatibility)
    public float PlayerPositionX { get; set; }
    public float PlayerPositionY { get; set; }
    public string PlayerName { get; set; } = "Farmer";

    // World state
    public string CurrentLocationName { get; set; } = "Farm";
    public int WorldSeed { get; set; }

    // Future expansion (commented out for V0)
    // public int Day { get; set; } = 1;
    // public int Season { get; set; } = 0;
    // public int Year { get; set; } = 1;
    // public int TimeOfDay { get; set; } = 600;
    // public Dictionary<string, bool> EventFlags { get; set; }
    // public List<InventoryItemData> Inventory { get; set; }
}
