#nullable enable
using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MagicVille;

/// <summary>
/// Static dialogue system manager for displaying text boxes.
/// Handles typewriter effect, input handling, and rendering.
/// Used for Signs, NPCs, and other narrative elements.
/// </summary>
public static class DialogueSystem
{
    // Dialogue state
    private static string _fullText = "";
    private static string _displayText = "";
    private static bool _isTyping = false;
    private static bool _isActive = false;

    // Typewriter timing
    private static double _typeTimer = 0;
    private const double TypeDelay = 0.03; // 0.03 seconds per character (fast but readable)

    // Input tracking (to prevent click-through)
    private static MouseState _previousMouse;
    private static KeyboardState _previousKeyboard;
    private static bool _inputConsumedThisFrame = false;

    // Callback for closing dialogue (sets game state)
    private static Action? _onClose;

    /// <summary>Whether dialogue is currently being displayed.</summary>
    public static bool IsActive => _isActive;

    /// <summary>Whether text is still typing out.</summary>
    public static bool IsTyping => _isTyping;

    /// <summary>
    /// Check if the last update consumed input (prevents tool swing on close).
    /// </summary>
    public static bool InputConsumedThisFrame => _inputConsumedThisFrame;

    /// <summary>
    /// Show a dialogue box with the specified text.
    /// </summary>
    /// <param name="text">The full text to display.</param>
    /// <param name="onClose">Callback when dialogue closes (typically sets game state to Playing).</param>
    public static void Show(string text, Action? onClose = null)
    {
        _fullText = text;
        _displayText = "";
        _isTyping = true;
        _isActive = true;
        _typeTimer = 0;
        _onClose = onClose;
        _inputConsumedThisFrame = true; // Consume the click that opened dialogue

        // Capture current input state to prevent immediate close
        _previousMouse = Mouse.GetState();
        _previousKeyboard = Keyboard.GetState();

        Debug.WriteLine($"[DialogueSystem] Showing: \"{text}\"");
    }

    /// <summary>
    /// Close the dialogue box immediately.
    /// </summary>
    public static void Close()
    {
        _isActive = false;
        _isTyping = false;
        _fullText = "";
        _displayText = "";
        _inputConsumedThisFrame = true; // Consume the closing click

        Debug.WriteLine("[DialogueSystem] Closed");

        _onClose?.Invoke();
    }

    /// <summary>
    /// Update the dialogue system (typewriter effect and input handling).
    /// Call this from Game1.Update when in Dialogue state.
    /// </summary>
    public static void Update(GameTime gameTime)
    {
        if (!_isActive)
            return;

        _inputConsumedThisFrame = false;

        var mouse = Mouse.GetState();
        var keyboard = Keyboard.GetState();

        // Check for action input (mouse click or action key)
        bool actionPressed = (mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released) ||
                             (keyboard.IsKeyDown(Keys.Space) && !_previousKeyboard.IsKeyDown(Keys.Space)) ||
                             (keyboard.IsKeyDown(Keys.Enter) && !_previousKeyboard.IsKeyDown(Keys.Enter)) ||
                             (keyboard.IsKeyDown(Keys.E) && !_previousKeyboard.IsKeyDown(Keys.E));

        if (_isTyping)
        {
            // Typewriter effect: add characters over time
            _typeTimer += gameTime.ElapsedGameTime.TotalSeconds;

            while (_typeTimer >= TypeDelay && _displayText.Length < _fullText.Length)
            {
                _displayText += _fullText[_displayText.Length];
                _typeTimer -= TypeDelay;
            }

            // Check if typing is complete
            if (_displayText.Length >= _fullText.Length)
            {
                _isTyping = false;
            }

            // If action pressed while typing, instant finish
            if (actionPressed)
            {
                _displayText = _fullText;
                _isTyping = false;
                _inputConsumedThisFrame = true;
            }
        }
        else
        {
            // Text fully displayed - action closes dialogue
            if (actionPressed)
            {
                Close();
            }
        }

        _previousMouse = mouse;
        _previousKeyboard = keyboard;
    }

    /// <summary>
    /// Draw the dialogue box at the bottom of the screen.
    /// Call this from Game1.Draw when in Dialogue state.
    /// </summary>
    public static void Draw(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport)
    {
        if (!_isActive)
            return;

        spriteBatch.Begin(
            sortMode: SpriteSortMode.Deferred,
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.PointClamp
        );

        // Box dimensions (80% width, 150px height, centered at bottom)
        int boxWidth = (int)(viewport.Width * 0.8f);
        int boxHeight = 120;
        int boxX = (viewport.Width - boxWidth) / 2;
        int boxY = viewport.Height - boxHeight - 20; // 20px from bottom

        // Background (dark blue-brown with high alpha)
        var boxRect = new Rectangle(boxX, boxY, boxWidth, boxHeight);
        spriteBatch.Draw(pixel, boxRect, new Color(30, 30, 50, 230)); // Dark blue-ish

        // Border (white)
        DrawRectBorder(spriteBatch, pixel, boxRect, Color.White, 2);

        // Text padding
        int textX = boxX + 20;
        int textY = boxY + 20;
        int maxTextWidth = boxWidth - 40;

        // Draw text with word wrapping
        DrawWrappedText(spriteBatch, pixel, _displayText, textX, textY, maxTextWidth, Color.White);

        // Draw "continue" indicator if not typing
        if (!_isTyping)
        {
            // Blinking arrow at bottom-right of box
            double time = DateTime.Now.Millisecond / 500.0;
            if ((int)time % 2 == 0)
            {
                int arrowX = boxX + boxWidth - 30;
                int arrowY = boxY + boxHeight - 25;
                DrawPixelChar(spriteBatch, pixel, '>', arrowX, arrowY, Color.Yellow);
            }
        }

        spriteBatch.End();
    }

    /// <summary>
    /// Draw text with word wrapping.
    /// </summary>
    private static void DrawWrappedText(SpriteBatch spriteBatch, Texture2D pixel, string text, int x, int y, int maxWidth, Color color)
    {
        const int charWidth = 6;  // 5px char + 1px spacing
        const int lineHeight = 10; // 7px char + 3px spacing

        int maxCharsPerLine = maxWidth / charWidth;
        int currentX = x;
        int currentY = y;

        string[] words = text.Split(' ');
        string currentLine = "";

        foreach (string word in words)
        {
            string testLine = currentLine.Length == 0 ? word : currentLine + " " + word;

            if (testLine.Length > maxCharsPerLine)
            {
                // Draw current line and start new one
                if (currentLine.Length > 0)
                {
                    DrawPixelText(spriteBatch, pixel, currentLine, currentX, currentY, color);
                    currentY += lineHeight;
                }
                currentLine = word;
            }
            else
            {
                currentLine = testLine;
            }
        }

        // Draw remaining text
        if (currentLine.Length > 0)
        {
            DrawPixelText(spriteBatch, pixel, currentLine, currentX, currentY, color);
        }
    }

    /// <summary>
    /// Draw a rectangle border.
    /// </summary>
    private static void DrawRectBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color, int thickness = 1)
    {
        // Top
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        // Bottom
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        // Left
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        // Right
        spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }

    /// <summary>
    /// Draw pixel text (simple bitmap font).
    /// </summary>
    private static void DrawPixelText(SpriteBatch spriteBatch, Texture2D pixel, string text, int x, int y, Color color)
    {
        int cursorX = x;
        foreach (char c in text)
        {
            DrawPixelChar(spriteBatch, pixel, c, cursorX, y, color);
            cursorX += 6; // 5px char + 1px spacing
        }
    }

    /// <summary>
    /// Draw a single pixel character.
    /// </summary>
    private static void DrawPixelChar(SpriteBatch spriteBatch, Texture2D pixel, char c, int x, int y, Color color)
    {
        string[] pattern = GetCharPattern(c);

        for (int row = 0; row < pattern.Length; row++)
        {
            for (int col = 0; col < pattern[row].Length; col++)
            {
                if (pattern[row][col] == '#')
                {
                    spriteBatch.Draw(pixel, new Rectangle(x + col, y + row, 1, 1), color);
                }
            }
        }
    }

    /// <summary>
    /// Get the 5x7 pixel pattern for a character.
    /// </summary>
    private static string[] GetCharPattern(char c)
    {
        return char.ToUpper(c) switch
        {
            'A' => new[] { " ### ", "#   #", "#   #", "#####", "#   #", "#   #", "#   #" },
            'B' => new[] { "#### ", "#   #", "#   #", "#### ", "#   #", "#   #", "#### " },
            'C' => new[] { " ### ", "#   #", "#    ", "#    ", "#    ", "#   #", " ### " },
            'D' => new[] { "#### ", "#   #", "#   #", "#   #", "#   #", "#   #", "#### " },
            'E' => new[] { "#####", "#    ", "#    ", "#### ", "#    ", "#    ", "#####" },
            'F' => new[] { "#####", "#    ", "#    ", "#### ", "#    ", "#    ", "#    " },
            'G' => new[] { " ### ", "#   #", "#    ", "# ###", "#   #", "#   #", " ### " },
            'H' => new[] { "#   #", "#   #", "#   #", "#####", "#   #", "#   #", "#   #" },
            'I' => new[] { "#####", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", "#####" },
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
            'V' => new[] { "#   #", "#   #", "#   #", "#   #", " # # ", " # # ", "  #  " },
            'W' => new[] { "#   #", "#   #", "#   #", "#   #", "# # #", "## ##", "#   #" },
            'X' => new[] { "#   #", " # # ", "  #  ", "  #  ", "  #  ", " # # ", "#   #" },
            'Y' => new[] { "#   #", " # # ", "  #  ", "  #  ", "  #  ", "  #  ", "  #  " },
            'Z' => new[] { "#####", "    #", "   # ", "  #  ", " #   ", "#    ", "#####" },
            '0' => new[] { " ### ", "#   #", "#  ##", "# # #", "##  #", "#   #", " ### " },
            '1' => new[] { "  #  ", " ##  ", "  #  ", "  #  ", "  #  ", "  #  ", "#####" },
            '2' => new[] { " ### ", "#   #", "    #", "  ## ", " #   ", "#    ", "#####" },
            '3' => new[] { " ### ", "#   #", "    #", "  ## ", "    #", "#   #", " ### " },
            '4' => new[] { "#   #", "#   #", "#   #", "#####", "    #", "    #", "    #" },
            '5' => new[] { "#####", "#    ", "#### ", "    #", "    #", "#   #", " ### " },
            '6' => new[] { " ### ", "#    ", "#### ", "#   #", "#   #", "#   #", " ### " },
            '7' => new[] { "#####", "    #", "   # ", "  #  ", "  #  ", "  #  ", "  #  " },
            '8' => new[] { " ### ", "#   #", "#   #", " ### ", "#   #", "#   #", " ### " },
            '9' => new[] { " ### ", "#   #", "#   #", " ####", "    #", "    #", " ### " },
            '.' => new[] { "     ", "     ", "     ", "     ", "     ", "     ", "  #  " },
            ',' => new[] { "     ", "     ", "     ", "     ", "     ", "  #  ", " #   " },
            '!' => new[] { "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", "     ", "  #  " },
            '?' => new[] { " ### ", "#   #", "    #", "  ## ", "  #  ", "     ", "  #  " },
            '\'' => new[] { "  #  ", "  #  ", "     ", "     ", "     ", "     ", "     " },
            '"' => new[] { " # # ", " # # ", "     ", "     ", "     ", "     ", "     " },
            '-' => new[] { "     ", "     ", "     ", "#####", "     ", "     ", "     " },
            ':' => new[] { "     ", "  #  ", "     ", "     ", "     ", "  #  ", "     " },
            '/' => new[] { "    #", "   # ", "  #  ", "  #  ", " #   ", "#    ", "     " },
            '(' => new[] { "  #  ", " #   ", "#    ", "#    ", "#    ", " #   ", "  #  " },
            ')' => new[] { "  #  ", "   # ", "    #", "    #", "    #", "   # ", "  #  " },
            '>' => new[] { "#    ", " #   ", "  #  ", "   # ", "  #  ", " #   ", "#    " },
            ' ' => new[] { "     ", "     ", "     ", "     ", "     ", "     ", "     " },
            _ => new[] { "#####", "#   #", "#   #", "#   #", "#   #", "#   #", "#####" } // Unknown char box
        };
    }
}
