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
        4 => new Color(100, 100, 140), // WetDirt - blue-gray tint (clearly wet)
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
    /// Creates the Farm location (50x100 fixed layout for controlled testing).
    /// Replaces random generation with a deterministic map.
    ///
    /// LAYOUT (North = Safe Zone, South = Danger Zone):
    /// - NORTH ZONE (Y 0-49): Safe farming area
    ///   - Lawn (0-29, 0-49): Safe grass area for zero-cost testing
    ///   - Garden (15-25, 10-20): Tillable dirt plot
    ///   - Forest (30+, 0-49): Trees and rocks for stamina testing
    ///   - Farmhouse area around (10, 10): Home base with ShippingBin, Sign
    ///   - Pond: Near bottom-left of north zone
    ///   - Cabin entrance: Right edge at Y=25
    ///
    /// - THE DIVIDER (Y=50): Water barrier with bridge gap in middle
    ///
    /// - SOUTH ZONE (Y 51-99): Danger Zone with enemies
    ///   - Sparse grass with rocky terrain
    ///   - Enemies spawn here
    /// </summary>
    public static GameLocation CreateFarm(int seed = 0)
    {
        int width = 50;
        int height = 100; // Expanded to 100 tiles tall

        var location = new GameLocation("Farm", width, height);

        // ═══════════════════════════════════════════════════════════════════
        // STEP 1: Fill entire map with grass (base layer)
        // ═══════════════════════════════════════════════════════════════════
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                location.Tiles[x, y] = Tile.Grass;
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // STEP 2: NORTH ZONE - Safe farming area (Y 0-49)
        // ═══════════════════════════════════════════════════════════════════

        // The Garden - tillable dirt plot (15-25, 10-20)
        for (int y = 10; y <= 20; y++)
        {
            for (int x = 15; x <= 25; x++)
            {
                location.Tiles[x, y] = Tile.Dirt;
            }
        }

        // Small pond in north zone (0-5, 40-45)
        for (int y = 40; y < 46; y++)
        {
            for (int x = 0; x < 6; x++)
            {
                location.Tiles[x, y] = Tile.Water;
            }
        }

        // Stone path to cabin entrance (45-49, 25)
        for (int x = 45; x < 50; x++)
        {
            location.Tiles[x, 25] = Tile.Stone;
        }

        // ═══════════════════════════════════════════════════════════════════
        // STEP 3: THE DIVIDER - Water barrier at Y=50 with bridge gap
        // ═══════════════════════════════════════════════════════════════════
        for (int x = 0; x < width; x++)
        {
            // Bridge gap in the middle (X 23-26)
            if (x >= 23 && x <= 26)
            {
                location.Tiles[x, 50] = Tile.Stone; // Bridge
            }
            else
            {
                location.Tiles[x, 50] = Tile.Water; // Water barrier
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // STEP 4: SOUTH ZONE - Danger Zone (Y 51-99)
        // ═══════════════════════════════════════════════════════════════════

        // Scatter some stone patches in the danger zone for atmosphere
        var random = seed == 0 ? new Random() : new Random(seed);
        for (int i = 0; i < 20; i++)
        {
            int patchX = random.Next(width);
            int patchY = 55 + random.Next(40); // Y 55-94
            int patchSize = random.Next(1, 3);

            for (int dy = 0; dy < patchSize; dy++)
            {
                for (int dx = 0; dx < patchSize; dx++)
                {
                    int tx = patchX + dx;
                    int ty = patchY + dy;
                    if (tx < width && ty < height)
                        location.Tiles[tx, ty] = Tile.Stone;
                }
            }
        }

        // Dirt paths in danger zone
        for (int y = 51; y < 60; y++)
        {
            location.Tiles[24, y] = Tile.Dirt; // Path from bridge
            location.Tiles[25, y] = Tile.Dirt;
        }

        // ═══════════════════════════════════════════════════════════════════
        // STEP 5: Add warp to Cabin at rightmost edge
        // ═══════════════════════════════════════════════════════════════════
        location.Warps.Add(Warp.FromTile(49, 25, "Cabin", 5, 8));

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
