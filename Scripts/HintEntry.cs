using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Archipelago.MultiClient.Net.Enums;

public class HintEntry : MonoBehaviour
{
    [Header("Text")]
    public TMP_Text locName;
    public TMP_Text finderName;
    public TMP_Text receiverAndItem;
    public TMP_Text statusText;

    [Header("Visuals")]
    public Image background;

    public RectTransform Rect { get; private set; }
    private bool clientType; // true is text client, false is manual client
    private static readonly Color32 FoundColor = new(0, 255, 127, 20); // same as #00FF7F, the color CommonClient uses
    private static readonly Color32 PriorityColor = new(175, 153, 239, 20); // again, the color CommonClient uses
    private static readonly Color32 AvoidColor = new(250, 128, 114, 20);
    private static readonly Color32 NoPriorityColor = new(0, 238, 238, 20);
    private static readonly Color32 DefaultColor = new(255, 255, 255, 20); // CommonClient technically uses #FEFEFE, but whatever

    public void SetHint(TextClient.APMobileHint hint)
    {
        Rect = (RectTransform)transform;
        if (TextClient.session != null)
        {
            clientType = true;
        }
        else if (ManualClient.session != null)
        {
            clientType = false;
        }
        else
        {
            Debug.LogError("A HintEntry exists, but no client is connected!");
        }
        locName.text = $"<color=#00FF7F>{hint.locationName}</color>";
        string itemColor = "00EEEE";
        if (hint.flags.HasFlag(ItemFlags.Advancement))
        {
            itemColor = "AF99EF";
        }
        else if (hint.flags.HasFlag(ItemFlags.NeverExclude))
        {
            itemColor = "6D8BE8";
        }
        else if (hint.flags.HasFlag(ItemFlags.Trap))
        {
            itemColor = "FA8072";
        }
        if (clientType)
        {
            if (hint.receiverName == TextClient.session.Players.ActivePlayer.Alias)
            {
                receiverAndItem.text = $"<color=#EE00EE>{hint.receiverName}</color>'s <color=#{itemColor}>{hint.itemName}</color>";
            }
            else
            {
                receiverAndItem.text = $"<color=#FAFAD2>{hint.receiverName}</color>'s <color=#{itemColor}>{hint.itemName}</color>";
            }
            if (hint.finderName == TextClient.session.Players.ActivePlayer.Alias)
            {
                finderName.text = $"<color=#EE00EE>{hint.finderName}</color>'s World ({hint.gameName})";
            }
            else
            {
                finderName.text = $"<color=#FAFAD2>{hint.finderName}</color>'s World ({hint.gameName})";
            }
        }
        else
        {
            if (hint.receiverName == ManualClient.session.Players.ActivePlayer.Alias)
            {
                receiverAndItem.text = $"<color=#EE00EE>{hint.receiverName}</color>'s <color=#{itemColor}>{hint.itemName}</color>";
            }
            else
            {
                receiverAndItem.text = $"<color=#FAFAD2>{hint.receiverName}</color>'s <color=#{itemColor}>{hint.itemName}</color>";
            }
            if (hint.finderName == ManualClient.session.Players.ActivePlayer.Alias)
            {
                finderName.text = $"<color=#EE00EE>{hint.finderName}</color>'s World ({hint.gameName})";
            }
            else
            {
                finderName.text = $"<color=#FAFAD2>{hint.finderName}</color>'s World ({hint.gameName})";
            }
        }
        if (background != null)
        {
            switch (hint.status)
            {
                case HintStatus.Found:
                    background.color = FoundColor;
                    statusText.text = "Found";
                    break;
                case HintStatus.Priority:
                    background.color = PriorityColor;
                    statusText.text = "Priority!";
                    break;
                case HintStatus.Avoid:
                    background.color = AvoidColor;
                    statusText.text = "Avoid...";
                    break;
                case HintStatus.NoPriority:
                    background.color = NoPriorityColor;
                    statusText.text = "Low Priority";
                    break;
                default: // either HintStatus.Unspecified, or hint.status is somehow none of the above
                    background.color = DefaultColor;
                    statusText.text = "Unspecified";
                    break;
            }
        }
    }
}