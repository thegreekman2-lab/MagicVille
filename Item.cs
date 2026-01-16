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

    // Parameterless constructor for JSON deserialization
    protected Item() { }

    protected Item(string name, string description)
    {
        Name = name;
        Description = description;
    }
}
