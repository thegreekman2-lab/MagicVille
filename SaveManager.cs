#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MagicVille;

/// <summary>
/// Static utility class for saving/loading game state.
/// Handles serialization to JSON with polymorphic type support.
/// </summary>
public static class SaveManager
{
    private static readonly string SaveFolder = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "Saves"
    );

    /// <summary>
    /// JSON serializer options configured for polymorphic Item serialization.
    /// The [JsonDerivedType] attributes on Item class handle Tool/Material discrimination.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        // Include fields marked with JsonInclude
        IncludeFields = false,
        // Handle reference loops if any
        ReferenceHandler = null,
        // Property naming
        PropertyNamingPolicy = null,
        // Allow reading/writing numbers from strings
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        // Converters for custom types if needed
        Converters = { }
    };

    /// <summary>
    /// Saves game state to disk as JSON.
    /// Polymorphic items (Tool, Material) are serialized with type discriminators.
    /// </summary>
    /// <param name="filename">Filename without path (e.g., "save1.json")</param>
    /// <param name="data">The SaveData object to serialize</param>
    /// <returns>True if save succeeded</returns>
    public static bool Save(string filename, SaveData data)
    {
        try
        {
            Directory.CreateDirectory(SaveFolder);
            string fullPath = Path.Combine(SaveFolder, filename);

            string json = JsonSerializer.Serialize(data, JsonOptions);
            File.WriteAllText(fullPath, json);

            Console.WriteLine($"[SaveManager] Game SAVED to: {fullPath}");
            Debug.WriteLine($"[SaveManager] Saved {data.InventorySlots.Count} inventory slots");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SaveManager] SAVE FAILED: {ex.Message}");
            Debug.WriteLine($"[SaveManager] SAVE FAILED: {ex}");
            return false;
        }
    }

    /// <summary>
    /// Loads game state from disk.
    /// Polymorphic items are deserialized back to correct Tool/Material types.
    /// </summary>
    /// <param name="filename">Filename without path (e.g., "save1.json")</param>
    /// <returns>SaveData object, or null if load failed</returns>
    public static SaveData? Load(string filename)
    {
        try
        {
            string fullPath = Path.Combine(SaveFolder, filename);

            if (!File.Exists(fullPath))
            {
                Console.WriteLine($"[SaveManager] No save file found: {fullPath}");
                return null;
            }

            string json = File.ReadAllText(fullPath);
            var data = JsonSerializer.Deserialize<SaveData>(json, JsonOptions);

            if (data != null)
            {
                Console.WriteLine($"[SaveManager] Game LOADED from: {fullPath}");
                Debug.WriteLine($"[SaveManager] Loaded {data.InventorySlots.Count} inventory slots");

                // Log loaded item types for debugging
                for (int i = 0; i < data.InventorySlots.Count; i++)
                {
                    var item = data.InventorySlots[i];
                    if (item != null)
                    {
                        Debug.WriteLine($"  Slot {i}: {item.GetType().Name} - {item.Name}");
                    }
                }
            }

            return data;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SaveManager] LOAD FAILED: {ex.Message}");
            Debug.WriteLine($"[SaveManager] LOAD FAILED: {ex}");
            return null;
        }
    }

    /// <summary>
    /// Checks if a save file exists.
    /// </summary>
    public static bool SaveExists(string filename)
    {
        return File.Exists(Path.Combine(SaveFolder, filename));
    }

    /// <summary>
    /// Deletes a save file.
    /// </summary>
    public static bool DeleteSave(string filename)
    {
        try
        {
            string fullPath = Path.Combine(SaveFolder, filename);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                Console.WriteLine($"[SaveManager] Save DELETED: {fullPath}");
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SaveManager] DELETE FAILED: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Gets the full path for a save file.
    /// </summary>
    public static string GetSavePath(string filename) => Path.Combine(SaveFolder, filename);
}
