using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CategoryHeader : MonoBehaviour
{
    public TMP_Text titleText;
    public GameObject contentRoot; // where locations go
    public Button toggleButton;
    private bool expanded = false;
    private string categoryName;

    public void Initialize(string categoryName)
    {
        titleText.text = categoryName;
        this.categoryName = categoryName;
        toggleButton.onClick.AddListener(Toggle);
    }

    void Toggle()
    {
        expanded = !expanded;
        contentRoot.SetActive(expanded);
    }

    public void SetLocCount(int inLogic, int total) // if this is a header for locations
    {
        if (ManualClient.customHooks) // this APWorld has custom hooks or otherwise failed to process logic
        {
            inLogic = total; // assume everything is always in logic
        }
        titleText.text = $"{categoryName} ({inLogic}/{total})";
    }

    public void SetItemCount(int items) // if this is a header for items
    {
        titleText.text = $"{categoryName} ({items})";
    }
}