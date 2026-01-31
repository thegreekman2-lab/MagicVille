# ROLE: Quality Assurance (QA)

**Objective:** Ensure stability, reproduce bugs, and verify the MVL.

## 1. The Debug Toolset
* **`K`:** Save game to `debug_save.json`.
* **`L`:** Load game from `debug_save.json`.
* **`F3`:** Toggle Hitbox Visualization (Red borders).
* **`T`:** Fast Forward Time (+1 Hour).
* **`P`:** Pause/Unpause Time.

## 2. Delicate Systems Watchlist
* **Inventory Drag/Drop:** Test closing the menu while holding an item (Item should snap back, not vanish).
* **Crop Regrowth:** Verify crops that "Regrow" (e.g., Corn) reset to the correct stage, not seed stage.
* **Projectile Clipping:** Test shooting projectiles at thin walls from close range (Step size check).
* **Save Scumming:** Verify `CurrentStamina` is saved. (Player cannot quit/reload to refill energy).

## 3. Reporting Standards
* **Goal-Driven:** Report "Hitbox off by ~10px" instead of "Combat feels weird."
* **Artifacts:** Attach `debug_save.json` content for logic bugs.

## KEY FILES AND LOCATIONS

### Debug Controls
- `Game1.cs` — Debug key bindings (K=Save, L=Load, F3=Hitboxes, T=Time+1hr, P=Pause)
- `WorldManager.cs` — F3 collision visualization toggle, debug rendering

### Delicate Systems (Test Priority)
- `InventoryMenu.cs` — Drag-and-drop, CancelDrag() on menu close (item snap-back)
- `Crop.cs` — Regrowth logic, HarvestResetStage verification
- `Projectile.cs` — Stepped wall collision (StepSize=8f), clipping prevention
- `Player.cs` — CurrentStamina persistence (anti-save-scum), HP/i-frames

### Save System Verification
- `SaveManager.cs` — JSON save/load, polymorphic Item handling
- `SaveData.cs` — DTO structure (player state, inventory, objects, locations)
- `TileSaveData.cs` — Delta compression (only modified tiles saved)
- `SaveSystemTests.cs` — Automated save system tests
- `Chest.cs` — Storage persistence across save/load

### Combat Verification
- `Enemy.cs` — Chase AI bounds, contact damage, knockback direction
- `Projectile.cs` — Wall collision at thin walls, enemy hit registration
- `Player.cs` — HP system, i-frame timing, attack cooldowns

### Test Artifacts
- `bin/Debug/net8.0/Saves/debug_save.json` — Debug save file for reproduction

### Edge Case Files
- `ShippingMenu.cs` — Bin slot buffer, item finalization on close
- `StorageMenu.cs` — Click-to-transfer, full inventory handling
- `TimeManager.cs` — Day boundary transitions, OnDayPassed events