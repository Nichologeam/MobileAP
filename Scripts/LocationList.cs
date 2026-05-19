using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LocationList : MonoBehaviour
{
    private Transform contentRoot;
    public CategoryHeader headerPrefab;
    public LocationEntry locationPrefab;
    public GameObject confirmationPopup;
    public TMP_Text confirmationName;
    public Button confirmationAccept;
    public static LocationList instance;
    private Dictionary<long, LocationEntry> locationsById = new();
    private Dictionary<string, CategoryHeader> headers = new();
    private Dictionary<string, List<LocationEntry>> entriesByCategory = new();

    private void Start()
    {
        instance = this;
        contentRoot = gameObject.transform;
        Build();
    }
    public void Build() // build everything once
    {
        Debug.Log("Building LocationList...");
        locationsById.Clear(); // clear any previous information
        headers.Clear();
        entriesByCategory.Clear();
        foreach (Transform child in contentRoot) // remove any buttons or categories from previous sessions
        {
            Destroy(child.gameObject);
        }
        if (ManualClient.locationCategories.TryGetValue("No Category", out var noCategory) && noCategory.Count > 0)
        {
            string categoryName = "No Category";
            CategoryHeader header = Instantiate(headerPrefab, contentRoot); // make the header object
            header.Initialize(categoryName); // set it up correctly
            headers[categoryName] = header; // put it in the dictionary
            entriesByCategory[categoryName] = new List<LocationEntry>(); // create entries list
            foreach (var loc in noCategory) // for every location inside this category...
            {
                if (ManualClient.session.Locations.AllLocationsChecked.Contains(loc.id))
                {
                    continue; // we already collected this location in a previous session
                }
                LocationEntry entry = Instantiate(locationPrefab, header.contentRoot.transform); // make the location
                entry.Initialize(loc); // set it up correctly
                locationsById[loc.id] = entry; // put it in the dictionary
                entriesByCategory[categoryName].Add(entry); // add to the entries list
            }
        }
        foreach (var pair in ManualClient.locationCategories) // for each category that exists...
        {
            string categoryName = pair.Key; // get its name
            if (categoryName == "No Category")
            {
                continue; // already processed
            }
            CategoryHeader header = Instantiate(headerPrefab, contentRoot); // make the header object
            header.Initialize(categoryName); // set it up correctly
            headers[categoryName] = header; // put it in the dictionary
            entriesByCategory[categoryName] = new List<LocationEntry>(); // create entries list
            foreach (var loc in pair.Value) // for every location inside this category...
            {
                if (ManualClient.session.Locations.AllLocationsChecked.Contains(loc.id))
                {
                    continue; // we already collected this location in a previous session
                }
                LocationEntry entry = Instantiate(locationPrefab, header.contentRoot.transform); // make the location
                entry.Initialize(loc); // set it up correctly
                locationsById[loc.id] = entry; // put it in the dictionary
                entriesByCategory[categoryName].Add(entry); // add to the entries list
            }
        }
        RefreshLogic();
    }
    bool EvaluateLocation(List<List<ManualClient.Requirement>> logic, Dictionary<string, ManualClient.ItemEntry> inventory, HashSet<string> enabledOptions)
    {
        if (logic == null || logic.Count == 0) // no logic
        {
            return true; // return as always in logic
        }
        // OR groups
        foreach (var andGroup in logic)
        {
            bool groupValid = true;

            // AND requirements
            foreach (var req in andGroup)
            {
                if (req.optionRequirement)
                {
                    if (!enabledOptions.Contains(req.optionName))
                    {
                        groupValid = false;
                        break;
                    }
                }
                else
                {
                    string itemName = req.item.name;

                    if (!inventory.TryGetValue(itemName, out ManualClient.ItemEntry entry) || entry.Count < req.amount)
                    {
                        groupValid = false;
                        break;
                    }
                }
            }

            if (groupValid)
                return true;
        }

        return false;
    }

    public void RefreshLogic()
    {
        var enabledOptions = ManualClient.enabledOptions;
        var inventory = ManualClient.receivedItemsDict;
        var locationRequirements = ManualClient.locationRequirements;
        List<long> toRemove = new();
        Debug.Log("Refreshing Logic...");
        foreach (var pair in locationsById)
        {
            long id = pair.Key;
            LocationEntry entry = pair.Value;
            if (entry.CheckLocationChecked())
            {
                toRemove.Add(id);
                continue;
            }
            bool inLogic = true;

            if (!ManualClient.customHooks && locationRequirements.TryGetValue(id, out var logic))
            {
                inLogic = EvaluateLocation(logic, inventory, enabledOptions);
            }
            entry.SetState(inLogic);
        }
        foreach (long id in toRemove)
        {
            RemoveLocation(id); // only remove once the locationsById dictionary is not being iterated anymore
        }
        UpdateCategoryCounts();
    }

    void UpdateCategoryCounts()
    {
        Debug.Log("Updating Location Category Counts...");
        foreach (var pair in entriesByCategory)
        {
            string category = pair.Key;
            var list = pair.Value;

            int total = list.Count;
            int inLogic = 0;

            foreach (var entry in list)
            {
                if (entry.IsInLogic())
                    inLogic++;
            }

            headers[category].SetLocCount(inLogic, total);
        }
    }

    public void RemoveLocation(long id)
    {
        if (!locationsById.ContainsKey(id))
            return;

        Debug.LogWarning($"Removing Location {id}");
        LocationEntry entry = locationsById[id];
        locationsById.Remove(id);

        // Remove from category list
        foreach (var pair in entriesByCategory)
        {
            if (pair.Value.Remove(entry))
                break;
        }

        Destroy(entry.gameObject);

        UpdateCategoryCounts();
    }
}
