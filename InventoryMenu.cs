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
/// 3. Tooltip (hovered item name + description)
/// 4. Held Item at mouse position (floats above everything)
///
/// v2.18: Font/border/color rendering delegated to UIRenderer.
/// Tooltip kept local (shows Name + Description, unlike UIRenderer's Name + Price).
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
        UIRenderer.DrawBorder(spriteBatch, _pixel, panelRect, new Color(80, 80, 100), 1);

        // Title (left side)
        string title = "INVENTORY";
        int titleY = panelY - 20;
        UIRenderer.DrawString(spriteBatch, _pixel, title, panelX + 10, titleY, Color.White, 1);

        // Gold display (right side, gold/yellow color)
        string goldText = $"{_player.Gold}g";
        int goldTextWidth = UIRenderer.MeasureString(goldText, 1);
        int goldX = panelX + panelWidth - goldTextWidth - 10;
        UIRenderer.DrawString(spriteBatch, _pixel, goldText, goldX, titleY, new Color(255, 215, 0), 1);
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
            UIRenderer.DrawString(spriteBatch, _pixel, slotNum, numX, numY, new Color(100, 100, 110), 1);
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
        Color itemColor = UIRenderer.GetItemColor(item);

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
            int qtyX = slotRect.X + slotRect.Width - UIRenderer.MeasureString(qtyStr, 1) - 4;
            int qtyY = slotRect.Y + slotRect.Height - 12;
            UIRenderer.DrawString(spriteBatch, _pixel, qtyStr, qtyX, qtyY, Color.White, 1);
        }
    }

    /// <summary>
    /// Draw tooltip for hovered item.
    /// Shows Name + Description (different from UIRenderer.DrawTooltip which shows Name + Price).
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
        int charHeight = UIRenderer.MeasureHeight(textScale);

        // Build tooltip text - FULL item name, no truncation
        string name = item.Name;
        string? description = item.Description;

        // Calculate tooltip size based on scaled text
        int nameWidth = UIRenderer.MeasureString(name, textScale);
        int descWidth = !string.IsNullOrEmpty(description) ? UIRenderer.MeasureString(description, textScale) : 0;
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
        UIRenderer.DrawBorder(spriteBatch, _pixel, tooltipRect, new Color(150, 150, 180), 1);

        // Draw FULL item name (scaled)
        UIRenderer.DrawString(spriteBatch, _pixel, name, tooltipX + 10, tooltipY + 8, Color.White, textScale);

        // Draw description (scaled, gray)
        if (!string.IsNullOrEmpty(description))
        {
            UIRenderer.DrawString(spriteBatch, _pixel, description, tooltipX + 10, tooltipY + 8 + charHeight + 4, new Color(180, 180, 180), textScale);
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
        Color itemColor = UIRenderer.GetItemColor(_heldItem);

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
        UIRenderer.DrawBorder(spriteBatch, _pixel, itemRect, Color.White, 1);

        // Quantity for materials
        if (_heldItem is Material mat && mat.Quantity > 1)
        {
            string qtyStr = mat.Quantity.ToString();
            int qtyX = itemRect.X + itemRect.Width - UIRenderer.MeasureString(qtyStr, 1) - 2;
            int qtyY = itemRect.Y + itemRect.Height - 10;
            UIRenderer.DrawString(spriteBatch, _pixel, qtyStr, qtyX, qtyY, Color.White, 1);
        }
    }
}
