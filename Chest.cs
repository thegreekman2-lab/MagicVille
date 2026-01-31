#nullable enable
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;

namespace MagicVille;

/// <summary>
/// A storage chest for holding items.
/// Supports smart stacking: tries to merge with existing stacks before using empty slots.
///
/// STORAGE LOGIC:
/// 1. For stackable items (Materials), scan existing slots for matching items with space
/// 2. Merge into existing stacks until item is fully added or stacks are full
/// 3. If quantity remains, find first empty slot
/// 4. Return true if fully added, false if no space (partial adds reduce item.Quantity)
/// </summary>
public class Chest : WorldObject
{
    /// <summary>Default chest capacity (6x6 grid = 36 slots).</summary>
    public const int DefaultCapacity = 36;

    /// <summary>Number of storage slots.</summary>
    public int Capacity { get; init; }

    /// <summary>Items stored in the chest (null = empty slot).</summary>
    public List<Item?> Items { get; private set; }

    /// <summary>Chest style for visual variations.</summary>
    public string ChestStyle { get; set; } = "wooden";

    public Chest() : this(DefaultCapacity) { }

    public Chest(int capacity) : base()
    {
        Capacity = capacity;
        Items = new List<Item?>(capacity);

        // Initialize with empty slots
        for (int i = 0; i < capacity; i++)
            Items.Add(null);

        Name = "chest";
        Width = 48;
        Height = 40;
        IsCollidable = true;
        Color = new Color(139, 90, 43); // Wooden brown
    }

    public Chest(Vector2 position, int capacity = DefaultCapacity) : this(capacity)
    {
        Position = position;
    }

    /// <summary>
    /// Add an item to the chest using smart stacking logic.
    /// For stackable items, merges with existing stacks first.
    /// </summary>
    /// <param name="item">Item to add.</param>
    /// <returns>True if item was fully added, false if no space (item may be partially added).</returns>
    public bool AddItem(Item item)
    {
        if (item == null) return false;

        // For stackable materials, try to merge with existing stacks first
        if (item is Material newMat && newMat.IsStackable)
        {
            // Pass 1: Merge with existing stacks
            for (int i = 0; i < Capacity && newMat.Quantity > 0; i++)
            {
                if (Items[i] is Material existing &&
                    existing.RegistryKey == newMat.RegistryKey &&
                    existing.Quantity < existing.MaxStack)
                {
                    int spaceAvailable = existing.MaxStack - existing.Quantity;
                    int toAdd = System.Math.Min(spaceAvailable, newMat.Quantity);
                    existing.Quantity += toAdd;
                    newMat.Quantity -= toAdd;

                    Debug.WriteLine($"[Chest] Merged {toAdd}x {newMat.Name} into slot {i} (now {existing.Quantity})");

                    if (newMat.Quantity <= 0)
                    {
                        Debug.WriteLine($"[Chest] Fully stacked {item.Name}");
                        return true;
                    }
                }
            }
        }

        // Pass 2: Find first empty slot for remaining quantity (or non-stackable item)
        for (int i = 0; i < Capacity; i++)
        {
            if (Items[i] == null)
            {
                Items[i] = item;
                Debug.WriteLine($"[Chest] Added {item.Name} to slot {i}");
                return true;
            }
        }

        // No space - partial add may have occurred for stackables
        if (item is Material mat && mat.Quantity > 0)
        {
            Debug.WriteLine($"[Chest] Partial add: {mat.Quantity}x {mat.Name} could not fit");
        }
        else
        {
            Debug.WriteLine($"[Chest] No room for {item.Name}");
        }

        return false;
    }

    /// <summary>
    /// Remove a specific item instance from the chest.
    /// </summary>
    /// <param name="item">Item to remove.</param>
    /// <returns>True if item was found and removed.</returns>
    public bool RemoveItem(Item item)
    {
        for (int i = 0; i < Capacity; i++)
        {
            if (Items[i] == item)
            {
                Items[i] = null;
                Debug.WriteLine($"[Chest] Removed {item.Name} from slot {i}");
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Get item at a specific slot index.
    /// </summary>
    public Item? GetSlot(int index)
    {
        if (index < 0 || index >= Capacity)
            return null;
        return Items[index];
    }

    /// <summary>
    /// Set item at a specific slot index.
    /// </summary>
    public void SetSlot(int index, Item? item)
    {
        if (index >= 0 && index < Capacity)
            Items[index] = item;
    }

    /// <summary>
    /// Count total items in the chest (non-null slots).
    /// </summary>
    public int ItemCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < Capacity; i++)
            {
                if (Items[i] != null)
                    count++;
            }
            return count;
        }
    }

    /// <summary>
    /// Check if chest is full (no empty slots).
    /// </summary>
    public bool IsFull
    {
        get
        {
            for (int i = 0; i < Capacity; i++)
            {
                if (Items[i] == null)
                    return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Check if chest is empty (all slots null).
    /// </summary>
    public bool IsEmpty => ItemCount == 0;

    /// <summary>
    /// Chests don't change on new day.
    /// </summary>
    public override bool OnNewDay(GameLocation? location = null)
    {
        return false; // Never remove
    }

    /// <summary>
    /// Update visual appearance based on contents.
    /// </summary>
    public void UpdateVisuals()
    {
        // Slightly different shade when chest has items
        Color = IsEmpty
            ? new Color(139, 90, 43)   // Empty: lighter brown
            : new Color(120, 75, 35);  // Has items: darker brown
    }

    /// <summary>
    /// Create a chest at a specific tile position.
    /// </summary>
    public static Chest Create(int tileX, int tileY, int capacity = DefaultCapacity)
    {
        const int TileSize = 64;
        float x = (tileX * TileSize) + (TileSize / 2f);
        float y = (tileY * TileSize) + TileSize;

        return new Chest(new Vector2(x, y), capacity);
    }
}
