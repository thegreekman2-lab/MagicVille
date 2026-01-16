using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MagicVille;

public class Camera2D
{
    public Vector2 Position { get; set; }
    public float Zoom { get; set; } = 1f;

    private readonly GraphicsDevice _graphicsDevice;

    /// <summary>Current viewport (always reflects current window size).</summary>
    private Viewport Viewport => _graphicsDevice.Viewport;

    public Camera2D(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
    }

    /// <summary>
    /// Centers the camera on a world position.
    /// </summary>
    public void CenterOn(Vector2 worldPosition)
    {
        Position = worldPosition;
    }

    /// <summary>
    /// Gets the transformation matrix for SpriteBatch.Begin().
    /// </summary>
    public Matrix GetTransformMatrix()
    {
        return Matrix.CreateTranslation(-Position.X, -Position.Y, 0f) *
               Matrix.CreateScale(Zoom, Zoom, 1f) *
               Matrix.CreateTranslation(Viewport.Width / 2f, Viewport.Height / 2f, 0f);
    }

    /// <summary>
    /// Returns the visible world bounds (for culling).
    /// </summary>
    public Rectangle GetVisibleBounds()
    {
        float width = Viewport.Width / Zoom;
        float height = Viewport.Height / Zoom;

        return new Rectangle(
            (int)(Position.X - width / 2),
            (int)(Position.Y - height / 2),
            (int)width,
            (int)height
        );
    }

    /// <summary>
    /// Converts screen coordinates to world coordinates.
    /// </summary>
    public Vector2 ScreenToWorld(Vector2 screenPosition)
    {
        return Vector2.Transform(screenPosition, Matrix.Invert(GetTransformMatrix()));
    }

    /// <summary>
    /// Converts world coordinates to screen coordinates.
    /// </summary>
    public Vector2 WorldToScreen(Vector2 worldPosition)
    {
        return Vector2.Transform(worldPosition, GetTransformMatrix());
    }
}
