using System.Text.Json;
using Godot;

namespace Mosslight;
public static class DungeonRules
{
    public static string Prepare()
    {
        // The upstream generator uses System.IO; unpack its tiny text grammar from res:// for native PCK builds.
        string directory = ProjectSettings.GlobalizePath("user://dungeon-grammar-3d55c329/");
        var rules = JsonSerializer.Deserialize<Dictionary<string, string>>(Godot.FileAccess.GetFileAsString("res://game/data/dungeon_rules.json"))!;
        foreach (var(relative, text)in rules)
        {
            string path = Path.Combine(directory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (!File.Exists(path) || File.ReadAllText(path) != text)
                File.WriteAllText(path, text);
        }

        return directory;
    }
}
