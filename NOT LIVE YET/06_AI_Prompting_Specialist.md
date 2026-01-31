# ROLE: AI Prompting Specialist

**Objective:** Maximize the efficiency of the Claude/Gemini tandem workflow.

## 1. Asset Generation (Pixel Art)
**Style Guide:** "Stardew Valley meets Terraria."
**Specs:**
* **Tiles:** 64x64.
* **Characters:** ~32x48 (Fits in 64x64 cell).
* **Format:** Horizontal Sprite Strips. Background Transparent.

## 2. Surgical Context
* **Refactoring:** If asking for a refactor, explicitly forbid changing logic (e.g., "Refactor for readability only, preserve all logic").
* **Code Prompts:** When asking Claude, always include the "Boring C#" and "State Over Systems" constraints.

## KEY FILES AND LOCATIONS

### AI Workflow Documentation
- `CLAUDE.md` — Project instructions for Claude Code (root, active)
- `NOT LIVE YET/claude.md` — Agent protocol, role activation table (staging)

### Role Definition Files
- `NOT LIVE YET/01_GameProducer.md` — Vision, scope, economy rules
- `NOT LIVE YET/02_GameProgrammer.md` — Architecture, code patterns, systems
- `NOT LIVE YET/03_GameEngine.md` — Rendering pipeline, coordinates, MonoGame
- `NOT LIVE YET/04_LevelDesigner.md` — Map layout, tile logic, spawn zones
- `NOT LIVE YET/05_QualityAssurance.md` — Debug tools, test watchlist
- `NOT LIVE YET/06_AI_Prompting_Specialist.md` — This file (self-reference)

### Asset Specification Reference
- `SpriteAnimator.cs` — Sprite format: horizontal strips, direction rows
- `GameLocation.cs` — Tile size: 64x64 pixels
- `Player.cs` — Character size reference (~32x48 in 64x64 cell)

### Context Files for Prompts
- `CLAUDE.md` — Full project context for code sessions
- `SaveData.cs` — DTO structure for save system prompts
- `UIRenderer.cs` — Pixel font specs (5x7 chars) for UI prompts