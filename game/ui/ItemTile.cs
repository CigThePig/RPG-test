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
        CustomMinimumSize = new Vector2(78 * scale, 90 * scale);
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
            Texture = ItemIconAtlas.Get(item.Id), CustomMinimumSize = new Vector2(46 * scale, 46 * scale),
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
            // Quantity is a corner badge, not a third line that steals the
            // item name's height at 140% text scale on a compact phone.
            var badge = new PanelContainer { Name = "quantity_badge",
                MouseFilter = MouseFilterEnum.Ignore };
            badge.AddThemeStyleboxOverride("panel", new StyleBoxFlat {
                BgColor = UiDesign.HighContrast ? new Color("0b1312") : new Color("20332e"),
                BorderColor = UiDesign.Border,
                BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
                CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5,
                CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5,
                ContentMarginLeft = 3, ContentMarginRight = 3,
                ContentMarginTop = 0, ContentMarginBottom = 0
            });
            AddChild(badge);
            badge.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight);
            badge.OffsetLeft = -43 * scale;
            badge.OffsetRight = -3 * scale;
            badge.OffsetTop = 3 * scale;
            badge.OffsetBottom = 25 * scale;
            var amount = new Label {
                Text = "×" + item.Count,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore
            };
            amount.AddThemeFontSizeOverride("font_size", (int)(13 * scale));
            amount.AddThemeColorOverride("font_color", UiDesign.Ink);
            badge.AddChild(amount);
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
