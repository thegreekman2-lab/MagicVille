#nullable enable
using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MagicVille;

/// <summary>
/// Inventory menu UI with drag-and-drop functionality.
///
/// ARCHITECTURE:
/// - View-Model Separation: References Player.Inventory, doesn't store its own copy
/// - Input Mode Controller: Only active when GameState == Inventory (no click-through)
/// - Drag-and-Drop: Full pick-up, swap, and snap-back protection
///
/// RENDER ORDER:
/// 1. Background/Slots
/// 2. Items in Slots (except held item)
/// 3. Tooltip (hovered item name)
/// 4. Held Item at mouse position (floats above everything)
/// </summary>
public class InventoryMenu
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
    private const int HotbarY = 120; // Distance from bottom

    // === Slot rectangles (computed on draw) ===
    private readonly Rectangle[] _slotRects = new Rectangle[Inventory.HotbarSize];

    // === Drag-and-Drop state ===
    private Item? _heldItem;
    private int _sourceIndex = -1;
    private bool _isDragging;

    // === Mouse state tracking ===
    private MouseState _previousMouse;
    private int _hoveredSlot = -1;

    public InventoryMenu(Player player, Texture2D pixel, GraphicsDevice graphics)
    {
        _player = player;
        _pixel = pixel;
        _graphics = graphics;
    }

    /// <summary>
    /// Update drag-and-drop logic.
    /// Called only when GameState == Inventory.
    /// </summary>
    public void Update(MouseState mouse)
    {
        // Update hovered slot
        _hoveredSlot = GetSlotAtPosition(mouse.Position);

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
    /// Handle mouse button press - pick up item from slot.
    /// </summary>
    private void OnMouseDown(Point mousePos)
    {
        int slotIndex = GetSlotAtPosition(mousePos);

        // Not clicking on a slot
        if (slotIndex < 0)
            return;

        // Get item at this slot
        Item? item = PlayerInventory.GetSlot(slotIndex);

        // No item to pick up
        if (item == null)
            return;

        // Pick up the item
        _heldItem = item;
        _sourceIndex = slotIndex;
        _isDragging = true;

        // Remove from inventory (will be placed back on mouse up)
        PlayerInventory.SetSlot(slotIndex, null);

        Debug.WriteLine($"[InventoryMenu] Picked up {item.Name} from slot {slotIndex}");
    }

    /// <summary>
    /// Handle mouse button release - place or swap item.
    /// </summary>
    private void OnMouseUp(Point mousePos)
    {
        // Not dragging anything
        if (!_isDragging || _heldItem == null)
            return;

        int targetIndex = GetSlotAtPosition(mousePos);

        if (targetIndex >= 0)
        {
            // Valid target slot - swap or place
            Item? targetItem = PlayerInventory.GetSlot(targetIndex);

            // Place held item in target slot
            PlayerInventory.SetSlot(targetIndex, _heldItem);

            // If target had an item, put it in source slot (swap)
            if (targetItem != null)
            {
                PlayerInventory.SetSlot(_sourceIndex, targetItem);
                Debug.WriteLine($"[InventoryMenu] Swapped {_heldItem.Name} with {targetItem.Name}");
            }
            else
            {
                Debug.WriteLine($"[InventoryMenu] Placed {_heldItem.Name} in slot {targetIndex}");
            }
        }
        else
        {
            // Invalid target (outside UI) - snap back to source
            PlayerInventory.SetSlot(_sourceIndex, _heldItem);
            Debug.WriteLine($"[InventoryMenu] Snap-back: Returned {_heldItem.Name} to slot {_sourceIndex}");
        }

        // Clear drag state
        _heldItem = null;
        _sourceIndex = -1;
        _isDragging = false;
    }

    /// <summary>
    /// Cancel current drag operation and return item to source.
    /// Called when closing inventory menu.
    /// </summary>
    public void CancelDrag()
    {
        if (_isDragging && _heldItem != null && _sourceIndex >= 0)
        {
            PlayerInventory.SetSlot(_sourceIndex, _heldItem);
            Debug.WriteLine($"[InventoryMenu] Drag cancelled: Returned {_heldItem.Name} to slot {_sourceIndex}");
        }

        _heldItem = null;
        _sourceIndex = -1;
        _isDragging = false;
    }

    /// <summary>
    /// Get the slot index at a screen position, or -1 if not over any slot.
    /// </summary>
    private int GetSlotAtPosition(Point screenPos)
    {
        for (int i = 0; i < _slotRects.Length; i++)
        {
            if (_slotRects[i].Contains(screenPos))
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Draw the inventory menu.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, Viewport viewport)
    {
        // Compute slot positions (centered at bottom)
        ComputeSlotRects(viewport);

        spriteBatch.Begin(
            sortMode: SpriteSortMode.Deferred,
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.PointClamp
        );

        // === LAYER 1: Background panel ===
        DrawBackground(spriteBatch, viewport);

        // === LAYER 2: Slot backgrounds ===
        DrawSlots(spriteBatch);

        // === LAYER 3: Items in slots (except held item) ===
        DrawItems(spriteBatch);

        // === LAYER 4: Tooltip ===
        DrawTooltip(spriteBatch, viewport);

        // === LAYER 5: Held item at mouse (floats above everything) ===
        DrawHeldItem(spriteBatch);

        spriteBatch.End();
    }

    /// <summary>
    /// Compute slot rectangles based on viewport size.
    /// </summary>
    private void ComputeSlotRects(Viewport viewport)
    {
        int totalWidth = Inventory.HotbarSize * (SlotSize + SlotPadding) - SlotPadding;
        int startX = (viewport.Width - totalWidth) / 2;
        int startY = viewport.Height - HotbarY;

        for (int i = 0; i < Inventory.HotbarSize; i++)
        {
            int x = startX + i * (SlotSize + SlotPadding);
            _slotRects[i] = new Rectangle(x, startY, SlotSize, SlotSize);
        }
    }

    /// <summary>
    /// Draw the menu background panel.
    /// </summary>
    private void DrawBackground(SpriteBatch spriteBatch, Viewport viewport)
    {
        int totalWidth = Inventory.HotbarSize * (SlotSize + SlotPadding) - SlotPadding;
        int panelPadding = 20;
        int panelWidth = totalWidth + panelPadding * 2;
        int panelHeight = SlotSize + panelPadding * 2;
        int panelX = (viewport.Width - panelWidth) / 2;
        int panelY = viewport.Height - HotbarY - panelPadding;

        // Panel background
        var panelRect = new Rectangle(panelX, panelY, panelWidth, panelHeight);
        spriteBatch.Draw(_pixel, panelRect, new Color(30, 30, 40, 230));

        // Panel border
        DrawRectBorder(spriteBatch, panelRect, new Color(80, 80, 100));

        // Title
        string title = "INVENTORY";
        int titleWidth = title.Length * 6;
        int titleX = (viewport.Width - titleWidth) / 2;
        int titleY = panelY - 20;
        DrawPixelText(spriteBatch, title, titleX, titleY, Color.White);
    }

    /// <summary>
    /// Draw slot backgrounds.
    /// </summary>
    private void DrawSlots(SpriteBatch spriteBatch)
    {
        for (int i = 0; i < Inventory.HotbarSize; i++)
        {
            var rect = _slotRects[i];

            // Slot states
            bool isHovered = i == _hoveredSlot;
            bool isSource = i == _sourceIndex && _isDragging;
            bool isSelected = i == PlayerInventory.ActiveSlotIndex;

            // Border color based on state
            Color borderColor;
            if (isSource)
                borderColor = new Color(255, 200, 100); // Gold for source
            else if (isHovered)
                borderColor = new Color(150, 200, 255); // Light blue for hover
            else if (isSelected)
                borderColor = Color.White; // White for selected
            else
                borderColor = new Color(60, 60, 70); // Default dark

            // Draw border
            spriteBatch.Draw(_pixel, rect, borderColor);

            // Draw inner area
            var innerRect = new Rectangle(
                rect.X + SlotBorder,
                rect.Y + SlotBorder,
                rect.Width - SlotBorder * 2,
                rect.Height - SlotBorder * 2
            );

            Color innerColor = isHovered
                ? new Color(60, 60, 70)
                : new Color(40, 40, 50);

            spriteBatch.Draw(_pixel, innerRect, innerColor);

            // Slot number (1-9, 0 for slot 10)
            string slotNum = i == 9 ? "0" : (i + 1).ToString();
            int numX = rect.X + rect.Width - 10;
            int numY = rect.Y + 3;
            DrawPixelText(spriteBatch, slotNum, numX, numY, new Color(100, 100, 110));
        }
    }

    /// <summary>
    /// Draw items in their slots (except held item).
    /// </summary>
    private void DrawItems(SpriteBatch spriteBatch)
    {
        for (int i = 0; i < Inventory.HotbarSize; i++)
        {
            // Skip source slot while dragging (item is held)
            if (i == _sourceIndex && _isDragging)
                continue;

            Item? item = PlayerInventory.GetSlot(i);
            if (item == null)
                continue;

            var slotRect = _slotRects[i];
            DrawItemInSlot(spriteBatch, item, slotRect);
        }
    }

    /// <summary>
    /// Draw an item within a slot rectangle.
    /// </summary>
    private void DrawItemInSlot(SpriteBatch spriteBatch, Item item, Rectangle slotRect)
    {
        // Item color based on type
        Color itemColor = GetItemColor(item);

        // Item rectangle (inset from slot edges)
        var itemRect = new Rectangle(
            slotRect.X + ItemInset,
            slotRect.Y + ItemInset,
            slotRect.Width - ItemInset * 2,
            slotRect.Height - ItemInset * 2
        );

        spriteBatch.Draw(_pixel, itemRect, itemColor);

        // Quantity indicator for materials
        if (item is Material mat && mat.Quantity > 1)
        {
            string qtyStr = mat.Quantity.ToString();
            int qtyX = slotRect.X + slotRect.Width - qtyStr.Length * 6 - 4;
            int qtyY = slotRect.Y + slotRect.Height - 12;
            DrawPixelText(spriteBatch, qtyStr, qtyX, qtyY, Color.White);
        }
    }

    /// <summary>
    /// Draw tooltip for hovered item.
    /// Uses scaled text (2x) for better readability.
    /// </summary>
    private void DrawTooltip(SpriteBatch spriteBatch, Viewport viewport)
    {
        // No tooltip while dragging
        if (_isDragging)
            return;

        // No slot hovered
        if (_hoveredSlot < 0)
            return;

        Item? item = PlayerInventory.GetSlot(_hoveredSlot);
        if (item == null)
            return;

        // Get mouse position for tooltip placement
        var mousePos = Mouse.GetState().Position;

        // Text scale for readability
        const int textScale = 2;
        const int charWidth = 5 * textScale + textScale; // 5px * scale + spacing
        const int charHeight = 7 * textScale;

        // Build tooltip text - FULL item name, no truncation
        string name = item.Name;
        string? description = item.Description;

        // Calculate tooltip size based on scaled text
        int nameWidth = name.Length * charWidth;
        int descWidth = !string.IsNullOrEmpty(description) ? description.Length * charWidth : 0;
        int tooltipWidth = Math.Max(nameWidth, descWidth) + 20;
        int tooltipHeight = string.IsNullOrEmpty(description) ? charHeight + 16 : charHeight * 2 + 24;

        // Position near mouse cursor (offset to the right)
        int tooltipX = mousePos.X + 20;
        int tooltipY = mousePos.Y;

        // Clamp to screen bounds
        if (tooltipX + tooltipWidth > viewport.Width - 4)
            tooltipX = mousePos.X - tooltipWidth - 10; // Flip to left side
        tooltipX = Math.Max(4, tooltipX);
        tooltipY = Math.Clamp(tooltipY, 4, viewport.Height - tooltipHeight - 4);

        // Draw background (dark with transparency)
        var tooltipRect = new Rectangle(tooltipX, tooltipY, tooltipWidth, tooltipHeight);
        spriteBatch.Draw(_pixel, tooltipRect, new Color(0, 0, 0, 200));
        DrawRectBorder(spriteBatch, tooltipRect, new Color(150, 150, 180));

        // Draw FULL item name (scaled)
        DrawScaledPixelText(spriteBatch, name, tooltipX + 10, tooltipY + 8, Color.White, textScale);

        // Draw description (scaled, gray)
        if (!string.IsNullOrEmpty(description))
        {
            DrawScaledPixelText(spriteBatch, description, tooltipX + 10, tooltipY + 8 + charHeight + 4, new Color(180, 180, 180), textScale);
        }
    }

    /// <summary>
    /// Draw the held item following the mouse cursor.
    /// </summary>
    private void DrawHeldItem(SpriteBatch spriteBatch)
    {
        if (!_isDragging || _heldItem == null)
            return;

        var mousePos = Mouse.GetState().Position;
        Color itemColor = GetItemColor(_heldItem);

        // Draw item centered on mouse
        int itemSize = SlotSize - ItemInset * 2;
        var itemRect = new Rectangle(
            mousePos.X - itemSize / 2,
            mousePos.Y - itemSize / 2,
            itemSize,
            itemSize
        );

        // Slight transparency to show it's being dragged
        Color dragColor = new Color((int)itemColor.R, (int)itemColor.G, (int)itemColor.B, 220);
        spriteBatch.Draw(_pixel, itemRect, dragColor);

        // Draw border around held item
        DrawRectBorder(spriteBatch, itemRect, Color.White);

        // Quantity for materials
        if (_heldItem is Material mat && mat.Quantity > 1)
        {
            string qtyStr = mat.Quantity.ToString();
            int qtyX = itemRect.X + itemRect.Width - qtyStr.Length * 6 - 2;
            int qtyY = itemRect.Y + itemRect.Height - 10;
            DrawPixelText(spriteBatch, qtyStr, qtyX, qtyY, Color.White);
        }
    }

    /// <summary>
    /// Get color for an item based on its type/registry key.
    /// </summary>
    private static Color GetItemColor(Item item)
    {
        return item.RegistryKey switch
        {
            // Standard Tools
            "hoe" => new Color(139, 90, 43),
            "axe" => new Color(100, 100, 100),
            "pickaxe" => new Color(120, 120, 140),
            "watering_can" => new Color(80, 130, 200),
            "scythe" => new Color(180, 180, 100),
            // Magic Wands
            "earth_wand" => new Color(180, 140, 60),
            "hydro_wand" => new Color(60, 180, 255),
            // Materials
            "wood" => new Color(139, 90, 43),
            "stone" => new Color(128, 128, 128),
            "fiber" => new Color(34, 139, 34),
            "coal" => new Color(30, 30, 30),
            "copper_ore" => new Color(184, 115, 51),
            // Harvest Items
            "corn" => new Color(255, 220, 80),
            "tomato" => new Color(220, 50, 50),
            "potato" => new Color(180, 140, 80),
            "carrot" => new Color(255, 140, 0),
            "wheat" => new Color(220, 190, 100),
            _ => item switch
            {
                Tool => new Color(100, 150, 255),
                Material => new Color(180, 160, 140),
                _ => new Color(200, 200, 200)
            }
        };
    }

    /// <summary>
    /// Draw a 1px border around a rectangle.
    /// </summary>
    private void DrawRectBorder(SpriteBatch spriteBatch, Rectangle rect, Color color)
    {
        // Top
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), color);
        // Bottom
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), color);
        // Left
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), color);
        // Right
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), color);
    }

    /// <summary>
    /// Simple pixel-based text rendering (1x scale).
    /// </summary>
    private void DrawPixelText(SpriteBatch spriteBatch, string text, int x, int y, Color color)
    {
        DrawScaledPixelText(spriteBatch, text, x, y, color, 1);
    }

    /// <summary>
    /// Scaled pixel-based text rendering.
    /// Scale of 2 makes each pixel 2x2, etc.
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
    /// Draw a single character using pixel patterns with scaling.
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
    /// Get 5x7 pixel pattern for a character.
    /// Supports uppercase, lowercase (rendered as small caps), digits, and punctuation.
    /// </summary>
    private static string[] GetCharPattern(char c) => c switch
    {
        // Digits
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

        // Uppercase letters
        'A' => new[] { " ### ", "#   #", "#   #", "#####", "#   #", "#   #", "#   #" },
        'B' => new[] { "#### ", "#   #", "#   #", "#### ", "#   #", "#   #", "#### " },
        'C' => new[] { " ### ", "#   #", "#    ", "#    ", "#    ", "#   #", " ### " },
        'D' => new[] { "#### ", "#   #", "#   #", "#   #", "#   #", "#   #", "#### " },
        'E' => new[] { "#####", "#    ", "#    ", "#### ", "#    ", "#    ", "#####" },
        'F' => new[] { "#####", "#    ", "#    ", "#### ", "#    ", "#    ", "#    " },
        'G' => new[] { " ### ", "#   #", "#    ", "# ###", "#   #", "#   #", " ### " },
        'H' => new[] { "#   #", "#   #", "#   #", "#####", "#   #", "#   #", "#   #" },
        'I' => new[] { " ### ", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", " ### " },
        'J' => new[] { "  ###", "   # ", "   # ", "   # ", "#  # ", "#  # ", " ##  " },
        'K' => new[] { "#   #", "#  # ", "# #  ", "##   ", "# #  ", "#  # ", "#   #" },
        'L' => new[] { "#    ", "#    ", "#    ", "#    ", "#    ", "#    ", "#####" },
        'M' => new[] { "#   #", "## ##", "# # #", "#   #", "#   #", "#   #", "#   #" },
        'N' => new[] { "#   #", "##  #", "# # #", "#  ##", "#   #", "#   #", "#   #" },
        'O' => new[] { " ### ", "#   #", "#   #", "#   #", "#   #", "#   #", " ### " },
        'P' => new[] { "#### ", "#   #", "#   #", "#### ", "#    ", "#    ", "#    " },
        'Q' => new[] { " ### ", "#   #", "#   #", "#   #", "# # #", "#  # ", " ## #" },
        'R' => new[] { "#### ", "#   #", "#   #", "#### ", "# #  ", "#  # ", "#   #" },
        'S' => new[] { " ####", "#    ", "#    ", " ### ", "    #", "    #", "#### " },
        'T' => new[] { "#####", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", "  #  " },
        'U' => new[] { "#   #", "#   #", "#   #", "#   #", "#   #", "#   #", " ### " },
        'V' => new[] { "#   #", "#   #", "#   #", "#   #", "#   #", " # # ", "  #  " },
        'W' => new[] { "#   #", "#   #", "#   #", "#   #", "# # #", "## ##", "#   #" },
        'X' => new[] { "#   #", "#   #", " # # ", "  #  ", " # # ", "#   #", "#   #" },
        'Y' => new[] { "#   #", "#   #", " # # ", "  #  ", "  #  ", "  #  ", "  #  " },
        'Z' => new[] { "#####", "    #", "   # ", "  #  ", " #   ", "#    ", "#####" },

        // Lowercase letters (rendered slightly smaller/different where practical)
        'a' => new[] { "     ", "     ", " ### ", "    #", " ####", "#   #", " ####" },
        'b' => new[] { "#    ", "#    ", "#### ", "#   #", "#   #", "#   #", "#### " },
        'c' => new[] { "     ", "     ", " ### ", "#    ", "#    ", "#    ", " ### " },
        'd' => new[] { "    #", "    #", " ####", "#   #", "#   #", "#   #", " ####" },
        'e' => new[] { "     ", "     ", " ### ", "#   #", "#####", "#    ", " ### " },
        'f' => new[] { "  ## ", " #   ", "#### ", " #   ", " #   ", " #   ", " #   " },
        'g' => new[] { "     ", " ####", "#   #", "#   #", " ####", "    #", " ### " },
        'h' => new[] { "#    ", "#    ", "#### ", "#   #", "#   #", "#   #", "#   #" },
        'i' => new[] { "  #  ", "     ", " ##  ", "  #  ", "  #  ", "  #  ", " ### " },
        'j' => new[] { "   # ", "     ", "  ## ", "   # ", "   # ", "#  # ", " ##  " },
        'k' => new[] { "#    ", "#    ", "#  # ", "# #  ", "##   ", "# #  ", "#  # " },
        'l' => new[] { " ##  ", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", " ### " },
        'm' => new[] { "     ", "     ", "## # ", "# # #", "# # #", "#   #", "#   #" },
        'n' => new[] { "     ", "     ", "#### ", "#   #", "#   #", "#   #", "#   #" },
        'o' => new[] { "     ", "     ", " ### ", "#   #", "#   #", "#   #", " ### " },
        'p' => new[] { "     ", "#### ", "#   #", "#### ", "#    ", "#    ", "#    " },
        'q' => new[] { "     ", " ####", "#   #", " ####", "    #", "    #", "    #" },
        'r' => new[] { "     ", "     ", "# ## ", "##   ", "#    ", "#    ", "#    " },
        's' => new[] { "     ", "     ", " ####", "#    ", " ### ", "    #", "#### " },
        't' => new[] { " #   ", " #   ", "#### ", " #   ", " #   ", " #   ", "  ## " },
        'u' => new[] { "     ", "     ", "#   #", "#   #", "#   #", "#   #", " ####" },
        'v' => new[] { "     ", "     ", "#   #", "#   #", "#   #", " # # ", "  #  " },
        'w' => new[] { "     ", "     ", "#   #", "#   #", "# # #", "# # #", " # # " },
        'x' => new[] { "     ", "     ", "#   #", " # # ", "  #  ", " # # ", "#   #" },
        'y' => new[] { "     ", "#   #", "#   #", " ####", "    #", "   # ", "###  " },
        'z' => new[] { "     ", "     ", "#####", "   # ", "  #  ", " #   ", "#####" },

        // Punctuation and symbols
        ' ' => new[] { "     ", "     ", "     ", "     ", "     ", "     ", "     " },
        '.' => new[] { "     ", "     ", "     ", "     ", "     ", "  #  ", "  #  " },
        ',' => new[] { "     ", "     ", "     ", "     ", "  #  ", "  #  ", " #   " },
        ':' => new[] { "     ", "  #  ", "  #  ", "     ", "  #  ", "  #  ", "     " },
        ';' => new[] { "     ", "  #  ", "  #  ", "     ", "  #  ", "  #  ", " #   " },
        '!' => new[] { "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", "     ", "  #  " },
        '?' => new[] { " ### ", "#   #", "    #", "   # ", "  #  ", "     ", "  #  " },
        '-' => new[] { "     ", "     ", "     ", "#####", "     ", "     ", "     " },
        '+' => new[] { "     ", "  #  ", "  #  ", "#####", "  #  ", "  #  ", "     " },
        '=' => new[] { "     ", "     ", "#####", "     ", "#####", "     ", "     " },
        '(' => new[] { "  #  ", " #   ", "#    ", "#    ", "#    ", " #   ", "  #  " },
        ')' => new[] { "  #  ", "   # ", "    #", "    #", "    #", "   # ", "  #  " },
        '[' => new[] { " ### ", " #   ", " #   ", " #   ", " #   ", " #   ", " ### " },
        ']' => new[] { " ### ", "   # ", "   # ", "   # ", "   # ", "   # ", " ### " },
        '/' => new[] { "    #", "    #", "   # ", "  #  ", " #   ", "#    ", "#    " },
        '\'' => new[] { "  #  ", "  #  ", " #   ", "     ", "     ", "     ", "     " },
        '"' => new[] { " # # ", " # # ", "     ", "     ", "     ", "     ", "     " },

        // Default: empty
        _ => new[] { "     ", "     ", "     ", "     ", "     ", "     ", "     " }
    };
}
