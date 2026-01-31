#nullable enable
using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MagicVille;

/// <summary>
/// Storage menu UI for chest interaction.
/// Click items to transfer between player inventory and chest.
///
/// LAYOUT:
/// - Top: Chest storage grid (capacity-based, e.g., 9x4 = 36 slots)
/// - Bottom: Player inventory (10-slot hotbar)
///
/// v2.18: Title text, tooltips, and rendering via UIRenderer.
/// </summary>
public class StorageMenu
{
    private readonly Player _player;
    private Inventory PlayerInventory => _player.Inventory;

    private readonly Texture2D _pixel;
    private readonly GraphicsDevice _graphics;

    // Layout constants
    private const int SlotSize = 50;
    private const int SlotPadding = 4;
    private const int SlotBorder = 2;
    private const int ItemInset = 6;
    private const int ChestColumns = 9;
    private const int SectionGap = 20;

    // Active chest reference
    private Chest? _activeChest;

    // Slot rectangles (computed on Open)
    private Rectangle[] _chestSlotRects = Array.Empty<Rectangle>();
    private Rectangle[] _inventorySlotRects = new Rectangle[Inventory.HotbarSize];
    private Rectangle _panelRect;

    // Mouse state
    private MouseState _previousMouse;
    private int _hoveredChestSlot = -1;
    private int _hoveredInventorySlot = -1;

    public StorageMenu(Player player, Texture2D pixel, GraphicsDevice graphics)
    {
        _player = player;
        _pixel = pixel;
        _graphics = graphics;
    }

    public void Open(Chest chest)
    {
        _activeChest = chest;
        _hoveredChestSlot = -1;
        _hoveredInventorySlot = -1;

        // Compute slot rectangles based on chest capacity
        int chestRows = (chest.Capacity + ChestColumns - 1) / ChestColumns;
        _chestSlotRects = new Rectangle[chest.Capacity];

        // Calculate panel dimensions
        int chestGridWidth = ChestColumns * (SlotSize + SlotPadding) - SlotPadding;
        int chestGridHeight = chestRows * (SlotSize + SlotPadding) - SlotPadding;
        int invGridWidth = Inventory.HotbarSize * (SlotSize + SlotPadding) - SlotPadding;
        int panelWidth = Math.Max(chestGridWidth, invGridWidth) + 40;
        int panelHeight = chestGridHeight + SectionGap + SlotSize + 80;

        // Center panel on screen
        var viewport = _graphics.Viewport;
        int panelX = (viewport.Width - panelWidth) / 2;
        int panelY = (viewport.Height - panelHeight) / 2;
        _panelRect = new Rectangle(panelX, panelY, panelWidth, panelHeight);

        // Compute chest slot positions
        int chestStartX = panelX + (panelWidth - chestGridWidth) / 2;
        int chestStartY = panelY + 40;

        for (int i = 0; i < chest.Capacity; i++)
        {
            int col = i % ChestColumns;
            int row = i / ChestColumns;
            _chestSlotRects[i] = new Rectangle(
                chestStartX + col * (SlotSize + SlotPadding),
                chestStartY + row * (SlotSize + SlotPadding),
                SlotSize, SlotSize
            );
        }

        // Compute inventory slot positions (centered below chest)
        int invStartX = panelX + (panelWidth - invGridWidth) / 2;
        int invStartY = chestStartY + chestGridHeight + SectionGap;

        for (int i = 0; i < Inventory.HotbarSize; i++)
        {
            _inventorySlotRects[i] = new Rectangle(
                invStartX + i * (SlotSize + SlotPadding),
                invStartY,
                SlotSize, SlotSize
            );
        }

        Debug.WriteLine($"[StorageMenu] Opened chest with {chest.Capacity} slots, {chest.ItemCount} items");
    }

    public void Close()
    {
        _activeChest = null;
        Debug.WriteLine("[StorageMenu] Closed");
    }

    public void Update(MouseState mouse)
    {
        if (_activeChest == null) return;

        // Update hover states
        _hoveredChestSlot = -1;
        _hoveredInventorySlot = -1;

        Point mousePos = mouse.Position;

        // Check chest slots
        for (int i = 0; i < _chestSlotRects.Length; i++)
        {
            if (_chestSlotRects[i].Contains(mousePos))
            {
                _hoveredChestSlot = i;
                break;
            }
        }

        // Check inventory slots
        for (int i = 0; i < _inventorySlotRects.Length; i++)
        {
            if (_inventorySlotRects[i].Contains(mousePos))
            {
                _hoveredInventorySlot = i;
                break;
            }
        }

        // Handle click to transfer
        bool clicked = mouse.LeftButton == ButtonState.Pressed &&
                       _previousMouse.LeftButton == ButtonState.Released;

        if (clicked)
        {
            if (_hoveredInventorySlot >= 0)
            {
                // Transfer from inventory to chest
                TransferToChest(_hoveredInventorySlot);
            }
            else if (_hoveredChestSlot >= 0)
            {
                // Transfer from chest to inventory
                TransferToInventory(_hoveredChestSlot);
            }
        }

        _previousMouse = mouse;
    }

    private void TransferToChest(int invSlot)
    {
        if (_activeChest == null) return;

        var item = PlayerInventory.GetSlot(invSlot);
        if (item == null) return;

        // Try to add to chest (handles stacking)
        if (_activeChest.AddItem(item))
        {
            PlayerInventory.SetSlot(invSlot, null);
            _activeChest.UpdateVisuals();
            Debug.WriteLine($"[StorageMenu] Moved {item.Name} to chest");
        }
        else
        {
            Debug.WriteLine($"[StorageMenu] Chest is full, can't add {item.Name}");
        }
    }

    private void TransferToInventory(int chestSlot)
    {
        if (_activeChest == null) return;

        var item = _activeChest.GetSlot(chestSlot);
        if (item == null) return;

        // Try to add to player inventory (handles stacking)
        if (PlayerInventory.AddItem(item))
        {
            _activeChest.SetSlot(chestSlot, null);
            _activeChest.UpdateVisuals();
            Debug.WriteLine($"[StorageMenu] Moved {item.Name} to inventory");
        }
        else
        {
            Debug.WriteLine($"[StorageMenu] Inventory is full, can't take {item.Name}");
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (_activeChest == null) return;

        var viewport = _graphics.Viewport;

        // Draw semi-transparent background
        spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewport.Width, viewport.Height),
            new Color(0, 0, 0, 180));

        // Draw panel background
        spriteBatch.Draw(_pixel, _panelRect, new Color(60, 50, 40));
        UIRenderer.DrawBorder(spriteBatch, _pixel, _panelRect, new Color(100, 80, 60), 3);

        // Draw title
        string title = "CHEST";
        int titleWidth = UIRenderer.MeasureString(title, 2);
        int titleX = _panelRect.X + (_panelRect.Width - titleWidth) / 2;
        int titleY = _panelRect.Y + 10;
        UIRenderer.DrawString(spriteBatch, _pixel, title, titleX, titleY, Color.White, 2);

        // Draw chest slots
        for (int i = 0; i < _chestSlotRects.Length; i++)
        {
            bool hovered = (i == _hoveredChestSlot);
            DrawSlot(spriteBatch, _chestSlotRects[i], _activeChest.GetSlot(i), hovered);
        }

        // Draw inventory slots
        for (int i = 0; i < _inventorySlotRects.Length; i++)
        {
            bool hovered = (i == _hoveredInventorySlot);
            DrawSlot(spriteBatch, _inventorySlotRects[i], PlayerInventory.GetSlot(i), hovered);
        }

        // Draw tooltip for hovered item
        Item? hoveredItem = GetHoveredItem();
        if (hoveredItem != null)
        {
            UIRenderer.DrawTooltip(spriteBatch, _pixel, hoveredItem,
                Mouse.GetState().Position, viewport);
        }
    }

    /// <summary>
    /// Get the item currently being hovered over (if any).
    /// </summary>
    private Item? GetHoveredItem()
    {
        if (_activeChest == null) return null;

        if (_hoveredChestSlot >= 0)
            return _activeChest.GetSlot(_hoveredChestSlot);

        if (_hoveredInventorySlot >= 0)
            return PlayerInventory.GetSlot(_hoveredInventorySlot);

        return null;
    }

    private void DrawSlot(SpriteBatch spriteBatch, Rectangle rect, Item? item, bool hovered)
    {
        // Slot background
        Color bgColor = hovered ? new Color(80, 70, 60) : new Color(40, 35, 30);
        spriteBatch.Draw(_pixel, rect, bgColor);

        // Slot border
        Color borderColor = hovered ? new Color(200, 180, 140) : new Color(100, 90, 70);
        UIRenderer.DrawBorder(spriteBatch, _pixel, rect, borderColor, SlotBorder);

        // Draw item if present
        if (item != null)
        {
            var itemRect = new Rectangle(
                rect.X + ItemInset,
                rect.Y + ItemInset,
                rect.Width - ItemInset * 2,
                rect.Height - ItemInset * 2
            );

            // Item color from centralized palette
            Color itemColor = UIRenderer.GetItemColor(item);
            spriteBatch.Draw(_pixel, itemRect, itemColor);

            // Quantity indicator for stackables
            if (item is Material m && m.Quantity > 1)
            {
                string qtyStr = m.Quantity.ToString();
                int qtyX = rect.Right - UIRenderer.MeasureString(qtyStr, 1) - 4;
                int qtyY = rect.Bottom - 12;
                UIRenderer.DrawString(spriteBatch, _pixel, qtyStr, qtyX, qtyY, Color.White, 1);
            }
        }
    }
}
