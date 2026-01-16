using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MagicVille;

public class GameLocation
{
    public const int TileSize = 64;

    public int Width { get; }
    public int Height { get; }
    public Tile[,] Tiles { get; }

    public GameLocation(int width, int height)
    {
        Width = width;
        Height = height;
        Tiles = new Tile[width, height];
    }

    public Tile GetTile(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return Tile.Water; // Out of bounds = impassable
        return Tiles[x, y];
    }

    public void SetTile(int x, int y, Tile tile)
    {
        if (x >= 0 && x < Width && y >= 0 && y < Height)
            Tiles[x, y] = tile;
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, Camera2D camera)
    {
        // Calculate visible tile range based on camera
        var viewBounds = camera.GetVisibleBounds();
        int startX = Math.Max(0, (int)(viewBounds.Left / TileSize));
        int startY = Math.Max(0, (int)(viewBounds.Top / TileSize));
        int endX = Math.Min(Width, (int)(viewBounds.Right / TileSize) + 1);
        int endY = Math.Min(Height, (int)(viewBounds.Bottom / TileSize) + 1);

        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                var tile = Tiles[x, y];
                var color = GetTileColor(tile.Id);
                var rect = new Rectangle(x * TileSize, y * TileSize, TileSize, TileSize);
                spriteBatch.Draw(pixel, rect, color);
            }
        }
    }

    private static Color GetTileColor(int tileId) => tileId switch
    {
        0 => new Color(34, 139, 34),   // Grass - forest green
        1 => new Color(139, 90, 43),   // Dirt - brown
        2 => new Color(30, 144, 255),  // Water - blue
        _ => Color.Magenta             // Unknown - debug pink
    };

    /// <summary>
    /// Creates a 20x20 test map: grass with a vertical dirt path down the center.
    /// </summary>
    public static GameLocation CreateTestMap()
    {
        var location = new GameLocation(20, 20);

        // Fill with grass
        for (int y = 0; y < location.Height; y++)
        {
            for (int x = 0; x < location.Width; x++)
            {
                location.Tiles[x, y] = Tile.Grass;
            }
        }

        // Add a dirt path down the center (columns 9 and 10)
        for (int y = 0; y < location.Height; y++)
        {
            location.Tiles[9, y] = Tile.Dirt;
            location.Tiles[10, y] = Tile.Dirt;
        }

        // Add a horizontal dirt path across the middle
        for (int x = 0; x < location.Width; x++)
        {
            location.Tiles[x, 9] = Tile.Dirt;
            location.Tiles[x, 10] = Tile.Dirt;
        }

        // Add some water in the corner
        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                location.Tiles[x, y] = Tile.Water;
            }
        }

        return location;
    }
}
