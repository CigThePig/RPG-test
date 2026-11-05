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
/// Gameplay HUD: a level medallion ringed by experience, name/place/time, glossy health
/// and mana bars with numbers, and the tracked quest on a translucent plate. Geometry is
/// fixed so value changes never move any rectangle; drawing only refreshes on change.
/// </summary>
public partial class GameplayHud : Control
{
    public const float MaxWidth = 400, Height = 196;
    const float Medallion = 62, BarsLeft = 74;
    Label objectiveHeading = null!, objectiveText = null!;
    PanelContainer tracker = null!;
    public HudBar HealthBar { get; private set; } = null!;
    public HudBar ManaBar { get; private set; } = null!;
    public HudBar ExperienceBar { get; private set; } = null!;
    public QuestObjective? Objective { get; private set; }
    public string IdentityText { get; private set; } = "";
    public string ObjectiveHeading => tracker.Visible ? objectiveHeading.Text : "";
    public string ObjectiveText => tracker.Visible ? objectiveText.Text : "";
    public Rect2 TrackerRect => tracker.GetGlobalRect();
    int identityKey = int.MinValue;
    string heroName = "", place = "", clock = "", status = "";
    int level = 1;
    float experience;
    bool defeated, highContrast;
    SurfaceBox trackerStyle = null!;

    /// <summary>High contrast makes the tracker opaque with an ivory border; otherwise the world shows through.</summary>
    public void ApplyContrast(bool high)
    {
        highContrast = high;
        trackerStyle = new SurfaceBox { Fill = high ? new Color("040706") : new Color("070d0c", .74f), Border = high ? UiDesign.Ink : new Color(UiDesign.Gold, .32f),
            BorderWidth = high ? 2 : 1, Radius = 12, Accent = UiDesign.Gold, AccentEdge = AccentEdge.Left, AccentWidth = 3 };
        trackerStyle.ContentMarginLeft = 18; trackerStyle.ContentMarginRight = 12; trackerStyle.ContentMarginTop = 7; trackerStyle.ContentMarginBottom = 8;
        tracker?.AddThemeStyleboxOverride("panel", trackerStyle);
        QueueRedraw();
    }

    public GameplayHud() { MouseFilter = MouseFilterEnum.Ignore; }

    public override void _Ready()
    {
        HealthBar = AddBar("HP", UiDesign.Health, 26, 20);
        ManaBar = AddBar("MP", UiDesign.Mana, 50, 16);
        ExperienceBar = AddBar("XP", UiDesign.Experience, 70, 20);
        ExperienceBar.Thin = true;
        tracker = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, Position = new Vector2(0, 102), Size = new Vector2(MaxWidth, 92), ClipContents = true };
        ApplyContrast(UiDesign.HighContrast);
        AddChild(tracker);
        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 3);
        tracker.AddChild(column);
        var head = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        head.AddThemeConstantOverride("separation", 7);
        column.AddChild(head);
        head.AddChild(new Glyph("diamond", UiDesign.Gold, 11));
        objectiveHeading = HudLabel(UiDesign.BodySemi, 14, UiDesign.Gold, head);
        objectiveHeading.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        objectiveHeading.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        objectiveText = HudLabel(UiDesign.Body, 16, UiDesign.Ink, column);
        // ClipText would collapse an autowrapped label to zero height inside the column.
        objectiveText.ClipText = false;
        objectiveText.CustomMinimumSize = new Vector2(0, 42);
        objectiveText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        objectiveText.MaxLinesVisible = 2;
        objectiveText.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        tracker.Visible = false;
    }

    Label HudLabel(Font font, int size, Color color, Node parent)
    {
        var label = new Label { MouseFilter = MouseFilterEnum.Ignore, ClipText = true };
        label.AddThemeFontOverride("font", font);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, .8f));
        label.AddThemeConstantOverride("outline_size", 3);
        parent.AddChild(label);
        return label;
    }

    HudBar AddBar(string name, Color fill, float y, float height)
    {
        var bar = new HudBar { Caption = name, Fill = fill, Position = new Vector2(BarsLeft, y), Size = new Vector2(MaxWidth - BarsLeft, height) };
        AddChild(bar); return bar;
    }

    /// <summary>Fixed rectangle; never depends on text or values.</summary>
    public void Arrange(Vector2 position, float availableWidth)
    {
        float width = Math.Clamp(availableWidth, 220, MaxWidth);
        Position = position; Size = new Vector2(width, Height);
        foreach (var bar in new[] { HealthBar, ManaBar, ExperienceBar }) bar.Size = new Vector2(width - BarsLeft, bar.Size.Y);
        tracker.Size = new Vector2(width, 92);
        QueueRedraw();
    }

    public void Refresh(ActorState player, string region, double worldTime)
    {
        HealthBar.Set(player.Health, player.MaxHealth, warnBelow: .3f);
        ManaBar.Set(player.Mana, player.MaxMana);
        ExperienceBar.Set(player.Experience, player.Level * 100);
        int minutes = (int)(worldTime / 60) % 24 * 60 + (int)worldTime % 60;
        float xp = Math.Clamp(player.Experience / Math.Max(1f, player.Level * 100), 0, 1);
        int key = HashCode.Combine(player.Level, region, minutes, player.Alive, player.State == ActionState.Dodging, (int)(xp * 100), player.Name);
        if (key == identityKey) return;
        identityKey = key;
        status = !player.Alive ? "DEFEATED" : player.State == ActionState.Dodging ? "dodging" : "";
        place = region == "ruin" ? "Ruin" : region == "reach" ? "Reach" : region;
        clock = $"{minutes / 60:00}:{minutes % 60:00}";
        heroName = player.Name; level = player.Level; experience = xp; defeated = !player.Alive;
        IdentityText = $"{player.Name} · Lv {player.Level} · {place} {clock}{(status == "" ? "" : " · " + status)}";
        QueueRedraw();
    }

    public override void _Draw()
    {
        // Level medallion: dark disc, engraved rim and an experience arc.
        var centre = new Vector2(Medallion / 2, Medallion / 2 + 4);
        float r = Medallion / 2;
        DrawCircle(centre + new Vector2(0, 3), r + 1, new Color(0, 0, 0, .45f), true, -1, true);
        DrawCircle(centre, r, highContrast ? Colors.Black : new Color("0c1412", .92f), true, -1, true);
        DrawArc(centre, r - 4, 0, MathF.Tau, 48, new Color(1, 1, 1, .10f), 4, true);
        if (experience > 0) DrawArc(centre, r - 4, -MathF.PI / 2, -MathF.PI / 2 + MathF.Tau * experience, 48, UiDesign.Experience.Lightened(.15f), 4, true);
        DrawArc(centre, r, 0, MathF.Tau, 48, defeated ? UiDesign.Ember : UiDesign.GoldDeep.Lightened(.2f), 1.5f, true);
        var semi = UiDesign.BodySemi; var outline = new Color(0, 0, 0, .85f);
        string lv = level.ToString();
        int size = lv.Length > 2 ? 18 : 24;
        float baseline = centre.Y + (semi.GetAscent(size) - semi.GetDescent(size)) / 2 - 4;
        DrawString(semi, new Vector2(centre.X - r, baseline), lv, HorizontalAlignment.Center, Medallion, size, UiDesign.GoldBright);
        DrawString(semi, new Vector2(centre.X - r, centre.Y + 18), "LV", HorizontalAlignment.Center, Medallion, 10, UiDesign.Faint);
        // Name, place and time.
        float y = 17, left = BarsLeft;
        DrawStringOutline(semi, new Vector2(left, y), heroName, HorizontalAlignment.Left, -1, 17, 4, outline);
        DrawString(semi, new Vector2(left, y), heroName, HorizontalAlignment.Left, -1, 17, UiDesign.Ink);
        string right = (status == "" ? "" : status + "  ·  ") + place + "  " + clock;
        var body = UiDesign.BodyMedium;
        DrawStringOutline(body, new Vector2(left, y), right, HorizontalAlignment.Right, Size.X - left, 14, 4, outline);
        DrawString(body, new Vector2(left, y), right, HorizontalAlignment.Right, Size.X - left, 14, defeated ? UiDesign.Ember : UiDesign.Muted);
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
    public bool Thin { get; set; }
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
        var fill = Warning ? Fill.Lightened(.12f) : Fill;
        Meter.DrawBar(this, new Rect2(Vector2.Zero, Size), Value / Maximum, fill,
            Warning ? Caption + "  ·  LOW" : Caption, $"{Math.Ceiling(Value):0} / {Maximum:0}", Thin, Warning);
    }
}
