using System;
using System.IO;
using System.Text.Json;
using AudioBridge.Desktop.Models;

namespace AudioBridge.Desktop.Services;

public sealed class SettingsService
{
    private static readonly string FolderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        OperatingSystem.IsWindows() ? "AudioBridge" : "audiobridge");

    private static readonly string FilePath = Path.Combine(FolderPath, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    static SettingsService()
    {
        Console.WriteLine($"[SettingsService] Percorso file impostazioni: {FilePath}");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                Console.WriteLine("[SettingsService] File non trovato, uso valori predefiniti.");
                return new AppSettings();
            }

            var json = File.ReadAllText(FilePath);
            Console.WriteLine("[SettingsService] Impostazioni caricate da disco.");
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SettingsService] Errore caricamento: {ex.Message}");
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(FolderPath);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(FilePath, json);
            Console.WriteLine("[SettingsService] Impostazioni salvate su disco.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SettingsService] Errore salvataggio: {ex.Message}");
        }
    }
}
