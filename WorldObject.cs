#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MagicVille;

/// <summary>
/// Represents a physical object in the world (rocks, trees, fences, etc.).
/// Objects have collision and are Y-sorted for proper 2.5D depth rendering.
/// </summary>
public class WorldObject : IRenderable
{
    /// <summary>Unique identifier for this object instance.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>World position (top-left of the visual sprite).</summary>
    public Vector2 Position { get; set; }

    /// <summary>Visual width in pixels.</summary>
    public int Width { get; init; }

    /// <summary>Visual height in pixels.</summary>
    public int Height { get; init; }

    /// <summary>Object type name (e.g., "rock", "tree", "fence").</summary>
    public string Name { get; init; } = "";

    /// <summary>Whether this object blocks movement.</summary>
    public bool IsCollidable { get; init; } = true;

    /// <summary>Height of the collision box at the base (feet) of the object.</summary>
    public int CollisionHeight { get; init; } = 16;

    /// <summary>
    /// Bounding box for collision detection.
    /// Covers only the "feet" or base of the object, not the full sprite.
    /// This allows visual overlap for proper depth perception.
    /// </summary>
    public Rectangle BoundingBox => new(
        (int)Position.X,
        (int)Position.Y + Height - CollisionHeight,
        Width,
        CollisionHeight
    );

    /// <summary>
    /// Y coordinate used for depth sorting.
    /// Objects with higher SortY are drawn in front (later).
    /// Uses the bottom of the bounding box for accurate sorting.
    /// </summary>
    public float SortY => Position.Y + Height;

    /// <summary>
    /// Visual bounds for rendering the full sprite.
    /// </summary>
    public Rectangle DrawBounds => new(
        (int)Position.X,
        (int)Position.Y,
        Width,
        Height
    );

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
        // Default collision height is 1/4 of visual height
        CollisionHeight = Math.Max(8, height / 4);
    }

    /// <summary>
    /// Draw the object using a placeholder colored rectangle.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
    {
        // Draw main body
        spriteBatch.Draw(pixel, DrawBounds, Color);

        // Draw darker base/shadow at feet for visual clarity
        var baseRect = new Rectangle(
            (int)Position.X,
            (int)Position.Y + Height - CollisionHeight,
            Width,
            CollisionHeight
        );
        spriteBatch.Draw(pixel, baseRect, new Color(0, 0, 0, 80));
    }

    #region Factory Methods

    /// <summary>Create a rock object.</summary>
    public static WorldObject CreateRock(Vector2 position)
    {
        return new WorldObject("rock", position, 48, 40, new Color(100, 100, 110), true)
        {
            CollisionHeight = 12
        };
    }

    /// <summary>Create a tree object.</summary>
    public static WorldObject CreateTree(Vector2 position)
    {
        return new WorldObject("tree", position, 64, 96, new Color(34, 100, 34), true)
        {
            CollisionHeight = 16
        };
    }

    /// <summary>Create a fence post.</summary>
    public static WorldObject CreateFence(Vector2 position)
    {
        return new WorldObject("fence", position, 32, 48, new Color(139, 90, 43), true)
        {
            CollisionHeight = 8
        };
    }

    /// <summary>Create a bush (decorative, smaller collision).</summary>
    public static WorldObject CreateBush(Vector2 position)
    {
        return new WorldObject("bush", position, 40, 32, new Color(50, 130, 50), true)
        {
            CollisionHeight = 10
        };
    }

    #endregion
}
