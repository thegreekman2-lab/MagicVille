#nullable enable
using Microsoft.Xna.Framework;

namespace MagicVille;

/// <summary>
/// A bed furniture piece. Player can interact to sleep, which:
/// 1. Saves the game
/// 2. Advances to the next day (fires OnDayChanged)
/// 3. Restores player energy (future)
/// </summary>
public class Bed : WorldObject
{
    /// <summary>Style of bed (affects color/appearance).</summary>
    public string BedStyle { get; set; } = "wooden";

    /// <summary>Whether the bed can be used (some beds might be decorative).</summary>
    public bool CanSleep { get; set; } = true;

    public Bed() : base()
    {
        Name = "bed";
        Width = 48;
        Height = 64;
        IsCollidable = true;
        UpdateVisuals();
    }

    public Bed(string bedStyle, Vector2 position) : base()
    {
        BedStyle = bedStyle;
        Position = position;
        Name = "bed";
        Width = 48;
        Height = 64;
        IsCollidable = true;
        UpdateVisuals();
    }

    /// <summary>
    /// Beds don't change on new day.
    /// </summary>
    /// <param name="location">The location containing this bed (unused).</param>
    public override bool OnNewDay(GameLocation? location = null)
    {
        return false;
    }

    /// <summary>
    /// Attempt to use the bed for sleeping.
    /// </summary>
    /// <returns>True if sleep was successful (bed is usable).</returns>
    public bool TrySleep()
    {
        return CanSleep;
    }

    /// <summary>
    /// Update visual appearance based on bed style.
    /// </summary>
    private void UpdateVisuals()
    {
        Color = BedStyle switch
        {
            "wooden" => new Color(139, 90, 60),       // Brown wood frame
            "fancy" => new Color(120, 60, 80),        // Dark red/mahogany
            "simple" => new Color(180, 160, 140),     // Light tan
            "royal" => new Color(80, 60, 140),        // Purple
            _ => new Color(139, 90, 60)               // Default wooden
        };
    }

    /// <summary>
    /// Create a simple wooden bed.
    /// </summary>
    public static Bed CreateWoodenBed(Vector2 position)
    {
        return new Bed("wooden", position);
    }

    /// <summary>
    /// Create a fancy bed.
    /// </summary>
    public static Bed CreateFancyBed(Vector2 position)
    {
        return new Bed("fancy", position);
    }
}
