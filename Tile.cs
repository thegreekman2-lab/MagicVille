namespace MagicVille;

public readonly struct Tile
{
    public int Id { get; }
    public bool Walkable { get; }

    public Tile(int id, bool walkable)
    {
        Id = id;
        Walkable = walkable;
    }

    // Tile type definitions
    public static readonly Tile Grass = new(0, true);
    public static readonly Tile Dirt = new(1, true);
    public static readonly Tile Water = new(2, false);
    public static readonly Tile Stone = new(3, true);      // Breakable with pickaxe
    public static readonly Tile WetDirt = new(4, true);    // Watered farmland
    public static readonly Tile Tilled = new(5, true);     // Tilled soil (hoed)
}
