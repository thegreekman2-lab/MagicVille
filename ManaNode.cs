#nullable enable
using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MagicVille;

/// <summary>
/// A magical mana crystal node. Can be harvested for mana, then recharges over days.
/// </summary>
public class ManaNode : WorldObject
{
    /// <summary>Type of mana crystal (affects color and yield).</summary>
    public string CrystalType { get; set; } = "arcane";

    /// <summary>Current charge level (0 = depleted, MaxCharge = fully charged).</summary>
    public int CurrentCharge { get; set; }

    /// <summary>Maximum charge capacity.</summary>
    public int MaxCharge { get; set; } = 3;

    /// <summary>Amount of charge restored per day.</summary>
    public int RechargePerDay { get; set; } = 1;

    /// <summary>Whether this node is depleted (no charge).</summary>
    public bool IsDepleted => CurrentCharge <= 0;

    /// <summary>Whether this node is fully charged.</summary>
    public bool IsFullyCharged => CurrentCharge >= MaxCharge;

    public ManaNode() : base()
    {
        Name = "mana_node";
        Width = 40;
        Height = 48;
        CurrentCharge = MaxCharge;
        UpdateVisuals();
    }

    public ManaNode(string crystalType, Vector2 position) : base()
    {
        CrystalType = crystalType;
        Position = position;
        Name = "mana_node";
        Width = 40;
        Height = 48;
        CurrentCharge = MaxCharge;
        UpdateVisuals();
    }

    /// <summary>
    /// Process daily recharge. Called at start of new day.
    /// </summary>
    /// <param name="location">The location containing this node (unused, mana nodes don't check tiles).</param>
    /// <returns>False - mana nodes never auto-remove.</returns>
    public override bool OnNewDay(GameLocation? location = null)
    {
        int previousCharge = CurrentCharge;

        if (CurrentCharge < MaxCharge)
        {
            CurrentCharge += RechargePerDay;
            if (CurrentCharge > MaxCharge)
                CurrentCharge = MaxCharge;

            Debug.WriteLine($"[ManaNode] {CrystalType} recharged: {previousCharge} -> {CurrentCharge}/{MaxCharge}");
        }
        else
        {
            Debug.WriteLine($"[ManaNode] {CrystalType} already full: {CurrentCharge}/{MaxCharge}");
        }

        UpdateVisuals();
        return false; // Mana nodes never auto-remove
    }

    /// <summary>
    /// Harvest mana from this node.
    /// </summary>
    /// <returns>Amount of mana harvested (0 if depleted).</returns>
    public int Harvest()
    {
        if (IsDepleted)
            return 0;

        int harvested = CurrentCharge;
        CurrentCharge = 0;
        UpdateVisuals();
        return harvested;
    }

    /// <summary>
    /// Attempt to harvest with a pickaxe. Returns mana amount.
    /// </summary>
    /// <returns>Mana amount harvested.</returns>
    public int TryHarvest()
    {
        return Harvest();
    }

    /// <summary>
    /// Update visual appearance based on charge level.
    /// </summary>
    private void UpdateVisuals()
    {
        // Brightness based on charge percentage
        float chargePercent = MaxCharge > 0 ? (float)CurrentCharge / MaxCharge : 0f;
        Color baseColor = GetBaseColor();

        if (IsDepleted)
        {
            // Depleted: dark, desaturated version
            Color = new Color(
                (int)(baseColor.R * 0.3f),
                (int)(baseColor.G * 0.3f),
                (int)(baseColor.B * 0.3f)
            );
        }
        else
        {
            // Lerp between dim (30%) and full brightness based on charge
            float brightness = 0.3f + (chargePercent * 0.7f);
            Color = new Color(
                (int)(baseColor.R * brightness),
                (int)(baseColor.G * brightness),
                (int)(baseColor.B * brightness)
            );
        }
    }

    /// <summary>
    /// Get the base color for this crystal type.
    /// </summary>
    private Color GetBaseColor() => CrystalType switch
    {
        "arcane" => new Color(80, 150, 255),    // Blue
        "fire" => new Color(255, 100, 50),      // Orange-red
        "nature" => new Color(80, 220, 100),    // Green
        "void" => new Color(150, 50, 200),      // Purple
        "lightning" => new Color(255, 255, 100), // Yellow
        _ => new Color(80, 150, 255)            // Default blue
    };

    /// <summary>
    /// Override draw to add a glow effect when charged.
    /// </summary>
    public new void Draw(SpriteBatch spriteBatch, Texture2D pixel)
    {
        // Draw base object
        base.Draw(spriteBatch, pixel);

        // Add glow effect if charged
        if (!IsDepleted)
        {
            float glowIntensity = (float)CurrentCharge / MaxCharge * 0.3f;
            var glowColor = new Color(
                Color.R,
                Color.G,
                Color.B,
                (int)(glowIntensity * 100)
            );

            // Draw slightly larger glow rectangle behind
            var glowRect = new Rectangle(
                (int)(Position.X - Width / 2f - 4),
                (int)(Position.Y - Height - 4),
                Width + 8,
                Height + 8
            );
            spriteBatch.Draw(pixel, glowRect, glowColor);
        }
    }

    /// <summary>
    /// Create an arcane (blue) mana crystal.
    /// </summary>
    public static ManaNode CreateArcaneCrystal(Vector2 position)
    {
        return new ManaNode("arcane", position)
        {
            MaxCharge = 3,
            RechargePerDay = 1
        };
    }

    /// <summary>
    /// Create a fire (red) mana crystal.
    /// </summary>
    public static ManaNode CreateFireCrystal(Vector2 position)
    {
        return new ManaNode("fire", position)
        {
            MaxCharge = 2,
            RechargePerDay = 1
        };
    }

    /// <summary>
    /// Create a nature (green) mana crystal.
    /// </summary>
    public static ManaNode CreateNatureCrystal(Vector2 position)
    {
        return new ManaNode("nature", position)
        {
            MaxCharge = 4,
            RechargePerDay = 2
        };
    }
}
