namespace MagicVille;

/// <summary>
/// Tools for farming, mining, combat, and magic.
/// Non-stackable items with resource cost and power level.
/// </summary>
public class Tool : Item
{
    public override bool IsStackable => false;
    public override string RegistryKey { get; init; } = "";

    /// <summary>Resource cost per use (mana or hybrid - legacy).</summary>
    public float ResourceCost { get; init; }

    /// <summary>Effectiveness level (upgradeable).</summary>
    public int PowerLevel { get; set; }

    /// <summary>
    /// Stamina (energy) cost per use.
    /// Standard costs: Watering Can = 2, Hoe = 3, Pickaxe/Axe = 4, Scythe = 0.
    /// </summary>
    public float StaminaCost { get; init; } = 0f;

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

    /// <summary>
    /// Whether this tool is a weapon (used for combat).
    /// Weapons use the attack hitbox system instead of tile interaction.
    /// </summary>
    public bool IsWeapon { get; init; } = false;

    /// <summary>
    /// Attack damage (for weapons only).
    /// </summary>
    public int AttackDamage { get; init; } = 1;

    // Parameterless constructor for JSON deserialization
    public Tool() { }

    public Tool(string registryKey, string name, string description, float resourceCost, int powerLevel, bool affectsTileThroughObjects = false, float staminaCost = 0f)
        : base(name, description, sellPrice: -1) // Tools are never sellable
    {
        RegistryKey = registryKey;
        ResourceCost = resourceCost;
        PowerLevel = powerLevel;
        AffectsTileThroughObjects = affectsTileThroughObjects;
        StaminaCost = staminaCost;
    }
}
