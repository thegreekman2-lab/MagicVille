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

    // Hardcoded tile types for V0
    public static readonly Tile Grass = new(0, true);
    public static readonly Tile Dirt = new(1, true);
    public static readonly Tile Water = new(2, false);
}
