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

    /// <summary>
    /// Fired when a new day starts. Parameter is the new Day number.
    /// Use this for OnNewDay() processing (crops, trees, mana nodes, etc.).
    /// </summary>
    public static event Action<int>? OnDayChanged;

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
            OnDayChanged?.Invoke(Day);
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

    // === Lighting Constants (Alpha Scaling Approach) ===
    // Fixed base colors - RGB stays constant, only alpha changes
    // This prevents muddy brown artifacts from Color.Lerp interpolation

    /// <summary>Sunset base color (bright warm orange).</summary>
    private static readonly Color SunsetBase = new Color(255, 100, 50);  // Bright orange

    /// <summary>Night base color (deep blue).</summary>
    private static readonly Color NightBase = new Color(20, 30, 80);     // Deep blue

    /// <summary>Maximum sunset overlay opacity (0.0 - 1.0).</summary>
    private const float MaxSunsetOpacity = 0.35f;

    /// <summary>Maximum night overlay opacity (0.0 - 1.0).</summary>
    private const float MaxNightOpacity = 0.5f;

    /// <summary>
    /// Get the overlay color for rendering the day/night filter.
    /// Uses ALPHA SCALING instead of Color.Lerp to prevent muddy transitions.
    /// RGB values stay vibrant; only alpha changes during fade-in.
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

        // 1700 - 1900: Sunset fade-in (Alpha Scaling)
        // RGB stays constant (bright orange), only alpha increases
        if (t >= 1700 && t < 1900)
        {
            float progress = (t - 1700) / 200f;  // 0.0 -> 1.0
            float alpha = progress * MaxSunsetOpacity;

            // Alpha scaling: multiply base color by alpha (keeps RGB vibrant)
            return new Color(
                (int)(SunsetBase.R * alpha),
                (int)(SunsetBase.G * alpha),
                (int)(SunsetBase.B * alpha),
                (int)(alpha * 255)
            );
        }

        // 1900 - 2100: Dusk transition (Sunset -> Night)
        // Lerp between two properly-opaque colors (both at their max opacity)
        if (t >= 1900 && t < 2100)
        {
            float progress = (t - 1900) / 200f;  // 0.0 -> 1.0

            // Create the sunset color at max opacity
            var sunsetAtMax = new Color(
                (int)(SunsetBase.R * MaxSunsetOpacity),
                (int)(SunsetBase.G * MaxSunsetOpacity),
                (int)(SunsetBase.B * MaxSunsetOpacity),
                (int)(MaxSunsetOpacity * 255)
            );

            // Create the night color at max opacity
            var nightAtMax = new Color(
                (int)(NightBase.R * MaxNightOpacity),
                (int)(NightBase.G * MaxNightOpacity),
                (int)(NightBase.B * MaxNightOpacity),
                (int)(MaxNightOpacity * 255)
            );

            // Lerp between two equally-opaque colors (no muddy transition)
            return Color.Lerp(sunsetAtMax, nightAtMax, progress);
        }

        // 2100 - 0600: Full night
        return new Color(
            (int)(NightBase.R * MaxNightOpacity),
            (int)(NightBase.G * MaxNightOpacity),
            (int)(NightBase.B * MaxNightOpacity),
            (int)(MaxNightOpacity * 255)
        );
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

    /// <summary>
    /// Force start a new day (used when player sleeps in bed).
    /// Sets time to 6 AM and fires OnDayChanged event.
    /// </summary>
    public static void StartNewDay()
    {
        Day++;
        TimeOfDay = 600;
        Accumulator = 0f;
        OnDayChanged?.Invoke(Day);
    }

    /// <summary>
    /// Set time and day directly (used when loading saved games).
    /// Does NOT fire OnDayChanged (loading should restore state, not advance it).
    /// </summary>
    public static void SetTime(int day, int timeOfDay)
    {
        Day = day;
        TimeOfDay = timeOfDay;
        Accumulator = 0f;
    }
}
