#nullable enable
using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MagicVille;

/// <summary>
/// Base enemy class for hostile creatures.
/// Enemies chase the player, deal contact damage, and can be killed with weapons.
///
/// BEHAVIOR:
/// - Idle until player within AggroRange
/// - Chase player when in range
/// - Deal contact damage on collision
/// - Take damage with knockback
/// - Drop loot on death
/// </summary>
public class Enemy : IRenderable
{
    /// <summary>Unique identifier for this enemy instance.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>World position (BOTTOM-CENTER / feet of the sprite).</summary>
    public Vector2 Position { get; set; }

    /// <summary>Current velocity (used for knockback).</summary>
    public Vector2 Velocity { get; set; } = Vector2.Zero;

    /// <summary>Visual width in pixels.</summary>
    public int Width { get; set; } = 40;

    /// <summary>Visual height in pixels.</summary>
    public int Height { get; set; } = 40;

    /// <summary>Enemy type name (e.g., "Goblin", "Slime").</summary>
    public string Name { get; set; } = "Enemy";

    /// <summary>Current hit points.</summary>
    public int HP { get; set; } = 3;

    /// <summary>Maximum hit points.</summary>
    public int MaxHP { get; set; } = 3;

    /// <summary>Movement speed in pixels per second.</summary>
    public float Speed { get; set; } = 50f;

    /// <summary>Distance at which enemy starts chasing player (in pixels).</summary>
    public float AggroRange { get; set; } = 150f;

    /// <summary>Contact damage dealt to player.</summary>
    public int ContactDamage { get; set; } = 1;

    /// <summary>Whether this enemy is dead and should be removed.</summary>
    public bool IsDead => HP <= 0;

    /// <summary>Whether the enemy was recently hit (for flash effect).</summary>
    public bool IsHit { get; private set; }

    /// <summary>Timer for hit flash effect.</summary>
    private float _hitFlashTimer;
    private const float HitFlashDuration = 0.15f;

    /// <summary>Knockback friction (velocity decay per second).</summary>
    private const float KnockbackFriction = 8f;

    /// <summary>Knockback strength multiplier.</summary>
    private const float KnockbackStrength = 300f;

    /// <summary>Cooldown between dealing contact damage (prevents rapid hits).</summary>
    private float _contactDamageCooldown;
    private const float ContactDamageCooldownTime = 1.0f;

    /// <summary>Base color for this enemy type.</summary>
    public Color BaseColor { get; set; } = new Color(200, 50, 50); // Red by default

    public Enemy() { }

    public Enemy(string name, Vector2 position, int hp = 3, float speed = 50f)
    {
        Name = name;
        Position = position;
        HP = hp;
        MaxHP = hp;
        Speed = speed;
    }

    /// <summary>
    /// Update enemy AI and physics.
    /// </summary>
    /// <param name="player">Reference to player for chasing.</param>
    /// <param name="deltaTime">Time since last frame.</param>
    /// <param name="canMove">Collision check function.</param>
    public void Update(Player player, float deltaTime, Func<Rectangle, bool>? canMove = null)
    {
        // Update timers
        if (_hitFlashTimer > 0)
        {
            _hitFlashTimer -= deltaTime;
            IsHit = _hitFlashTimer > 0;
        }

        if (_contactDamageCooldown > 0)
        {
            _contactDamageCooldown -= deltaTime;
        }

        // Apply knockback velocity with friction
        if (Velocity != Vector2.Zero)
        {
            Position += Velocity * deltaTime;
            Velocity *= MathF.Max(0, 1 - KnockbackFriction * deltaTime);

            // Stop when velocity is negligible
            if (Velocity.LengthSquared() < 1f)
                Velocity = Vector2.Zero;
        }

        // AI: Chase player if within aggro range
        float distanceToPlayer = Vector2.Distance(Center, player.Center);

        if (distanceToPlayer < AggroRange && Velocity == Vector2.Zero)
        {
            // Calculate direction to player
            Vector2 direction = player.Center - Center;
            if (direction != Vector2.Zero)
                direction.Normalize();

            // Move toward player
            Vector2 movement = direction * Speed * deltaTime;
            Vector2 newPosition = Position + movement;

            // Check collision before moving
            if (canMove != null)
            {
                var newBounds = GetCollisionBoundsAt(newPosition);
                if (canMove(newBounds))
                {
                    Position = newPosition;
                }
            }
            else
            {
                Position = newPosition;
            }
        }

        // Contact damage check
        if (_contactDamageCooldown <= 0 && BoundingBox.Intersects(player.CollisionBounds))
        {
            player.TakeDamage(ContactDamage, Position);
            _contactDamageCooldown = ContactDamageCooldownTime;
            Debug.WriteLine($"[Enemy] {Name} hit player for {ContactDamage} damage!");
        }
    }

    /// <summary>
    /// Take damage from an attack.
    /// </summary>
    /// <param name="damage">Amount of damage.</param>
    /// <param name="knockbackSource">Position of damage source (for knockback direction).</param>
    /// <returns>True if enemy died from this damage.</returns>
    public bool TakeDamage(int damage, Vector2 knockbackSource)
    {
        HP -= damage;
        IsHit = true;
        _hitFlashTimer = HitFlashDuration;

        // Calculate knockback direction (away from source)
        Vector2 knockbackDir = Center - knockbackSource;
        if (knockbackDir != Vector2.Zero)
            knockbackDir.Normalize();

        Velocity = knockbackDir * KnockbackStrength;

        Debug.WriteLine($"[Enemy] {Name} took {damage} damage! HP: {HP}/{MaxHP}");

        if (IsDead)
        {
            Debug.WriteLine($"[Enemy] {Name} defeated!");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Draw the enemy.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
    {
        // Color: White flash when hit, otherwise base color
        Color drawColor = IsHit ? Color.White : BaseColor;

        // Draw with bottom-center origin
        var origin = new Vector2(Width / 2f, Height);
        var destRect = new Rectangle(0, 0, Width, Height);

        spriteBatch.Draw(
            pixel,
            Position,
            destRect,
            drawColor,
            0f,
            origin,
            1f,
            SpriteEffects.None,
            0f
        );

        // Draw shadow/base
        var shadowRect = new Rectangle(
            (int)(Position.X - Width * 0.3f),
            (int)(Position.Y - 4),
            (int)(Width * 0.6f),
            4
        );
        spriteBatch.Draw(pixel, shadowRect, new Color(0, 0, 0, 80));
    }

    #region Bounds & Positioning

    /// <summary>Get the center position.</summary>
    public Vector2 Center => new(Position.X, Position.Y - Height / 2f);

    /// <summary>Y coordinate for depth sorting.</summary>
    public float SortY => Position.Y;

    /// <summary>
    /// Bounding box for collision (feet-only, ~30% of height).
    /// </summary>
    public Rectangle BoundingBox
    {
        get
        {
            int collisionHeight = (int)(Height * 0.3f);
            int collisionWidth = (int)(Width * 0.8f);
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
    /// </summary>
    public Rectangle GetCollisionBoundsAt(Vector2 position)
    {
        int collisionHeight = (int)(Height * 0.3f);
        int collisionWidth = (int)(Width * 0.8f);
        return new Rectangle(
            (int)(position.X - collisionWidth / 2f),
            (int)(position.Y - collisionHeight),
            collisionWidth,
            collisionHeight
        );
    }

    #endregion

    #region Factory Methods

    /// <summary>Create a Goblin enemy.</summary>
    public static Enemy CreateGoblin(Vector2 position)
    {
        return new Enemy("Goblin", position, hp: 3, speed: 60f)
        {
            Width = 36,
            Height = 40,
            BaseColor = new Color(50, 180, 50), // Green
            AggroRange = 150f,
            ContactDamage = 1
        };
    }

    /// <summary>Create a Slime enemy.</summary>
    public static Enemy CreateSlime(Vector2 position)
    {
        return new Enemy("Slime", position, hp: 2, speed: 40f)
        {
            Width = 32,
            Height = 28,
            BaseColor = new Color(100, 200, 100), // Light green
            AggroRange = 100f,
            ContactDamage = 1
        };
    }

    /// <summary>Create a Skeleton enemy (stronger).</summary>
    public static Enemy CreateSkeleton(Vector2 position)
    {
        return new Enemy("Skeleton", position, hp: 5, speed: 45f)
        {
            Width = 40,
            Height = 48,
            BaseColor = new Color(220, 220, 200), // Bone white
            AggroRange = 180f,
            ContactDamage = 2
        };
    }

    #endregion
}
