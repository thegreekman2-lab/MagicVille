#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MagicVille;

/// <summary>
/// Transition states for location warping and sleeping.
/// </summary>
public enum TransitionState
{
    /// <summary>Normal gameplay, no transition active.</summary>
    Playing,
    /// <summary>Screen fading to black.</summary>
    FadingOut,
    /// <summary>Screen fully black, safe to swap maps.</summary>
    SwappingMap,
    /// <summary>Screen fading back in from black.</summary>
    FadingIn,
    /// <summary>Screen fully black, performing sleep (save + day advance).</summary>
    Sleeping
}

/// <summary>
/// Manages visual transitions between locations (fade to black).
/// Hides map swapping artifacts behind a black screen.
/// </summary>
public class TransitionManager
{
    /// <summary>Current transition state.</summary>
    public TransitionState State { get; private set; } = TransitionState.Playing;

    /// <summary>Current fade alpha (0 = transparent, 1 = fully black).</summary>
    public float Alpha { get; private set; }

    /// <summary>Whether a transition is currently active.</summary>
    public bool IsTransitioning => State != TransitionState.Playing;

    /// <summary>Duration of fade in/out in seconds.</summary>
    public float FadeDuration { get; set; } = 0.3f;

    // Pending warp data (set when transition starts)
    private string _pendingTargetLocation = "";
    private Vector2 _pendingTargetPosition;

    /// <summary>
    /// Event fired when the screen is fully black and it's safe to swap maps.
    /// Handler receives (targetLocationName, targetPlayerPosition).
    /// </summary>
    public event Action<string, Vector2>? OnSwapMap;

    /// <summary>
    /// Event fired when the transition completes and gameplay resumes.
    /// </summary>
    public event Action? OnTransitionComplete;

    /// <summary>
    /// Event fired when screen is black during sleep transition.
    /// Handler should save game and advance day.
    /// </summary>
    public event Action? OnSleep;

    // Whether current transition is a sleep (not a map swap)
    private bool _isSleeping;

    /// <summary>
    /// Start a transition to a new location.
    /// </summary>
    /// <param name="targetLocation">Name of the destination location.</param>
    /// <param name="targetPosition">Player spawn position in destination.</param>
    public void StartTransition(string targetLocation, Vector2 targetPosition)
    {
        if (IsTransitioning)
            return; // Don't interrupt an active transition

        _pendingTargetLocation = targetLocation;
        _pendingTargetPosition = targetPosition;
        _isSleeping = false;
        State = TransitionState.FadingOut;
        Alpha = 0f;
    }

    /// <summary>
    /// Start a sleep transition (fade out -> save/advance day -> fade in).
    /// </summary>
    public void StartSleepTransition()
    {
        if (IsTransitioning)
            return;

        _isSleeping = true;
        State = TransitionState.FadingOut;
        Alpha = 0f;
    }

    /// <summary>
    /// Update the transition state machine.
    /// </summary>
    public void Update(float deltaTime)
    {
        switch (State)
        {
            case TransitionState.Playing:
                // Nothing to do
                break;

            case TransitionState.FadingOut:
                Alpha += deltaTime / FadeDuration;
                if (Alpha >= 1f)
                {
                    Alpha = 1f;
                    // Branch based on whether this is a sleep or map swap
                    State = _isSleeping ? TransitionState.Sleeping : TransitionState.SwappingMap;
                }
                break;

            case TransitionState.SwappingMap:
                // Fire the map swap event - this is when it's safe to change locations
                OnSwapMap?.Invoke(_pendingTargetLocation, _pendingTargetPosition);
                State = TransitionState.FadingIn;
                break;

            case TransitionState.Sleeping:
                // Fire the sleep event - save game and advance day
                OnSleep?.Invoke();
                State = TransitionState.FadingIn;
                break;

            case TransitionState.FadingIn:
                Alpha -= deltaTime / FadeDuration;
                if (Alpha <= 0f)
                {
                    Alpha = 0f;
                    State = TransitionState.Playing;
                    _isSleeping = false;
                    OnTransitionComplete?.Invoke();
                }
                break;
        }
    }

    /// <summary>
    /// Draw the fade overlay (full-screen black rectangle).
    /// Call this after world rendering but before UI.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, Rectangle screenBounds)
    {
        if (Alpha <= 0f)
            return;

        int alphaByte = (int)(Alpha * 255);
        var fadeColor = new Color(0, 0, 0, alphaByte);

        spriteBatch.Begin(
            sortMode: SpriteSortMode.Deferred,
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.PointClamp
        );

        spriteBatch.Draw(pixel, screenBounds, fadeColor);

        spriteBatch.End();
    }
}
