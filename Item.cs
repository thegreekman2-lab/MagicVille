using System;
using System.Text.Json.Serialization;

namespace MagicVille;

/// <summary>
/// Abstract base class for all items.
/// Uses System.Text.Json polymorphic serialization.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Tool), "tool")]
[JsonDerivedType(typeof(Material), "material")]
public abstract class Item
{
    /// <summary>Unique instance identifier.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Display name.</summary>
    public string Name { get; init; } = "";

    /// <summary>Flavor text or usage hint.</summary>
    public string Description { get; init; } = "";

    /// <summary>Whether this item can stack with others of the same type.</summary>
    public abstract bool IsStackable { get; }

    /// <summary>Registry key for item identification.</summary>
    public abstract string RegistryKey { get; init; }

    /// <summary>
    /// Sell price in gold. -1 means item cannot be sold.
    /// Tools default to -1 (unsellable), Materials/Crops have positive values.
    /// </summary>
    public int SellPrice { get; init; } = -1;

    /// <summary>Whether this item can be sold (SellPrice > 0).</summary>
    public bool IsSellable => SellPrice > 0;

    // Parameterless constructor for JSON deserialization
    protected Item() { }

    protected Item(string name, string description, int sellPrice = -1)
    {
        Name = name;
        Description = description;
        SellPrice = sellPrice;
    }
}
