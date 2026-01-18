#nullable enable
using System;
using Microsoft.Xna.Framework;

namespace MagicVille;

/// <summary>
/// Global time system managing in-game day/night cycle.
/// Time format: Military time (0600 = 6 AM, 1400 = 2 PM, 2400 = Midnight).
/// Day ends at 2600 (2 AM) and rolls over to 0600 (6 AM) next day.
/// </summary>
public static class TimeManager
{
    /// <summary>Current in-game day (starts at 1).</summary>
    public static int Day { get; private set; } = 1;

    /// <summary>
    /// Current time in military format (0600-2559).
    /// 0600 = 6:00 AM, 1200 = Noon, 2400 = Midnight, 2559 = 1:59 AM.
    /// </summary>
    public static int TimeOfDay { get; private set; } = 600;

    /// <summary>Accumulated real-world seconds since last 10-minute tick.</summary>
    public static float Accumulator { get; private set; }

    /// <summary>Whether time progression is paused.</summary>
    public static bool IsPaused { get; set; }

    /// <summary>
    /// Fired every 10 in-game minutes. Parameter is the new TimeOfDay value.
    /// Use this for crop growth, machine processing, etc.
    /// </summary>
    public static event Action<int>? OnTenMinutesPassed;

    // 7 real-world seconds = 10 in-game minutes
    private const float SecondsPerTenMinutes = 7f;

    /// <summary>
    /// Update time progression. Call once per frame.
    /// </summary>
    public static void Update(float deltaTime)
    {
        if (IsPaused)
            return;

        Accumulator += deltaTime;

        while (Accumulator >= SecondsPerTenMinutes)
        {
            Accumulator -= SecondsPerTenMinutes;
            AdvanceTime(10);
        }
    }

    /// <summary>
    /// Advance time by the specified number of minutes.
    /// Properly handles minute overflow (60 -> next hour).
    /// Handles day rollover at 2600 (2 AM).
    /// </summary>
    private static void AdvanceTime(int minutes)
    {
        // Extract hours and minutes from military time format
        int hours = TimeOfDay / 100;
        int mins = TimeOfDay % 100;

        // Add the minutes
        mins += minutes;

        // Roll over minutes to hours when >= 60
        while (mins >= 60)
        {
            mins -= 60;
            hours++;
        }

        // Reconstruct TimeOfDay in military format
        TimeOfDay = hours * 100 + mins;

        // Fire event for each 10-minute tick
        OnTenMinutesPassed?.Invoke(TimeOfDay);

        // Day rollover at 2600 (2 AM) -> 0600 (6 AM) next day
        if (TimeOfDay >= 2600)
        {
            Day++;
            TimeOfDay = 600;
        }
    }

    /// <summary>
    /// Instantly advance time by 1 hour. For debug purposes.
    /// </summary>
    public static void AdvanceHour()
    {
        // Advance in 10-minute increments to trigger events properly
        for (int i = 0; i < 6; i++)
        {
            AdvanceTime(10);
        }
    }

    /// <summary>
    /// Get the ambient light color based on current time of day.
    /// Used for the day/night visual atmosphere.
    /// </summary>
    public static Color GetAmbientLightColor()
    {
        // Normalize time: treat 0000-0559 as 2400-2959 for continuous math
        int t = TimeOfDay < 600 ? TimeOfDay + 2400 : TimeOfDay;

        // 0600 - 1700: Full daylight (White)
        if (t >= 600 && t < 1700)
        {
            return Color.White;
        }
        // 1700 - 2000: Sunset transition (White -> Warm Orange)
        else if (t >= 1700 && t < 2000)
        {
            float progress = (t - 1700) / 300f;
            return Color.Lerp(Color.White, new Color(255, 200, 120), progress);
        }
        // 2000 - 2200: Dusk transition (Warm Orange -> Deep Blue)
        else if (t >= 2000 && t < 2200)
        {
            float progress = (t - 2000) / 200f;
            return Color.Lerp(new Color(255, 200, 120), new Color(50, 60, 100), progress);
        }
        // 2200 - 0600: Night (Deep Blue)
        else
        {
            return new Color(50, 60, 100);
        }
    }

    /// <summary>
    /// Get the overlay color for rendering the night filter.
    /// Returns a semi-transparent tint to apply over the world.
    /// </summary>
    public static Color GetNightOverlayColor()
    {
        // Normalize time: treat 0000-0559 as 2400-2959 for continuous math
        int t = TimeOfDay < 600 ? TimeOfDay + 2400 : TimeOfDay;

        // 0600 - 1700: Full daylight - no overlay
        if (t >= 600 && t < 1700)
        {
            return Color.Transparent;
        }
        // 1700 - 1900: Sunset start - Lerp from Transparent to soft Orange/Red
        else if (t >= 1700 && t < 1900)
        {
            float progress = (t - 1700) / 200f;
            // Soft, low-alpha orange/red sunset glow
            int alpha = (int)(progress * 80);
            return new Color(255, 100, 50, alpha);
        }
        // 1900 - 2000: Dusk - Lerp from Orange to Dark Blue
        else if (t >= 1900 && t < 2000)
        {
            float progress = (t - 1900) / 100f;
            // Blend from sunset orange to night blue
            int r = (int)(255 - progress * 225);  // 255 -> 30
            int g = (int)(100 - progress * 60);   // 100 -> 40
            int b = (int)(50 + progress * 70);    // 50 -> 120
            int alpha = (int)(80 + progress * 40); // 80 -> 120
            return new Color(r, g, b, alpha);
        }
        // 2000 - 0600: Night - dark blue overlay
        else
        {
            return new Color(30, 40, 120, 120);
        }
    }

    /// <summary>
    /// Format current time for display: "Day X - HH:MM AM/PM"
    /// </summary>
    public static string GetFormattedTime()
    {
        int hours = TimeOfDay / 100;
        int minutes = TimeOfDay % 100;

        // Handle times past midnight (2400+)
        if (hours >= 24)
            hours -= 24;

        // Convert to 12-hour format
        string period = hours >= 12 ? "PM" : "AM";
        int displayHour = hours % 12;
        if (displayHour == 0)
            displayHour = 12;

        return $"Day {Day} - {displayHour}:{minutes:D2} {period}";
    }

    /// <summary>
    /// Reset time to start of day 1 at 6 AM.
    /// </summary>
    public static void Reset()
    {
        Day = 1;
        TimeOfDay = 600;
        Accumulator = 0f;
        IsPaused = false;
    }
}
