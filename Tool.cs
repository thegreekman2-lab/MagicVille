namespace MagicVille;

/// <summary>
/// Tools for farming, mining, combat, and magic.
/// Non-stackable items with resource cost and power level.
/// </summary>
public class Tool : Item
{
    public override bool IsStackable => false;
    public override string RegistryKey { get; init; } = "";

    /// <summary>Resource cost per use (stamina, mana, or hybrid).</summary>
    public float ResourceCost { get; init; }

    /// <summary>Effectiveness level (upgradeable).</summary>
    public int PowerLevel { get; set; }

    /// <summary>
    /// Whether this tool affects the underlying tile even when an object is present.
    /// True = tool effect "passes through" objects to also affect the tile.
    /// False = tool effect stops at the object (default behavior).
    ///
    /// Examples:
    /// - Watering Can (true): Waters crop AND wets the soil underneath
    /// - Pickaxe (false): Breaks rock but doesn't affect tile under it
    /// - Sprinkler (true): Future items can inherit this behavior
    /// </summary>
    public bool AffectsTileThroughObjects { get; init; } = false;

    // Parameterless constructor for JSON deserialization
    public Tool() { }

    public Tool(string registryKey, string name, string description, float resourceCost, int powerLevel, bool affectsTileThroughObjects = false)
        : base(name, description)
    {
        RegistryKey = registryKey;
        ResourceCost = resourceCost;
        PowerLevel = powerLevel;
        AffectsTileThroughObjects = affectsTileThroughObjects;
    }
}
