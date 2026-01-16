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

    // Parameterless constructor for JSON deserialization
    public Tool() { }

    public Tool(string registryKey, string name, string description, float resourceCost, int powerLevel)
        : base(name, description)
    {
        RegistryKey = registryKey;
        ResourceCost = resourceCost;
        PowerLevel = powerLevel;
    }
}
