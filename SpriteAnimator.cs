using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MagicVille;

/// <summary>
/// Facing direction - maps directly to spritesheet row index.
/// </summary>
public enum Direction
{
    Down = 0,
    Up = 1,
    Left = 2,
    Right = 3
}

/// <summary>
/// Handles spritesheet-based frame animation.
/// Spritesheet layout: rows = directions, columns = animation frames.
/// </summary>
public class SpriteAnimator
{
    private readonly Texture2D _spritesheet;
    private readonly int _frameWidth;
    private readonly int _frameHeight;
    private readonly Dictionary<string, AnimationData> _animations = new();

    private string _currentAnimation = "";
    private int _currentFrame;
    private float _timer;

    /// <summary>
    /// Current facing direction - determines which row to sample.
    /// </summary>
    public Direction Direction { get; set; } = Direction.Down;

    /// <summary>
    /// Seconds per frame. Lower = faster animation.
    /// </summary>
    public float FrameTime { get; set; } = 0.15f;

    public SpriteAnimator(Texture2D spritesheet, int frameWidth, int frameHeight)
    {
        _spritesheet = spritesheet;
        _frameWidth = frameWidth;
        _frameHeight = frameHeight;
    }

    /// <summary>
    /// Register an animation by name.
    /// </summary>
    /// <param name="name">Animation name (e.g., "Idle", "Walk")</param>
    /// <param name="startFrame">Starting column index in the spritesheet</param>
    /// <param name="frameCount">Number of frames in this animation</param>
    public void AddAnimation(string name, int startFrame, int frameCount)
    {
        _animations[name] = new AnimationData(startFrame, frameCount);
    }

    /// <summary>
    /// Switch to a different animation. Resets frame to 0 if animation changes.
    /// </summary>
    public void PlayAnimation(string name)
    {
        if (_currentAnimation == name)
            return;

        if (!_animations.ContainsKey(name))
            return;

        _currentAnimation = name;
        _currentFrame = 0;
        _timer = 0f;
    }

    /// <summary>
    /// Advance animation timer and cycle frames.
    /// </summary>
    public void Update(GameTime gameTime)
    {
        if (!_animations.TryGetValue(_currentAnimation, out var anim))
            return;

        // Single-frame animations don't need timing
        if (anim.FrameCount <= 1)
            return;

        _timer += (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (_timer >= FrameTime)
        {
            _timer -= FrameTime;
            _currentFrame = (_currentFrame + 1) % anim.FrameCount;
        }
    }

    /// <summary>
    /// Draw the current animation frame at the specified position.
    /// Uses BOTTOM-CENTER origin: Position represents the feet/base of the sprite.
    /// </summary>
    /// <param name="spriteBatch">SpriteBatch (must be inside Begin/End)</param>
    /// <param name="position">Feet position in world coordinates (bottom-center of sprite)</param>
    /// <param name="drawWidth">Destination width</param>
    /// <param name="drawHeight">Destination height</param>
    public void Draw(SpriteBatch spriteBatch, Vector2 position, int drawWidth, int drawHeight)
    {
        if (!_animations.TryGetValue(_currentAnimation, out var anim))
            return;

        int row = (int)Direction;
        int col = anim.StartFrame + _currentFrame;

        var sourceRect = new Rectangle(
            col * _frameWidth,
            row * _frameHeight,
            _frameWidth,
            _frameHeight
        );

        // Origin at bottom-center of the source frame
        var origin = new Vector2(_frameWidth / 2f, _frameHeight);

        // Scale to match desired draw size
        var scale = new Vector2(
            drawWidth / (float)_frameWidth,
            drawHeight / (float)_frameHeight
        );

        spriteBatch.Draw(
            _spritesheet,
            position,           // Position = where the origin (feet) goes
            sourceRect,
            Color.White,
            0f,                 // No rotation
            origin,             // Bottom-center origin
            scale,
            SpriteEffects.None,
            0f
        );
    }

    /// <summary>
    /// Animation definition: which frames to use.
    /// </summary>
    private readonly record struct AnimationData(int StartFrame, int FrameCount);

    #region Placeholder Spritesheet Generator

    /// <summary>
    /// Creates a debug spritesheet with colored rectangles.
    /// Layout: 4 columns (frames) x 4 rows (directions).
    /// Each direction has a distinct base color, each frame has different brightness.
    /// </summary>
    public static Texture2D CreatePlaceholderSpritesheet(GraphicsDevice graphicsDevice, int frameWidth, int frameHeight)
    {
        const int cols = 4; // Frame 0 = Idle, Frames 1-3 = Walk
        const int rows = 4; // Down, Up, Left, Right

        int textureWidth = cols * frameWidth;
        int textureHeight = rows * frameHeight;

        var texture = new Texture2D(graphicsDevice, textureWidth, textureHeight);
        var pixels = new Color[textureWidth * textureHeight];

        // Base color per direction (row)
        Color[] directionColors =
        {
            new Color(65, 105, 225),   // Down  - Royal Blue
            new Color(220, 60, 100),   // Up    - Crimson
            new Color(60, 180, 90),    // Left  - Forest Green
            new Color(230, 180, 50)    // Right - Gold
        };

        for (int row = 0; row < rows; row++)
        {
            Color baseColor = directionColors[row];

            for (int col = 0; col < cols; col++)
            {
                // Vary brightness per frame for visual debugging
                // Frame 0 (idle) = darker, frames 1-3 cycle through brightness
                float brightness = 0.6f + (col * 0.12f);

                Color cellColor = new Color(
                    (int)(baseColor.R * brightness),
                    (int)(baseColor.G * brightness),
                    (int)(baseColor.B * brightness)
                );

                // Fill this cell
                FillCell(pixels, textureWidth, col, row, frameWidth, frameHeight, cellColor);
            }
        }

        texture.SetData(pixels);
        return texture;
    }

    private static void FillCell(Color[] pixels, int textureWidth, int col, int row, int frameWidth, int frameHeight, Color fillColor)
    {
        int cellStartX = col * frameWidth;
        int cellStartY = row * frameHeight;

        for (int y = 0; y < frameHeight; y++)
        {
            for (int x = 0; x < frameWidth; x++)
            {
                int px = cellStartX + x;
                int py = cellStartY + y;

                // Border (2px) for visibility
                bool isBorder = x < 2 || y < 2 || x >= frameWidth - 2 || y >= frameHeight - 2;

                // Frame number indicator in corner (simple: darker square for frame index)
                bool isFrameIndicator = x >= 4 && x < 4 + (col + 1) * 6 && y >= 4 && y < 12;

                Color pixelColor;
                if (isBorder)
                    pixelColor = Color.White;
                else if (isFrameIndicator)
                    pixelColor = Color.Black;
                else
                    pixelColor = fillColor;

                pixels[py * textureWidth + px] = pixelColor;
            }
        }
    }

    #endregion
}
