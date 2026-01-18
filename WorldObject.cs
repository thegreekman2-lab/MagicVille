#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MagicVille;

/// <summary>
/// Represents a physical object in the world (rocks, trees, fences, etc.).
/// Objects have collision and are Y-sorted for proper 2.5D depth rendering.
/// Position uses BOTTOM-CENTER pivot (feet position).
/// </summary>
public class WorldObject : IRenderable
{
    /// <summary>Unique identifier for this object instance.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>World position (BOTTOM-CENTER / feet of the sprite).</summary>
    public Vector2 Position { get; set; }

    /// <summary>Visual width in pixels.</summary>
    public int Width { get; init; }

    /// <summary>Visual height in pixels.</summary>
    public int Height { get; init; }

    /// <summary>Object type name (e.g., "rock", "tree", "fence").</summary>
    public string Name { get; init; } = "";

    /// <summary>Whether this object blocks movement.</summary>
    public bool IsCollidable { get; init; } = true;

    /// <summary>
    /// Bounding box for collision detection.
    /// Covers only the "feet" or base of the object (~20% of height).
    /// This allows visual overlap for proper 2.5D depth perception.
    /// </summary>
    public Rectangle BoundingBox
    {
        get
        {
            int collisionHeight = Math.Max(8, Height / 5); // 20% of height
            int collisionWidth = (int)(Width * 0.8f);      // 80% of width, centered
            return new Rectangle(
                (int)(Position.X - collisionWidth / 2f),
                (int)(Position.Y - collisionHeight),
                collisionWidth,
                collisionHeight
            );
        }
    }

    /// <summary>
    /// Y coordinate used for depth sorting.
    /// Since Position is at feet, we sort directly by Position.Y.
    /// Objects with higher Y are drawn in front (later).
    /// </summary>
    public float SortY => Position.Y;

    /// <summary>Placeholder color for rendering (until we have sprites).</summary>
    public Color Color { get; init; } = Color.Gray;

    public WorldObject() { }

    public WorldObject(string name, Vector2 position, int width, int height, Color color, bool isCollidable = true)
    {
        Name = name;
        Position = position;
        Width = width;
        Height = height;
        Color = color;
        IsCollidable = isCollidable;
    }

    /// <summary>
    /// Draw the object using a placeholder colored rectangle.
    /// Uses BOTTOM-CENTER origin: Position is where the feet go.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
    {
        // Origin at bottom-center
        var origin = new Vector2(Width / 2f, Height);

        // Draw main body using origin-based positioning
        var destRect = new Rectangle(0, 0, Width, Height);
        spriteBatch.Draw(
            pixel,
            Position,
            destRect,
            Color,
            0f,
            origin,
            1f,
            SpriteEffects.None,
            0f
        );

        // Draw darker base/shadow at feet for visual clarity
        var shadowBounds = BoundingBox;
        spriteBatch.Draw(pixel, shadowBounds, new Color(0, 0, 0, 80));
    }

    #region Factory Methods

    /// <summary>Create a rock object.</summary>
    public static WorldObject CreateRock(Vector2 position)
    {
        return new WorldObject("rock", position, 48, 40, new Color(100, 100, 110), true);
    }

    /// <summary>Create a tree object.</summary>
    public static WorldObject CreateTree(Vector2 position)
    {
        return new WorldObject("tree", position, 64, 96, new Color(34, 100, 34), true);
    }

    /// <summary>Create a fence post.</summary>
    public static WorldObject CreateFence(Vector2 position)
    {
        return new WorldObject("fence", position, 32, 48, new Color(139, 90, 43), true);
    }

    /// <summary>Create a bush (decorative, smaller collision).</summary>
    public static WorldObject CreateBush(Vector2 position)
    {
        return new WorldObject("bush", position, 40, 32, new Color(50, 130, 50), true);
    }

    /// <summary>Create a mana node (blue glowing rock, harvestable for mana).</summary>
    public static WorldObject CreateManaNode(Vector2 position)
    {
        return new WorldObject("mana_node", position, 40, 48, new Color(80, 150, 255), true);
    }

    #endregion
}
