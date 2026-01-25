#nullable enable
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;

namespace MagicVille;

/// <summary>
/// A shipping bin for selling items overnight.
///
/// ════════════════════════════════════════════════════════════════════════════
/// TWO-TIER STORAGE SYSTEM (v2.15):
/// ════════════════════════════════════════════════════════════════════════════
///
/// 1. BUFFER SLOT (LastShippedItem):
///    - Holds the MOST RECENT item dropped into the bin
///    - Persists until end of day (player can re-open bin to retrieve it)
///    - Acts as an "undo" mechanism - take it back before sleeping!
///    - Only ONE item can be in the buffer at a time
///
/// 2. SHIPPING MANIFEST (ShippingManifest):
///    - Items that are COMMITTED for sale (no retrieval possible)
///    - When a NEW item is dropped, the PREVIOUS buffer item moves here
///    - Processed at end of day along with buffer slot
///
/// ECONOMY LOOP:
/// 1. Player drops item into bin -> Goes to Buffer Slot
/// 2. Player drops ANOTHER item -> Old buffer moves to Manifest, new item in buffer
/// 3. Player can re-open bin to retrieve buffer item (undo last action)
/// 4. At end of day: Buffer + Manifest are sold, player gets gold, both cleared
/// ════════════════════════════════════════════════════════════════════════════
/// </summary>
public class ShippingBin : WorldObject
{
    /// <summary>
    /// BUFFER SLOT: The most recently shipped item.
    /// Player can retrieve this by re-opening the bin before sleeping.
    /// Cleared at end of day after processing.
    /// </summary>
    public Item? LastShippedItem { get; set; }

    /// <summary>
    /// COMMITTED MANIFEST: Items pushed out of the buffer slot.
    /// These cannot be retrieved - they are committed for sale.
    /// Processed and cleared at end of day.
    /// </summary>
    public List<ShippedItem> ShippingManifest { get; set; } = new();

    /// <summary>
    /// Simplified item data for shipping (avoids polymorphic complexity).
    /// </summary>
    public class ShippedItem
    {
        public string Name { get; set; } = "";
        public int Quantity { get; set; } = 1;
        public int SellPrice { get; set; } = 0;

        public int TotalValue => SellPrice * Quantity;

        public ShippedItem() { }

        public ShippedItem(Item item)
        {
            Name = item.Name;
            SellPrice = item.SellPrice;
            Quantity = item is Material mat ? mat.Quantity : 1;
        }
    }

    public ShippingBin() : base()
    {
        Name = "shipping_bin";
        Width = 64;
        Height = 48;
        IsCollidable = true;
        Color = new Color(139, 90, 43); // Wooden brown
    }

    public ShippingBin(Vector2 position) : this()
    {
        Position = position;
    }

    /// <summary>
    /// Attempt to ship an item from the player's inventory.
    /// </summary>
    /// <param name="inventory">Player's inventory.</param>
    /// <param name="slotIndex">Slot containing item to ship.</param>
    /// <returns>True if item was shipped, false otherwise.</returns>
    public bool TryShipItem(Inventory inventory, int slotIndex)
    {
        Item? item = inventory.GetSlot(slotIndex);

        // Case 3: Empty hand
        if (item == null)
        {
            Debug.WriteLine($"[ShippingBin] Bin contains {ShippingManifest.Count} item stacks.");
            return false;
        }

        // Case 2: Unsellable item
        if (!item.IsSellable)
        {
            Debug.WriteLine($"[ShippingBin] Cannot ship {item.Name} (not sellable).");
            return false;
        }

        // Case 1: Sellable item - add to manifest
        var shippedItem = new ShippedItem(item);
        ShippingManifest.Add(shippedItem);

        // Remove from inventory
        inventory.SetSlot(slotIndex, null);

        Debug.WriteLine($"[ShippingBin] ★ Shipped {shippedItem.Name} x{shippedItem.Quantity} ({shippedItem.TotalValue}g)");

        return true;
    }

    /// <summary>
    /// Ship an item directly (called by ShippingMenu when finalizing).
    /// This commits the item to the manifest (no retrieval possible).
    /// </summary>
    /// <param name="item">The item to ship.</param>
    public void ShipItem(Item item)
    {
        var shippedItem = new ShippedItem(item);
        ShippingManifest.Add(shippedItem);
        Debug.WriteLine($"[ShippingBin] ★ Committed to manifest: {shippedItem.Name} x{shippedItem.Quantity} ({shippedItem.TotalValue}g)");
    }

    /// <summary>
    /// Add an item directly to the committed manifest.
    /// Called by ShippingMenu when "pushing" the old buffer item.
    /// </summary>
    /// <param name="item">The item to commit.</param>
    public void AddToManifest(Item item)
    {
        ShipItem(item); // Same behavior, just an alias for clarity
    }

    /// <summary>
    /// Calculate total value of all items (buffer + manifest).
    /// </summary>
    public int CalculateTotalValue()
    {
        int total = 0;

        // Add manifest items
        foreach (var item in ShippingManifest)
        {
            total += item.TotalValue;
        }

        // Add buffer slot item
        if (LastShippedItem != null)
        {
            int qty = LastShippedItem is Material mat ? mat.Quantity : 1;
            total += LastShippedItem.SellPrice * qty;
        }

        return total;
    }

    /// <summary>
    /// Process nightly shipment - called during OnDayPassed.
    /// Returns total gold earned and clears BOTH buffer slot AND manifest.
    /// </summary>
    /// <returns>Total gold from shipped items.</returns>
    public int ProcessNightlyShipment()
    {
        bool hasBufferItem = LastShippedItem != null;
        bool hasManifestItems = ShippingManifest.Count > 0;

        if (!hasBufferItem && !hasManifestItems)
        {
            Debug.WriteLine("[ShippingBin] No items to ship tonight.");
            return 0;
        }

        int totalGold = 0;
        Debug.WriteLine("[ShippingBin] ════════════════════════════════════");
        Debug.WriteLine("[ShippingBin] NIGHTLY SHIPMENT REPORT:");

        // Process buffer slot first (the "last shipped" item)
        if (LastShippedItem != null)
        {
            int qty = LastShippedItem is Material mat ? mat.Quantity : 1;
            int value = LastShippedItem.SellPrice * qty;
            Debug.WriteLine($"  ★ {LastShippedItem.Name} x{qty} = {value}g (buffer)");
            totalGold += value;

            // Clear buffer
            LastShippedItem = null;
        }

        // Process committed manifest items
        foreach (var item in ShippingManifest)
        {
            Debug.WriteLine($"  • {item.Name} x{item.Quantity} = {item.TotalValue}g");
            totalGold += item.TotalValue;
        }

        Debug.WriteLine($"[ShippingBin] TOTAL EARNINGS: {totalGold}g");
        Debug.WriteLine("[ShippingBin] ════════════════════════════════════");

        // Clear manifest to prevent duplicate processing
        ShippingManifest.Clear();

        return totalGold;
    }

    /// <summary>
    /// Shipping bins don't change on new day (manifest processed separately).
    /// </summary>
    public override bool OnNewDay(GameLocation? location = null)
    {
        // Manifest processing is handled by WorldManager.OnDayPassed
        // to ensure proper gold transfer to player
        return false;
    }

    /// <summary>
    /// Create a shipping bin at a specific tile position.
    /// Uses smart alignment for proper placement.
    /// </summary>
    public static ShippingBin Create(int tileX, int tileY)
    {
        const int TileSize = 64;
        // Center horizontally, align to bottom of tile
        float x = (tileX * TileSize) + (TileSize / 2f);
        float y = (tileY * TileSize) + TileSize;

        return new ShippingBin(new Vector2(x, y));
    }
}
