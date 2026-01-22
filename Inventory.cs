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

    /// <summary>Get all items as a list for serialization.</summary>
    public List<Item?> ToSaveList()
    {
        var list = new List<Item?>(HotbarSize);
        for (int i = 0; i < HotbarSize; i++)
            list.Add(_slots[i]);
        return list;
    }

    /// <summary>Restore inventory from saved list.</summary>
    public void LoadFromSaveList(List<Item?>? items)
    {
        // Clear all slots
        for (int i = 0; i < HotbarSize; i++)
            _slots[i] = null;

        if (items == null) return;

        for (int i = 0; i < Math.Min(items.Count, HotbarSize); i++)
            _slots[i] = items[i];
    }

    #endregion

    /// <summary>Give starter items for new game.</summary>
    public void GiveStarterItems()
    {
        // Standard Tools with stamina costs
        // Stamina: Hoe=3, Axe=4, Pickaxe=4, WateringCan=2, Scythe=0 (free)
        SetSlot(0, new Tool("hoe", "Hoe", "Tills soil for planting.", resourceCost: 2f, powerLevel: 1, staminaCost: 3f));
        SetSlot(1, new Tool("axe", "Axe", "Chops wood from trees.", resourceCost: 3f, powerLevel: 1, staminaCost: 4f));
        SetSlot(2, new Tool("pickaxe", "Pickaxe", "Breaks rocks and ore.", resourceCost: 3f, powerLevel: 1, staminaCost: 4f));
        SetSlot(4, new Tool("scythe", "Scythe", "Harvests crops.", resourceCost: 1f, powerLevel: 1, staminaCost: 0f));

        // Watering Tools (affectsTileThroughObjects: true - waters crop AND wets tile)
        SetSlot(3, new Tool("watering_can", "Watering Can", "Waters crops.", resourceCost: 1f, powerLevel: 1, affectsTileThroughObjects: true, staminaCost: 2f));

        // Magic Wands (lower stamina cost due to magical efficiency)
        SetSlot(5, new Tool("earth_wand", "Earth Wand", "Magically tills soil with earth energy.", resourceCost: 1f, powerLevel: 2, staminaCost: 1f));
        SetSlot(6, new Tool("hydro_wand", "Hydro Wand", "Conjures water to nourish crops.", resourceCost: 1f, powerLevel: 2, affectsTileThroughObjects: true, staminaCost: 1f));

        // Weapons (combat tools)
        SetSlot(7, new Tool("sword", "Rusty Sword", "A basic sword for combat.", resourceCost: 0f, powerLevel: 1, staminaCost: 2f) { IsWeapon = true, AttackDamage = 1 });

        // Materials (with sell prices)
        SetSlot(8, new Material("wood", "Wood", "Basic building material.", quantity: 25, sellPrice: 2));
        SetSlot(9, new Material("stone", "Stone", "Hard and sturdy.", quantity: 15, sellPrice: 2));
    }
}
