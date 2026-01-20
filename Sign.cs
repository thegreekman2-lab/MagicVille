#nullable enable
using Microsoft.Xna.Framework;

namespace MagicVille;

/// <summary>
/// A sign object that displays text when interacted with.
/// Used for tutorials, hints, and world-building narrative.
/// </summary>
public class Sign : WorldObject
{
    /// <summary>The text displayed when reading the sign.</summary>
    public string Text { get; set; } = "";

    /// <summary>Parameterless constructor for JSON deserialization.</summary>
    public Sign() : base()
    {
        Name = "sign";
        Width = 32;
        Height = 48;
        Color = new Color(139, 90, 43); // Wood brown
        IsCollidable = true;
    }

    /// <summary>
    /// Create a sign with specified position and text.
    /// </summary>
    /// <param name="position">World position (bottom-center).</param>
    /// <param name="text">Text to display when read.</param>
    public Sign(Vector2 position, string text)
        : base("sign", position, 32, 48, new Color(139, 90, 43), true)
    {
        Text = text;
    }

    /// <summary>
    /// Factory method to create a sign at a specific tile.
    /// Uses WorldManager's smart alignment.
    /// </summary>
    public static Sign CreateAtTile(int tileX, int tileY, string text)
    {
        const int TileSize = 64;
        // Center horizontally on tile, align to bottom
        float x = (tileX * TileSize) + (TileSize / 2f);
        float y = (tileY * TileSize) + TileSize; // Bottom of tile

        return new Sign(new Vector2(x, y), text);
    }
}
