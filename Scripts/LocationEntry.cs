using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LocationEntry : MonoBehaviour
{
    public TMP_Text label;
    public Image background;
    private long id;
    bool isInLogic;
    bool isVictory;
    int confirmSendLoc;

    public void Initialize(ManualClient.LocationData data)
    {
        id = data.id;
        isVictory = data.victory;
        if (isVictory)
        {
            label.text = $"GOAL: {data.name}";
        }
        else
        {
            label.text = data.name;
        }
        if (ManualClient.customHooks)
        {
            SetState(true); // force to be in logic
        }
        confirmSendLoc = PlayerPrefs.GetInt("confirmSendLoc", 2);
    }

    public void SetState(bool inLogic)
    {
        isInLogic = inLogic;
        if (inLogic)
        {
            background.color = new Color32(100, 215, 50, 255);
        }
        else
        {
            background.color = new Color32(100, 100, 100, 255);
        }
    }

    public bool IsInLogic() => isInLogic; // true = in logic, false = out of logic

    public bool CheckLocationChecked() => ManualClient.session.Locations.AllLocationsChecked.Contains(id); // true = checked, false = unchecked

    public void SendLocation(bool bypass = false)
    {
        if (!bypass)
        {
            if (!isInLogic && confirmSendLoc == 2)
            {
                Debug.Log("Attempted to send out of logic location with allow set to never.");
                return;
            }
            if (!isInLogic && confirmSendLoc == 1)
            {
                Debug.Log("Attempted to send out of logic location with allow set to ask.");
                ShowConfirmation();
                return;
            }
        }
        Debug.Log($"Sending location {id}");
        if (isVictory)
        {
            ManualClient.session.SetGoalAchieved();
        }
        ManualClient.SendLocation(id); // fire and forget. we don't need a response, and multiclient will force a disconnect if the websocket is dead
        LocationList.instance.RemoveLocation(id);
    }

    private void ShowConfirmation()
    {
        var parent = LocationList.instance; // references stored on parent so not every entry needs them
        parent.confirmationPopup.SetActive(true); // parent object that holds the popup
        parent.confirmationName.text = $"Are you sure you want to send the out of logic location {label.text}?"; // set location name
        parent.confirmationAccept.onClick.RemoveAllListeners();
        parent.confirmationAccept.onClick.AddListener(() =>
        {
            parent.confirmationPopup.SetActive(false); // make the accept button close the popup...
            SendLocation(true); // ...and send the location (decline is always set to just close the popup)
        }); 
    }
}
