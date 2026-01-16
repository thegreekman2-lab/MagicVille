using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MagicVille;

public class Player
{
    // Position in world coordinates (pixels, not tiles)
    public Vector2 Position { get; set; }

    // Player name
    public string Name { get; set; } = "Farmer";

    // Movement speed in pixels per second
    public float Speed { get; set; } = 200f;

    // Visual size (slightly smaller than a tile)
    public const int Width = 48;
    public const int Height = 48;

    public Player(Vector2 startPosition)
    {
        Position = startPosition;
    }

    public void Update(GameTime gameTime, KeyboardState keyboard)
    {
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        Vector2 movement = Vector2.Zero;

        // WASD and Arrow keys
        if (keyboard.IsKeyDown(Keys.W) || keyboard.IsKeyDown(Keys.Up))
            movement.Y -= 1;
        if (keyboard.IsKeyDown(Keys.S) || keyboard.IsKeyDown(Keys.Down))
            movement.Y += 1;
        if (keyboard.IsKeyDown(Keys.A) || keyboard.IsKeyDown(Keys.Left))
            movement.X -= 1;
        if (keyboard.IsKeyDown(Keys.D) || keyboard.IsKeyDown(Keys.Right))
            movement.X += 1;

        // Normalize diagonal movement so it's not faster
        if (movement != Vector2.Zero)
        {
            movement.Normalize();
            Position += movement * Speed * deltaTime;
        }
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
    {
        // Draw player as a colored rectangle centered on position
        var drawRect = new Rectangle(
            (int)(Position.X - Width / 2f),
            (int)(Position.Y - Height / 2f),
            Width,
            Height
        );

        // Player color: a nice blue
        spriteBatch.Draw(pixel, drawRect, new Color(65, 105, 225));
    }

    /// <summary>
    /// Get the center position (useful for camera targeting).
    /// </summary>
    public Vector2 Center => Position;

    /// <summary>
    /// Get the bounding rectangle for collision (future use).
    /// </summary>
    public Rectangle Bounds => new(
        (int)(Position.X - Width / 2f),
        (int)(Position.Y - Height / 2f),
        Width,
        Height
    );
}
