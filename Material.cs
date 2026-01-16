namespace MagicVille;

/// <summary>
/// Stackable materials for crafting and building.
/// </summary>
public class Material : Item
{
    public override bool IsStackable => true;
    public override string RegistryKey { get; init; } = "";

    /// <summary>Current quantity in this stack.</summary>
    public int Quantity { get; set; }

    /// <summary>Maximum stack size.</summary>
    public int MaxStack { get; init; }

    // Parameterless constructor for JSON deserialization
    public Material() { }

    public Material(string registryKey, string name, string description, int quantity = 1, int maxStack = 99)
        : base(name, description)
    {
        RegistryKey = registryKey;
        Quantity = quantity;
        MaxStack = maxStack;
    }
}
