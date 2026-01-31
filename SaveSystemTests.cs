#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Xna.Framework;

namespace MagicVille;

/// <summary>
/// Test harness for validating Save/Load system integrity.
/// Simulates a game session, serializes state, wipes, loads, and verifies nothing was lost.
///
/// Run via: SaveSystemTests.RunTests(worldManager) in Game1.Initialize()
/// </summary>
public static class SaveSystemTests
{
    private const string TestLocationName = "Farm";

    // Test data constants
    private const string TestCropType = "corn";
    private const CropStage TestCropStage = CropStage.Growing;
    private const int TestCropDaysAtStage = 1;
    private const bool TestCropWatered = true;

    private const string TestBinItemName = "Diamond";
    private const string TestBinItemKey = "diamond";
    private const int TestBinItemSellPrice = 750;

    private const string TestInventoryItemKey = "wood";
    private const string TestInventoryItemName = "Wood";
    private const int TestInventoryQuantity = 50;

    private const string TestEnemyType = "Goblin";
    private const int TestEnemyHP = 2; // Damaged (MaxHP is 3)

    private const string TestChestItemKey = "gold_ore";
    private const string TestChestItemName = "Gold Ore";
    private const int TestChestItemQuantity = 25;
    private const int TestChestSlotIndex = 5; // Put item in slot 5, not 0

    /// <summary>
    /// Run all save system tests.
    /// Call this from Game1.Initialize() after WorldManager.Initialize().
    /// </summary>
    /// <param name="world">The initialized WorldManager instance.</param>
    public static void RunTests(WorldManager world)
    {
        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║           SAVE SYSTEM TEST HARNESS - v2.17                   ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
        Debug.WriteLine("[SaveSystemTests] Starting test harness...");

        int passed = 0;
        int failed = 0;

        try
        {
            // ═══════════════════════════════════════════════════════════════════
            // STEP 1: SETUP - Create known test state
            // ═══════════════════════════════════════════════════════════════════
            Console.WriteLine("║ STEP 1: Setting up test state...                             ║");
            SetupTestState(world);

            // ═══════════════════════════════════════════════════════════════════
            // STEP 2: SERIALIZE - Create save data
            // ═══════════════════════════════════════════════════════════════════
            Console.WriteLine("║ STEP 2: Serializing to SaveData DTO...                       ║");
            SaveData saveData = world.CreateSaveData();

            // Optional: Print JSON for visual inspection
            PrintJsonPreview(saveData);

            // ═══════════════════════════════════════════════════════════════════
            // STEP 3: THE WIPE - Clear all state
            // ═══════════════════════════════════════════════════════════════════
            Console.WriteLine("║ STEP 3: Wiping state (simulating fresh load)...              ║");
            WipeState(world);

            // Verify wipe worked
            if (VerifyStateWiped(world))
            {
                Console.WriteLine("║   [OK] State successfully wiped                              ║");
            }
            else
            {
                Console.WriteLine("║   [!!] WARNING: State wipe may be incomplete                 ║");
            }

            // ═══════════════════════════════════════════════════════════════════
            // STEP 4: DESERIALIZE - Apply save data
            // ═══════════════════════════════════════════════════════════════════
            Console.WriteLine("║ STEP 4: Applying SaveData (deserializing)...                 ║");
            world.ApplySaveData(saveData);

            // ═══════════════════════════════════════════════════════════════════
            // STEP 5: ASSERTIONS - Verify nothing was lost
            // ═══════════════════════════════════════════════════════════════════
            Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ STEP 5: RUNNING ASSERTIONS                                   ║");
            Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");

            // Test 1: Crop State
            if (VerifyCrop(world, out string cropResult))
            {
                passed++;
                Console.WriteLine($"║   [PASS] Crop: {cropResult,-43}║");
            }
            else
            {
                failed++;
                Console.WriteLine($"║   [FAIL] Crop: {cropResult,-43}║");
            }

            // Test 2: ShippingBin Buffer State
            if (VerifyShippingBin(world, out string binResult))
            {
                passed++;
                Console.WriteLine($"║   [PASS] ShippingBin: {binResult,-36}║");
            }
            else
            {
                failed++;
                Console.WriteLine($"║   [FAIL] ShippingBin: {binResult,-36}║");
            }

            // Test 3: Inventory State
            if (VerifyInventory(world, out string invResult))
            {
                passed++;
                Console.WriteLine($"║   [PASS] Inventory: {invResult,-38}║");
            }
            else
            {
                failed++;
                Console.WriteLine($"║   [FAIL] Inventory: {invResult,-38}║");
            }

            // Test 4: Enemy State
            if (VerifyEnemy(world, out string enemyResult))
            {
                passed++;
                Console.WriteLine($"║   [PASS] Enemy: {enemyResult,-42}║");
            }
            else
            {
                failed++;
                Console.WriteLine($"║   [FAIL] Enemy: {enemyResult,-42}║");
            }

            // Test 5: Chest Storage State
            if (VerifyChest(world, out string chestResult))
            {
                passed++;
                Console.WriteLine($"║   [PASS] Chest: {chestResult,-42}║");
            }
            else
            {
                failed++;
                Console.WriteLine($"║   [FAIL] Chest: {chestResult,-42}║");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"║   [FAIL] Exception: {ex.Message,-38}║");
            Debug.WriteLine($"[SaveSystemTests] EXCEPTION: {ex}");
        }

        // ═══════════════════════════════════════════════════════════════════
        // SUMMARY
        // ═══════════════════════════════════════════════════════════════════
        Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
        string summary = $"RESULTS: {passed} PASSED, {failed} FAILED";
        string status = failed == 0 ? "ALL TESTS PASSED!" : "SOME TESTS FAILED!";
        Console.WriteLine($"║ {summary,-60}║");
        Console.WriteLine($"║ {status,-60}║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        Debug.WriteLine($"[SaveSystemTests] Completed: {passed} passed, {failed} failed");
    }

    /// <summary>
    /// Set up known test state in the WorldManager.
    /// </summary>
    private static void SetupTestState(WorldManager world)
    {
        // Clear existing objects for clean test
        if (world.LocationObjects.ContainsKey(TestLocationName))
        {
            world.LocationObjects[TestLocationName].Clear();
        }
        else
        {
            world.LocationObjects[TestLocationName] = new List<WorldObject>();
        }

        var objects = world.LocationObjects[TestLocationName];

        // 1. Add test Crop (Corn, Growing stage, watered)
        var testCrop = new Crop
        {
            CropType = TestCropType,
            Stage = TestCropStage,
            DaysAtStage = TestCropDaysAtStage,
            WasWateredToday = TestCropWatered,
            DaysPerStage = 2,
            Position = new Vector2(500, 500),
            HarvestItemId = "corn",
            HarvestQuantity = 2
        };
        objects.Add(testCrop);
        Debug.WriteLine($"[Test] Added Crop: {TestCropType}, Stage={TestCropStage}, Watered={TestCropWatered}");

        // 2. Add ShippingBin with item in LastShippedItem buffer
        var testBin = new ShippingBin(new Vector2(600, 500));
        testBin.LastShippedItem = new Material(
            registryKey: TestBinItemKey,
            name: TestBinItemName,
            description: "A precious gem.",
            quantity: 1,
            maxStack: 99,
            sellPrice: TestBinItemSellPrice
        );
        objects.Add(testBin);
        Debug.WriteLine($"[Test] Added ShippingBin with LastShippedItem: {TestBinItemName}");

        // 3. Add item to Player Inventory (Slot 0)
        world.Player.Inventory.SetSlot(0, new Material(
            registryKey: TestInventoryItemKey,
            name: TestInventoryItemName,
            description: "Basic building material.",
            quantity: TestInventoryQuantity,
            maxStack: 99,
            sellPrice: 2
        ));
        Debug.WriteLine($"[Test] Added Inventory item: {TestInventoryItemName} x{TestInventoryQuantity}");

        // 4. Add a damaged enemy (Goblin with 2 HP instead of max 3)
        if (!world.LocationEnemies.ContainsKey(TestLocationName))
        {
            world.LocationEnemies[TestLocationName] = new List<Enemy>();
        }
        else
        {
            world.LocationEnemies[TestLocationName].Clear();
        }

        var testEnemy = Enemy.CreateGoblin(new Vector2(700, 500));
        testEnemy.HP = TestEnemyHP; // Simulate damaged enemy
        world.LocationEnemies[TestLocationName].Add(testEnemy);
        Debug.WriteLine($"[Test] Added Enemy: {TestEnemyType}, HP={TestEnemyHP}/{testEnemy.MaxHP}");

        // 5. Add a chest with items inside
        var testChest = new Chest(new Vector2(800, 500));
        testChest.SetSlot(TestChestSlotIndex, new Material(
            registryKey: TestChestItemKey,
            name: TestChestItemName,
            description: "Valuable ore.",
            quantity: TestChestItemQuantity,
            maxStack: 99,
            sellPrice: 100
        ));
        objects.Add(testChest);
        Debug.WriteLine($"[Test] Added Chest with {TestChestItemName} x{TestChestItemQuantity} in slot {TestChestSlotIndex}");

        Console.WriteLine("║   - Crop: Corn, Growing stage, watered                        ║");
        Console.WriteLine("║   - ShippingBin: Diamond in buffer slot                       ║");
        Console.WriteLine("║   - Inventory: Wood x50 in slot 0                             ║");
        Console.WriteLine("║   - Enemy: Goblin with 2/3 HP (damaged)                       ║");
        Console.WriteLine("║   - Chest: Gold Ore x25 in slot 5                             ║");
    }

    /// <summary>
    /// Print a preview of the serialized JSON (truncated).
    /// </summary>
    private static void PrintJsonPreview(SaveData data)
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(data, options);

            // Truncate for console display
            const int MaxPreviewLength = 500;
            if (json.Length > MaxPreviewLength)
            {
                json = json[..MaxPreviewLength] + "\n... (truncated)";
            }

            Debug.WriteLine("[SaveSystemTests] JSON Preview:");
            Debug.WriteLine(json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SaveSystemTests] JSON serialization failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Wipe the WorldManager state to simulate a fresh session.
    /// </summary>
    private static void WipeState(WorldManager world)
    {
        // Clear location objects
        if (world.LocationObjects.ContainsKey(TestLocationName))
        {
            world.LocationObjects[TestLocationName].Clear();
        }

        // Clear location enemies
        if (world.LocationEnemies.ContainsKey(TestLocationName))
        {
            world.LocationEnemies[TestLocationName].Clear();
        }

        // Clear inventory
        for (int i = 0; i < Inventory.HotbarSize; i++)
        {
            world.Player.Inventory.SetSlot(i, null);
        }

        Debug.WriteLine("[Test] State wiped: objects cleared, enemies cleared, inventory emptied");
    }

    /// <summary>
    /// Verify the state was actually wiped.
    /// </summary>
    private static bool VerifyStateWiped(WorldManager world)
    {
        // Check objects are gone
        if (world.LocationObjects.TryGetValue(TestLocationName, out var objects))
        {
            if (objects.Count > 0)
                return false;
        }

        // Check enemies are gone
        if (world.LocationEnemies.TryGetValue(TestLocationName, out var enemies))
        {
            if (enemies.Count > 0)
                return false;
        }

        // Check inventory is empty
        if (world.Player.Inventory.GetSlot(0) != null)
            return false;

        return true;
    }

    /// <summary>
    /// Verify the crop was restored correctly.
    /// </summary>
    private static bool VerifyCrop(WorldManager world, out string result)
    {
        if (!world.LocationObjects.TryGetValue(TestLocationName, out var objects))
        {
            result = "No objects in location";
            return false;
        }

        // Find the crop
        Crop? crop = null;
        foreach (var obj in objects)
        {
            if (obj is Crop c)
            {
                crop = c;
                break;
            }
        }

        if (crop == null)
        {
            result = "Crop not found";
            return false;
        }

        // Verify crop properties
        bool typeMatch = crop.CropType == TestCropType;
        bool stageMatch = crop.Stage == TestCropStage;
        bool wateredMatch = crop.WasWateredToday == TestCropWatered;
        bool daysMatch = crop.DaysAtStage == TestCropDaysAtStage;

        if (!typeMatch)
        {
            result = $"Type mismatch: {crop.CropType} != {TestCropType}";
            return false;
        }
        if (!stageMatch)
        {
            result = $"Stage mismatch: {crop.Stage} != {TestCropStage}";
            return false;
        }
        if (!wateredMatch)
        {
            result = $"Watered mismatch: {crop.WasWateredToday} != {TestCropWatered}";
            return false;
        }
        if (!daysMatch)
        {
            result = $"DaysAtStage mismatch: {crop.DaysAtStage} != {TestCropDaysAtStage}";
            return false;
        }

        result = $"{TestCropType}, {TestCropStage}, Watered={TestCropWatered}";
        return true;
    }

    /// <summary>
    /// Verify the ShippingBin buffer was restored correctly.
    /// </summary>
    private static bool VerifyShippingBin(WorldManager world, out string result)
    {
        if (!world.LocationObjects.TryGetValue(TestLocationName, out var objects))
        {
            result = "No objects in location";
            return false;
        }

        // Find the shipping bin
        ShippingBin? bin = null;
        foreach (var obj in objects)
        {
            if (obj is ShippingBin b)
            {
                bin = b;
                break;
            }
        }

        if (bin == null)
        {
            result = "ShippingBin not found";
            return false;
        }

        // Verify buffer slot
        if (bin.LastShippedItem == null)
        {
            result = "LastShippedItem is NULL (data lost!)";
            return false;
        }

        bool nameMatch = bin.LastShippedItem.Name == TestBinItemName;
        bool keyMatch = bin.LastShippedItem.RegistryKey == TestBinItemKey;
        bool priceMatch = bin.LastShippedItem.SellPrice == TestBinItemSellPrice;

        if (!nameMatch)
        {
            result = $"Name mismatch: {bin.LastShippedItem.Name}";
            return false;
        }
        if (!keyMatch)
        {
            result = $"Key mismatch: {bin.LastShippedItem.RegistryKey}";
            return false;
        }
        if (!priceMatch)
        {
            result = $"Price mismatch: {bin.LastShippedItem.SellPrice}";
            return false;
        }

        result = $"{TestBinItemName} preserved in buffer";
        return true;
    }

    /// <summary>
    /// Verify the inventory was restored correctly.
    /// </summary>
    private static bool VerifyInventory(WorldManager world, out string result)
    {
        var item = world.Player.Inventory.GetSlot(0);

        if (item == null)
        {
            result = "Slot 0 is NULL (data lost!)";
            return false;
        }

        if (item is not Material mat)
        {
            result = $"Wrong type: {item.GetType().Name}";
            return false;
        }

        bool keyMatch = mat.RegistryKey == TestInventoryItemKey;
        bool nameMatch = mat.Name == TestInventoryItemName;
        bool qtyMatch = mat.Quantity == TestInventoryQuantity;

        if (!keyMatch)
        {
            result = $"Key mismatch: {mat.RegistryKey}";
            return false;
        }
        if (!nameMatch)
        {
            result = $"Name mismatch: {mat.Name}";
            return false;
        }
        if (!qtyMatch)
        {
            result = $"Quantity mismatch: {mat.Quantity} != {TestInventoryQuantity}";
            return false;
        }

        result = $"{TestInventoryItemName} x{TestInventoryQuantity}";
        return true;
    }

    /// <summary>
    /// Verify the enemy was restored correctly.
    /// </summary>
    private static bool VerifyEnemy(WorldManager world, out string result)
    {
        if (!world.LocationEnemies.TryGetValue(TestLocationName, out var enemies))
        {
            result = "No enemies in location";
            return false;
        }

        if (enemies.Count == 0)
        {
            result = "Enemy list is empty (data lost!)";
            return false;
        }

        // Find the goblin
        Enemy? goblin = null;
        foreach (var enemy in enemies)
        {
            if (enemy.EnemyType == TestEnemyType)
            {
                goblin = enemy;
                break;
            }
        }

        if (goblin == null)
        {
            result = $"No {TestEnemyType} found";
            return false;
        }

        // Verify enemy properties
        bool typeMatch = goblin.EnemyType == TestEnemyType;
        bool hpMatch = goblin.HP == TestEnemyHP;

        if (!typeMatch)
        {
            result = $"Type mismatch: {goblin.EnemyType} != {TestEnemyType}";
            return false;
        }
        if (!hpMatch)
        {
            result = $"HP mismatch: {goblin.HP} != {TestEnemyHP}";
            return false;
        }

        result = $"{TestEnemyType} HP={TestEnemyHP}/{goblin.MaxHP} preserved";
        return true;
    }

    /// <summary>
    /// Verify the chest contents were restored correctly.
    /// </summary>
    private static bool VerifyChest(WorldManager world, out string result)
    {
        if (!world.LocationObjects.TryGetValue(TestLocationName, out var objects))
        {
            result = "No objects in location";
            return false;
        }

        // Find the chest
        Chest? chest = null;
        foreach (var obj in objects)
        {
            if (obj is Chest c)
            {
                chest = c;
                break;
            }
        }

        if (chest == null)
        {
            result = "Chest not found";
            return false;
        }

        // Verify chest has item in correct slot
        var item = chest.GetSlot(TestChestSlotIndex);

        if (item == null)
        {
            result = $"Slot {TestChestSlotIndex} is NULL (data lost!)";
            return false;
        }

        if (item is not Material mat)
        {
            result = $"Wrong type: {item.GetType().Name}";
            return false;
        }

        bool keyMatch = mat.RegistryKey == TestChestItemKey;
        bool nameMatch = mat.Name == TestChestItemName;
        bool qtyMatch = mat.Quantity == TestChestItemQuantity;

        if (!keyMatch)
        {
            result = $"Key mismatch: {mat.RegistryKey}";
            return false;
        }
        if (!nameMatch)
        {
            result = $"Name mismatch: {mat.Name}";
            return false;
        }
        if (!qtyMatch)
        {
            result = $"Quantity mismatch: {mat.Quantity} != {TestChestItemQuantity}";
            return false;
        }

        result = $"{TestChestItemName} x{TestChestItemQuantity} in slot {TestChestSlotIndex}";
        return true;
    }
}
