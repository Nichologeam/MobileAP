using System;
using System.Collections;
using System.Collections.Generic;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TextClient : MonoBehaviour
{
    private UIHandler UI;
    [SerializeField] private HintList hintHandler;
    public TMP_InputField address;
    public TMP_InputField slot;
    public TMP_InputField password;
    public Button connectButton;
    public TMP_Text errorMessage;
    public TMP_Text messageFeed;
    public ScrollRect messageFeedScroll;
    public TMP_InputField chatBox;
    private float virtualKeyboardHeight;
    public TMP_Text itemList;
    public GameObject messageFeedTab;
    public GameObject itemTab;
    public GameObject hintTab;
    public static ArchipelagoSession session;
    private Dictionary<string, long> receivedItemsDict = new();
    public struct APMobileHint
    {
        public string finderName;
        public string receiverName;
        public string itemName;
        public string gameName;
        public string locationName;
        public ItemFlags flags;
        public HintStatus status;
    }

    private void Start()
    {
        UI = GetComponent<UIHandler>();
        StartCoroutine(CheckKeyboardHeight());
    }
    public void SendMessage()
    {
        session.Say(chatBox.text);
        chatBox.text = "";
    }
    public void Connect()
    {
        connectButton.interactable = false;
        if (address.text == "" || slot.text == "")
        {
            errorMessage.text = "Address and Slot are required.";
            connectButton.interactable = true;
            return;
        }
        if (address.text.ToLower() == "localhost")
        {
            errorMessage.text = "Due to security restrictions, unsecure websockets are unsupported.";
            connectButton.interactable = true;
            return;
        }
        StartCoroutine(ConnectRoutine());
    }
    public void Disconnect(string reason = null)
    {
        session.Socket.DisconnectAsync();
        session = null;
        receivedItemsDict.Clear();
        hintHandler.Clear();
        messageFeed.text = "";
        itemList.text = "";
        if (messageFeedTab.activeSelf)
        {
            UI.SwitchTabs(messageFeedTab, UI.textClientConnect, false);
        }
        if (itemTab.activeSelf)
        {
            UI.SwitchTabs(itemTab, UI.textClientConnect, false);
        }
        if (hintTab.activeSelf)
        {
            UI.SwitchTabs(hintTab, UI.textClientConnect, false);
        }
        if (!string.IsNullOrWhiteSpace(reason))
        {
            errorMessage.text = $"Disconnected: {reason}";
        }
        else
        {
            errorMessage.text = $"Disconnected.";
        }
    }
    public void ItemButton()
    {
        if (messageFeedTab.activeSelf)
        {
            UI.SwitchTabs(messageFeedTab, itemTab, false);
        }
        else
        {
            UI.SwitchTabs(hintTab, itemTab, false);
        }
        UpdateItemDisplay();
    }
    public void ClientButton()
    {
        if (itemTab.activeSelf)
        {
            UI.SwitchTabs(itemTab, messageFeedTab, true);
        }
        else
        {
            UI.SwitchTabs(hintTab, messageFeedTab, false);
        }
    }
    public void HintButton()
    {
        if (itemTab.activeSelf)
        {
            UI.SwitchTabs(itemTab, hintTab, true);
        }
        else
        {
            UI.SwitchTabs(messageFeedTab, hintTab, true);
        }
    }
    private IEnumerator ConnectRoutine()
    {
        try
        {
            session = ArchipelagoSessionFactory.CreateSession($"wss://{address.text}");
            session.Items.ItemReceived += (item) => UpdateItemList(item);
            session.MessageLog.OnMessageReceived += message => ProcessMessage(message);
            var result = session.TryConnectAndLogin
                ("", slot.text, ItemsHandlingFlags.AllItems, new Version(0,6,6), tags: new string[] {"TextOnly","Mobile"}, password: password.text);
            connectButton.interactable = true;
            if (result is LoginFailure)
            {
                errorMessage.text = "";
                var error = result as LoginFailure;
                foreach (string msg in error.Errors)
                {
                    errorMessage.text += $"{msg}\n";
                }
            }
            if (result is LoginSuccessful)
            {
                OnConnect();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
            errorMessage.text = ex.ToString();
        }
        yield return null;
    }
    private void OnConnect()
    {
        UI.SwitchTabs(UI.textClientConnect, messageFeedTab, true);
        session.Hints.TrackHints(hints => ProcessHint(hints), true);
        messageFeed.fontSize = PlayerPrefs.GetInt("fontSize", 35);
        itemList.fontSize = PlayerPrefs.GetInt("fontSize", 35);
        PlayerPrefs.SetString("textAddress", address.text);
        PlayerPrefs.SetString("textSlot", slot.text);
        PlayerPrefs.SetString("textPass", password.text);
        PlayerPrefs.Save();
    }
    private void UpdateItemList(ReceivedItemsHelper helper)
    {
        var item = helper.DequeueItem();
        if (receivedItemsDict.ContainsKey(item.ItemName))
        {
            receivedItemsDict[item.ItemName] = receivedItemsDict[item.ItemName] + 1;
        }
        else
        {
            receivedItemsDict.Add(item.ItemName, 1);
        }
    }
    private void UpdateItemDisplay()
    {
        itemList.text = "";
        foreach (var kvp in receivedItemsDict)
        {
            if (kvp.Value > 1)
            {
                ThreadManager.RunOnMainThread(() => itemList.text = $"{itemList.text}{kvp.Value} {kvp.Key}s<br>");
            }
            else
            {
                ThreadManager.RunOnMainThread(() => itemList.text = $"{itemList.text}{kvp.Key}<br>");
            }
        }
    }
    private void ProcessMessage(LogMessage message)
    {
        /* Color Key
        <color=#FA8072>trap</color>
        <color=#AF99EF>progression</color>
        <color=#6D8BE8>useful</color>
        <color=#00EEEE>filler</color>
        <color=#EE00EE>myself</color>
        <color=#FAFAD2>someone else</color>
        <color=#00FF7F>location</color>
        */
        string processedMessage;
        string itemColor = "00EEEE"; // default to filler
        switch (message)
        {
            case HintItemSendLogMessage hint:
                if (hint.Item.Flags.HasFlag(ItemFlags.Advancement)) // progression
                {
                    itemColor = "AF99EF";
                }
                else if (hint.Item.Flags.HasFlag(ItemFlags.NeverExclude)) // useful
                {
                    itemColor = "6D8BE8";
                }
                else if (hint.Item.Flags.HasFlag(ItemFlags.Trap)) // trap
                {
                    itemColor = "FA8072";
                }
                if (hint.IsSenderTheActivePlayer)
                {
                    processedMessage = $"<color=#FAFAD2>{hint.Receiver.Alias}</color>'s <color=#{itemColor}>{hint.Item.ItemName}</color> is at (<color=#00FF7F>{hint.Item.LocationName}</color>) in <color=#EE00EE>{hint.Sender.Alias}</color>'s World.";
                }
                else if (hint.IsReceiverTheActivePlayer)
                {
                    processedMessage = $"<color=#EE00EE>{hint.Receiver.Alias}</color>'s <color=#{itemColor}>{hint.Item.ItemName}</color> is at (<color=#00FF7F>{hint.Item.LocationName}</color>) in <color=#FAFAD2>{hint.Sender.Alias}</color>'s World.";
                }
                else
                {
                    processedMessage = $"<color=#FAFAD2>{hint.Receiver.Alias}</color>'s <color=#{itemColor}>{hint.Item.ItemName}</color> is at (<color=#00FF7F>{hint.Item.LocationName}</color>) in <color=#FAFAD2>{hint.Sender.Alias}</color>'s World.";
                }
                ThreadManager.RunOnMainThread(() => messageFeed.text = $"{messageFeed.text}[Hint]: {processedMessage}<br>");
                ThreadManager.RunOnMainThread(() => StartCoroutine(AutoScroll()));
                break;

            case ItemSendLogMessage item:
                if (item.Item.Flags.HasFlag(ItemFlags.Advancement))
                {
                    itemColor = "AF99EF";
                }
                else if (item.Item.Flags.HasFlag(ItemFlags.NeverExclude))
                {
                    itemColor = "6D8BE8";
                }
                else if (item.Item.Flags.HasFlag(ItemFlags.Trap))
                {
                    itemColor = "FA8072";
                }
                if (item.Sender == item.Receiver)
                {
                    if (item.IsSenderTheActivePlayer)
                    {
                        processedMessage = $"<color=#EE00EE>{item.Sender.Alias}</color> found their <color=#{itemColor}>{item.Item.ItemName}</color> (<color=#00FF7F>{item.Item.LocationName}</color>)";
                    }
                    else
                    {
                        processedMessage = $"<color=#FAFAD2>{item.Sender.Alias}</color> found their <color=#{itemColor}>{item.Item.ItemName}</color> (<color=#00FF7F>{item.Item.LocationName}</color>)";
                    }
                    ThreadManager.RunOnMainThread(() => messageFeed.text = $"{messageFeed.text}{processedMessage}<br>");
                    ThreadManager.RunOnMainThread(() => StartCoroutine(AutoScroll()));
                    break;
                }
                if (item.IsSenderTheActivePlayer)
                {
                    processedMessage = $"<color=#EE00EE>{item.Sender.Alias}</color> sent <color=#{itemColor}>{item.Item.ItemName}</color> to <color=#FAFAD2>{item.Receiver.Alias}</color> (<color=#00FF7F>{item.Item.LocationName}</color>)";
                }
                else if (item.IsReceiverTheActivePlayer)
                {
                    processedMessage = $"<color=#FAFAD2>{item.Sender.Alias}</color> sent <color=#{itemColor}>{item.Item.ItemName}</color> to <color=#EE00EE>{item.Receiver.Alias}</color> (<color=#00FF7F>{item.Item.LocationName}</color>)";
                }
                else
                {
                    processedMessage = $"<color=#FAFAD2>{item.Sender.Alias}</color> sent <color=#{itemColor}>{item.Item.ItemName}</color> to <color=#FAFAD2>{item.Receiver.Alias}</color> (<color=#00FF7F>{item.Item.LocationName}</color>)";
                }
                ThreadManager.RunOnMainThread(() => messageFeed.text = $"{messageFeed.text}{processedMessage}<br>");
                ThreadManager.RunOnMainThread(() => StartCoroutine(AutoScroll()));
                break;

            default:
                ThreadManager.RunOnMainThread(() => messageFeed.text = $"{messageFeed.text}{message.ToString()}<br>");
                ThreadManager.RunOnMainThread(() => StartCoroutine(AutoScroll()));
                break;
        }
    }

    private IEnumerator AutoScroll()
    {
        if (PlayerPrefs.GetInt("autoscroll") == 1)
        {
            yield return 0;
            messageFeedScroll.verticalNormalizedPosition = 0f;
        }
        yield return null;
    }

    private void ProcessHint(Hint[] hints)
    {
        hintHandler.Clear();
        foreach (Hint hint in hints)
        {
            var finderGame = session.Players.GetPlayerInfo(hint.FindingPlayer).Game;
            var receiverGame = session.Players.GetPlayerInfo(hint.ReceivingPlayer).Game;
            ThreadManager.RunOnMainThread(() => hintHandler.AddHint(new APMobileHint()
            {
                finderName = session.Players.GetPlayerAlias(hint.FindingPlayer),
                receiverName = session.Players.GetPlayerAlias(hint.ReceivingPlayer),
                itemName = session.Items.GetItemName(hint.ItemId, receiverGame),
                gameName = finderGame,
                locationName = session.Locations.GetLocationNameFromId(hint.LocationId, session.Players.GetPlayerInfo(hint.FindingPlayer).Game),
                flags = hint.ItemFlags,
                status = hint.Status
            }));
        }
    }

    private IEnumerator CheckKeyboardHeight()
    {
        var chatboxRect = chatBox.GetComponent<RectTransform>();
        var scrollRect = messageFeedScroll.gameObject.GetComponent<RectTransform>();
        var sendRect = gameObject.transform.Find("Text Client Message Feed/Send Button").GetComponent<RectTransform>();
        var feedHeight = scrollRect.offsetMin; // area from bottom of the feed from the bottom of the screen
        var defaultBoxPos = chatboxRect.anchoredPosition; // don't hardcode these, since they also change depending on screen size
        var defaultSendPos = sendRect.anchoredPosition.x;
        while (true)
        {
            if (TouchScreenKeyboard.visible) // does work on all platforms (unless someone runs this on apple TV)
            {
                virtualKeyboardHeight = GetKeyboardHeight();
                float offset = virtualKeyboardHeight + defaultBoxPos.y; // how far from the bottom should the input be?
                chatboxRect.anchoredPosition = new(defaultBoxPos.x, offset); // put it there (default 40px clearance from keyboard to bottom of input)
                sendRect.anchoredPosition = new(defaultSendPos, offset); // same for the send button
                float bottomPadding = virtualKeyboardHeight + feedHeight.y; // how far from the bottom should be feed be?
                scrollRect.offsetMin = new Vector2(feedHeight.x, bottomPadding); // put it there
                yield return null; // do again the next frame
            }
            else
            {
                virtualKeyboardHeight = 0; // keyboard is closed, return 0
                float offset = virtualKeyboardHeight + defaultBoxPos.y; // returns just the default height
                chatboxRect.anchoredPosition = new(defaultBoxPos.x, offset); // just the 40px clearance from screen bottom to bottom of input
                sendRect.anchoredPosition = new(defaultSendPos, offset); // same for the send button
                float bottomPadding = virtualKeyboardHeight + feedHeight.y; // return default height
                scrollRect.offsetMin = new Vector2(feedHeight.x, bottomPadding); // default
                yield return new WaitForSecondsRealtime(0.3f); // check if the keyboard is open every 0.3 seconds
            }
        }
    }

    // Modified version of a helper function made by cankirici34 (https://discussions.unity.com/t/keyboard-height/563437/56)
    public float GetKeyboardHeight()
    {
        var referenceHeight = GetComponent<CanvasScaler>().referenceResolution.y;
        var referenceWidth = GetComponent<CanvasScaler>().referenceResolution.x;
#if UNITY_ANDROID
        using (var unityPlayerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        {
            var currentActivity = unityPlayerClass.GetStatic<AndroidJavaObject>("currentActivity");
            var unityPlayer = currentActivity.Get<AndroidJavaObject>("mUnityPlayer");
            var view = unityPlayer.Call<AndroidJavaObject>("getView");
            using (var rect = new AndroidJavaObject("android.graphics.Rect"))
            {
                view.Call("getWindowVisibleDisplayFrame", rect);
                int screenHeight = Screen.height;
                int rectHeight = rect.Call<int>("height");
                int keyboardHeight = screenHeight - rectHeight;
                float scaleFactor = GetComponent<Canvas>().scaleFactor;
                float keyboardHeightInCanvasUnits = keyboardHeight / scaleFactor;
                return keyboardHeightInCanvasUnits;
            }
        }
#else
        int screenHeight = Screen.height;
        int keyboardHeight = (int)TouchScreenKeyboard.area.height;
        float scaleFactor = screenHeight / referenceHeight;
        float keyboardHeightInCanvasUnits = keyboardHeight / scaleFactor;
        return keyboardHeightInCanvasUnits;
#endif
    }
}