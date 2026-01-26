#nullable enable
using System;
using System.Diagnostics;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;

namespace MagicVille;

/// <summary>
/// Player inventory with hotbar functionality.
/// Fixed 10-slot hotbar with slot selection via scroll/number keys.
/// </summary>
public class Inventory
{
    public const int HotbarSize = 10;

    /// <summary>Hotbar slots (fixed size array, null = empty slot).</summary>
    private readonly Item?[] _slots = new Item?[HotbarSize];

    /// <summary>Currently selected slot index (0-9).</summary>
    public int ActiveSlotIndex { get; private set; } = 0;

    // Input tracking
    private int _previousScrollValue;
    private KeyboardState _previousKeyboard;

    /// <summary>Get the item in the currently selected slot.</summary>
    public Item? GetActiveItem() => _slots[ActiveSlotIndex];

    /// <summary>Get item at a specific slot index.</summary>
    public Item? GetSlot(int index)
    {
        if (index < 0 || index >= HotbarSize)
            return null;
        return _slots[index];
    }

    /// <summary>Set item at a specific slot index.</summary>
    public void SetSlot(int index, Item? item)
    {
        if (index >= 0 && index < HotbarSize)
            _slots[index] = item;
    }

    /// <summary>
    /// Add an item to the first available slot.
    /// For stackable items, tries to stack with existing items first.
    /// Returns true if item was added successfully.
    /// </summary>
    public bool AddItem(Item item)
    {
        // For stackable materials, try to add to existing stack first
        if (item is Material newMat)
        {
            for (int i = 0; i < HotbarSize; i++)
            {
                if (_slots[i] is Material existing &&
                    existing.RegistryKey == newMat.RegistryKey &&
                    existing.Quantity < existing.MaxStack)
                {
                    int spaceAvailable = existing.MaxStack - existing.Quantity;
                    int toAdd = Math.Min(spaceAvailable, newMat.Quantity);
                    existing.Quantity += toAdd;
                    newMat.Quantity -= toAdd;

                    if (newMat.Quantity <= 0)
                        return true; // Fully stacked
                }
            }
        }

        // Find first empty slot
        for (int i = 0; i < HotbarSize; i++)
        {
            if (_slots[i] == null)
            {
                _slots[i] = item;
                Debug.WriteLine($"[Inventory] Added {item.Name} to slot {i}");
                return true;
            }
        }

        Debug.WriteLine($"[Inventory] No room for {item.Name}");
        return false; // No space
    }

    /// <summary>
    /// Remove an item from inventory by reference.
    /// Returns true if item was found and removed.
    /// </summary>
    public bool RemoveItem(Item item)
    {
        for (int i = 0; i < HotbarSize; i++)
        {
            if (_slots[i] == item)
            {
                _slots[i] = null;
                Debug.WriteLine($"[Inventory] Removed {item.Name} from slot {i}");
                return true;
            }
        }
        return false;
    }

    /// <summary>Remove item at specific slot index.</summary>
    public void RemoveSlot(int index)
    {
        if (index >= 0 && index < HotbarSize)
            _slots[index] = null;
    }

    /// <summary>Select a specific slot by index (0-9).</summary>
    public void SelectSlot(int index)
    {
        int oldIndex = ActiveSlotIndex;
        ActiveSlotIndex = Math.Clamp(index, 0, HotbarSize - 1);

        if (oldIndex != ActiveSlotIndex)
        {
            var item = GetActiveItem();
            Debug.WriteLine($"[Inventory] Selected slot {ActiveSlotIndex}: {item?.Name ?? "(empty)"}");
        }
    }

    /// <summary>Scroll through slots (positive = right, negative = left).</summary>
    public void ScrollSlot(int delta)
    {
        int newIndex = (ActiveSlotIndex - delta + HotbarSize) % HotbarSize;
        SelectSlot(newIndex);
    }

    /// <summary>
    /// Handle input for slot selection.
    /// Call this in Update loop.
    /// </summary>
    public void Update(KeyboardState keyboard, MouseState mouse)
    {
        // Mouse scroll wheel
        int scrollDelta = mouse.ScrollWheelValue - _previousScrollValue;
        if (scrollDelta != 0)
        {
            ScrollSlot(scrollDelta > 0 ? 1 : -1);
        }
        _previousScrollValue = mouse.ScrollWheelValue;

        // Number keys 1-9 = slots 0-8, 0 = slot 9
        CheckNumberKey(keyboard, Keys.D1, 0);
        CheckNumberKey(keyboard, Keys.D2, 1);
        CheckNumberKey(keyboard, Keys.D3, 2);
        CheckNumberKey(keyboard, Keys.D4, 3);
        CheckNumberKey(keyboard, Keys.D5, 4);
        CheckNumberKey(keyboard, Keys.D6, 5);
        CheckNumberKey(keyboard, Keys.D7, 6);
        CheckNumberKey(keyboard, Keys.D8, 7);
        CheckNumberKey(keyboard, Keys.D9, 8);
        CheckNumberKey(keyboard, Keys.D0, 9);

        _previousKeyboard = keyboard;
    }

    private void CheckNumberKey(KeyboardState keyboard, Keys key, int slotIndex)
    {
        if (keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key))
        {
            SelectSlot(slotIndex);
        }
    }

    #region Save/Load

    /// <summary>Get all items as a list for serialization (legacy polymorphic).</summary>
    [Obsolete("Use ToSaveData() for v2.16+ saves")]
    public List<Item?> ToSaveList()
    {
        var list = new List<Item?>(HotbarSize);
        for (int i = 0; i < HotbarSize; i++)
            list.Add(_slots[i]);
        return list;
    }

    /// <summary>Restore inventory from saved list (legacy polymorphic).</summary>
    [Obsolete("Use LoadFromData() for v2.16+ saves")]
    public void LoadFromSaveList(List<Item?>? items)
    {
        // Clear all slots
        for (int i = 0; i < HotbarSize; i++)
            _slots[i] = null;

        if (items == null) return;

        for (int i = 0; i < Math.Min(items.Count, HotbarSize); i++)
            _slots[i] = items[i];
    }

    /// <summary>
    /// Convert inventory to flat DTOs for serialization (v2.16+).
    /// Avoids polymorphic serialization issues.
    /// </summary>
    public List<ItemData?> ToSaveData()
    {
        var list = new List<ItemData?>(HotbarSize);
        for (int i = 0; i < HotbarSize; i++)
        {
            list.Add(ItemToData(_slots[i]));
        }
        return list;
    }

    /// <summary>
    /// Restore inventory from flat DTOs (v2.16+).
    /// </summary>
    public void LoadFromData(List<ItemData?>? items)
    {
        // Clear all slots
        for (int i = 0; i < HotbarSize; i++)
            _slots[i] = null;

        if (items == null) return;

        for (int i = 0; i < Math.Min(items.Count, HotbarSize); i++)
        {
            _slots[i] = DataToItem(items[i]);
        }
    }

    /// <summary>
    /// Convert an Item to ItemData DTO.
    /// </summary>
    public static ItemData? ItemToData(Item? item)
    {
        if (item == null) return null;

        var data = new ItemData
        {
            RegistryKey = item.RegistryKey,
            Name = item.Name,
            Description = item.Description,
            SellPrice = item.SellPrice
        };

        switch (item)
        {
            case Tool tool:
                data.Type = "tool";
                data.IsWeapon = tool.IsWeapon;
                data.AttackStyle = tool.Style.ToString();
                data.Damage = tool.Damage;
                data.Range = tool.Range;
                data.ProjectileSpeed = tool.ProjectileSpeed;
                data.Cooldown = tool.Cooldown;
                data.StaminaCost = tool.StaminaCost;
                data.ResourceCost = tool.ResourceCost;
                data.PowerLevel = tool.PowerLevel;
                data.AffectsTileThroughObjects = tool.AffectsTileThroughObjects;
                data.ProjectileColorPacked = tool.ProjectileColorPacked;
                data.HitboxWidth = tool.HitboxWidth;
                data.HitboxHeight = tool.HitboxHeight;
                break;

            case Material mat:
                data.Type = "material";
                data.Quantity = mat.Quantity;
                data.MaxStack = mat.MaxStack;
                break;
        }

        return data;
    }

    /// <summary>
    /// Convert ItemData DTO back to an Item.
    /// </summary>
    public static Item? DataToItem(ItemData? data)
    {
        if (data == null) return null;

        return data.Type switch
        {
            "tool" => new Tool
            {
                RegistryKey = data.RegistryKey,
                Name = data.Name,
                Description = data.Description,
                IsWeapon = data.IsWeapon,
                Style = Enum.TryParse<AttackStyle>(data.AttackStyle, out var style) ? style : AttackStyle.None,
                Damage = data.Damage,
                Range = data.Range,
                ProjectileSpeed = data.ProjectileSpeed,
                Cooldown = data.Cooldown,
                StaminaCost = data.StaminaCost,
                ResourceCost = data.ResourceCost,
                PowerLevel = data.PowerLevel,
                AffectsTileThroughObjects = data.AffectsTileThroughObjects,
                ProjectileColorPacked = data.ProjectileColorPacked,
                HitboxWidth = data.HitboxWidth,
                HitboxHeight = data.HitboxHeight
            },

            "material" => new Material(
                registryKey: data.RegistryKey,
                name: data.Name,
                description: data.Description,
                quantity: data.Quantity,
                maxStack: data.MaxStack,
                sellPrice: data.SellPrice
            ),

            _ => null
        };
    }

    #endregion

    /// <summary>Give starter items for new game.</summary>
    public void GiveStarterItems()
    {
        // ═══════════════════════════════════════════════════════════════════
        // FARMING TOOLS (Slots 0-4)
        // Stamina: Hoe=3, Axe=4, Pickaxe=4, WateringCan=2, Scythe=0 (free)
        // ═══════════════════════════════════════════════════════════════════
        SetSlot(0, new Tool("hoe", "Hoe", "Tills soil for planting.", resourceCost: 2f, powerLevel: 1, staminaCost: 3f));
        SetSlot(1, new Tool("axe", "Axe", "Chops wood from trees.", resourceCost: 3f, powerLevel: 1, staminaCost: 4f));
        SetSlot(2, new Tool("pickaxe", "Pickaxe", "Breaks rocks and ore.", resourceCost: 3f, powerLevel: 1, staminaCost: 4f));
        SetSlot(3, new Tool("watering_can", "Watering Can", "Waters crops.", resourceCost: 1f, powerLevel: 1, affectsTileThroughObjects: true, staminaCost: 2f));
        SetSlot(4, new Tool("scythe", "Scythe", "Harvests crops.", resourceCost: 1f, powerLevel: 1, staminaCost: 0f));

        // ═══════════════════════════════════════════════════════════════════
        // COMBAT WEAPONS (Slots 5-7) - Test the 3 attack styles
        // ═══════════════════════════════════════════════════════════════════

        // MELEE: Sword - Creates hitbox in facing direction
        SetSlot(5, Tool.CreateMeleeWeapon(
            registryKey: "sword",
            name: "Iron Sword",
            description: "A sturdy blade for close combat.",
            damage: 2,
            range: 48f,
            cooldown: 0.3f,
            staminaCost: 2f,
            hitboxWidth: 48,
            hitboxHeight: 32
        ));

        // PROJECTILE: Fire Wand - Shoots fireballs
        SetSlot(6, Tool.CreateProjectileWeapon(
            registryKey: "fire_wand",
            name: "Fire Wand",
            description: "Hurls fireballs at your foes.",
            damage: 3,
            projectileSpeed: 300f,
            cooldown: 0.5f,
            staminaCost: 4f,
            projectileColor: 0xFFFF6600 // Orange-red
        ));

        // RAYCAST: Lightning Staff - Instant zap in a line
        SetSlot(7, Tool.CreateRaycastWeapon(
            registryKey: "lightning_staff",
            name: "Lightning Staff",
            description: "Channels lightning to strike instantly.",
            damage: 4,
            range: 200f,
            cooldown: 0.8f,
            staminaCost: 6f
        ));

        // ═══════════════════════════════════════════════════════════════════
        // MATERIALS (Slots 8-9)
        // ═══════════════════════════════════════════════════════════════════
        SetSlot(8, new Material("wood", "Wood", "Basic building material.", quantity: 25, sellPrice: 2));
        SetSlot(9, new Material("stone", "Stone", "Hard and sturdy.", quantity: 15, sellPrice: 2));
    }
}
