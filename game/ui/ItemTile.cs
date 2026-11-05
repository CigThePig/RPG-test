using Godot;
using System;

namespace Mosslight;

/// <summary>One selectable Gloot stack or instance shown as an inventory slot.</summary>
public partial class ItemTile : Button
{
    public const float Width = 86, Height = 100;
    public ItemView Item { get; }
    public bool IsSelected { get; private set; }

    public ItemTile(ItemView item, float scale, Action select)
    {
        Item = item;
        Name = "item_" + item.Selector.ToLowerInvariant().Replace(':', '_');
        CustomMinimumSize = new Vector2(Width * scale, Height * scale);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Pass;
        FocusMode = FocusModeEnum.All;
        ThemeTypeVariation = "SlotButton";
        TooltipText = item.Title + (item.Count > 1 ? " ×" + item.Count : "") +
            (PackScreen.Describe(item) is var d && d != "" ? " · " + d : "");
        Pressed += select;
        var content = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        AddChild(content);
        content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        content.OffsetTop = 5 * scale; content.OffsetBottom = -3 * scale;
        content.OffsetLeft = 4 * scale; content.OffsetRight = -4 * scale;
        content.AddThemeConstantOverride("separation", 2);
        var iconHolder = new Control { MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(0, 50 * scale) };
        content.AddChild(iconHolder);
        var halo = new Halo { Tint = PackScreen.KindTone(item) == Tone.Neutral ? UiDesign.Gold : UiDesign.ToneColor(PackScreen.KindTone(item)) };
        iconHolder.AddChild(halo); halo.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var icon = new TextureRect {
            Texture = ItemIconAtlas.Get(item.Id), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest, MouseFilter = MouseFilterEnum.Ignore
        };
        iconHolder.AddChild(icon); icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var title = new Label {
            Text = item.Title, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 36 * scale), MaxLinesVisible = 2,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            MouseFilter = MouseFilterEnum.Ignore, ThemeTypeVariation = "RowSubtitle"
        };
        title.AddThemeFontSizeOverride("font_size", (int)(13 * scale));
        title.AddThemeConstantOverride("line_spacing", -2);
        content.AddChild(title);
        if (item.Count > 1)
        {
            // Quantity is a corner badge, not a third line that steals the
            // item name's height at 140% text scale on a compact phone.
            var badge = new PanelContainer { Name = "quantity_badge", MouseFilter = MouseFilterEnum.Ignore, ThemeTypeVariation = "ChipGold" };
            AddChild(badge);
            badge.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight);
            badge.GrowHorizontal = GrowDirection.Begin;
            badge.OffsetRight = -4 * scale; badge.OffsetTop = 4 * scale;
            var amount = new Label { Text = "×" + item.Count, ThemeTypeVariation = "ChipGoldLabel",
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
            amount.AddThemeFontSizeOverride("font_size", (int)(12 * scale));
            badge.AddChild(amount);
        }
        if (item.Instance != "" && item.Properties.TryGetValue("quality", out var quality))
        {
            var pip = new Label { Text = "Q" + quality, MouseFilter = MouseFilterEnum.Ignore, ThemeTypeVariation = "CaptionLabel" };
            pip.AddThemeFontSizeOverride("font_size", (int)(11 * scale));
            AddChild(pip);
            pip.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
            pip.OffsetLeft = 8 * scale; pip.OffsetTop = 4 * scale;
        }
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        // Selected uses its own raised face with a gold edge and glow; never Modulate,
        // which a touch gesture also uses for momentary press feedback.
        UiDesign.SetSelected(this, selected, "SlotButton", "SlotSelected");
    }
}

/// <summary>An unoccupied backpack cell: decoration only, never a selectable stack.</summary>
public partial class EmptySlot : Panel
{
    public EmptySlot(float scale)
    {
        MouseFilter = MouseFilterEnum.Ignore; FocusMode = FocusModeEnum.None;
        CustomMinimumSize = new Vector2(ItemTile.Width * scale, ItemTile.Height * scale);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeStyleboxOverride("panel", new SurfaceBox { Fill = new Color(UiDesign.Inset, UiDesign.HighContrast ? 1 : .55f), Border = new Color(UiDesign.Line, .7f), Radius = 12 });
    }
}
