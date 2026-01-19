#nullable enable
using Microsoft.Xna.Framework;

namespace MagicVille;

/// <summary>
/// Growth stages for trees.
/// </summary>
public enum TreeStage
{
    Sapling = 0,
    Young = 1,
    Mature = 2,
    Stump = 3
}

/// <summary>
/// A tree that grows over multiple days. Can be chopped for wood.
/// Mature trees can be harvested; stumps can regrow or be removed.
/// </summary>
public class Tree : WorldObject
{
    /// <summary>Tree type identifier (e.g., "oak", "pine", "maple").</summary>
    public string TreeType { get; set; } = "oak";

    /// <summary>Current growth stage.</summary>
    public TreeStage Stage { get; set; } = TreeStage.Sapling;

    /// <summary>Days spent at current stage.</summary>
    public int DaysAtStage { get; set; }

    /// <summary>Days required for sapling to become young.</summary>
    public int DaysToYoung { get; set; } = 3;

    /// <summary>Days required for young to become mature.</summary>
    public int DaysToMature { get; set; } = 5;

    /// <summary>Days required for stump to regrow to sapling (0 = no regrow).</summary>
    public int DaysToRegrow { get; set; } = 7;

    public Tree() : base()
    {
        Name = "tree";
        UpdateVisuals();
    }

    public Tree(string treeType, Vector2 position, TreeStage initialStage = TreeStage.Sapling) : base()
    {
        TreeType = treeType;
        Position = position;
        Stage = initialStage;
        Name = "tree";
        UpdateVisuals();
    }

    /// <summary>
    /// Process daily growth. Called at start of new day.
    /// </summary>
    /// <param name="location">The location containing this tree (unused, trees don't check tiles).</param>
    /// <returns>True if tree should be removed (never, trees persist).</returns>
    public override bool OnNewDay(GameLocation? location = null)
    {
        DaysAtStage++;

        switch (Stage)
        {
            case TreeStage.Sapling:
                if (DaysAtStage >= DaysToYoung)
                {
                    Stage = TreeStage.Young;
                    DaysAtStage = 0;
                }
                break;

            case TreeStage.Young:
                if (DaysAtStage >= DaysToMature)
                {
                    Stage = TreeStage.Mature;
                    DaysAtStage = 0;
                }
                break;

            case TreeStage.Mature:
                // Mature trees stay mature
                break;

            case TreeStage.Stump:
                // Stumps can regrow into saplings
                if (DaysToRegrow > 0 && DaysAtStage >= DaysToRegrow)
                {
                    Stage = TreeStage.Sapling;
                    DaysAtStage = 0;
                }
                break;
        }

        UpdateVisuals();
        return false; // Trees never auto-remove
    }

    /// <summary>
    /// Chop the tree with an axe. Returns true if chopped successfully.
    /// Mature trees become stumps; stumps can be fully removed.
    /// </summary>
    /// <returns>True if tree should be removed (stump destroyed).</returns>
    public bool TryChop()
    {
        switch (Stage)
        {
            case TreeStage.Sapling:
            case TreeStage.Young:
                // Small trees are fully removed
                return true;

            case TreeStage.Mature:
                // Mature trees become stumps
                Stage = TreeStage.Stump;
                DaysAtStage = 0;
                UpdateVisuals();
                // TODO: Drop wood
                return false;

            case TreeStage.Stump:
                // Stumps are fully removed
                // TODO: Drop wood
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Update visual appearance based on stage.
    /// </summary>
    private void UpdateVisuals()
    {
        (Color, Width, Height, IsCollidable) = Stage switch
        {
            TreeStage.Sapling => (new Color(80, 140, 60), 24, 32, false),   // Small, no collision
            TreeStage.Young => (new Color(50, 120, 50), 48, 64, true),     // Medium
            TreeStage.Mature => (GetTreeColor(), 64, 96, true),             // Full size
            TreeStage.Stump => (new Color(100, 70, 40), 40, 24, true),     // Short stump
            _ => (Color.Green, 64, 96, true)
        };
    }

    /// <summary>
    /// Get the color based on tree type.
    /// </summary>
    private Color GetTreeColor() => TreeType switch
    {
        "oak" => new Color(34, 100, 34),        // Forest green
        "pine" => new Color(20, 80, 40),        // Dark green
        "maple" => new Color(50, 120, 50),      // Bright green
        "birch" => new Color(70, 140, 70),      // Light green
        _ => new Color(34, 100, 34)             // Default forest green
    };

    /// <summary>
    /// Create an oak sapling.
    /// </summary>
    public static Tree CreateOakSapling(Vector2 position)
    {
        return new Tree("oak", position, TreeStage.Sapling);
    }

    /// <summary>
    /// Create a mature oak tree (for initial world population).
    /// </summary>
    public static Tree CreateMatureOak(Vector2 position)
    {
        return new Tree("oak", position, TreeStage.Mature);
    }

    /// <summary>
    /// Create a pine sapling.
    /// </summary>
    public static Tree CreatePineSapling(Vector2 position)
    {
        return new Tree("pine", position, TreeStage.Sapling)
        {
            DaysToYoung = 4,
            DaysToMature = 6
        };
    }
}
