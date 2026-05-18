using Archipelago.MultiClient.Net.Enums;
using TMPro;
using UnityEngine;

public class ItemEntry : MonoBehaviour
{
    public TMP_Text item;
    public TMP_Text quantityText;

    private Color32 filler = new Color32 (0x00, 0xEE, 0xEE, 0xFF);
    private Color32 progression = new Color32 (0xAF, 0x99, 0xEF, 0xFF);
    private Color32 useful = new Color32 (0x6D, 0x8B, 0xE8, 0xFF);
    private Color32 trap = new Color32 (0xFA, 0x80, 0x72, 0xFF);

    public void SetItem(string itemName, ManualClient.ItemEntry itemInfo)
    {
        item.text = itemName;
        quantityText.text = $"x{itemInfo.Count}";
        gameObject.name = itemName;
        var flags = itemInfo.Info.Flags;
        Color color = filler; // default to filler
        if (flags.HasFlag(ItemFlags.Advancement)) // progression
        {
            color = progression;
        }
        else if (flags.HasFlag(ItemFlags.NeverExclude)) // useful
        {
            color = useful;
        }
        else if (flags.HasFlag(ItemFlags.Trap)) // trap
        {
            color = trap;
        }
        item.color = color;
    }
}