#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MagicVille;

/// <summary>
/// Centralized UI rendering utilities.
/// Provides pixel-font text rendering, tooltips, borders, and item color mapping.
/// All methods are static — no instance state needed.
///
/// CHARACTER SET: Full A-Z uppercase, a-z lowercase, 0-9 digits,
/// and common punctuation (. , : ; ! ? - + = / ' " ( ) [ ] &lt; &gt;).
///
/// FONT METRICS (before scaling):
/// - Character width: 5px
/// - Character height: 7px
/// - Spacing: 1px between characters
/// - Total per char at scale N: (5N + N) = 6N px wide, 7N px tall
/// </summary>
public static class UIRenderer
{
    private const int CharWidth = 5;
    private const int CharHeight = 7;

    // ═══════════════════════════════════════════════════════════════════
    // TEXT MEASUREMENT
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Measure the pixel width of a string at the given scale.
    /// Includes trailing spacing (matches draw behavior for layout calculations).
    /// </summary>
    public static int MeasureString(string text, int scale = 1)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return text.Length * (CharWidth + 1) * scale;
    }

    /// <summary>
    /// Get the pixel height of text at the given scale.
    /// </summary>
    public static int MeasureHeight(int scale = 1) => CharHeight * scale;

    // ═══════════════════════════════════════════════════════════════════
    // TEXT RENDERING
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Draw pixel-font text at the specified position.
    /// Replaces all local DrawScaledPixelText / DrawPixelText methods.
    /// </summary>
    public static void DrawString(SpriteBatch sb, Texture2D pixel, string text,
        int x, int y, Color color, int scale = 1)
    {
        int cursorX = x;
        int charW = CharWidth * scale;
        int spacing = scale;

        foreach (char c in text)
        {
            DrawChar(sb, pixel, c, cursorX, y, color, scale);
            cursorX += charW + spacing;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // TOOLTIP
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Draw a tooltip box for an item near the mouse cursor.
    /// Shows: Item Name (white, 2x) + Sell Price (yellow/red, 1x).
    /// Clamped to stay within viewport bounds.
    /// </summary>
    public static void DrawTooltip(SpriteBatch sb, Texture2D pixel, Item item,
        Point mousePos, Viewport vp)
    {
        string name = item.Name;
        string priceText = item.IsSellable
            ? $"Sells for: {item.SellPrice}g"
            : "Cannot sell";

        int nameWidth = MeasureString(name, 2);
        int priceWidth = MeasureString(priceText, 1);
        int tooltipWidth = Math.Max(nameWidth, priceWidth) + 20;
        int tooltipHeight = 40;

        // Position: offset right of mouse, clamped to viewport
        int tooltipX = mousePos.X + 20;
        int tooltipY = mousePos.Y;

        if (tooltipX + tooltipWidth > vp.Width - 4)
            tooltipX = mousePos.X - tooltipWidth - 10;
        tooltipX = Math.Max(4, tooltipX);
        tooltipY = Math.Clamp(tooltipY, 4, vp.Height - tooltipHeight - 4);

        // Background
        var tooltipRect = new Rectangle(tooltipX, tooltipY, tooltipWidth, tooltipHeight);
        sb.Draw(pixel, tooltipRect, new Color(0, 0, 0, 220));
        DrawBorder(sb, pixel, tooltipRect, new Color(150, 150, 180), 1);

        // Item name (white, 2x scale)
        DrawString(sb, pixel, name, tooltipX + 10, tooltipY + 6, Color.White, 2);

        // Sell price (yellow if sellable, red if not)
        Color priceColor = item.IsSellable ? new Color(255, 215, 0) : new Color(200, 100, 100);
        DrawString(sb, pixel, priceText, tooltipX + 10, tooltipY + 24, priceColor, 1);
    }

    // ═══════════════════════════════════════════════════════════════════
    // BORDER
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Draw a rectangular border (outline only).
    /// Replaces all local DrawRectBorder / DrawBorder methods.
    /// </summary>
    public static void DrawBorder(SpriteBatch sb, Texture2D pixel, Rectangle rect,
        Color color, int thickness)
    {
        sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        sb.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        sb.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }

    // ═══════════════════════════════════════════════════════════════════
    // ITEM COLOR
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get the display color for an item based on its registry key and type.
    /// Unified color palette shared by all menus (inventory, shipping, storage).
    /// </summary>
    public static Color GetItemColor(Item item)
    {
        return item.RegistryKey switch
        {
            // Standard Tools
            "hoe" => new Color(139, 90, 43),
            "axe" => new Color(100, 100, 100),
            "pickaxe" => new Color(120, 120, 140),
            "watering_can" => new Color(80, 130, 200),
            "scythe" => new Color(180, 180, 100),
            // Weapons
            "sword" => new Color(200, 200, 220),
            // Magic Wands
            "earth_wand" => new Color(180, 140, 60),
            "hydro_wand" => new Color(60, 180, 255),
            // Raw Materials
            "wood" => new Color(139, 90, 43),
            "stone" => new Color(128, 128, 128),
            "fiber" => new Color(34, 139, 34),
            "coal" => new Color(30, 30, 30),
            "copper_ore" => new Color(184, 115, 51),
            "gold_ore" => new Color(255, 215, 0),
            "iron_ore" => new Color(180, 180, 190),
            "diamond" => new Color(150, 220, 255),
            // Harvest Items
            "corn" => new Color(255, 220, 80),
            "tomato" => new Color(220, 50, 50),
            "potato" => new Color(180, 140, 80),
            "carrot" => new Color(255, 140, 0),
            "wheat" => new Color(220, 190, 100),
            // Fallback by type
            _ => item switch
            {
                Tool => new Color(100, 150, 255),
                Material => new Color(180, 160, 140),
                _ => new Color(200, 200, 200)
            }
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    // PRIVATE: CHARACTER RENDERING
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Draw a single pixel-font character at the given position and scale.
    /// </summary>
    private static void DrawChar(SpriteBatch sb, Texture2D pixel, char c,
        int x, int y, Color color, int scale)
    {
        string[] pattern = GetCharPattern(c);

        for (int row = 0; row < CharHeight; row++)
        {
            for (int col = 0; col < CharWidth; col++)
            {
                if (col < pattern[row].Length && pattern[row][col] == '#')
                {
                    sb.Draw(pixel, new Rectangle(
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
    /// Get the 5x7 pixel pattern for a character.
    /// Comprehensive set: A-Z, a-z, 0-9, common punctuation.
    /// </summary>
    private static string[] GetCharPattern(char c) => c switch
    {
        // ── Digits ──
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

        // ── Uppercase Letters ──
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

        // ── Lowercase Letters ──
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

        // ── Punctuation & Symbols ──
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
        '/' => new[] { "    #", "    #", "   # ", "  #  ", " #   ", "#    ", "#    " },
        '\'' => new[] { "  #  ", "  #  ", " #   ", "     ", "     ", "     ", "     " },
        '"' => new[] { " # # ", " # # ", "     ", "     ", "     ", "     ", "     " },
        '(' => new[] { "  #  ", " #   ", "#    ", "#    ", "#    ", " #   ", "  #  " },
        ')' => new[] { "  #  ", "   # ", "    #", "    #", "    #", "   # ", "  #  " },
        '[' => new[] { " ### ", " #   ", " #   ", " #   ", " #   ", " #   ", " ### " },
        ']' => new[] { " ### ", "   # ", "   # ", "   # ", "   # ", "   # ", " ### " },
        '<' => new[] { "    #", "   # ", "  #  ", " #   ", "  #  ", "   # ", "    #" },
        '>' => new[] { "#    ", " #   ", "  #  ", "   # ", "  #  ", " #   ", "#    " },

        // Default: blank
        _ => new[] { "     ", "     ", "     ", "     ", "     ", "     ", "     " }
    };
}
