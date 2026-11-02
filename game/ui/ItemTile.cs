using Godot;
using System;

namespace Mosslight;

/// <summary>One selectable Gloot stack or instance, never an invented empty slot.</summary>
public partial class ItemTile : Button
{
    public ItemView Item { get; }
    public bool IsSelected { get; private set; }

    public ItemTile(ItemView item, float scale, Action select)
    {
        Item = item;
        Name = "item_" + item.Selector.ToLowerInvariant().Replace(':', '_');
        CustomMinimumSize = new Vector2(96 * scale, 110 * scale);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Pass;
        FocusMode = FocusModeEnum.All;
        TooltipText = item.Title + (item.Count > 1 ? " ×" + item.Count : "") +
            (PackScreen.Describe(item) is var d && d != "" ? " · " + d : "");
        Pressed += select;
        var content = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.Center };
        content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        content.AddThemeConstantOverride("separation", 2);
        AddChild(content);
        var icon = new TextureRect {
            Texture = ItemIconAtlas.Get(item.Id), CustomMinimumSize = new Vector2(50 * scale, 50 * scale),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest, MouseFilter = MouseFilterEnum.Ignore
        };
        content.AddChild(icon);
        var title = new Label {
            Text = item.Title, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 35 * scale), MaxLinesVisible = 2,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            MouseFilter = MouseFilterEnum.Ignore
        };
        title.AddThemeFontSizeOverride("font_size", (int)(13 * scale));
        content.AddChild(title);
        if (item.Count > 1)
        {
            var amount = new Label {
                Text = "×" + item.Count, HorizontalAlignment = HorizontalAlignment.Right,
                MouseFilter = MouseFilterEnum.Ignore
            };
            amount.AddThemeFontSizeOverride("font_size", (int)(15 * scale));
            amount.AddThemeColorOverride("font_color", UiDesign.Ink);
            content.AddChild(amount);
        }
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        var normal = UiDesign.InventoryTile(selected);
        AddThemeStyleboxOverride("normal", normal);
        AddThemeStyleboxOverride("hover", normal);
        AddThemeStyleboxOverride("pressed", UiDesign.InventoryTile(selected, pressed: true));
        // Focus gets its own outer ring from the theme. Selected never uses Modulate
        // or the pressed-black state left behind by a prior touchscreen gesture.
    }
}
