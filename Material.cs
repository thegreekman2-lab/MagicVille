namespace MagicVille;

/// <summary>
/// Stackable materials for crafting, building, and selling.
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

    /// <summary>
    /// Create a new Material item.
    /// </summary>
    /// <param name="registryKey">Unique identifier for this material type.</param>
    /// <param name="name">Display name.</param>
    /// <param name="description">Flavor text.</param>
    /// <param name="quantity">Initial stack size.</param>
    /// <param name="maxStack">Maximum stack size.</param>
    /// <param name="sellPrice">Price when sold (-1 for unsellable, or use default based on type).</param>
    public Material(string registryKey, string name, string description, int quantity = 1, int maxStack = 99, int sellPrice = 10)
        : base(name, description, sellPrice)
    {
        RegistryKey = registryKey;
        Quantity = quantity;
        MaxStack = maxStack;
    }
}
