using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MagicVille;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private WorldManager _world;

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
    }

    protected override void Update(GameTime gameTime)
    {
        _world.Update(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);
        _world.Draw(_spriteBatch);
        base.Draw(gameTime);
    }
}
