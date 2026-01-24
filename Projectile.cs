#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MagicVille;

/// <summary>
/// A moving projectile that can damage enemies on collision.
///
/// LIFECYCLE:
/// 1. Spawned by Player attack (Projectile style weapon)
/// 2. Moves each frame by Velocity
/// 3. Checks collisions: Walls (destroy), Enemies (damage + destroy)
/// 4. Removed when IsActive = false or MaxLifetime exceeded
///
/// PHYSICS:
/// - Simple linear motion (no gravity for magic projectiles)
/// - Instant destruction on collision (no bouncing/piercing by default)
/// </summary>
public class Projectile : IRenderable
{
    /// <summary>Unique identifier for this projectile.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>World position (CENTER of the projectile).</summary>
    public Vector2 Position { get; set; }

    /// <summary>Movement velocity in pixels per second.</summary>
    public Vector2 Velocity { get; set; }

    /// <summary>Damage dealt on enemy hit.</summary>
    public int Damage { get; set; } = 1;

    /// <summary>Visual size in pixels.</summary>
    public int Size { get; set; } = 12;

    /// <summary>Whether this projectile is still active (alive).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Projectile color for rendering.</summary>
    public Color Color { get; set; } = new Color(255, 136, 0); // Orange

    /// <summary>Time alive in seconds (for lifetime limit).</summary>
    public float TimeAlive { get; private set; }

    /// <summary>Maximum lifetime in seconds before auto-destroy.</summary>
    public float MaxLifetime { get; set; } = 5f;

    /// <summary>Owner reference to prevent self-damage (future: enemy projectiles).</summary>
    public string OwnerId { get; set; } = "player";

    /// <summary>Whether this projectile pierces through enemies (hits multiple).</summary>
    public bool IsPiercing { get; set; } = false;

    /// <summary>Trail particles for visual effect (recent positions).</summary>
    private readonly Queue<Vector2> _trailPositions = new();
    private const int MaxTrailLength = 8;
    private float _trailTimer;
    private const float TrailInterval = 0.02f;

    public Projectile() { }

    public Projectile(Vector2 position, Vector2 velocity, int damage, Color color)
    {
        Position = position;
        Velocity = velocity;
        Damage = damage;
        Color = color;
    }

    /// <summary>
    /// Update projectile position and check for collisions.
    /// </summary>
    /// <param name="deltaTime">Time since last frame.</param>
    /// <param name="enemies">List of enemies to check collision against.</param>
    /// <param name="isTileSolid">Function to check if a world position hits a solid tile.</param>
    /// <returns>True if projectile should be removed.</returns>
    public bool Update(float deltaTime, List<Enemy> enemies, Func<Vector2, bool> isTileSolid)
    {
        if (!IsActive)
            return true;

        // Update lifetime
        TimeAlive += deltaTime;
        if (TimeAlive >= MaxLifetime)
        {
            IsActive = false;
            Debug.WriteLine($"[Projectile] Expired after {MaxLifetime}s");
            return true;
        }

        // Store position for trail
        _trailTimer += deltaTime;
        if (_trailTimer >= TrailInterval)
        {
            _trailTimer = 0;
            _trailPositions.Enqueue(Position);
            while (_trailPositions.Count > MaxTrailLength)
                _trailPositions.Dequeue();
        }

        // Move projectile
        Position += Velocity * deltaTime;

        // Check wall collision
        if (isTileSolid(Position))
        {
            IsActive = false;
            Debug.WriteLine($"[Projectile] Hit wall at ({Position.X:F0}, {Position.Y:F0})");
            return true;
        }

        // Check enemy collision
        var hitbox = GetBoundingBox();
        foreach (var enemy in enemies)
        {
            if (enemy.IsDead)
                continue;

            if (hitbox.Intersects(enemy.BoundingBox))
            {
                // Deal damage with knockback from projectile direction
                enemy.TakeDamage(Damage, Position - Velocity * 0.1f);
                Debug.WriteLine($"[Projectile] Hit {enemy.Name} for {Damage} damage!");

                if (!IsPiercing)
                {
                    IsActive = false;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Draw the projectile with trail effect.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
    {
        if (!IsActive)
            return;

        // Draw trail (fading circles)
        int trailIndex = 0;
        foreach (var trailPos in _trailPositions)
        {
            float alpha = (float)trailIndex / MaxTrailLength * 0.5f;
            int trailSize = (int)(Size * 0.6f * ((float)trailIndex / MaxTrailLength));
            var trailColor = Color * alpha;

            var trailRect = new Rectangle(
                (int)(trailPos.X - trailSize / 2f),
                (int)(trailPos.Y - trailSize / 2f),
                trailSize,
                trailSize
            );
            spriteBatch.Draw(pixel, trailRect, trailColor);
            trailIndex++;
        }

        // Draw main projectile (bright center)
        var mainRect = new Rectangle(
            (int)(Position.X - Size / 2f),
            (int)(Position.Y - Size / 2f),
            Size,
            Size
        );
        spriteBatch.Draw(pixel, mainRect, Color);

        // Draw bright core
        int coreSize = Size / 2;
        var coreRect = new Rectangle(
            (int)(Position.X - coreSize / 2f),
            (int)(Position.Y - coreSize / 2f),
            coreSize,
            coreSize
        );
        spriteBatch.Draw(pixel, coreRect, Color.White);
    }

    /// <summary>Get bounding box for collision detection.</summary>
    public Rectangle GetBoundingBox()
    {
        return new Rectangle(
            (int)(Position.X - Size / 2f),
            (int)(Position.Y - Size / 2f),
            Size,
            Size
        );
    }

    /// <summary>Y coordinate for depth sorting (same as position).</summary>
    public float SortY => Position.Y;

    #region Factory Methods

    /// <summary>Create a fireball projectile.</summary>
    public static Projectile CreateFireball(Vector2 position, Vector2 direction, int damage, float speed = 300f)
    {
        Vector2 normalizedDir = direction;
        if (normalizedDir != Vector2.Zero)
            normalizedDir.Normalize();

        return new Projectile
        {
            Position = position,
            Velocity = normalizedDir * speed,
            Damage = damage,
            Size = 14,
            Color = new Color(255, 100, 0), // Orange-red
            MaxLifetime = 3f
        };
    }

    /// <summary>Create an ice bolt projectile (slower, more damage).</summary>
    public static Projectile CreateIceBolt(Vector2 position, Vector2 direction, int damage, float speed = 200f)
    {
        Vector2 normalizedDir = direction;
        if (normalizedDir != Vector2.Zero)
            normalizedDir.Normalize();

        return new Projectile
        {
            Position = position,
            Velocity = normalizedDir * speed,
            Damage = damage,
            Size = 10,
            Color = new Color(100, 200, 255), // Ice blue
            MaxLifetime = 4f
        };
    }

    /// <summary>Create an arcane missile (fast, small).</summary>
    public static Projectile CreateArcaneMissile(Vector2 position, Vector2 direction, int damage, float speed = 400f)
    {
        Vector2 normalizedDir = direction;
        if (normalizedDir != Vector2.Zero)
            normalizedDir.Normalize();

        return new Projectile
        {
            Position = position,
            Velocity = normalizedDir * speed,
            Damage = damage,
            Size = 8,
            Color = new Color(200, 100, 255), // Purple
            MaxLifetime = 2f
        };
    }

    #endregion
}
