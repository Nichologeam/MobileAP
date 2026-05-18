using System.Collections;
using UnityEngine;

public class UIHandler : MonoBehaviour
{
    private SettingsManager settings;
    private TextClient tc;
    private ManualClient mc;
    public GameObject clientSelector;
    public GameObject textClientConnect;
    public GameObject manualClientConnect;
    public GameObject settingsTab;
    private Coroutine tabRoutine;
    private RectTransform canvasRect;
    
    void Start()
    {
        settings = GetComponent<SettingsManager>();
        tc = GetComponent<TextClient>();
        mc = GetComponent<ManualClient>();
        canvasRect = GetComponent<RectTransform>();
        clientSelector.transform.position = Vector3.zero;
        textClientConnect.transform.position = new(1000,0,0);
        manualClientConnect.transform.position = new(1000,0,0);
        settingsTab.transform.position = new(1000,0,0);
    }

    public void TextClientButton()
    {
        SwitchTabs(clientSelector, textClientConnect, true);
        tc.errorMessage.fontSize = PlayerPrefs.GetInt("fontSize", 35);
        tc.address.text = PlayerPrefs.GetString("textAddress", "");
        tc.slot.text = PlayerPrefs.GetString("textSlot", "");
        tc.password.text = PlayerPrefs.GetString("textPass", "");
    }

    public void TextConnectorBackButton()
    {
        SwitchTabs(textClientConnect, clientSelector, false);
    }

    public void ManualClientButton()
    {
        SwitchTabs(clientSelector, manualClientConnect, true);
        mc.errorMessage.fontSize = PlayerPrefs.GetInt("fontSize", 35);
        mc.address.text = PlayerPrefs.GetString("manualAddress", "");
        mc.slot.text = PlayerPrefs.GetString("manualSlot", "");
        mc.password.text = PlayerPrefs.GetString("manualPass", "");
    }

    public void ManualConnectorBackButton()
    {
        SwitchTabs(manualClientConnect, clientSelector, false);
    }

    public void SettingsButton()
    {
        settings.LoadSettings();
        SwitchTabs(clientSelector, settingsTab, false);
    }

    public void SettingsBackButton()
    {
        settings.UpdateSettings();
        SwitchTabs(settingsTab, clientSelector, true);
    }

    public void SwitchTabs(GameObject currentTab, GameObject newTab, bool left)
    {
        float duration = PlayerPrefs.GetFloat("tabSwitchSpeed");
        if (tabRoutine != null)
        {
            StopCoroutine(tabRoutine);
        }
        tabRoutine = StartCoroutine(SwitchTab(currentTab.GetComponent<RectTransform>(), newTab.GetComponent<RectTransform>(), left, duration));
    }

    private IEnumerator SwitchTab(RectTransform currentTab, RectTransform newTab, bool left, float duration)
    {
        newTab.gameObject.SetActive(true);
        currentTab.GetComponent<CanvasGroup>().blocksRaycasts = false;
        newTab.GetComponent<CanvasGroup>().blocksRaycasts = false;
        float direction = left ? -1f : 1f;
        float width = canvasRect.rect.width;
        Vector2 offscreen = new Vector2(direction * width, 0);
        Vector2 startingPosCurrent = currentTab.anchoredPosition;
        Vector2 startingPosNew = -offscreen;
        newTab.anchoredPosition = startingPosNew;
        float time = 0;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            float eased = Mathf.SmoothStep(0, 1, t);
            currentTab.anchoredPosition = Vector2.Lerp(startingPosCurrent, offscreen, eased); // move off screen
            newTab.anchoredPosition = Vector2.Lerp(startingPosNew, Vector2.zero, eased);
            yield return null;
        }
        currentTab.anchoredPosition = offscreen;
        newTab.anchoredPosition = Vector2.zero;
        newTab.GetComponent<CanvasGroup>().blocksRaycasts = true;
        currentTab.gameObject.SetActive(false);
    }
}
