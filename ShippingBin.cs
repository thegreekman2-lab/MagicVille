#nullable enable
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;

namespace MagicVille;

/// <summary>
/// A shipping bin for selling items overnight.
///
/// ECONOMY LOOP:
/// 1. Player interacts with bin while holding a sellable item
/// 2. Item is removed from inventory and added to shipping manifest
/// 3. At end of day (OnNewDay), all items in manifest are sold
/// 4. Player receives gold, manifest is cleared
/// </summary>
public class ShippingBin : WorldObject
{
    /// <summary>
    /// Items queued for shipping at end of day.
    /// Serialized with save data for persistence.
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
    /// </summary>
    /// <param name="item">The item to ship.</param>
    public void ShipItem(Item item)
    {
        var shippedItem = new ShippedItem(item);
        ShippingManifest.Add(shippedItem);
        Debug.WriteLine($"[ShippingBin] ★ Shipped {shippedItem.Name} x{shippedItem.Quantity} ({shippedItem.TotalValue}g)");
    }

    /// <summary>
    /// Calculate total value of all items in shipping manifest.
    /// </summary>
    public int CalculateTotalValue()
    {
        int total = 0;
        foreach (var item in ShippingManifest)
        {
            total += item.TotalValue;
        }
        return total;
    }

    /// <summary>
    /// Process nightly shipment - called during OnDayPassed.
    /// Returns total gold earned and clears the manifest.
    /// </summary>
    /// <returns>Total gold from shipped items.</returns>
    public int ProcessNightlyShipment()
    {
        if (ShippingManifest.Count == 0)
        {
            Debug.WriteLine("[ShippingBin] No items to ship tonight.");
            return 0;
        }

        int totalGold = 0;
        Debug.WriteLine("[ShippingBin] ════════════════════════════════════");
        Debug.WriteLine("[ShippingBin] NIGHTLY SHIPMENT REPORT:");

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
