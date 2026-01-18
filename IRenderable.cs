using Microsoft.Xna.Framework.Graphics;

namespace MagicVille;

/// <summary>
/// Interface for objects that can be Y-sorted and rendered in the world.
/// Used for depth sorting player and world objects together.
/// </summary>
public interface IRenderable
{
    /// <summary>
    /// Y coordinate used for depth sorting.
    /// Objects with higher SortY values are drawn in front (later).
    /// Typically the bottom of the object's feet/base.
    /// </summary>
    float SortY { get; }

    /// <summary>
    /// Draw the object using a placeholder texture.
    /// </summary>
    void Draw(SpriteBatch spriteBatch, Texture2D pixel);
}
