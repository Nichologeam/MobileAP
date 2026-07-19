using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsManager : MonoBehaviour
{
    public TMP_Text versionNumberDisplay;
    public Toggle autoscrollToggle;
    public Slider tabSwitchSpeedSlider;
    public Slider fontSizeSlider;
    public Toggle dlVibrateToggle;
    public TMP_Dropdown confirmSendLocDropdown;
    public Toggle bypassLockToggle;
    public GameObject iOSVibrateWarning;

    void Start()
    {
        versionNumberDisplay.text = $"MobileAP V0.0.3 ({SystemInfo.operatingSystem})";
        if (!PlayerPrefs.HasKey("bypassLock")) // there aren't any saved settings
        {
            PlayerPrefs.SetInt("autoscroll", 1);
            PlayerPrefs.SetFloat("tabSwitchSpeed", 0.25f);
            PlayerPrefs.SetInt("fontSize", 35);
            PlayerPrefs.SetInt("deathlinkVibrate", 1);
            PlayerPrefs.SetInt("confirmSendLoc", 2); // Options are 'Always(0), Ask(1), Never(2)'
            PlayerPrefs.SetInt("bypassLock", 0);
            PlayerPrefs.Save();
        }
        if (Application.platform == RuntimePlatform.IPhonePlayer) // covers iOS, iPad, and Apple Watch (but not tvOS?)
        {
            iOSVibrateWarning.SetActive(true);
            bypassLockToggle.interactable = false; // android only setting
            string osString = SystemInfo.operatingSystem;
            string versionPart = osString.Split(' ')[1];
            if (Version.TryParse(versionPart, out Version iosVersion))
            {
                if (iosVersion.Major < 10)
                {
                    var warningText = iOSVibrateWarning.GetComponent<TMP_Text>();
                    warningText.text = "Vibration does not work on this iOS version!";
                    warningText.color = Color.red;
                }
            }
        }
        else
        {
            iOSVibrateWarning.SetActive(false);
        }
    }
    public void UpdateSettings()
    {
        PlayerPrefs.SetInt("autoscroll", autoscrollToggle.isOn ? 1 : 0);
        PlayerPrefs.SetFloat("tabSwitchSpeed", tabSwitchSpeedSlider.value);
        PlayerPrefs.SetInt("fontSize", (int)fontSizeSlider.value); // this slider is set to whole numbers only, so no issue
        PlayerPrefs.SetInt("deathlinkVibrate", dlVibrateToggle.isOn ? 1 : 0);
        PlayerPrefs.SetInt("confirmSendLoc", confirmSendLocDropdown.value);
        PlayerPrefs.SetInt("bypassLock", bypassLockToggle.isOn ? 1 : 0);
        PlayerPrefs.Save();
    }
    public void LoadSettings()
    {
        autoscrollToggle.isOn = PlayerPrefs.GetInt("autoscroll", 1) == 1;
        tabSwitchSpeedSlider.value = PlayerPrefs.GetFloat("tabSwitchSpeed", 0.25f);
        fontSizeSlider.value = PlayerPrefs.GetInt("fontSize");
        dlVibrateToggle.isOn = PlayerPrefs.GetInt("deathlinkVibrate", 1) == 1;
        confirmSendLocDropdown.value = PlayerPrefs.GetInt("confirmSendLoc", 2);
        if (Application.platform == RuntimePlatform.Android)
        {
            bypassLockToggle.isOn = PlayerPrefs.GetInt("bypassLock", 0) == 0;
            BypassLockScreen(bypassLockToggle.isOn);
        }
    }
    public void BypassLockScreen(bool enable)
    {
        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        {
            if (enable)
            {
                activity.Call("setShowWhenLocked", true);
                activity.Call("setTurnScreenOn", true);
            }
            else
            {
                activity.Call("setShowWhenLocked", false);
                activity.Call("setTurnScreenOn", false);
            }
        }
    }
}