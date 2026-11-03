using Godot;
namespace Mosslight;

/// <summary>Part 2's independent icon, layout, input and visual-identity gates.</summary>
public static class PackPresentationChecks
{
    static readonly string[] Known = { "iron_sword", "oak_staff", "leather_coat",
        "potion", "herb", "ore", "wood", "coin", "relic", "dungeon_key" };

    public static void Run(Foundation app, Action<bool,string> check)
    {
        var textures = Known.Select(ItemIconAtlas.Get).ToArray();
        check(textures.Length == 10 && textures.All(t => t.GetWidth() == 32 && t.GetHeight() == 32),
            "all ten catalog icons have 32x32 source pixels");
        check(textures.Distinct().Count() == 10, "all ten item kinds use individual cached art");
        check(Known.All(id => ReferenceEquals(ItemIconAtlas.Get(id),
            textures[Array.IndexOf(Known, id)])), "reopening items never regenerates textures");
        check(ReferenceEquals(ItemIconAtlas.Get("unknown_from_future"), ItemIconAtlas.Get("unrecognized")),
            "future item IDs share a safe unknown icon");
        check(ItemIconAtlas.CachedCount == 11, "icon cache is bounded by known types plus fallback");

        check(PackLayout.ChooseMode(new Vector2(1096,640),1) == PackLayout.LayoutKind.Wide,
            "S25-like landscape uses the three-region composition");
        check(PackLayout.ChooseMode(new Vector2(840,632),1) == PackLayout.LayoutKind.Compact,
            "medium landscape switches to two-region composition");
        check(PackLayout.ChooseMode(new Vector2(716,400),1.4f) == PackLayout.LayoutKind.Single,
            "small viewport with largest text uses a single readable region");

        var state = new PackViewState();
        var rows = app.World.Items.View(app.World.Player.Id);
        var bag = rows.FirstOrDefault(v => !v.Equipped);
        if (bag != null)
        {
            state.SelectBag(bag.Selector);
            check(state.Reconcile(rows)?.Selector == bag.Selector && state.EquippedSlot == null,
                "selection reuses the Gloot selector without modifying inventory");
            state.Reconcile(Array.Empty<ItemView>());
            check(state.Selector == null && !state.DetailOpen,
                "stale selector after load or consumption is discarded");
        }
        var worn = rows.FirstOrDefault(v => v.Equipped && v.EquippedSlot is "weapon" or "body");
        if (worn != null)
        {
            state.SelectEquipped(worn.EquippedSlot, worn.Selector);
            check(state.Reconcile(rows)?.Equipped == true &&
                state.EquippedSlot == worn.EquippedSlot, "equipped selection identifies a real Gloot slot");
        }
    }

    public static async System.Threading.Tasks.Task Rendered(Foundation app)
    {
        var ui=app.Ui;
        var window=app.GetWindow();
        var windowBefore=window.Size;
        var safeBefore=ui.VerificationSafeArea;
        var preferences=ui.Preferences.Copy();
        var tabBefore=ui.PackTab;
        var selectionBefore=ui.PackSelection;
        string save=Persistence.Capture(app.World, app.Narrative);
        string prefix=ui.MobileLayout ? "mobile-" : "";
        async System.Threading.Tasks.Task Frames(int count=6)
        { for(int i=0;i<count;i++)await app.ToSignal(app.GetTree(),SceneTree.SignalName.ProcessFrame); }
        void Require(bool ok,string reason)
        { if(!ok)throw new InvalidOperationException("Wayfarer's Kit: "+reason); }
        T? Find<T>(string name) where T:Node => ui.FindChild(name,true,false) as T;
        async System.Threading.Tasks.Task Shot(string title)
        {
            await Frames();
            Directory.CreateDirectory("docs/native-screenshots");
            await app.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            app.GetViewport().GetTexture().GetImage().SavePng("docs/native-screenshots/"+prefix+title+".png");
        }
        async System.Threading.Tasks.Task Resize(int w,int h,float scale=1f)
        {
            window.Size=new Vector2I(w,h);
            await Frames();
            // Under Xvfb the root Godot viewport can remain at 1280x800 even
            // after Window.Size changes. Exercise the requested logical safe
            // rectangle explicitly rather than re-reading the stale root size.
            var visible=app.GetViewport().GetVisibleRect();
            ui.VerificationSafeArea=new Rect2(visible.Position+new Vector2(60,28),
                new Vector2(w-120,h-56));
            ui.SetTextScale(scale);
            ui.PackTab=0;ui.Inventory();
            await Frames(8);
        }
        try
        {
            if(app.World.Items.Count("player","potion")==0)app.World.Items.Add("player","potion");
            ui.PackState.SelectBag(null);ui.PackState.GearOpen=false;ui.PackState.BagScroll=0;
            ui.PauseForVerification();
            await Resize(1280,800);
            Require(Find<PackLayout>("wayfarers_kit")?.Mode == PackLayout.LayoutKind.Wide,
                "1280x800 is three-region");
            foreach(var region in new[]{"equipment_panel","backpack_panel","inspector_panel"})
                Require(Find<Control>(region)?.IsVisibleInTree()==true,region+" always visible in wide mode");
            Require(ui.ScreenRect.Encloses(ui.ScreenBack.GetGlobalRect()),
                "fixed Back remains inside the safe screen");
            await Shot("pack-default-wide");

            var potion=Find<ItemTile>("item_potion");
            Require(potion!=null,"potion tile renders an actual stack");
            potion!.EmitSignal(Button.SignalName.Pressed);await Frames();
            Require(ui.PackSelection=="potion" && Find<Button>("use")?.IsVisibleInTree()==true,
                "tap selects consumable and exposes an explicit Use action");
            Require(Find<ItemTile>("item_potion")?.IsSelected==true,
                "selected state is separate from pressed state");
            await Shot("pack-selected-potion");

            var eq=app.World.Items.View("player").FirstOrDefault(v=>v.Equipped && v.EquippedSlot=="weapon");
            if(eq!=null)
            {
                Find<Button>("inspect_weapon")!.EmitSignal(Button.SignalName.Pressed);await Frames();
                Require(ui.PackState.EquippedSlot=="weapon" && Find<Button>("unequip_inspector_weapon")!=null,
                    "equipped weapon opens an Unequip inspector, not Equip");
                await Shot("pack-equipped-weapon");
            }

            ui.PackState.DetailOpen=false;
            await Resize(1024,768);
            Require(Find<PackLayout>("wayfarers_kit")?.Mode == PackLayout.LayoutKind.Compact,
                "1024x768 uses compact two-region arrangement; actual " + Find<PackLayout>("wayfarers_kit")?.Mode + " panel " + ui.ScreenRect.Size + " viewport " + app.GetViewport().GetVisibleRect().Size + " scale " + ui.TextScale);
            var selected=Find<ItemTile>("item_potion");
            Require(selected!=null,"compact backpack shows real items");
            selected!.EmitSignal(Button.SignalName.Pressed);await Frames();
            Require(Find<Control>("inspector_panel")?.IsVisibleInTree()==true &&
                Find<Control>("backpack_panel")?.IsVisibleInTree()==false,
                "compact inspection swaps only the right region");
            await Shot("pack-compact-inspector");
            ui.Back();await Frames();
            Require(ui.Navigation.Screen=="PACK & EQUIPMENT" &&
                Find<Control>("backpack_panel")?.IsVisibleInTree()==true,
                "compact Back returns locally to backpack, preserving caller");

            var large=preferences.Copy();large.HighContrast=true;large.TextSize=2;
            ui.ApplyPreferences(large,persist:false);
            ui.PackState.DetailOpen=false;ui.PackState.GearOpen=false;
            await Resize(900,560,1.4f);
            Require(Find<PackLayout>("wayfarers_kit")?.Mode == PackLayout.LayoutKind.Single,
                "900x560 largest text chooses single region");
            Require(ui.ScreenRect.Encloses(ui.ScreenBack.GetGlobalRect()),
                "largest text keeps Back in safe bounds");
            await Shot("pack-compact-900x560-high-contrast");
            var gear=Find<Button>("view_equipment");
            Require(gear!=null,"single-region layout offers Equipment explicitly");
            gear!.EmitSignal(Button.SignalName.Pressed);await Frames();
            Require(Find<Control>("equipment_panel")?.IsVisibleInTree()==true,
                "single region switches to gear without a fake slot");
            ui.Back();await Frames();
            Require(Find<Control>("backpack_panel")?.IsVisibleInTree()==true,
                "single-region Back restores backpack");

            ui.PackSelection=null;ui.PackState.DetailOpen=false;ui.Inventory();await Frames();
            var tile=Find<ItemTile>("item_potion");
            var scroll=Find<TouchScroll>("backpack_scroll");
            Require(tile!=null && scroll!=null && scroll.IsAncestorOf(tile),
                "bag tiles have exactly one scrolling owner");
            scroll!.EnsureControlVisible(tile!);await Frames();
            Vector2 at=tile!.GetGlobalRect().GetCenter(), end=at-new Vector2(0,78);
            Input.ParseInputEvent(new InputEventMouseButton {
                Device=(int)InputEvent.DeviceIdEmulation,ButtonIndex=MouseButton.Left,Pressed=true,Position=at });
            Input.ParseInputEvent(new InputEventScreenTouch {Index=47,Pressed=true,Position=at});
            Input.ParseInputEvent(new InputEventMouseButton {
                Device=(int)InputEvent.DeviceIdEmulation,ButtonIndex=MouseButton.Left,Pressed=false,Position=at});
            tile.GrabFocus();
            Input.ParseInputEvent(new InputEventScreenDrag {Index=47,Position=end,Relative=end-at});
            Input.ParseInputEvent(new InputEventScreenTouch {Index=47,Pressed=false,Position=end});
            await Frames();
            Require(ui.PackSelection==null && tile.Modulate==Colors.White && !tile.HasFocus(),
                "dragging a tile never selects, activates or leaves it dark/focused; selector=" +
                (ui.PackSelection ?? "(null)") + " tint=" + tile.Modulate +
                " focused=" + tile.HasFocus() + " hit=" + scroll.GetGlobalRect().HasPoint(at) +
                " at=" + at + " scroll=" + scroll.GetGlobalRect() +
                " touches=" + scroll.DiagnosticTouches + " swipes=" + scroll.DiagnosticSwipes +
                " focusReleased=" + scroll.DiagnosticFocusReleases +
                " scrollY=" + scroll.ScrollVertical);
        }
        finally
        {
            window.Size=windowBefore;ui.VerificationSafeArea=safeBefore;
            Persistence.Restore(app.World,app.Narrative,save);app.RebuildViews();
            ui.PackState.SelectBag(null);ui.PackState.GearOpen=false;ui.PackState.BagScroll=0;
            ui.PackTab=tabBefore;ui.PackSelection=selectionBefore;
            ui.ApplyPreferences(preferences,persist:false);
            ui.PauseForVerification();await Frames(3);
        }
    }
}
