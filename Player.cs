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

    #region Stats

    /// <summary>Maximum stamina (energy) capacity.</summary>
    public float MaxStamina { get; private set; } = 100f;

    /// <summary>Current stamina level. Depletes with tool use, restores on sleep.</summary>
    public float CurrentStamina { get; set; }

    /// <summary>Maximum health points.</summary>
    public int MaxHP { get; private set; } = 10;

    /// <summary>Current health points.</summary>
    public int CurrentHP { get; set; }

    /// <summary>Invincibility frames after taking damage.</summary>
    private float _invincibilityTimer;
    private const float InvincibilityDuration = 1.0f;

    /// <summary>Whether player is currently invincible (i-frames).</summary>
    public bool IsInvincible => _invincibilityTimer > 0;

    /// <summary>Whether player was recently hit (for flash effect).</summary>
    public bool IsHit { get; private set; }

    /// <summary>
    /// Attempt to use stamina for an action.
    /// </summary>
    /// <param name="cost">Stamina cost of the action.</param>
    /// <returns>True if action succeeded (enough stamina), false if too tired.</returns>
    public bool TryUseStamina(float cost)
    {
        if (cost <= 0f)
            return true; // Free actions always succeed

        if (CurrentStamina >= cost)
        {
            CurrentStamina -= cost;
            return true;
        }

        return false; // Too tired!
    }

    /// <summary>
    /// Fully restore stamina (called on sleep/new day).
    /// </summary>
    public void RestoreStamina()
    {
        CurrentStamina = MaxStamina;
    }

    /// <summary>
    /// Fully restore health (called on sleep/new day).
    /// </summary>
    public void RestoreHealth()
    {
        CurrentHP = MaxHP;
    }

    /// <summary>
    /// Take damage from an enemy or hazard.
    /// </summary>
    /// <param name="damage">Amount of damage.</param>
    /// <param name="sourcePosition">Position of damage source (for knockback).</param>
    /// <returns>True if player died.</returns>
    public bool TakeDamage(int damage, Vector2 sourcePosition)
    {
        if (IsInvincible)
            return false;

        CurrentHP -= damage;
        IsHit = true;
        _invincibilityTimer = InvincibilityDuration;

        System.Diagnostics.Debug.WriteLine($"[Player] Ouch! Took {damage} damage. HP: {CurrentHP}/{MaxHP}");

        // TODO: Apply knockback
        // TODO: Play hurt sound

        if (CurrentHP <= 0)
        {
            System.Diagnostics.Debug.WriteLine("[Player] Player defeated!");
            // TODO: Handle death (respawn, game over screen, etc.)
            return true;
        }

        return false;
    }

    /// <summary>
    /// Update all combat-related timers (invincibility, attack cooldown).
    /// Call each frame.
    /// </summary>
    public void UpdateCombatTimers(float deltaTime)
    {
        // Invincibility frames
        if (_invincibilityTimer > 0)
        {
            _invincibilityTimer -= deltaTime;
            IsHit = _invincibilityTimer > InvincibilityDuration - 0.15f; // Flash for first 0.15s
        }

        // Attack cooldown
        UpdateAttackCooldown(deltaTime);
    }

    #endregion

    #region Combat

    /// <summary>Default attack hitbox size (in pixels) - used if weapon doesn't specify.</summary>
    private const int DefaultAttackHitboxWidth = 48;
    private const int DefaultAttackHitboxDepth = 32;

    /// <summary>Current attack cooldown remaining (seconds).</summary>
    private float _attackCooldown;

    /// <summary>Whether player can attack (cooldown expired).</summary>
    public bool CanAttack => _attackCooldown <= 0;

    /// <summary>
    /// Start attack cooldown after using a weapon.
    /// </summary>
    public void StartAttackCooldown(float duration)
    {
        _attackCooldown = duration;
    }

    /// <summary>
    /// Update attack cooldown timer.
    /// </summary>
    private void UpdateAttackCooldown(float deltaTime)
    {
        if (_attackCooldown > 0)
            _attackCooldown -= deltaTime;
    }

    /// <summary>
    /// Get the facing direction as a unit vector.
    /// Used for projectile/raycast direction.
    /// </summary>
    public Vector2 GetFacingVector()
    {
        return Facing switch
        {
            Direction.Up => new Vector2(0, -1),
            Direction.Down => new Vector2(0, 1),
            Direction.Left => new Vector2(-1, 0),
            Direction.Right => new Vector2(1, 0),
            _ => new Vector2(0, 1) // Default down
        };
    }

    /// <summary>
    /// Get the spawn point for projectiles (in front of player).
    /// </summary>
    public Vector2 GetProjectileSpawnPoint(float offset = 32f)
    {
        Vector2 facingDir = GetFacingVector();
        return Center + facingDir * offset;
    }

    /// <summary>
    /// Get a target position clamped to maximum range.
    /// Allows aiming at any distance up to maxRange - if target is beyond,
    /// returns point at maxRange in that direction.
    ///
    /// SMART AIMING: Click anywhere and the attack goes toward that point,
    /// but won't exceed weapon range. No more "must click inside circle" frustration.
    /// </summary>
    /// <param name="targetPos">World position the player clicked/aimed at.</param>
    /// <param name="maxRange">Maximum weapon range in pixels.</param>
    /// <returns>Clamped target position within range.</returns>
    public Vector2 GetClampedTarget(Vector2 targetPos, float maxRange)
    {
        Vector2 toTarget = targetPos - Center;
        float distance = toTarget.Length();

        if (distance <= maxRange || distance < 0.001f)
        {
            // Target is within range - use exact position
            return targetPos;
        }

        // Target is beyond range - clamp to max range in that direction
        Vector2 direction = toTarget / distance; // Normalize
        return Center + direction * maxRange;
    }

    /// <summary>
    /// Get the direction vector from player center to a target position.
    /// </summary>
    /// <param name="targetPos">World position to aim at.</param>
    /// <returns>Normalized direction vector.</returns>
    public Vector2 GetDirectionTo(Vector2 targetPos)
    {
        Vector2 toTarget = targetPos - Center;
        float distance = toTarget.Length();

        if (distance < 0.001f)
            return GetFacingVector(); // Fallback to facing direction if clicking on self

        return toTarget / distance;
    }

    /// <summary>
    /// Get the attack hitbox rectangle in front of the player.
    /// Used for melee weapon collision detection.
    /// </summary>
    /// <param name="hitboxWidth">Width of hitbox (default 48).</param>
    /// <param name="hitboxDepth">Depth/reach of hitbox (default 32).</param>
    /// <returns>Rectangle representing the attack area.</returns>
    public Rectangle GetAttackHitbox(int hitboxWidth = 0, int hitboxDepth = 0)
    {
        // Use defaults if not specified
        if (hitboxWidth <= 0) hitboxWidth = DefaultAttackHitboxWidth;
        if (hitboxDepth <= 0) hitboxDepth = DefaultAttackHitboxDepth;

        // Hitbox positioned in front of player based on facing direction
        int offsetX = 0, offsetY = 0;

        switch (Facing)
        {
            case Direction.Up:
                offsetX = -hitboxWidth / 2;
                offsetY = -Height - hitboxDepth;
                return new Rectangle(
                    (int)Position.X + offsetX,
                    (int)Position.Y + offsetY,
                    hitboxWidth,
                    hitboxDepth
                );

            case Direction.Down:
                offsetX = -hitboxWidth / 2;
                offsetY = 0;
                return new Rectangle(
                    (int)Position.X + offsetX,
                    (int)Position.Y + offsetY,
                    hitboxWidth,
                    hitboxDepth
                );

            case Direction.Left:
                offsetX = -Width / 2 - hitboxDepth;
                offsetY = -Height / 2 - hitboxWidth / 2;
                return new Rectangle(
                    (int)Position.X + offsetX,
                    (int)Position.Y + offsetY,
                    hitboxDepth,
                    hitboxWidth
                );

            case Direction.Right:
                offsetX = Width / 2;
                offsetY = -Height / 2 - hitboxWidth / 2;
                return new Rectangle(
                    (int)Position.X + offsetX,
                    (int)Position.Y + offsetY,
                    hitboxDepth,
                    hitboxWidth
                );

            default:
                return Rectangle.Empty;
        }
    }

    #endregion

    public Player(Vector2 startPosition)
    {
        Position = startPosition;
        CurrentStamina = MaxStamina; // Start fully rested
        CurrentHP = MaxHP; // Start at full health
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

        // Update combat timers (invincibility frames)
        UpdateCombatTimers(deltaTime);

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
