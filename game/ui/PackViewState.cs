using System.Linq;

namespace Mosslight;

/// <summary>
/// Ephemeral UI state only. A Gloot selector is never a second inventory and is
/// reconciled against Items.View every time the pack is opened or mutated.
/// </summary>
public sealed class PackViewState
{
    public string? Selector { get; private set; }
    public string? EquippedSlot { get; private set; }
    public bool DetailOpen { get; set; }
    public bool GearOpen { get; set; }
    public int BagScroll { get; set; }

    public void SelectBag(string? selector)
    {
        Selector = selector;
        EquippedSlot = null;
        if (selector == null) DetailOpen = false;
    }

    public void SelectEquipped(string slot, string selector)
    {
        Selector = selector;
        EquippedSlot = slot;
    }

    public ItemView? Reconcile(IReadOnlyList<ItemView> snapshot)
    {
        if (Selector == null) return null;
        var selected = snapshot.FirstOrDefault(v => v.Selector == Selector &&
            (EquippedSlot == null ? !v.Equipped : v.Equipped && v.EquippedSlot == EquippedSlot));
        if (selected != null) return selected;
        // Item may have moved in a legitimate equip/unequip transaction.
        var moved = snapshot.FirstOrDefault(v => v.Selector == Selector);
        if (moved != null)
        {
            EquippedSlot = moved.Equipped ? moved.EquippedSlot : null;
            return moved;
        }
        SelectBag(null);
        return null;
    }
}
