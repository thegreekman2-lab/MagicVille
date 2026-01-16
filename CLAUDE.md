# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
dotnet restore    # Restore NuGet packages
dotnet build      # Build the project
dotnet run        # Build and run the game
```

## Project Overview

MagicVille is a MonoGame project targeting .NET 8.0 using the DesktopGL (OpenGL) backend.

## Architecture

- **Program.cs**: Entry point that instantiates and runs the game
- **Game1.cs**: Main game class inheriting from MonoGame's `Game` class
  - `LoadContent()`: Load textures, sounds, and other assets
  - `Update(GameTime)`: Game logic runs here (input, physics, AI)
  - `Draw(GameTime)`: Rendering code goes here

## MonoGame Specifics

- Window size: 800x480 pixels
- Uses `SpriteBatch` for 2D rendering
- Game assets should be placed in a Content folder and processed with the MonoGame Content Pipeline (MGCB)
