#nullable enable
using System;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MagicVille;

/// <summary>
/// Represents a physical object in the world (rocks, trees, fences, etc.).
/// Objects have collision and are Y-sorted for proper 2.5D depth rendering.
/// Position uses BOTTOM-CENTER pivot (feet position).
///
/// This is the base class for polymorphic world objects.
/// Subclasses (Crop, Tree, ManaNode, Bed) override OnNewDay() for daily updates.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(WorldObject), "base")]
[JsonDerivedType(typeof(Crop), "crop")]
[JsonDerivedType(typeof(Tree), "tree")]
[JsonDerivedType(typeof(ManaNode), "mana_node")]
[JsonDerivedType(typeof(Bed), "bed")]
[JsonDerivedType(typeof(ShippingBin), "shipping_bin")]
[JsonDerivedType(typeof(Sign), "sign")]
public class WorldObject : IRenderable
{
    /// <summary>Unique identifier for this object instance.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>World position (BOTTOM-CENTER / feet of the sprite).</summary>
    [JsonIgnore]
    public Vector2 Position { get; set; }

    // Serialization helpers for Vector2
    public float PositionX { get => Position.X; set => Position = new Vector2(value, Position.Y); }
    public float PositionY { get => Position.Y; set => Position = new Vector2(Position.X, value); }

    /// <summary>Visual width in pixels.</summary>
    public int Width { get; set; }

    /// <summary>Visual height in pixels.</summary>
    public int Height { get; set; }

    /// <summary>Object type name (e.g., "rock", "tree", "fence").</summary>
    public string Name { get; set; } = "";

    /// <summary>Whether this object blocks movement.</summary>
    public bool IsCollidable { get; set; } = true;

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

    /// <summary>
    /// Get the grid (tile) coordinates for this object.
    /// Converts from World Space (pixels) to Grid Space (tile indices).
    ///
    /// This is essential for bridging the "Visual World" (pixels with smart alignment)
    /// and the "Data World" (tile-based lookups like ModifiedTiles dictionary).
    ///
    /// Uses the visual center of the object to determine tile ownership:
    /// - centerX = Position.X (already at horizontal center for bottom-center pivot)
    /// - centerY = Position.Y - Height/2 (vertical center of the object)
    /// </summary>
    /// <returns>Grid coordinates (tile X, tile Y) as a Point.</returns>
    public Point GetGridPosition()
    {
        const int TileSize = GameLocation.TileSize;

        // For bottom-center pivot: Position.X is already at horizontal center
        // Vertical center is halfway up from the feet
        float centerX = Position.X;
        float centerY = Position.Y - (Height / 2f);

        int gridX = (int)(centerX / TileSize);
        int gridY = (int)(centerY / TileSize);

        return new Point(gridX, gridY);
    }

    /// <summary>
    /// Get the grid X coordinate for this object.
    /// Shorthand for GetGridPosition().X.
    /// </summary>
    public int GridX => GetGridPosition().X;

    /// <summary>
    /// Get the grid Y coordinate for this object.
    /// Shorthand for GetGridPosition().Y.
    /// </summary>
    public int GridY => GetGridPosition().Y;

    /// <summary>Placeholder color for rendering (until we have sprites).</summary>
    [JsonIgnore]
    public Color Color { get; set; } = Color.Gray;

    // Serialization helpers for Color (packed RGBA int)
    public uint ColorPacked { get => Color.PackedValue; set => Color = new Color(value); }

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

    /// <summary>
    /// Called at the start of each new day. Override in subclasses for daily updates.
    /// Base implementation does nothing (rocks, fences, etc. don't change daily).
    ///
    /// IMPORTANT: This is called BEFORE tiles are reset (dried).
    /// Crops can check if their tile is WetDirt to determine watering status.
    /// </summary>
    /// <param name="location">The location containing this object (for tile state checks).</param>
    /// <returns>True if object should be removed (dead/depleted), false to keep.</returns>
    public virtual bool OnNewDay(GameLocation? location = null)
    {
        return false; // Base objects don't change
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
