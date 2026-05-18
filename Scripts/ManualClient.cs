using System;
using System.IO;
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
using SimpleFileBrowser;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using System.IO.Compression;

public class ManualClient : MonoBehaviour
{
    private UIHandler UI;
    [SerializeField] private HintList hintHandler;
    [SerializeField] private ItemList itemHandler;
    public TMP_InputField address;
    public TMP_InputField slot;
    public TMP_InputField password;
    public Button connectButton;
    public GameObject processingScreen;
    public TMP_Text processingStatus;
    public TMP_Text errorMessage;
    public TMP_Text messageFeed;
    public ScrollRect messageFeedScroll;
    public TMP_InputField chatBox;
    public GameObject messageFeedTab;
    public GameObject itemTab;
    public GameObject hintTab;
    public GameObject locationsTab;
    public GameObject dlTab;
    public GameObject currentTab;
    public static ArchipelagoSession session;
    public static DeathLinkService dlHandler;
    public static Dictionary<string, object> slotdata = new();
    public TMP_InputField dlReason;
    private string APWorldPath;
    private bool processingWorld;
    private string manualGameID;
    private float virtualKeyboardHeight;
    public static bool customHooks;
    public static Dictionary<string, ItemEntry> receivedItemsDict = new();
    public class ItemEntry
    {
        public long Count;
        public ItemInfo Info;
    }
    // APManual Processing Values
    class APManualRoot
    {
        public string game;
        public string player_name;
        public Dictionary<string, ItemData> items;
        public Dictionary<string, LocationData> locations;
        public Dictionary<string, RegionData> regions;
    }
    public class ItemData
    {
        public string name;
        public List<string> category;
        public int id;
        public bool progression;
        public bool useful;
        public bool trap;
    }
    public class LocationData
    {
        public string name;
        public bool victory;
        public List<string> category;
        public JToken requires;
        public long id;
        public string region;
    }
    class RegionData
    {
        public bool starting;
        public List<string> connects_to;
        public JToken requires;
    }
    public class Requirement
    {
        public ItemData item;          // null if option
        public int amount;
        public bool optionRequirement; // true if this is a YamlEnabled option
        public string optionName;      // the string, e.g. "{YamlEnabled(Option)}"
    }
    Dictionary<int, ItemData> itemsById = new();
    Dictionary<string, ItemData> itemsByName = new();
    SortedDictionary<string, List<ItemData>> itemCategories = new();
    Dictionary<long, LocationData> locationsById = new();
    public static List<LocationData> locations = new();
    public static SortedDictionary<string, List<LocationData>> locationCategories = new();
    public static Dictionary<long, List<List<Requirement>>> locationRequirements = new();
    public static HashSet<string> enabledOptions = new();
    Dictionary<string, List<string>> itemToLocations;

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
    public void SelectAPManualButton()
    {
        if (processingWorld) // this shouldn't be possible to trigger, but better safe than sorry
        {
            errorMessage.text = "An APManual is already being processed, please wait.";
        }
        FileBrowser.SetFilters(true, new FileBrowser.Filter("APManual Files", ".apmanual")); // filter to only APManual files
        FileBrowser.ShowLoadDialog( // show the file browser
            path => { // when a file is selected
                processingWorld = true;
                try
                {
                    processingStatus.text = "Starting...";
                    processingScreen.SetActive(true);
                    StartCoroutine(ProcessAPManual(path[0], error => // this is a string[], but it will only contain one item, since it passes one path for each item
                    {
                        if (error != null)
                        {
                            errorMessage.text = $"Could not process APManual<br>{error}";
                            APWorldPath = null;
                            Debug.LogError("----- APMANUAL EXTRACTION FAILED -----");
                        }
                        processingWorld = false;
                        processingScreen.SetActive(false);
                        if (error == null)
                        {
                            errorMessage.text = $"{manualGameID} processed successfully!";
                            Debug.Log($"A total of {locationRequirements.Count} locations were processed to have logic.");
                            Debug.LogWarning("----- APMANUAL EXTRACTION FINISHED -----");
                            if (customHooks)
                            {
                                errorMessage.text = $"{manualGameID} processed successfully, but logic processing failed.<br>The client will still work, but location highlighting is disabled.";
                            }
                        }
                    }));
                } 
                catch (Exception ex)
                {
                    errorMessage.text = $"Uncaught Exception during APManual processing!<br>{ex}";
                    Debug.LogError($"Uncaught Exception during APManual processing!     {ex}");
                    APWorldPath = null;
                    processingWorld = false;
                    processingScreen.SetActive(false);
                }
            },
            () => { errorMessage.text = "No APManual was selected."; }, // when a file is not selected
            FileBrowser.PickMode.Files, // only allow files to be selected (not folders)
            allowMultiSelection: false, // we only need one APManual
            title: "Select APManual..." // set window title
        );
    }
    private IEnumerator ProcessAPManual(string path, Action<string> callback)
    {
        Debug.LogWarning("----- STARTING APMANUAL EXTRACTION -----");
        customHooks = false;
        APWorldPath = path;
        Debug.Log($"File location: {path}");
        processingStatus.text = "Creating temp directory...";
        byte[] rawBytes = FileBrowserHelpers.ReadBytesFromFile(path);
        if (rawBytes == null || rawBytes.Length == 0)
        {
            callback?.Invoke($"File either does not exist or could not be accessed.<br>Location read as: [{path}]");
            yield break;
        }
        string extractPath = Path.Combine(Application.persistentDataPath, "apmanual_temp");
        if (Directory.Exists(extractPath)) Directory.Delete(extractPath, true); // remove the directory if it already exists
        Directory.CreateDirectory(extractPath);
        processingStatus.text = "Detecting .apmanual format...";
        yield return null; // UI Update
        APManualRoot root = null;
        bool isZip = rawBytes.Length >= 4 && rawBytes[0] == 0x50 && rawBytes[1] == 0x4B && rawBytes[2] == 0x03 && rawBytes[3] == 0x04;
        if (isZip) // ZIP folder (new format)
        {
            Debug.Log(".apmanual is in new format (ZIP)");
            processingStatus.text = "Decoding .apmanual as ZIP...";
            yield return null; // UI Update
            try
            {
                root = new APManualRoot();
                using (var ms = new MemoryStream(rawBytes))
                using (var archive = new ZipArchive(ms, ZipArchiveMode.Read))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name)) continue; // skip empty files (shouldn't happen)
                        using var stream = entry.Open();
                        using var reader = new StreamReader(stream);
                        string json = reader.ReadToEnd();
                        switch (entry.Name.ToLower()) // names are already lowercase, but better safe than sorry
                        {
                            case "archipelago.json":
                            {
                                var apData = JObject.Parse(json);
                                root.game = apData["game"]?.ToString();
                                root.player_name = apData["player_name"]?.ToString();
                                string server = apData["server"]?.ToString();
                                if (!string.IsNullOrWhiteSpace(server)) address.text = server; // autoset address if downloaded from a webhost patch
                                break;
                            }
                            case "items.json":
                                root.items = JsonConvert.DeserializeObject<Dictionary<string, ItemData>>(json);
                                break;
                            case "locations.json":
                                root.locations = JsonConvert.DeserializeObject<Dictionary<string, LocationData>>(json);
                                break;
                            case "regions.json":
                                root.regions = JsonConvert.DeserializeObject<Dictionary<string, RegionData>>(json);
                                break;
                            case "categories.json":
                                // explicitly ignore. already handled below
                                break;
                            default:
                                Debug.LogWarning($"Ignoring unknown file in .apmanual: {entry.Name}");
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                callback?.Invoke($"Failed to parse .apmanual as ZIP.<br>{ex}");
                yield break;
            }
        }
        else // Base-64 (old format)
        {
            Debug.Log(".apmanual is in old format (Base-64)");
            processingStatus.text = "Decoding .apmanual as Base-64...";
            yield return null; // UI Update
            string base64 = System.Text.Encoding.UTF8.GetString(rawBytes);
            byte[] decoded = Convert.FromBase64String(base64);
            string json = System.Text.Encoding.UTF8.GetString(decoded);
            root = JsonConvert.DeserializeObject<APManualRoot>(json);
        }
        if (root == null) // root is still null
        {
            callback?.Invoke($"Failed to parse .apmanual file.<br>Detected as {(isZip ? "ZIP" : "Base-64")}");
            yield break;
        }
        if (root.locations == null) // no locations
        {
            callback?.Invoke($"Locations missing or could not be accessed.<br>.apmanual detected as {(isZip ? "ZIP" : "Base-64")}");
            yield break;
        }
        if (root.items == null) // no items
        {
            callback?.Invoke($"Items missing or could not be accessed.<br>.apmanual detected as {(isZip ? "ZIP" : "Base-64")}");
            yield break;
        }
        processingStatus.text = "Storing game information...";
        yield return null; // UI Update
        manualGameID = root.game;
        slot.text = root.player_name;
        processingStatus.text = "Processing items...";
        yield return null; // UI Update
        itemCategories["No Category"] = new List<ItemData>();
        int i = 0;
        foreach (var pair in root.items)
        {
            processingStatus.text = $"Processing items... ({i}/{root.items.Count})";
            ItemData item = pair.Value;
            itemsById[item.id] = item;
            itemsByName[item.name] = item;
            if (item.category == null || item.category.Count == 0)
            {
                itemCategories["No Category"].Add(item);
            }
            else
            {
                foreach (string cat in item.category)
                {
                    if (!itemCategories.ContainsKey(cat))
                        itemCategories[cat] = new List<ItemData>();

                    itemCategories[cat].Add(item);
                }
            }
            if (++i % 50 == 0) yield return null; // allow UI update every 50 items
        }
        processingStatus.text = "Processing locations...";
        yield return null; // UI Update
        locationCategories["No Category"] = new List<LocationData>();
        i = 0;
        foreach (var pair in root.locations)
        {
            processingStatus.text = $"Processing locations... ({i}/{root.locations.Count})";
            LocationData loc = pair.Value;
            locationsById[loc.id] = loc;
            locations.Add(loc);
            if (loc.category == null || loc.category.Count == 0)
            {
                locationCategories["No Category"].Add(loc);
            }
            else
            {
                foreach (string cat in loc.category)
                {
                    if (!locationCategories.ContainsKey(cat))
                        locationCategories[cat] = new List<LocationData>();
                    locationCategories[cat].Add(loc);
                }
            }
            if (++i % 50 == 0) yield return null; // allow UI update every 50 locations
        }
        processingStatus.text = "Processing regions...";
        yield return null; // UI Update
        Dictionary<string, RegionData> regions = root.regions;
        processingStatus.text = "Replicating logic...";
        yield return null; // UI Update
        locationRequirements = new Dictionary<long, List<List<Requirement>>>();
        itemToLocations = new Dictionary<string, List<string>>();
        foreach (var loc in locations)
        {
            List<string> regionReqs = new();
            if (!string.IsNullOrWhiteSpace(loc.region) && regions.TryGetValue(loc.region, out var region)) // region requirements
            {
                regionReqs.AddRange(NormalizeRequires(region.requires));
            }
            List<string> locReqs = NormalizeRequires(loc.requires); // location requirements
            string combinedString; // combine
            if (regionReqs.Count == 0) combinedString = string.Join(" AND ", locReqs);
            else if (locReqs.Count == 0) combinedString = string.Join(" AND ", regionReqs);
            else combinedString = string.Join(" AND ", regionReqs) + " AND " + string.Join(" AND ", locReqs);
            if (string.IsNullOrWhiteSpace(combinedString)) continue;
            var dnfStrings = DNFParser.Parse(combinedString);
            List<List<Requirement>> parsed = new();
            foreach (var group in dnfStrings)
            {
                List<Requirement> andReqs = new();
                foreach (var reqString in group)
                {
                    string trimmed = reqString.Trim('|').Trim();
                    string itemName = trimmed;
                    int count = 1;
                    if (trimmed.Contains(':'))
                    {
                        var split = trimmed.Split(':');
                        itemName = split[0];
                        int.TryParse(split[1], out count);
                    }
                    bool isOption = itemName.StartsWith("{YamlEnabled(") && itemName.EndsWith(")}");
                    ItemData itemData = null;
                    if ((!isOption && !itemsByName.TryGetValue(itemName, out itemData)) || itemName.EndsWith("()"))
                    {
                        Debug.LogError($"Could not find item '{itemName}' required for '{loc.name}'. Assuming it's a custom hook.");
                        customHooks = true;
                        continue; // skip to the next location
                    }
                    andReqs.Add(new Requirement
                    {
                        item = itemData,
                        amount = count,
                        optionRequirement = isOption,
                        optionName = isOption ? itemName : null
                    });
                    if (itemData != null)
                    {
                        if (!itemToLocations.TryGetValue(itemName, out var list))
                        {
                            list = new List<string>();
                            itemToLocations[itemName] = list;
                        }
                        if (!list.Contains(loc.name)) list.Add(loc.name);
                    }
                }
                parsed.Add(andReqs);
            }
            locationRequirements[loc.id] = parsed;
        }
        processingStatus.text = "Cleaning up...";
        yield return null; // UI Update
        Directory.Delete(extractPath, true);
        callback?.Invoke(null);
    }
    List<string> NormalizeRequires(JToken token)
    {
        if (token == null) return new List<string>();

        if (token.Type == JTokenType.String)
        {
            string s = token.ToObject<string>();
            return string.IsNullOrWhiteSpace(s) ? new List<string>() : new List<string> { s };
        }

        if (token.Type == JTokenType.Array)
        {
            return token.ToObject<List<string>>();
        }

        return new List<string>();
    }
    public void CancelProcessingButton()
    {
        errorMessage.text = "APManual processing canceled!";
        StopCoroutine(nameof(ProcessAPManual));
        APWorldPath = null;
        processingScreen.SetActive(false);
        processingWorld = false;
    }
    public void Connect()
    {
        connectButton.interactable = false;
        if (address.text == "" || slot.text == "")
        {
            errorMessage.text = "Both Address and Slot are required.";
            connectButton.interactable = true;
            return;
        }
        if (!address.text.StartsWith("archipelago.gg"))
        {
            errorMessage.text = "Due to security restrictions, unsecure websockets may not work on certain devices.<br>Try hosting on the archipelago.gg website or a rehost if connection fails.";
        }
        if (APWorldPath is null || APWorldPath.Length == 0)
        {
            errorMessage.text = "An APManual is required.";
            connectButton.interactable = true;
            return;
        }
        if (processingWorld) // it shouldn't be possible to trigger this, but better safe than sorry
        {
            errorMessage.text = "The APManual is being processed, please wait.";
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
        itemHandler.Clear();
        enabledOptions.Clear();
        UI.SwitchTabs(currentTab, UI.manualClientConnect, false);
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
        if (currentTab == messageFeedTab || currentTab == hintTab)
        {
            UI.SwitchTabs(currentTab, itemTab, true);
        }
        else
        {
            UI.SwitchTabs(currentTab, itemTab, false);
        }
        currentTab = itemTab;
        UpdateItemDisplay();
    }
    public void ClientButton()
    {
        UI.SwitchTabs(currentTab, messageFeedTab, false);
        currentTab = messageFeedTab;
    }
    public void HintButton()
    {
        if (currentTab == messageFeedTab)
        {
            UI.SwitchTabs(currentTab, hintTab, false);
        }
        else
        {
            UI.SwitchTabs(currentTab, hintTab, true);
        }
        currentTab = hintTab;
    }
    public void LocationsButton()
    {
        if (currentTab == messageFeedTab || currentTab == hintTab)
        {
            UI.SwitchTabs(currentTab, locationsTab, true);
        }
        else
        {
            UI.SwitchTabs(currentTab, locationsTab, false);
        }
        currentTab = locationsTab;
        if (LocationList.instance != null)
        {
            LocationList.instance.RefreshLogic();
        }
    }
    public void DeathLinkButton()
    {
        UI.SwitchTabs(currentTab, dlTab, true);
        currentTab = dlTab;
    }
    public void ToggleDeathLink(bool enable)
    {
        Debug.Log($"deathlink: {enable}");
        if (dlHandler == null)
        {
            dlHandler = session.CreateDeathLinkService();
        }
        if (enable)
        {
            dlHandler.EnableDeathLink();
        }
        else
        {
            dlHandler.DisableDeathLink();
        }
    }
    public void SendDeathLink()
    {
        if (dlHandler == null)
        {
            dlHandler = session.CreateDeathLinkService();
        }
        if (string.IsNullOrWhiteSpace(dlReason.text))
        {
            dlHandler.SendDeathLink(new DeathLink(session.Players.ActivePlayer.Name, $"{session.Players.ActivePlayer.Alias} died."));
        }
        else
        {
            dlHandler.SendDeathLink(new DeathLink(session.Players.ActivePlayer.Name, dlReason.text));
        }
    }
    private IEnumerator ConnectRoutine()
    {
        try
        {
            session = ArchipelagoSessionFactory.CreateSession($"wss://{address.text}");
            session.Items.ItemReceived += (item) => ThreadManager.RunOnMainThread(() => UpdateItemList(item));
            session.MessageLog.OnMessageReceived += message => ProcessMessage(message);
            var result = session.TryConnectAndLogin
                (manualGameID, slot.text, ItemsHandlingFlags.AllItems, new Version(0,6,7), tags: new string[] {"MobileAP"}, requestSlotData: true, password: password.text);
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
    private async void OnConnect()
    {
        UI.SwitchTabs(UI.manualClientConnect, messageFeedTab, true);
        currentTab = messageFeedTab;
        session.Hints.TrackHints(hints => ProcessHint(hints), true);
        messageFeed.fontSize = PlayerPrefs.GetInt("fontSize", 35);
        PlayerPrefs.SetString("manualAddress", address.text);
        PlayerPrefs.SetString("manualSlot", slot.text);
        PlayerPrefs.SetString("manualPass", password.text);
        PlayerPrefs.Save();
        slotdata = await session.DataStorage.GetSlotDataAsync(session.Players.ActivePlayer.Slot);
        foreach (var pair in slotdata)
        {
            if (pair.Value is bool enabled && enabled)
            {
                Debug.Log($"Option {pair.Key} is enabled.");
                enabledOptions.Add(pair.Key);
            }
        }
    }
    public static async void SendLocation(long locationID)
    {
        await session.Locations.CompleteLocationChecksAsync(new long[] {locationID}); // fire and forget
    }
    private void UpdateItemList(ReceivedItemsHelper helper)
    {
        if (!helper.Any())
        {
            Debug.LogWarning("UpdateItemList called with no items in the queue! This is expected on reconnect, but is a bug elsewhere!");
            return;
        }
        while (helper.Any())
        {
            var item = helper.DequeueItem();
            Debug.Log($"Received {item.ItemName}");
            if (receivedItemsDict.TryGetValue(item.ItemName, out var entry))
            {
                entry.Count++;
            }
            else
            {
                receivedItemsDict[item.ItemName] = new ItemEntry
                {
                    Count = 1,
                    Info = item
                };
            }
            if (item.Flags.HasFlag(ItemFlags.Advancement) && LocationList.instance != null)
            {
                LocationList.instance.RefreshLogic();
            }
        }
        UpdateItemDisplay();
    }
    private void UpdateItemDisplay()
    {
        itemHandler.Clear();
        foreach (KeyValuePair<string, ItemEntry> kvp in receivedItemsDict)
        {
            var key = kvp.Key;
            var value = kvp.Value;
            itemHandler.AddItem(key, value);
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
            ThreadManager.RunOnMainThread(() => hintHandler.AddHint(new TextClient.APMobileHint()
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