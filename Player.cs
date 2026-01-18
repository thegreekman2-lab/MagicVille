using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MagicVille;

public class Player : IRenderable
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

    // Current facing direction
    public Direction Facing { get; private set; } = Direction.Down;

    // Sprite animation
    private SpriteAnimator _animator;

    // Player inventory (hotbar)
    public Inventory Inventory { get; } = new();

    public Player(Vector2 startPosition)
    {
        Position = startPosition;
        Inventory.GiveStarterItems();
    }

    /// <summary>
    /// Initialize the animator with a spritesheet.
    /// Call this after creating the player and loading textures.
    /// </summary>
    public void SetSpritesheet(Texture2D spritesheet, int frameWidth, int frameHeight)
    {
        _animator = new SpriteAnimator(spritesheet, frameWidth, frameHeight);
        _animator.FrameTime = 0.12f; // Slightly fast walk cycle

        // Define animations: column indices in the spritesheet
        // Idle = frame 0, Walk = frames 1, 2, 3
        _animator.AddAnimation("Idle", startFrame: 0, frameCount: 1);
        _animator.AddAnimation("Walk", startFrame: 1, frameCount: 3);

        // Start idle
        _animator.PlayAnimation("Idle");
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
        bool isMoving = movement != Vector2.Zero;

        if (isMoving)
        {
            // Update facing direction based on movement
            UpdateFacingDirection(movement);

            movement.Normalize();
            Position += movement * Speed * deltaTime;
        }

        // Update animation state
        if (_animator != null)
        {
            _animator.Direction = Facing;
            _animator.PlayAnimation(isMoving ? "Walk" : "Idle");
            _animator.Update(gameTime);
        }
    }

    private void UpdateFacingDirection(Vector2 movement)
    {
        // Prioritize vertical when pressing diagonals (common in top-down games)
        // You can change this to prioritize horizontal if preferred
        if (MathF.Abs(movement.Y) >= MathF.Abs(movement.X))
        {
            Facing = movement.Y < 0 ? Direction.Up : Direction.Down;
        }
        else
        {
            Facing = movement.X < 0 ? Direction.Left : Direction.Right;
        }
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D fallbackPixel)
    {
        if (_animator != null)
        {
            _animator.Draw(spriteBatch, Position, Width, Height);
        }
        else
        {
            // Fallback to colored rectangle if no spritesheet set
            var drawRect = new Rectangle(
                (int)(Position.X - Width / 2f),
                (int)(Position.Y - Height / 2f),
                Width,
                Height
            );
            spriteBatch.Draw(fallbackPixel, drawRect, new Color(65, 105, 225));
        }
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

    /// <summary>
    /// Y coordinate used for depth sorting.
    /// Uses the bottom of the player's feet for accurate Y-sorting.
    /// </summary>
    public float SortY => Position.Y + Height / 2f;
}
