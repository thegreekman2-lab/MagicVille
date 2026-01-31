#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MagicVille;

/// <summary>
/// High-level game states for the FSM.
/// </summary>
public enum GameState
{
    Playing,    // Normal gameplay - world updates, player moves
    Inventory,  // Inventory menu open - world paused, UI active
    Shipping,   // Shipping bin menu open - drag items to sell
    Dialogue,   // Dialogue box displayed - world paused, reading text
    Storage     // Storage menu open - transfer items to/from chest
}

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private WorldManager _world = null!;
    private InventoryMenu _inventoryMenu = null!;
    private ShippingMenu _shippingMenu = null!;
    private StorageMenu _storageMenu = null!;
    private Texture2D _pixel = null!;

    // Input tracking for state transitions
    private KeyboardState _previousKeyboard;

    // FSM: Current game state
    public GameState CurrentState { get; private set; } = GameState.Playing;

    // Reference to active shipping bin (set when opening shipping menu)
    private ShippingBin? _activeShippingBin;

    // Reference to active chest (set when opening storage menu)
    private Chest? _activeChest;

    // Animation timer for UI effects
    private double _totalTime;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        _graphics.PreferredBackBufferWidth = 800;
        _graphics.PreferredBackBufferHeight = 480;

        IsMouseVisible = true;

        // Enable window resizing
        Window.AllowUserResizing = true;
        Window.ClientSizeChanged += OnClientSizeChanged;
    }

    /// <summary>
    /// Handle window resize events.
    /// Updates the graphics buffer to match the new window size.
    /// </summary>
    private void OnClientSizeChanged(object? sender, EventArgs e)
    {
        // Get new window dimensions
        int newWidth = Window.ClientBounds.Width;
        int newHeight = Window.ClientBounds.Height;

        // Ignore invalid sizes (can happen during minimize)
        if (newWidth <= 0 || newHeight <= 0)
            return;

        // Update graphics buffer to match window size
        _graphics.PreferredBackBufferWidth = newWidth;
        _graphics.PreferredBackBufferHeight = newHeight;
        _graphics.ApplyChanges();
    }

    protected override void Initialize()
    {
        _world = new WorldManager();
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _world.Initialize(GraphicsDevice);

        // Get pixel texture for overlays
        _pixel = _world.GetPixelTexture();

        // Initialize inventory menu with player reference (View-Model binding)
        _inventoryMenu = new InventoryMenu(_world.Player, _pixel, GraphicsDevice);

        // Initialize shipping menu
        _shippingMenu = new ShippingMenu(_world.Player, _pixel, GraphicsDevice);

        // Initialize storage menu
        _storageMenu = new StorageMenu(_world.Player, _pixel, GraphicsDevice);

        // Subscribe to world events
        _world.OnOpenShippingMenu += OpenShippingMenu;
        _world.OnOpenDialogue += OpenDialogue;
        _world.OnOpenStorage += OpenStorageMenu;
    }

    protected override void Update(GameTime gameTime)
    {
        _totalTime += gameTime.ElapsedGameTime.TotalSeconds;

        var keyboard = Keyboard.GetState();

        // === FSM: State-based update logic ===
        switch (CurrentState)
        {
            case GameState.Playing:
                UpdatePlaying(gameTime, keyboard);
                break;

            case GameState.Inventory:
                UpdateInventory(gameTime, keyboard);
                break;

            case GameState.Shipping:
                UpdateShipping(gameTime, keyboard);
                break;

            case GameState.Dialogue:
                UpdateDialogue(gameTime, keyboard);
                break;

            case GameState.Storage:
                UpdateStorage(gameTime, keyboard);
                break;
        }

        _previousKeyboard = keyboard;
        base.Update(gameTime);
    }

    /// <summary>
    /// Update logic for Playing state.
    /// World and player update normally.
    /// </summary>
    private void UpdatePlaying(GameTime gameTime, KeyboardState keyboard)
    {
        // Check for state transition to Inventory
        if (IsKeyPressed(keyboard, Keys.E) || IsKeyPressed(keyboard, Keys.Tab))
        {
            CurrentState = GameState.Inventory;
            return;
        }

        // Normal gameplay updates
        _world.Update(gameTime);
    }

    /// <summary>
    /// Update logic for Inventory state.
    /// World is paused, only inventory UI updates.
    /// </summary>
    private void UpdateInventory(GameTime gameTime, KeyboardState keyboard)
    {
        // Check for state transition back to Playing
        if (IsKeyPressed(keyboard, Keys.E) ||
            IsKeyPressed(keyboard, Keys.Tab) ||
            IsKeyPressed(keyboard, Keys.Escape))
        {
            // Cancel any held item before closing
            _inventoryMenu.CancelDrag();
            CurrentState = GameState.Playing;
            return;
        }

        // Update inventory menu (handles drag-and-drop)
        _inventoryMenu.Update(Mouse.GetState());
    }

    /// <summary>
    /// Update logic for Shipping state.
    /// World is paused, shipping menu handles drag-and-drop to sell items.
    /// </summary>
    private void UpdateShipping(GameTime gameTime, KeyboardState keyboard)
    {
        // Check for state transition back to Playing
        if (IsKeyPressed(keyboard, Keys.E) ||
            IsKeyPressed(keyboard, Keys.Tab) ||
            IsKeyPressed(keyboard, Keys.Escape))
        {
            // Finalize shipping (move bin slot item to manifest)
            _shippingMenu.FinalizeAndClose(_activeShippingBin);
            _activeShippingBin = null;
            CurrentState = GameState.Playing;
            return;
        }

        // Update shipping menu (handles drag-and-drop)
        _shippingMenu.Update(Mouse.GetState());
    }

    /// <summary>
    /// Update logic for Storage state.
    /// World is paused, storage menu handles item transfers.
    /// </summary>
    private void UpdateStorage(GameTime gameTime, KeyboardState keyboard)
    {
        // Check for state transition back to Playing
        if (IsKeyPressed(keyboard, Keys.E) ||
            IsKeyPressed(keyboard, Keys.Tab) ||
            IsKeyPressed(keyboard, Keys.Escape))
        {
            _storageMenu.Close();
            _activeChest = null;
            CurrentState = GameState.Playing;
            return;
        }

        // Update storage menu (handles click-to-transfer)
        _storageMenu.Update(Mouse.GetState());
    }

    /// <summary>
    /// Update logic for Dialogue state.
    /// World is paused, dialogue box handles typewriter and input.
    /// </summary>
    private void UpdateDialogue(GameTime gameTime, KeyboardState keyboard)
    {
        // DialogueSystem handles its own input and closing
        DialogueSystem.Update(gameTime);

        // If dialogue closed itself, we're already back to Playing (via callback)
    }

    /// <summary>
    /// Open the shipping menu for a specific shipping bin.
    /// Called by WorldManager when player interacts with ShippingBin.
    /// </summary>
    public void OpenShippingMenu(ShippingBin bin)
    {
        _activeShippingBin = bin;
        _shippingMenu.Open(bin);
        CurrentState = GameState.Shipping;
    }

    /// <summary>
    /// Open a dialogue box with the specified text.
    /// Called by WorldManager when player interacts with Sign or NPC.
    /// </summary>
    public void OpenDialogue(string text)
    {
        DialogueSystem.Show(text, () =>
        {
            // Callback when dialogue closes
            CurrentState = GameState.Playing;
        });
        CurrentState = GameState.Dialogue;
    }

    /// <summary>
    /// Open the storage menu for a specific chest.
    /// Called by WorldManager when player interacts with Chest.
    /// </summary>
    public void OpenStorageMenu(Chest chest)
    {
        _activeChest = chest;
        _storageMenu.Open(chest);
        CurrentState = GameState.Storage;
    }

    /// <summary>
    /// Check if a key was just pressed this frame.
    /// </summary>
    private bool IsKeyPressed(KeyboardState current, Keys key)
    {
        return current.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);

        // === LAYER 1: World (tiles, objects, player) ===
        // Always draw world (visible as frozen background when paused)
        _world.DrawWorld(_spriteBatch);

        // === LAYER 2: Night Overlay (day/night atmosphere) ===
        DrawNightOverlay();

        // === LAYER 3: UI (clock, hotbar) ===
        _world.DrawUI(_spriteBatch);

        // === LAYER 4: Transition Fade ===
        var viewport = GraphicsDevice.Viewport;
        var screenRect = new Rectangle(0, 0, viewport.Width, viewport.Height);
        _world.Transition.Draw(_spriteBatch, _pixel, screenRect);

        // === LAYER 5: Menu Overlays ===
        if (CurrentState == GameState.Inventory)
        {
            DrawInventoryOverlay(viewport);
        }
        else if (CurrentState == GameState.Shipping)
        {
            DrawShippingOverlay(viewport);
        }
        else if (CurrentState == GameState.Dialogue)
        {
            DrawDialogueOverlay(viewport);
        }
        else if (CurrentState == GameState.Storage)
        {
            DrawStorageOverlay(viewport);
        }

        base.Draw(gameTime);
    }

    /// <summary>
    /// Draw the shipping menu overlay.
    /// </summary>
    private void DrawShippingOverlay(Viewport viewport)
    {
        _spriteBatch.Begin(
            sortMode: SpriteSortMode.Deferred,
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.PointClamp
        );

        // Dim background
        var screenRect = new Rectangle(0, 0, viewport.Width, viewport.Height);
        _spriteBatch.Draw(_pixel, screenRect, new Color(0, 0, 0, 150));

        _spriteBatch.End();

        // Draw shipping menu (has its own SpriteBatch begin/end)
        _shippingMenu.Draw(_spriteBatch, viewport);
    }

    /// <summary>
    /// Draw the storage menu overlay.
    /// </summary>
    private void DrawStorageOverlay(Viewport viewport)
    {
        _spriteBatch.Begin(
            sortMode: SpriteSortMode.Deferred,
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.PointClamp
        );

        _storageMenu.Draw(_spriteBatch);

        _spriteBatch.End();
    }

    /// <summary>
    /// Draw the dialogue box overlay.
    /// </summary>
    private void DrawDialogueOverlay(Viewport viewport)
    {
        // Dialogue system draws its own box
        DialogueSystem.Draw(_spriteBatch, _pixel, viewport);
    }

    /// <summary>
    /// Draw the inventory menu overlay with dimmed background and pause indicator.
    /// </summary>
    private void DrawInventoryOverlay(Viewport viewport)
    {
        _spriteBatch.Begin(
            sortMode: SpriteSortMode.Deferred,
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.PointClamp
        );

        // Dim background
        var screenRect = new Rectangle(0, 0, viewport.Width, viewport.Height);
        _spriteBatch.Draw(_pixel, screenRect, new Color(0, 0, 0, 150));

        // Draw PAUSED indicator with sine wave pulse
        DrawPausedIndicator(viewport);

        _spriteBatch.End();

        // Draw inventory menu (has its own SpriteBatch begin/end)
        _inventoryMenu.Draw(_spriteBatch, viewport);
    }

    /// <summary>
    /// Draw pulsing "PAUSED" text near the top-right of the screen.
    /// Uses sine wave for alpha animation. Positioned below clock area.
    /// </summary>
    private void DrawPausedIndicator(Viewport viewport)
    {
        // Sine wave pulse: oscillates between 0.5 and 1.0 intensity
        float pulseSpeed = 3f;
        float pulse = (float)((Math.Sin(_totalTime * pulseSpeed) + 1.0) / 2.0); // 0 to 1
        float intensity = 0.5f + pulse * 0.5f; // 0.5 to 1.0

        // Use Yellow color with pulse applied
        Color textColor = Color.Yellow * intensity;

        // Draw "PAUSED" text at top-right, below clock area
        // Scale = 3 for visibility (each pixel becomes 3x3)
        string text = "PAUSED";
        int scale = 3;
        int textWidth = UIRenderer.MeasureString(text, scale);
        int startX = viewport.Width - textWidth - 20; // 20px from right edge
        int startY = 70; // Below clock

        UIRenderer.DrawString(_spriteBatch, _pixel, text, startX, startY, textColor, scale);
    }

    /// <summary>
    /// Draw the day/night atmosphere overlay.
    /// Covers the world layer but not the UI.
    /// </summary>
    private void DrawNightOverlay()
    {
        Color overlayColor = TimeManager.GetNightOverlayColor();

        // Skip if fully transparent (daytime)
        if (overlayColor.A == 0)
            return;

        _spriteBatch.Begin(
            sortMode: SpriteSortMode.Deferred,
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.PointClamp
        );

        // Draw full-screen overlay
        var viewport = GraphicsDevice.Viewport;
        var screenRect = new Rectangle(0, 0, viewport.Width, viewport.Height);
        _spriteBatch.Draw(_pixel, screenRect, overlayColor);

        _spriteBatch.End();
    }
}
