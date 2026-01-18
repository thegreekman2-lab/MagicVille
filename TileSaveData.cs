#nullable enable
using Microsoft.Xna.Framework;

namespace MagicVille;

/// <summary>
/// Serializable data for a single modified tile.
/// Only non-default tiles are saved to minimize file size.
/// </summary>
public class TileSaveData
{
    /// <summary>Tile X coordinate.</summary>
    public int X { get; set; }

    /// <summary>Tile Y coordinate.</summary>
    public int Y { get; set; }

    /// <summary>Tile type ID (matches Tile.Id).</summary>
    public int TileId { get; set; }

    public TileSaveData() { }

    public TileSaveData(int x, int y, int tileId)
    {
        X = x;
        Y = y;
        TileId = tileId;
    }

    public TileSaveData(Point position, Tile tile)
    {
        X = position.X;
        Y = position.Y;
        TileId = tile.Id;
    }
}
