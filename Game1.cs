using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MagicVille;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private WorldManager _world = null!;
    private Texture2D _pixel = null!;

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
    private void OnClientSizeChanged(object sender, System.EventArgs e)
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
    }

    protected override void Update(GameTime gameTime)
    {
        _world.Update(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);

        // === LAYER 1: World (tiles, objects, player) ===
        _world.DrawWorld(_spriteBatch);

        // === LAYER 2: Night Overlay (day/night atmosphere) ===
        DrawNightOverlay();

        // === LAYER 3: UI (clock, hotbar - not affected by night filter) ===
        _world.DrawUI(_spriteBatch);

        // === LAYER 4: Transition Fade (covers everything during location change) ===
        var viewport = GraphicsDevice.Viewport;
        var screenRect = new Rectangle(0, 0, viewport.Width, viewport.Height);
        _world.Transition.Draw(_spriteBatch, _pixel, screenRect);

        base.Draw(gameTime);
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
