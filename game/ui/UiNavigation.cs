namespace Mosslight;

/// <summary>Presentation state only. Foundation remains the authority for gameplay.</summary>
public sealed class UiNavigation
{
    public string? Screen { get; private set; }
    public bool Menu { get; private set; } = true;
    public bool Confirmation { get; private set; }
    bool returnToMenu;
    public bool ReturnsToMenu => returnToMenu;
    public bool Blocked => Menu || Screen != null || Confirmation;
    public void Open(string screen)
    {
        if (Screen == null) returnToMenu = Menu;
        Screen = screen; Menu = false;
    }
    public void Pause() { Screen = null; Confirmation = false; Menu = true; returnToMenu = false; }
    public void Resume() { Screen = null; Confirmation = false; Menu = false; returnToMenu = false; }
    public void Confirm() => Confirmation = true;
    public void CancelConfirmation() => Confirmation = false;
    public void Back(bool dialogue, bool alive)
    {
        if (dialogue) return;
        if (Confirmation) { Confirmation = false; return; }
        if (Screen != null) { Screen = null; Menu = returnToMenu || !alive; returnToMenu = false; return; }
        if (Menu && alive) Resume(); else Pause();
    }
}
