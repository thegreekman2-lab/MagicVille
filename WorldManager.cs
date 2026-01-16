using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MagicVille;

public class WorldManager
{
    // Live game objects
    public GameLocation CurrentLocation { get; private set; }
    public Camera2D Camera { get; private set; }
    public Player Player { get; private set; }

    // World metadata
    public string CurrentLocationName { get; private set; } = "Farm";
    public int WorldSeed { get; private set; }

    // Save file name for debug saves
    private const string DebugSaveFile = "debug_save.json";

    private Texture2D _pixel;
    private KeyboardState _previousKeyboard;
    private GraphicsDevice _graphicsDevice;

    public WorldManager()
    {
    }

    public void Initialize(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;

        // Create 1x1 white pixel texture for placeholder rendering
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        // Create camera
        Camera = new Camera2D(graphicsDevice.Viewport);

        // Generate world seed
        WorldSeed = Environment.TickCount;

        // Load test map
        CurrentLocation = GameLocation.CreateTestMap();

        // Spawn player at tile (5, 5) as per requirements
        var startPosition = new Vector2(
            5 * GameLocation.TileSize + GameLocation.TileSize / 2f,
            5 * GameLocation.TileSize + GameLocation.TileSize / 2f
        );
        Player = new Player(startPosition);
    }

    public void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();

        // Debug Save/Load hotkeys: K = Save, L = Load
        if (IsKeyPressed(keyboard, Keys.K))
        {
            Save();
        }
        if (IsKeyPressed(keyboard, Keys.L))
        {
            Load();
        }

        // Update player movement
        Player.Update(gameTime, keyboard);

        // Camera follows player
        Camera.CenterOn(Player.Center);

        _previousKeyboard = keyboard;
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        spriteBatch.Begin(
            sortMode: SpriteSortMode.Deferred,
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.PointClamp,
            transformMatrix: Camera.GetTransformMatrix()
        );

        CurrentLocation.Draw(spriteBatch, _pixel, Camera);
        Player.Draw(spriteBatch, _pixel);

        spriteBatch.End();
    }

    #region Save/Load Integration

    /// <summary>
    /// Converts live game objects → SaveData (DTO).
    /// This is the "Live to Data" conversion.
    /// </summary>
    public SaveData CreateSaveData()
    {
        return new SaveData
        {
            // Player state
            PlayerPositionX = Player.Position.X,
            PlayerPositionY = Player.Position.Y,
            PlayerName = Player.Name,

            // World state
            CurrentLocationName = CurrentLocationName,
            WorldSeed = WorldSeed
        };
    }

    /// <summary>
    /// Applies SaveData (DTO) → live game objects.
    /// This is the "Data to Live" conversion.
    /// </summary>
    public void ApplySaveData(SaveData data)
    {
        // Restore player state
        Player.Position = new Vector2(data.PlayerPositionX, data.PlayerPositionY);
        Player.Name = data.PlayerName;

        // Restore world state
        CurrentLocationName = data.CurrentLocationName;
        WorldSeed = data.WorldSeed;

        // Future: regenerate/load location based on CurrentLocationName and WorldSeed
    }

    /// <summary>
    /// Debug save - triggered by K key.
    /// </summary>
    public void Save()
    {
        Console.WriteLine("[WorldManager] Saving game...");
        var data = CreateSaveData();
        SaveManager.Save(DebugSaveFile, data);
    }

    /// <summary>
    /// Debug load - triggered by L key.
    /// </summary>
    public void Load()
    {
        Console.WriteLine("[WorldManager] Loading game...");
        var data = SaveManager.Load(DebugSaveFile);
        if (data != null)
        {
            ApplySaveData(data);
            Console.WriteLine($"[WorldManager] Restored player at ({data.PlayerPositionX}, {data.PlayerPositionY})");
        }
    }

    #endregion

    private bool IsKeyPressed(KeyboardState current, Keys key)
    {
        return current.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
    }
}
