using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MagicVille;

public class GameLocation
{
    public const int TileSize = 64;

    /// <summary>Unique name for this location (e.g., "Farm", "Cabin").</summary>
    public string Name { get; init; } = "";

    public int Width { get; }
    public int Height { get; }
    public Tile[,] Tiles { get; }

    /// <summary>Warp points in this location that lead to other locations.</summary>
    public List<Warp> Warps { get; } = new();

    public GameLocation(int width, int height)
    {
        Width = width;
        Height = height;
        Tiles = new Tile[width, height];
    }

    public GameLocation(string name, int width, int height) : this(width, height)
    {
        Name = name;
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
        3 => new Color(128, 128, 128), // Stone - gray
        4 => new Color(90, 60, 30),    // WetDirt - dark brown
        5 => new Color(160, 110, 60),  // Tilled - light brown with furrows
        6 => new Color(139, 90, 60),   // Wood - wood floor
        7 => new Color(80, 80, 90),    // Wall - dark gray stone wall
        _ => Color.Magenta             // Unknown - debug pink
    };

    /// <summary>
    /// Creates a random test map sized for 1920x1080 (30x17 tiles).
    /// Uses seed for deterministic generation (same seed = same map).
    /// </summary>
    public static GameLocation CreateTestMap(int seed = 0)
    {
        // 1920x1080 at 64px tiles = 30x17
        int width = 30;
        int height = 17;
        var location = new GameLocation(width, height);
        var random = seed == 0 ? new Random() : new Random(seed);

        // Fill with grass
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                location.Tiles[x, y] = Tile.Grass;
            }
        }

        // Add random dirt patches (15-20 patches)
        int dirtPatches = random.Next(15, 21);
        for (int i = 0; i < dirtPatches; i++)
        {
            int px = random.Next(width);
            int py = random.Next(height);
            int patchSize = random.Next(1, 4);

            for (int dy = 0; dy < patchSize; dy++)
            {
                for (int dx = 0; dx < patchSize; dx++)
                {
                    int tx = px + dx;
                    int ty = py + dy;
                    if (tx < width && ty < height)
                        location.Tiles[tx, ty] = Tile.Dirt;
                }
            }
        }

        // Add random stone clusters (8-12 clusters)
        int stoneClusters = random.Next(8, 13);
        for (int i = 0; i < stoneClusters; i++)
        {
            int sx = random.Next(width);
            int sy = random.Next(height);
            int clusterSize = random.Next(1, 3);

            for (int dy = 0; dy < clusterSize; dy++)
            {
                for (int dx = 0; dx < clusterSize; dx++)
                {
                    int tx = sx + dx;
                    int ty = sy + dy;
                    if (tx < width && ty < height)
                        location.Tiles[tx, ty] = Tile.Stone;
                }
            }
        }

        // Add a pond (water) in a random corner
        int cornerChoice = random.Next(4);
        int waterX = (cornerChoice % 2 == 0) ? 0 : width - 5;
        int waterY = (cornerChoice < 2) ? 0 : height - 4;

        for (int dy = 0; dy < 4; dy++)
        {
            for (int dx = 0; dx < 5; dx++)
            {
                int tx = waterX + dx;
                int ty = waterY + dy;
                if (tx >= 0 && tx < width && ty >= 0 && ty < height)
                    location.Tiles[tx, ty] = Tile.Water;
            }
        }

        // Add a dirt path from left to right
        int pathY = height / 2;
        for (int x = 0; x < width; x++)
        {
            location.Tiles[x, pathY] = Tile.Dirt;
            if (pathY + 1 < height)
                location.Tiles[x, pathY + 1] = Tile.Dirt;
        }

        // Add a vertical path crossing
        int pathX = width / 2;
        for (int y = 0; y < height; y++)
        {
            location.Tiles[pathX, y] = Tile.Dirt;
            if (pathX + 1 < width)
                location.Tiles[pathX + 1, y] = Tile.Dirt;
        }

        return location;
    }

    /// <summary>
    /// Creates the Farm location (20x20 grass with features).
    /// Includes a warp to the Cabin at tile (19, 10).
    /// </summary>
    public static GameLocation CreateFarm(int seed = 0)
    {
        int width = 20;
        int height = 20;
        var location = new GameLocation("Farm", width, height);
        var random = seed == 0 ? new Random() : new Random(seed);

        // Fill with grass
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                location.Tiles[x, y] = Tile.Grass;
            }
        }

        // Add some dirt patches
        int dirtPatches = random.Next(8, 12);
        for (int i = 0; i < dirtPatches; i++)
        {
            int px = random.Next(width - 3);
            int py = random.Next(height - 3);
            int patchSize = random.Next(2, 4);

            for (int dy = 0; dy < patchSize; dy++)
            {
                for (int dx = 0; dx < patchSize; dx++)
                {
                    int tx = px + dx;
                    int ty = py + dy;
                    if (tx < width && ty < height)
                        location.Tiles[tx, ty] = Tile.Dirt;
                }
            }
        }

        // Add a small pond in bottom-left
        for (int y = height - 4; y < height; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                location.Tiles[x, y] = Tile.Water;
            }
        }

        // Mark the cabin entrance area (stone path to door)
        for (int x = 17; x < width; x++)
        {
            location.Tiles[x, 10] = Tile.Stone;
        }

        // Add warp to Cabin at rightmost edge, middle height
        // Player spawns at (5, 8) in Cabin - one tile away from return warp
        location.Warps.Add(Warp.FromTile(19, 10, "Cabin", 5, 8));

        return location;
    }

    /// <summary>
    /// Creates the Cabin location (10x10 wood floor interior).
    /// Includes a warp back to the Farm at tile (5, 9).
    /// </summary>
    public static GameLocation CreateCabin()
    {
        int width = 10;
        int height = 10;
        var location = new GameLocation("Cabin", width, height);

        // Fill with wood floor
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                location.Tiles[x, y] = Tile.Wood;
            }
        }

        // Add walls around the edges (impassable border)
        for (int x = 0; x < width; x++)
        {
            location.Tiles[x, 0] = Tile.Wall;           // Top wall
            location.Tiles[x, height - 1] = Tile.Wall;  // Bottom wall
        }
        for (int y = 0; y < height; y++)
        {
            location.Tiles[0, y] = Tile.Wall;           // Left wall
            location.Tiles[width - 1, y] = Tile.Wall;   // Right wall
        }

        // Create doorway at bottom center (remove stone for walkable exit)
        location.Tiles[5, height - 1] = Tile.Wood;

        // Add warp to Farm at the doorway
        // Player spawns at (18, 10) in Farm - one tile away from cabin entrance
        location.Warps.Add(Warp.FromTile(5, 9, "Farm", 18, 10));

        return location;
    }
}
