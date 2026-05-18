using System.Collections.Generic;
using UnityEngine;

public class ItemList : MonoBehaviour
{
    private RectTransform contentRoot;
    [SerializeField] private ItemEntry itemEntryPrefab;

    private readonly List<ItemEntry> entries = new();

    public void Clear()
    {
        for (int i = 0; i < entries.Count; i++)
            Destroy(entries[i].gameObject);

        entries.Clear();
    }

    public void AddItem(string item, ManualClient.ItemEntry data)
    {
        if (contentRoot == null)
        {
            contentRoot = gameObject.GetComponent<RectTransform>();
        }
        ItemEntry entry = Instantiate(itemEntryPrefab, contentRoot, false);
        entry.SetItem(item, data);
        entries.Add(entry);
    }
}