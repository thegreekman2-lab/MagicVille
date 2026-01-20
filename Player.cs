#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MagicVille;

public class Player : IRenderable
{
    // Position in world coordinates (BOTTOM-CENTER / feet position)
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

    // Sprite animation (set via SetSpritesheet, nullable until then)
    private SpriteAnimator? _animator;

    // Player inventory (hotbar)
    public Inventory Inventory { get; } = new();

    // Player currency
    public int Gold { get; set; } = 500;

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

    public void Update(GameTime gameTime, KeyboardState keyboard, Func<Rectangle, bool>? canMove = null, int mapWidth = 0, int mapHeight = 0)
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
            Vector2 newPosition = Position + movement * Speed * deltaTime;

            // Check collision if a collision checker was provided
            if (canMove != null)
            {
                var newBounds = GetCollisionBoundsAt(newPosition);
                if (!canMove(newBounds))
                {
                    // Try sliding along X axis only
                    var slideX = new Vector2(newPosition.X, Position.Y);
                    var slideXBounds = GetCollisionBoundsAt(slideX);
                    if (canMove(slideXBounds))
                    {
                        newPosition = slideX;
                    }
                    else
                    {
                        // Try sliding along Y axis only
                        var slideY = new Vector2(Position.X, newPosition.Y);
                        var slideYBounds = GetCollisionBoundsAt(slideY);
                        if (canMove(slideYBounds))
                        {
                            newPosition = slideY;
                        }
                        else
                        {
                            // Can't move at all
                            newPosition = Position;
                        }
                    }
                }
            }

            // Clamp to map boundaries (prevent walking into the void)
            // Position is BOTTOM-CENTER (feet), so account for sprite dimensions
            if (mapWidth > 0 && mapHeight > 0)
            {
                float minX = Width / 2f;
                float maxX = mapWidth - Width / 2f;
                float minY = Height;
                float maxY = mapHeight;

                newPosition.X = Math.Clamp(newPosition.X, minX, maxX);
                newPosition.Y = Math.Clamp(newPosition.Y, minY, maxY);
            }

            Position = newPosition;
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
            // SpriteAnimator uses bottom-center origin automatically
            _animator.Draw(spriteBatch, Position, Width, Height);
        }
        else
        {
            // Fallback: draw using bottom-center origin
            var origin = new Vector2(Width / 2f, Height);
            var destRect = new Rectangle(0, 0, Width, Height);
            spriteBatch.Draw(
                fallbackPixel,
                Position,
                destRect,
                new Color(65, 105, 225),
                0f,
                origin,
                1f,
                SpriteEffects.None,
                0f
            );
        }
    }

    /// <summary>
    /// Get the center position (useful for camera targeting).
    /// Since Position is at feet, center is half-height above.
    /// </summary>
    public Vector2 Center => new(Position.X, Position.Y - Height / 2f);

    /// <summary>
    /// Get the bounding rectangle for visual bounds.
    /// </summary>
    public Rectangle Bounds => new(
        (int)(Position.X - Width / 2f),
        (int)(Position.Y - Height),
        Width,
        Height
    );

    /// <summary>
    /// Get the collision bounding box (feet-only, ~20% of height).
    /// Used for collision detection with world objects.
    /// Covers only the base/shadow area for proper 2.5D depth.
    /// </summary>
    public Rectangle CollisionBounds
    {
        get
        {
            int collisionHeight = Height / 5;  // 20% of height
            int collisionWidth = (int)(Width * 0.7f); // 70% of width
            return new Rectangle(
                (int)(Position.X - collisionWidth / 2f),
                (int)(Position.Y - collisionHeight),
                collisionWidth,
                collisionHeight
            );
        }
    }

    /// <summary>
    /// Get collision bounds at a hypothetical position.
    /// Used to check collisions before moving.
    /// </summary>
    public Rectangle GetCollisionBoundsAt(Vector2 position)
    {
        int collisionHeight = Height / 5;
        int collisionWidth = (int)(Width * 0.7f);
        return new Rectangle(
            (int)(position.X - collisionWidth / 2f),
            (int)(position.Y - collisionHeight),
            collisionWidth,
            collisionHeight
        );
    }

    /// <summary>
    /// Y coordinate used for depth sorting.
    /// Since Position is at feet, we use it directly.
    /// </summary>
    public float SortY => Position.Y;
}
