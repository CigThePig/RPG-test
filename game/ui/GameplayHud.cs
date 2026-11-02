using Godot;
namespace Mosslight;

/// <summary>
/// Read-only structured view of the tracked quest, produced by <c>Narrative.objective()</c>.
/// The HUD never parses <c>Narrative.journal()</c> and never mutates quest state.
/// </summary>
public sealed record QuestObjective(int Id, string Title, string Status, string Objective, bool Staged,
    string StageId, int Progress, int Required, bool PendingReward, string Reward)
{
    public static QuestObjective? Read(Node narrative)
    {
        var row = narrative.Call("objective").AsGodotDictionary();
        if (row.Count == 0) return null;
        return new(row["id"].AsInt32(), row["title"].AsString(), row["status"].AsString(), row["objective"].AsString(),
            row["staged"].AsBool(), row["stage_id"].AsString(), row["progress"].AsInt32(), row["required"].AsInt32(),
            row["pending_reward"].AsBool(), row["reward"].AsString());
    }

    /// <summary>Short header shown above the objective text. Words, not colour, carry the status.</summary>
    public string Heading => Status switch
    {
        "available" => "NEW QUEST · " + Title,
        "reward_pending" => "REWARD WAITING · " + Title,
        _ when Staged => $"QUEST · {Title} · step {Math.Min(Progress + 1, Required)} of {Required}",
        _ => $"QUEST · {Title} · {Progress}/{Required}"
    };
}

/// <summary>
/// Gameplay HUD: identity/status line, health/mana/experience bars with numbers, and the
/// tracked quest objective. Geometry is fixed so value changes never move any rectangle;
/// labels and bars only redraw when their displayed value changes.
/// </summary>
public partial class GameplayHud : Control
{
    public const float MaxWidth = 400, Height = 196;
    const int BarHeight = 20, IdentityFont = 18, BodyFont = 16;
    Label identity = null!, objectiveHeading = null!, objectiveText = null!;
    PanelContainer tracker = null!;
    public HudBar HealthBar { get; private set; } = null!;
    public HudBar ManaBar { get; private set; } = null!;
    public HudBar ExperienceBar { get; private set; } = null!;
    public QuestObjective? Objective { get; private set; }
    public string IdentityText => identity.Text;
    public string ObjectiveHeading => tracker.Visible ? objectiveHeading.Text : "";
    public string ObjectiveText => tracker.Visible ? objectiveText.Text : "";
    public Rect2 TrackerRect => tracker.GetGlobalRect();
    int identityKey = int.MinValue;
    StyleBoxFlat trackerStyle = null!;
    /// <summary>High contrast makes the tracker opaque with an ivory border; otherwise the world shows through.</summary>
    public void ApplyContrast(bool high)
    {
        trackerStyle.BgColor = high ? new Color("0b1312") : new Color(UiDesign.Surface, .86f);
        trackerStyle.BorderColor = high ? UiDesign.Ink : UiDesign.Bronze;
    }

    public GameplayHud() { MouseFilter = MouseFilterEnum.Ignore; }

    public override void _Ready()
    {
        identity = HudLabel(IdentityFont, UiDesign.Ink);
        identity.Position = Vector2.Zero; identity.Size = new Vector2(MaxWidth, 26);
        HealthBar = AddBar("HP", new Color("8e2f2a"), 30);
        ManaBar = AddBar("MP", new Color("2d5591"), 54);
        ExperienceBar = AddBar("XP", new Color("7d6322"), 78);
        tracker = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, Position = new Vector2(0, 106), Size = new Vector2(MaxWidth, 88), ClipContents = true };
        var style = UiDesign.Notification();
        style.BgColor = new Color(UiDesign.Surface, .86f);
        trackerStyle = style;
        style.ContentMarginTop = style.ContentMarginBottom = 6; style.ContentMarginLeft = style.ContentMarginRight = 10;
        tracker.AddThemeStyleboxOverride("panel", style);
        AddChild(tracker);
        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 2);
        tracker.AddChild(column);
        objectiveHeading = HudLabel(BodyFont, UiDesign.Bronze.Lightened(.25f), column);
        objectiveHeading.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        objectiveText = HudLabel(BodyFont, UiDesign.Ink, column);
        // ClipText would collapse an autowrapped label to zero height inside the column.
        objectiveText.ClipText = false;
        objectiveText.CustomMinimumSize = new Vector2(0, 42);
        objectiveText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        objectiveText.MaxLinesVisible = 2;
        objectiveText.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        tracker.Visible = false;
    }

    Label HudLabel(int size, Color color, Node? parent = null)
    {
        var label = new Label { MouseFilter = MouseFilterEnum.Ignore, ClipText = true };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, .85f));
        label.AddThemeConstantOverride("outline_size", 4);
        (parent ?? this).AddChild(label);
        return label;
    }

    HudBar AddBar(string name, Color fill, float y)
    {
        var bar = new HudBar { Caption = name, Fill = fill, Position = new Vector2(0, y), Size = new Vector2(MaxWidth, BarHeight) };
        AddChild(bar); return bar;
    }

    /// <summary>Fixed rectangle; never depends on text or values.</summary>
    public void Arrange(Vector2 position, float availableWidth)
    {
        float width = Math.Clamp(availableWidth, 220, MaxWidth);
        Position = position; Size = new Vector2(width, Height);
        identity.Size = new Vector2(width, 26);
        foreach (var bar in new[] { HealthBar, ManaBar, ExperienceBar }) bar.Size = new Vector2(width, BarHeight);
        tracker.Size = new Vector2(width, 88);
    }

    public void Refresh(ActorState player, string region, double worldTime)
    {
        HealthBar.Set(player.Health, player.MaxHealth, warnBelow: .3f);
        ManaBar.Set(player.Mana, player.MaxMana);
        ExperienceBar.Set(player.Experience, player.Level * 100);
        int minutes = (int)(worldTime / 60) % 24 * 60 + (int)worldTime % 60;
        int key = HashCode.Combine(player.Level, region, minutes, player.Alive, player.State == ActionState.Dodging);
        if (key == identityKey) return;
        identityKey = key;
        string status = !player.Alive ? " · DEFEATED" : player.State == ActionState.Dodging ? " · dodging" : "";
        string place = region == "ruin" ? "Ruin" : region == "reach" ? "Reach" : region;
        identity.Text = $"{player.Name} · Lv {player.Level} · {place} {minutes / 60:00}:{minutes % 60:00}{status}";
    }

    public void ShowObjective(QuestObjective? objective)
    {
        if (objective == Objective) return;
        Objective = objective;
        tracker.Visible = objective != null;
        if (objective == null) return;
        objectiveHeading.Text = objective.Heading;
        objectiveText.Text = objective.Objective;
    }
}

/// <summary>Custom-drawn value bar. Caption and numbers are always drawn, so the bar never relies on colour.</summary>
public partial class HudBar : Control
{
    public string Caption { get; set; } = "";
    public Color Fill { get; set; }
    public float Value { get; private set; }
    public float Maximum { get; private set; } = 1;
    public bool Warning { get; private set; }
    public string Text { get; private set; } = "";
    public HudBar() { MouseFilter = MouseFilterEnum.Ignore; }

    public void Set(float value, float maximum, float warnBelow = -1)
    {
        maximum = Math.Max(1, maximum);
        value = Math.Clamp(value, 0, maximum);
        // Bars display whole numbers; redraw only when the displayed value changes.
        if ((int)Math.Ceiling(value) == (int)Math.Ceiling(Value) && maximum == Maximum && Text != "") return;
        Value = value; Maximum = maximum;
        Warning = warnBelow > 0 && value > 0 && value / maximum <= warnBelow;
        Text = $"{Caption}  {Math.Ceiling(value):0} / {maximum:0}{(Warning ? "  LOW" : "")}";
        QueueRedraw();
    }

    public override void _Draw()
    {
        var area = new Rect2(Vector2.Zero, Size);
        DrawRect(area, new Color(.05f, .08f, .08f, .82f));
        DrawRect(new Rect2(Vector2.Zero, new Vector2(Size.X * Value / Maximum, Size.Y)), Fill);
        DrawRect(area, Warning ? new Color("ffdf8c") : new Color(UiDesign.Bronze, .7f), false, Warning ? 2 : 1);
        var font = ThemeDB.FallbackFont;
        const int size = 15;
        var at = new Vector2(8, (Size.Y + font.GetAscent(size) - font.GetDescent(size)) / 2);
        DrawStringOutline(font, at, Text, HorizontalAlignment.Left, Size.X - 12, size, 4, new Color(0, 0, 0, .9f));
        DrawString(font, at, Text, HorizontalAlignment.Left, Size.X - 12, size, UiDesign.Ink);
    }
}
