using System;
using System.Collections.Generic;
using UnityEngine;

public class HintList : MonoBehaviour
{
    private RectTransform contentRoot;
    [SerializeField] private HintEntry hintEntryPrefab;

    private readonly List<HintEntry> entries = new();

    public void Clear()
    {
        for (int i = 0; i < entries.Count; i++)
            Destroy(entries[i].gameObject);

        entries.Clear();
    }

    public void AddHint(TextClient.APMobileHint hint)
    {
        if (contentRoot is null)
        {
            contentRoot = gameObject.GetComponent<RectTransform>();
        }
        HintEntry entry = Instantiate(hintEntryPrefab, contentRoot, false);
        entry.SetHint(hint);
        entries.Add(entry);
    }
}