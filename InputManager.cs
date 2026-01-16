using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MagicVille;

/// <summary>
/// Handles input state and coordinate conversion.
/// </summary>
public class InputManager
{
    private MouseState _currentMouse;
    private MouseState _previousMouse;
    private KeyboardState _currentKeyboard;
    private KeyboardState _previousKeyboard;

    /// <summary>Current mouse position in screen coordinates.</summary>
    public Point MouseScreenPosition => _currentMouse.Position;

    /// <summary>
    /// Get mouse position in world coordinates.
    /// Uses camera transform to convert screen → world.
    /// </summary>
    public Vector2 GetMouseWorldPosition(Camera2D camera)
    {
        var screenPos = new Vector2(_currentMouse.X, _currentMouse.Y);
        return camera.ScreenToWorld(screenPos);
    }

    /// <summary>
    /// Get the tile coordinates under the mouse cursor.
    /// </summary>
    public Point GetMouseTilePosition(Camera2D camera)
    {
        var worldPos = GetMouseWorldPosition(camera);
        return new Point(
            (int)MathF.Floor(worldPos.X / GameLocation.TileSize),
            (int)MathF.Floor(worldPos.Y / GameLocation.TileSize)
        );
    }

    /// <summary>
    /// Get the world position of a tile's center.
    /// </summary>
    public static Vector2 GetTileCenterWorld(Point tileCoords)
    {
        return new Vector2(
            tileCoords.X * GameLocation.TileSize + GameLocation.TileSize / 2f,
            tileCoords.Y * GameLocation.TileSize + GameLocation.TileSize / 2f
        );
    }

    /// <summary>Update input state. Call once per frame at start of Update.</summary>
    public void Update()
    {
        _previousMouse = _currentMouse;
        _previousKeyboard = _currentKeyboard;

        _currentMouse = Mouse.GetState();
        _currentKeyboard = Keyboard.GetState();
    }

    // === Mouse button checks ===

    public bool IsLeftMouseDown() => _currentMouse.LeftButton == ButtonState.Pressed;
    public bool IsRightMouseDown() => _currentMouse.RightButton == ButtonState.Pressed;

    public bool IsLeftMousePressed() =>
        _currentMouse.LeftButton == ButtonState.Pressed &&
        _previousMouse.LeftButton == ButtonState.Released;

    public bool IsRightMousePressed() =>
        _currentMouse.RightButton == ButtonState.Pressed &&
        _previousMouse.RightButton == ButtonState.Released;

    // === Keyboard checks ===

    public bool IsKeyDown(Keys key) => _currentKeyboard.IsKeyDown(key);

    public bool IsKeyPressed(Keys key) =>
        _currentKeyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);

    public KeyboardState GetKeyboardState() => _currentKeyboard;
    public MouseState GetMouseState() => _currentMouse;
}
