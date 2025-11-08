using System;
using System.IO;
using System.Text.Json;

public class Settings
{
    public int MaxEntries { get; set; } = 100;
}

public static class SettingsManager
{
    public static Settings Current { get; private set; }

    public static void Load()
    {
        string path = "settings.json";

        try
        {
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                Current = JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
            }
            else
            {
                Current = new Settings();
                string json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
                //Console.WriteLine("Created default settings.json");
            }
        }
        catch (Exception ex)
        {
            //Console.WriteLine("Error loading settings: " + ex.Message);
            Current = new Settings();
        }

        //Console.WriteLine("MaxEntries loaded: " + Current.MaxEntries);
    }
}
