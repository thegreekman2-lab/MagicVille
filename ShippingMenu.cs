#nullable enable
using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MagicVille;

/// <summary>
/// Shipping menu UI for selling items (Stardew Valley style).
///
/// ARCHITECTURE (v2.15 - Persistent Buffer):
/// - View-Model Separation: References Player.Inventory
/// - Bin Slot: Acts as "undo buffer" - persists until end of day
/// - On Open: Loads the bin's LastShippedItem into the slot
/// - On Close: Saves slot back to LastShippedItem (NOT shipped immediately)
/// - Buffer is only sold at end of day in ProcessNightlyShipment
///
/// LAYOUT:
/// - Top: Large "Bin Slot" for dropping items to sell
/// - Bottom: Player's inventory for dragging items from
/// </summary>
public class ShippingMenu
{
    // === View-Model binding ===
    private readonly Player _player;
    private Inventory PlayerInventory => _player.Inventory;

    // === Graphics resources ===
    private readonly Texture2D _pixel;
    private readonly GraphicsDevice _graphics;

    // === Layout constants ===
    private const int SlotSize = 60;
    private const int SlotPadding = 6;
    private const int SlotBorder = 3;
    private const int ItemInset = 8;
    private const int BinSlotSize = 80;

    // === Slot rectangles ===
    private readonly Rectangle[] _inventorySlotRects = new Rectangle[Inventory.HotbarSize];
    private Rectangle _binSlotRect;

    // === Bin Slot State (Undo Buffer) ===
    private Item? _binSlotItem;
    private ShippingBin? _activeBin;

    // === Drag-and-Drop state ===
    private Item? _heldItem;
    private int _sourceIndex = -1; // -1 = none, -2 = from bin slot, 0-9 = from inventory
    private bool _isDragging;

    // === Mouse state tracking ===
    private MouseState _previousMouse;
    private int _hoveredInventorySlot = -1;
    private bool _hoveringBinSlot;

    public ShippingMenu(Player player, Texture2D pixel, GraphicsDevice graphics)
    {
        _player = player;
        _pixel = pixel;
        _graphics = graphics;
    }

    /// <summary>
    /// Open the shipping menu for a specific bin.
    /// Loads the bin's LastShippedItem into the buffer slot.
    /// </summary>
    public void Open(ShippingBin bin)
    {
        _activeBin = bin;

        // Load the persistent buffer item from the bin
        _binSlotItem = bin.LastShippedItem;

        _heldItem = null;
        _sourceIndex = -1;
        _isDragging = false;

        if (_binSlotItem != null)
        {
            Debug.WriteLine($"[ShippingMenu] Opened with existing item: {_binSlotItem.Name}");
        }
        else
        {
            Debug.WriteLine("[ShippingMenu] Opened for shipping bin (empty buffer)");
        }
    }

    /// <summary>
    /// Close the menu and persist the buffer slot.
    /// CRITICAL: Does NOT ship the buffer item - it stays in LastShippedItem
    /// until end of day, allowing the player to retrieve it later.
    /// </summary>
    public void FinalizeAndClose(ShippingBin? bin)
    {
        // If holding an item, return it to appropriate location first
        if (_isDragging && _heldItem != null)
        {
            if (_sourceIndex >= 0)
            {
                // Was from inventory - return to inventory
                PlayerInventory.SetSlot(_sourceIndex, _heldItem);
            }
            else if (_sourceIndex == -2)
            {
                // Was from bin slot - put back in buffer
                _binSlotItem = _heldItem;
            }
            _heldItem = null;
            _isDragging = false;
        }

        // ═══════════════════════════════════════════════════════════════════
        // CRITICAL CHANGE (v2.15): PERSIST buffer, don't ship it!
        // The item stays in LastShippedItem until OnDayPassed processes it.
        // This allows the player to re-open the bin and retrieve the item.
        // ═══════════════════════════════════════════════════════════════════
        if (bin != null)
        {
            bin.LastShippedItem = _binSlotItem;

            if (_binSlotItem != null)
            {
                Debug.WriteLine($"[ShippingMenu] Saved buffer item: {_binSlotItem.Name} (retrievable until sleep)");
            }
        }

        _binSlotItem = null;
        _activeBin = null;
        Debug.WriteLine("[ShippingMenu] Closed");
    }

    /// <summary>
    /// Update drag-and-drop logic.
    /// </summary>
    public void Update(MouseState mouse)
    {
        // Update hovered states
        _hoveredInventorySlot = GetInventorySlotAtPosition(mouse.Position);
        _hoveringBinSlot = _binSlotRect.Contains(mouse.Position);

        // Handle mouse input
        bool mouseDown = mouse.LeftButton == ButtonState.Pressed;
        bool mouseWasDown = _previousMouse.LeftButton == ButtonState.Pressed;
        bool mousePressed = mouseDown && !mouseWasDown;
        bool mouseReleased = !mouseDown && mouseWasDown;

        if (mousePressed)
        {
            OnMouseDown(mouse.Position);
        }
        else if (mouseReleased)
        {
            OnMouseUp(mouse.Position);
        }

        _previousMouse = mouse;
    }

    /// <summary>
    /// Handle mouse button press - pick up item.
    /// </summary>
    private void OnMouseDown(Point mousePos)
    {
        // Check if clicking on bin slot
        if (_binSlotRect.Contains(mousePos) && _binSlotItem != null)
        {
            // Pick up from bin slot (undo)
            _heldItem = _binSlotItem;
            _binSlotItem = null;
            _sourceIndex = -2; // Special index for bin slot
            _isDragging = true;
            Debug.WriteLine($"[ShippingMenu] Picked up from bin slot: {_heldItem.Name}");
            return;
        }

        // Check if clicking on inventory slot
        int slotIndex = GetInventorySlotAtPosition(mousePos);
        if (slotIndex >= 0)
        {
            Item? item = PlayerInventory.GetSlot(slotIndex);
            if (item != null)
            {
                _heldItem = item;
                _sourceIndex = slotIndex;
                _isDragging = true;
                PlayerInventory.SetSlot(slotIndex, null);
                Debug.WriteLine($"[ShippingMenu] Picked up from slot {slotIndex}: {item.Name}");
            }
        }
    }

    /// <summary>
    /// Handle mouse button release - place or swap item.
    /// </summary>
    private void OnMouseUp(Point mousePos)
    {
        if (!_isDragging || _heldItem == null)
            return;

        // Check if dropping on bin slot
        if (_binSlotRect.Contains(mousePos))
        {
            // Check if item is sellable
            if (!_heldItem.IsSellable)
            {
                Debug.WriteLine($"[ShippingMenu] Cannot sell {_heldItem.Name} (not sellable)");
                // Return to source
                ReturnHeldItemToSource();
                return;
            }

            // ═══════════════════════════════════════════════════════════════════
            // PUSH SYSTEM: If bin slot has an item, COMMIT it to manifest
            // The old item becomes unretrievable, new item goes to buffer
            // ═══════════════════════════════════════════════════════════════════
            if (_binSlotItem != null && _activeBin != null)
            {
                _activeBin.AddToManifest(_binSlotItem);
                Debug.WriteLine($"[ShippingMenu] Pushed to manifest: {_binSlotItem.Name} (committed)");
            }

            // Place held item in bin slot (buffer)
            _binSlotItem = _heldItem;
            int value = _heldItem.SellPrice * (_heldItem is Material m ? m.Quantity : 1);
            Debug.WriteLine($"[ShippingMenu] Added to buffer: {_heldItem.Name} ({value}g) - retrievable until sleep");

            _heldItem = null;
            _sourceIndex = -1;
            _isDragging = false;
            return;
        }

        // Check if dropping on inventory slot
        int targetSlot = GetInventorySlotAtPosition(mousePos);
        if (targetSlot >= 0)
        {
            Item? targetItem = PlayerInventory.GetSlot(targetSlot);

            // Place held item
            PlayerInventory.SetSlot(targetSlot, _heldItem);

            // If target had an item, pick it up (swap) or return to source
            if (targetItem != null && _sourceIndex >= 0)
            {
                PlayerInventory.SetSlot(_sourceIndex, targetItem);
            }
            else if (targetItem != null && _sourceIndex == -2)
            {
                // Swapping with bin slot
                _binSlotItem = targetItem;
            }

            Debug.WriteLine($"[ShippingMenu] Placed {_heldItem.Name} in slot {targetSlot}");

            _heldItem = null;
            _sourceIndex = -1;
            _isDragging = false;
            return;
        }

        // Invalid drop location - return to source
        ReturnHeldItemToSource();
    }

    /// <summary>
    /// Return held item to its source location.
    /// </summary>
    private void ReturnHeldItemToSource()
    {
        if (_heldItem == null)
            return;

        if (_sourceIndex >= 0)
        {
            PlayerInventory.SetSlot(_sourceIndex, _heldItem);
        }
        else if (_sourceIndex == -2)
        {
            _binSlotItem = _heldItem;
        }

        Debug.WriteLine($"[ShippingMenu] Returned {_heldItem.Name} to source");
        _heldItem = null;
        _sourceIndex = -1;
        _isDragging = false;
    }

    /// <summary>
    /// Get inventory slot at screen position.
    /// </summary>
    private int GetInventorySlotAtPosition(Point screenPos)
    {
        for (int i = 0; i < _inventorySlotRects.Length; i++)
        {
            if (_inventorySlotRects[i].Contains(screenPos))
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Draw the shipping menu.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, Viewport viewport)
    {
        ComputeSlotRects(viewport);

        spriteBatch.Begin(
            sortMode: SpriteSortMode.Deferred,
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.PointClamp
        );

        // === LAYER 1: Title ===
        DrawTitle(spriteBatch, viewport);

        // === LAYER 2: Bin Slot (center) ===
        DrawBinSlot(spriteBatch, viewport);

        // === LAYER 3: Inventory slots (bottom) ===
        DrawInventorySlots(spriteBatch);

        // === LAYER 4: Items in inventory (except held) ===
        DrawInventoryItems(spriteBatch);

        // === LAYER 5: Item in bin slot ===
        DrawBinSlotItem(spriteBatch);

        // === LAYER 6: Tooltip ===
        DrawTooltip(spriteBatch, viewport);

        // === LAYER 7: Held item at mouse ===
        DrawHeldItem(spriteBatch);

        // === LAYER 8: Gold display ===
        DrawGoldDisplay(spriteBatch, viewport);

        spriteBatch.End();
    }

    /// <summary>
    /// Compute slot positions.
    /// </summary>
    private void ComputeSlotRects(Viewport viewport)
    {
        // Inventory slots at bottom
        int totalWidth = Inventory.HotbarSize * (SlotSize + SlotPadding) - SlotPadding;
        int startX = (viewport.Width - totalWidth) / 2;
        int startY = viewport.Height - 100;

        for (int i = 0; i < Inventory.HotbarSize; i++)
        {
            int x = startX + i * (SlotSize + SlotPadding);
            _inventorySlotRects[i] = new Rectangle(x, startY, SlotSize, SlotSize);
        }

        // Bin slot in center-upper area
        int binX = (viewport.Width - BinSlotSize) / 2;
        int binY = viewport.Height / 2 - BinSlotSize - 20;
        _binSlotRect = new Rectangle(binX, binY, BinSlotSize, BinSlotSize);
    }

    /// <summary>
    /// Draw title.
    /// </summary>
    private void DrawTitle(SpriteBatch spriteBatch, Viewport viewport)
    {
        string title = "SHIPPING BIN";
        int charWidth = 6 * 2; // 2x scale
        int titleWidth = title.Length * charWidth;
        int titleX = (viewport.Width - titleWidth) / 2;
        int titleY = _binSlotRect.Y - 40;

        DrawScaledPixelText(spriteBatch, title, titleX, titleY, Color.White, 2);
    }

    /// <summary>
    /// Draw the bin slot.
    /// </summary>
    private void DrawBinSlot(SpriteBatch spriteBatch, Viewport viewport)
    {
        // Border color based on hover
        Color borderColor = _hoveringBinSlot
            ? new Color(255, 215, 0) // Gold on hover
            : new Color(139, 90, 43); // Brown (wooden)

        // Draw border
        spriteBatch.Draw(_pixel, _binSlotRect, borderColor);

        // Draw inner area
        var innerRect = new Rectangle(
            _binSlotRect.X + SlotBorder,
            _binSlotRect.Y + SlotBorder,
            _binSlotRect.Width - SlotBorder * 2,
            _binSlotRect.Height - SlotBorder * 2
        );
        spriteBatch.Draw(_pixel, innerRect, new Color(60, 40, 20));

        // Label
        string label = "DROP HERE";
        int labelWidth = label.Length * 6;
        int labelX = _binSlotRect.X + (_binSlotRect.Width - labelWidth) / 2;
        int labelY = _binSlotRect.Bottom + 8;
        DrawScaledPixelText(spriteBatch, label, labelX, labelY, new Color(180, 180, 180), 1);

        // Show shipping manifest count
        if (_activeBin != null && _activeBin.ShippingManifest.Count > 0)
        {
            string countText = $"{_activeBin.ShippingManifest.Count} shipped";
            int countWidth = countText.Length * 6;
            int countX = _binSlotRect.X + (_binSlotRect.Width - countWidth) / 2;
            int countY = labelY + 14;
            DrawScaledPixelText(spriteBatch, countText, countX, countY, new Color(150, 255, 150), 1);
        }
    }

    /// <summary>
    /// Draw inventory slot backgrounds.
    /// </summary>
    private void DrawInventorySlots(SpriteBatch spriteBatch)
    {
        for (int i = 0; i < Inventory.HotbarSize; i++)
        {
            var rect = _inventorySlotRects[i];

            bool isHovered = i == _hoveredInventorySlot;
            bool isSource = i == _sourceIndex && _isDragging;

            Color borderColor;
            if (isSource)
                borderColor = new Color(255, 200, 100);
            else if (isHovered)
                borderColor = new Color(150, 200, 255);
            else
                borderColor = new Color(60, 60, 70);

            spriteBatch.Draw(_pixel, rect, borderColor);

            var innerRect = new Rectangle(
                rect.X + SlotBorder,
                rect.Y + SlotBorder,
                rect.Width - SlotBorder * 2,
                rect.Height - SlotBorder * 2
            );
            spriteBatch.Draw(_pixel, innerRect, new Color(40, 40, 50));
        }
    }

    /// <summary>
    /// Draw items in inventory slots.
    /// </summary>
    private void DrawInventoryItems(SpriteBatch spriteBatch)
    {
        for (int i = 0; i < Inventory.HotbarSize; i++)
        {
            if (i == _sourceIndex && _isDragging)
                continue;

            Item? item = PlayerInventory.GetSlot(i);
            if (item == null)
                continue;

            var slotRect = _inventorySlotRects[i];
            DrawItemInSlot(spriteBatch, item, slotRect);
        }
    }

    /// <summary>
    /// Draw item in the bin slot.
    /// </summary>
    private void DrawBinSlotItem(SpriteBatch spriteBatch)
    {
        if (_binSlotItem == null)
            return;

        // Don't draw if we're dragging from bin slot
        if (_sourceIndex == -2 && _isDragging)
            return;

        DrawItemInSlot(spriteBatch, _binSlotItem, _binSlotRect, showPrice: true);
    }

    /// <summary>
    /// Draw an item in a slot.
    /// </summary>
    private void DrawItemInSlot(SpriteBatch spriteBatch, Item item, Rectangle slotRect, bool showPrice = false)
    {
        Color itemColor = GetItemColor(item);

        var itemRect = new Rectangle(
            slotRect.X + ItemInset,
            slotRect.Y + ItemInset,
            slotRect.Width - ItemInset * 2,
            slotRect.Height - ItemInset * 2
        );

        spriteBatch.Draw(_pixel, itemRect, itemColor);

        // Quantity for materials
        if (item is Material mat && mat.Quantity > 1)
        {
            string qtyStr = mat.Quantity.ToString();
            int qtyX = slotRect.X + slotRect.Width - qtyStr.Length * 6 - 4;
            int qtyY = slotRect.Y + slotRect.Height - 12;
            DrawScaledPixelText(spriteBatch, qtyStr, qtyX, qtyY, Color.White, 1);
        }

        // Price for bin slot
        if (showPrice && item.IsSellable)
        {
            int totalPrice = item.SellPrice * (item is Material m ? m.Quantity : 1);
            string priceStr = $"{totalPrice}g";
            int priceX = slotRect.X + (slotRect.Width - priceStr.Length * 6) / 2;
            int priceY = slotRect.Y - 14;
            DrawScaledPixelText(spriteBatch, priceStr, priceX, priceY, new Color(255, 215, 0), 1);
        }
    }

    /// <summary>
    /// Draw tooltip for hovered item.
    /// </summary>
    private void DrawTooltip(SpriteBatch spriteBatch, Viewport viewport)
    {
        if (_isDragging)
            return;

        Item? item = null;

        if (_hoveringBinSlot && _binSlotItem != null)
        {
            item = _binSlotItem;
        }
        else if (_hoveredInventorySlot >= 0)
        {
            item = PlayerInventory.GetSlot(_hoveredInventorySlot);
        }

        if (item == null)
            return;

        var mousePos = Mouse.GetState().Position;

        // Build tooltip
        string name = item.Name;
        string priceText = item.IsSellable
            ? $"Sells for: {item.SellPrice}g"
            : "Cannot sell";

        int nameWidth = name.Length * 12; // 2x scale
        int priceWidth = priceText.Length * 6;
        int tooltipWidth = Math.Max(nameWidth, priceWidth) + 20;
        int tooltipHeight = 40;

        int tooltipX = mousePos.X + 20;
        int tooltipY = mousePos.Y;

        if (tooltipX + tooltipWidth > viewport.Width - 4)
            tooltipX = mousePos.X - tooltipWidth - 10;
        tooltipX = Math.Max(4, tooltipX);
        tooltipY = Math.Clamp(tooltipY, 4, viewport.Height - tooltipHeight - 4);

        var tooltipRect = new Rectangle(tooltipX, tooltipY, tooltipWidth, tooltipHeight);
        spriteBatch.Draw(_pixel, tooltipRect, new Color(0, 0, 0, 220));
        DrawRectBorder(spriteBatch, tooltipRect, new Color(150, 150, 180));

        DrawScaledPixelText(spriteBatch, name, tooltipX + 10, tooltipY + 6, Color.White, 2);

        Color priceColor = item.IsSellable ? new Color(255, 215, 0) : new Color(200, 100, 100);
        DrawScaledPixelText(spriteBatch, priceText, tooltipX + 10, tooltipY + 24, priceColor, 1);
    }

    /// <summary>
    /// Draw held item at mouse.
    /// </summary>
    private void DrawHeldItem(SpriteBatch spriteBatch)
    {
        if (!_isDragging || _heldItem == null)
            return;

        var mousePos = Mouse.GetState().Position;
        Color itemColor = GetItemColor(_heldItem);

        int itemSize = SlotSize - ItemInset * 2;
        var itemRect = new Rectangle(
            mousePos.X - itemSize / 2,
            mousePos.Y - itemSize / 2,
            itemSize,
            itemSize
        );

        Color dragColor = new Color((int)itemColor.R, (int)itemColor.G, (int)itemColor.B, 220);
        spriteBatch.Draw(_pixel, itemRect, dragColor);
        DrawRectBorder(spriteBatch, itemRect, Color.White);

        if (_heldItem is Material mat && mat.Quantity > 1)
        {
            string qtyStr = mat.Quantity.ToString();
            int qtyX = itemRect.X + itemRect.Width - qtyStr.Length * 6 - 2;
            int qtyY = itemRect.Y + itemRect.Height - 10;
            DrawScaledPixelText(spriteBatch, qtyStr, qtyX, qtyY, Color.White, 1);
        }
    }

    /// <summary>
    /// Draw gold display.
    /// </summary>
    private void DrawGoldDisplay(SpriteBatch spriteBatch, Viewport viewport)
    {
        string goldText = $"Gold: {_player.Gold}g";
        int goldX = 20;
        int goldY = viewport.Height - 130;
        DrawScaledPixelText(spriteBatch, goldText, goldX, goldY, new Color(255, 215, 0), 2);

        // Show pending value
        // BUG FIX (v2.15.1): Don't use CalculateTotalValue() - it includes LastShippedItem
        // which is the same as _binSlotItem (causes double counting while menu is open).
        // Instead, manually sum manifest + current UI slot.
        if (_activeBin != null)
        {
            // Sum committed manifest items only
            int manifestValue = 0;
            foreach (var item in _activeBin.ShippingManifest)
            {
                manifestValue += item.TotalValue;
            }

            // Add current buffer slot value (the item being edited in UI)
            int bufferValue = 0;
            if (_binSlotItem != null)
            {
                int qty = _binSlotItem is Material m ? m.Quantity : 1;
                bufferValue = _binSlotItem.SellPrice * qty;
            }

            int pendingValue = manifestValue + bufferValue;

            if (pendingValue > 0)
            {
                string pendingText = $"Pending: +{pendingValue}g";
                DrawScaledPixelText(spriteBatch, pendingText, goldX, goldY + 20, new Color(150, 255, 150), 1);
            }
        }
    }

    /// <summary>
    /// Get item color.
    /// </summary>
    private static Color GetItemColor(Item item)
    {
        return item.RegistryKey switch
        {
            "hoe" => new Color(139, 90, 43),
            "axe" => new Color(100, 100, 100),
            "pickaxe" => new Color(120, 120, 140),
            "watering_can" => new Color(80, 130, 200),
            "scythe" => new Color(180, 180, 100),
            "earth_wand" => new Color(180, 140, 60),
            "hydro_wand" => new Color(60, 180, 255),
            "wood" => new Color(139, 90, 43),
            "stone" => new Color(128, 128, 128),
            "fiber" => new Color(34, 139, 34),
            "corn" => new Color(255, 220, 80),
            "tomato" => new Color(220, 50, 50),
            "potato" => new Color(180, 140, 80),
            _ => item switch
            {
                Tool => new Color(100, 150, 255),
                Material => new Color(180, 160, 140),
                _ => new Color(200, 200, 200)
            }
        };
    }

    /// <summary>
    /// Draw rectangle border.
    /// </summary>
    private void DrawRectBorder(SpriteBatch spriteBatch, Rectangle rect, Color color)
    {
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), color);
    }

    /// <summary>
    /// Scaled pixel text.
    /// </summary>
    private void DrawScaledPixelText(SpriteBatch spriteBatch, string text, int x, int y, Color color, int scale)
    {
        int cursorX = x;
        int charWidth = 5 * scale;
        int spacing = scale;

        foreach (char c in text)
        {
            DrawScaledPixelChar(spriteBatch, c, cursorX, y, color, scale);
            cursorX += charWidth + spacing;
        }
    }

    /// <summary>
    /// Draw scaled pixel character.
    /// </summary>
    private void DrawScaledPixelChar(SpriteBatch spriteBatch, char c, int x, int y, Color color, int scale)
    {
        string[] pattern = GetCharPattern(c);

        for (int row = 0; row < 7; row++)
        {
            for (int col = 0; col < 5; col++)
            {
                if (col < pattern[row].Length && pattern[row][col] == '#')
                {
                    spriteBatch.Draw(_pixel, new Rectangle(
                        x + col * scale,
                        y + row * scale,
                        scale,
                        scale
                    ), color);
                }
            }
        }
    }

    /// <summary>
    /// Get character pattern.
    /// </summary>
    private static string[] GetCharPattern(char c) => c switch
    {
        '0' => new[] { " ### ", "#   #", "#  ##", "# # #", "##  #", "#   #", " ### " },
        '1' => new[] { "  #  ", " ##  ", "  #  ", "  #  ", "  #  ", "  #  ", " ### " },
        '2' => new[] { " ### ", "#   #", "    #", "  ## ", " #   ", "#    ", "#####" },
        '3' => new[] { " ### ", "#   #", "    #", "  ## ", "    #", "#   #", " ### " },
        '4' => new[] { "   # ", "  ## ", " # # ", "#  # ", "#####", "   # ", "   # " },
        '5' => new[] { "#####", "#    ", "#### ", "    #", "    #", "#   #", " ### " },
        '6' => new[] { " ### ", "#    ", "#### ", "#   #", "#   #", "#   #", " ### " },
        '7' => new[] { "#####", "    #", "   # ", "  #  ", " #   ", " #   ", " #   " },
        '8' => new[] { " ### ", "#   #", "#   #", " ### ", "#   #", "#   #", " ### " },
        '9' => new[] { " ### ", "#   #", "#   #", " ####", "    #", "   # ", " ##  " },
        'A' => new[] { " ### ", "#   #", "#   #", "#####", "#   #", "#   #", "#   #" },
        'B' => new[] { "#### ", "#   #", "#   #", "#### ", "#   #", "#   #", "#### " },
        'C' => new[] { " ### ", "#   #", "#    ", "#    ", "#    ", "#   #", " ### " },
        'D' => new[] { "#### ", "#   #", "#   #", "#   #", "#   #", "#   #", "#### " },
        'E' => new[] { "#####", "#    ", "#    ", "#### ", "#    ", "#    ", "#####" },
        'F' => new[] { "#####", "#    ", "#    ", "#### ", "#    ", "#    ", "#    " },
        'G' => new[] { " ### ", "#   #", "#    ", "# ###", "#   #", "#   #", " ### " },
        'H' => new[] { "#   #", "#   #", "#   #", "#####", "#   #", "#   #", "#   #" },
        'I' => new[] { " ### ", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", " ### " },
        'K' => new[] { "#   #", "#  # ", "# #  ", "##   ", "# #  ", "#  # ", "#   #" },
        'L' => new[] { "#    ", "#    ", "#    ", "#    ", "#    ", "#    ", "#####" },
        'N' => new[] { "#   #", "##  #", "# # #", "#  ##", "#   #", "#   #", "#   #" },
        'O' => new[] { " ### ", "#   #", "#   #", "#   #", "#   #", "#   #", " ### " },
        'P' => new[] { "#### ", "#   #", "#   #", "#### ", "#    ", "#    ", "#    " },
        'R' => new[] { "#### ", "#   #", "#   #", "#### ", "# #  ", "#  # ", "#   #" },
        'S' => new[] { " ####", "#    ", "#    ", " ### ", "    #", "    #", "#### " },
        'T' => new[] { "#####", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", "  #  " },
        'U' => new[] { "#   #", "#   #", "#   #", "#   #", "#   #", "#   #", " ### " },
        'a' => new[] { "     ", "     ", " ### ", "    #", " ####", "#   #", " ####" },
        'c' => new[] { "     ", "     ", " ### ", "#    ", "#    ", "#    ", " ### " },
        'd' => new[] { "    #", "    #", " ####", "#   #", "#   #", "#   #", " ####" },
        'e' => new[] { "     ", "     ", " ### ", "#   #", "#####", "#    ", " ### " },
        'f' => new[] { "  ## ", " #   ", "#### ", " #   ", " #   ", " #   ", " #   " },
        'g' => new[] { "     ", " ####", "#   #", "#   #", " ####", "    #", " ### " },
        'h' => new[] { "#    ", "#    ", "#### ", "#   #", "#   #", "#   #", "#   #" },
        'i' => new[] { "  #  ", "     ", " ##  ", "  #  ", "  #  ", "  #  ", " ### " },
        'l' => new[] { " ##  ", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", " ### " },
        'n' => new[] { "     ", "     ", "#### ", "#   #", "#   #", "#   #", "#   #" },
        'o' => new[] { "     ", "     ", " ### ", "#   #", "#   #", "#   #", " ### " },
        'p' => new[] { "     ", "#### ", "#   #", "#### ", "#    ", "#    ", "#    " },
        'r' => new[] { "     ", "     ", "# ## ", "##   ", "#    ", "#    ", "#    " },
        's' => new[] { "     ", "     ", " ####", "#    ", " ### ", "    #", "#### " },
        't' => new[] { " #   ", " #   ", "#### ", " #   ", " #   ", " #   ", "  ## " },
        'u' => new[] { "     ", "     ", "#   #", "#   #", "#   #", "#   #", " ####" },
        ' ' => new[] { "     ", "     ", "     ", "     ", "     ", "     ", "     " },
        ':' => new[] { "     ", "  #  ", "  #  ", "     ", "  #  ", "  #  ", "     " },
        '+' => new[] { "     ", "  #  ", "  #  ", "#####", "  #  ", "  #  ", "     " },
        '-' => new[] { "     ", "     ", "     ", "#####", "     ", "     ", "     " },
        _ => new[] { "     ", "     ", "     ", "     ", "     ", "     ", "     " }
    };
}
