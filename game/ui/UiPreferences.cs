using System.Text.Json;
using Godot;
namespace Mosslight;

/// <summary>
/// Player-facing UI preferences, stored on this device in their own file, never in a
/// world save. Loading or starting a game never changes them, and they never change
/// gameplay rules. A missing or damaged file falls back to defaults without being
/// overwritten until the player changes a setting.
/// </summary>
public sealed class UiPreferences
{
    public static readonly float[] TextScales = { 1f, 1.3f, 1.4f };
    public static readonly string[] TextScaleNames = { "Standard", "Large", "Largest" };
    public int Version { get; set; } = 1;
    /// <summary>Index into <see cref="TextScales"/>.</summary>
    public int TextSize { get; set; }
    public bool Muted { get; set; }
    /// <summary>Removes camera impact nudges. Presentation only; combat timing is unchanged.</summary>
    public bool ReducedMotion { get; set; }
    /// <summary>Opaque panels, brighter borders and secondary text.</summary>
    public bool HighContrast { get; set; }

    public float TextScale => TextScales[Math.Clamp(TextSize, 0, TextScales.Length - 1)];
    public string TextSizeName => TextScaleNames[Math.Clamp(TextSize, 0, TextScaleNames.Length - 1)];

    /// <summary>Overridable so verification never touches a real player's file.</summary>
    public static string FilePath { get; set; } = "user://ui_preferences.json";
    static string Absolute => ProjectSettings.GlobalizePath(FilePath);

    public static UiPreferences Load(out string? problem)
    {
        problem = null;
        try
        {
            if (!File.Exists(Absolute)) return new();
            var loaded = JsonSerializer.Deserialize<UiPreferences>(File.ReadAllText(Absolute), Content.Json);
            if (loaded == null || loaded.Version != 1) { problem = "Settings file was not recognised; defaults are in use"; return new(); }
            loaded.TextSize = Math.Clamp(loaded.TextSize, 0, TextScales.Length - 1);
            return loaded;
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            problem = "Settings file could not be read; defaults are in use";
            return new();
        }
    }

    public void Save()
    {
        string path = Absolute;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, Content.Json));
        File.Move(temporary, path, true);
    }

    public UiPreferences Copy() => (UiPreferences)MemberwiseClone();
}
