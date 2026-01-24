namespace MagicVille;

/// <summary>
/// Attack style determines how a weapon deals damage.
/// </summary>
public enum AttackStyle
{
    /// <summary>No attack capability (farming tools).</summary>
    None,

    /// <summary>Melee attack - creates hitbox in facing direction.</summary>
    Melee,

    /// <summary>Projectile attack - spawns a moving projectile.</summary>
    Projectile,

    /// <summary>Raycast attack - instant hit along a line (lightning, beam).</summary>
    Raycast
}

/// <summary>
/// Tools for farming, mining, combat, and magic.
/// Non-stackable items with resource cost and power level.
/// </summary>
public class Tool : Item
{
    public override bool IsStackable => false;
    public override string RegistryKey { get; init; } = "";

    #region Farming Properties

    /// <summary>Resource cost per use (mana or hybrid - legacy).</summary>
    public float ResourceCost { get; init; }

    /// <summary>Effectiveness level (upgradeable).</summary>
    public int PowerLevel { get; set; }

    /// <summary>
    /// Stamina (energy) cost per use.
    /// Standard costs: Watering Can = 2, Hoe = 3, Pickaxe/Axe = 4, Scythe = 0.
    /// </summary>
    public float StaminaCost { get; init; } = 0f;

    /// <summary>
    /// Whether this tool affects the underlying tile even when an object is present.
    /// True = tool effect "passes through" objects to also affect the tile.
    /// False = tool effect stops at the object (default behavior).
    ///
    /// Examples:
    /// - Watering Can (true): Waters crop AND wets the soil underneath
    /// - Pickaxe (false): Breaks rock but doesn't affect tile under it
    /// - Sprinkler (true): Future items can inherit this behavior
    /// </summary>
    public bool AffectsTileThroughObjects { get; init; } = false;

    #endregion

    #region Combat Properties

    /// <summary>
    /// Whether this tool is a weapon (used for combat).
    /// Weapons use the attack system based on AttackStyle.
    /// </summary>
    public bool IsWeapon { get; init; } = false;

    /// <summary>
    /// The attack style for this weapon.
    /// Determines how damage is dealt (Melee hitbox, Projectile, or Raycast).
    /// </summary>
    public AttackStyle Style { get; init; } = AttackStyle.None;

    /// <summary>
    /// Base damage dealt by this weapon.
    /// </summary>
    public int Damage { get; init; } = 1;

    /// <summary>
    /// Attack range in pixels.
    /// - Melee: Hitbox reach distance from player.
    /// - Raycast: Maximum ray distance.
    /// - Projectile: Not used (projectiles travel until collision).
    /// </summary>
    public float Range { get; init; } = 48f;

    /// <summary>
    /// Projectile speed in pixels per second.
    /// Only used for Projectile style weapons.
    /// </summary>
    public float ProjectileSpeed { get; init; } = 300f;

    /// <summary>
    /// Time in seconds between attacks (attack cooldown).
    /// Prevents attack spamming.
    /// </summary>
    public float Cooldown { get; init; } = 0.3f;

    /// <summary>
    /// Color tint for projectiles spawned by this weapon.
    /// </summary>
    public uint ProjectileColorPacked { get; init; } = 0xFFFF8800; // Orange default

    /// <summary>
    /// Size of hitbox for melee attacks (width x height in pixels).
    /// </summary>
    public int HitboxWidth { get; init; } = 48;
    public int HitboxHeight { get; init; } = 32;

    // Legacy alias for backwards compatibility
    public int AttackDamage
    {
        get => Damage;
        init => Damage = value;
    }

    #endregion

    // Parameterless constructor for JSON deserialization
    public Tool() { }

    /// <summary>
    /// Create a farming tool (non-weapon).
    /// </summary>
    public Tool(string registryKey, string name, string description, float resourceCost, int powerLevel, bool affectsTileThroughObjects = false, float staminaCost = 0f)
        : base(name, description, sellPrice: -1) // Tools are never sellable
    {
        RegistryKey = registryKey;
        ResourceCost = resourceCost;
        PowerLevel = powerLevel;
        AffectsTileThroughObjects = affectsTileThroughObjects;
        StaminaCost = staminaCost;
    }

    #region Factory Methods

    /// <summary>
    /// Create a melee weapon (sword, axe-type).
    /// </summary>
    public static Tool CreateMeleeWeapon(
        string registryKey,
        string name,
        string description,
        int damage,
        float range = 48f,
        float cooldown = 0.3f,
        float staminaCost = 2f,
        int hitboxWidth = 48,
        int hitboxHeight = 32)
    {
        return new Tool
        {
            RegistryKey = registryKey,
            Name = name,
            Description = description,
            IsWeapon = true,
            Style = AttackStyle.Melee,
            Damage = damage,
            Range = range,
            Cooldown = cooldown,
            StaminaCost = staminaCost,
            HitboxWidth = hitboxWidth,
            HitboxHeight = hitboxHeight
        };
    }

    /// <summary>
    /// Create a projectile weapon (wand, bow).
    /// </summary>
    public static Tool CreateProjectileWeapon(
        string registryKey,
        string name,
        string description,
        int damage,
        float projectileSpeed = 300f,
        float cooldown = 0.5f,
        float staminaCost = 3f,
        uint projectileColor = 0xFFFF8800) // Orange
    {
        return new Tool
        {
            RegistryKey = registryKey,
            Name = name,
            Description = description,
            IsWeapon = true,
            Style = AttackStyle.Projectile,
            Damage = damage,
            ProjectileSpeed = projectileSpeed,
            Cooldown = cooldown,
            StaminaCost = staminaCost,
            ProjectileColorPacked = projectileColor
        };
    }

    /// <summary>
    /// Create a raycast weapon (staff, lightning rod).
    /// </summary>
    public static Tool CreateRaycastWeapon(
        string registryKey,
        string name,
        string description,
        int damage,
        float range = 200f,
        float cooldown = 0.8f,
        float staminaCost = 5f)
    {
        return new Tool
        {
            RegistryKey = registryKey,
            Name = name,
            Description = description,
            IsWeapon = true,
            Style = AttackStyle.Raycast,
            Damage = damage,
            Range = range,
            Cooldown = cooldown,
            StaminaCost = staminaCost
        };
    }

    #endregion
}
