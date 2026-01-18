#nullable enable
using Microsoft.Xna.Framework;

namespace MagicVille;

/// <summary>
/// Defines a warp point that teleports the player to another location.
/// Trigger zone is checked against player collision bounds.
/// </summary>
public class Warp
{
    /// <summary>
    /// The area (in world pixels) that triggers this warp when the player steps on it.
    /// Typically covers one tile.
    /// </summary>
    public Rectangle TriggerZone { get; init; }

    /// <summary>
    /// The name of the destination location (must exist in WorldManager.Locations).
    /// </summary>
    public string TargetLocationName { get; init; } = "";

    /// <summary>
    /// The player's spawn position in the target location (world coordinates).
    /// Should be offset from the return warp to prevent infinite warp loops.
    /// </summary>
    public Vector2 TargetPlayerPosition { get; init; }

    public Warp() { }

    public Warp(Rectangle triggerZone, string targetLocation, Vector2 targetPosition)
    {
        TriggerZone = triggerZone;
        TargetLocationName = targetLocation;
        TargetPlayerPosition = targetPosition;
    }

    /// <summary>
    /// Create a warp from tile coordinates.
    /// </summary>
    /// <param name="tileX">Trigger tile X coordinate</param>
    /// <param name="tileY">Trigger tile Y coordinate</param>
    /// <param name="targetLocation">Destination location name</param>
    /// <param name="targetTileX">Spawn tile X in destination</param>
    /// <param name="targetTileY">Spawn tile Y in destination</param>
    public static Warp FromTile(int tileX, int tileY, string targetLocation, int targetTileX, int targetTileY)
    {
        int tileSize = GameLocation.TileSize;
        return new Warp
        {
            TriggerZone = new Rectangle(
                tileX * tileSize,
                tileY * tileSize,
                tileSize,
                tileSize
            ),
            TargetLocationName = targetLocation,
            // Spawn at center of target tile, player position is feet (bottom-center)
            TargetPlayerPosition = new Vector2(
                targetTileX * tileSize + tileSize / 2f,
                targetTileY * tileSize + tileSize / 2f
            )
        };
    }
}
