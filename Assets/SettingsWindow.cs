using Platinum.Settings;
using System;
using System.Collections.Generic;
using TMPro;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public enum ButtonArrow
{
    Left,
    Right,
    Up,
    Down
}

public class SettingsWindow : MonoBehaviour
{

    [Header("General")]
    public SettingsManager SettingsManager;

    [Header("Match Settings")]
    public Toggle DisableInstanceHitToggle;
    public Toggle PeacifulModeToggle;
    public TMP_InputField SeedInputField;

    [Header("Player Settings")]

    public Toggle FramerateToggle;
    public Toggle VisiblyTrailBulletToggle;
    public TMP_Text ShadowsText;
    public TMP_Text GraphicsQualityText;
    public Slider MusicVolumeSlider;
    public Slider LookSensitivitySlider;
    public Slider CrosshairVerticalSlider;
    public Slider CrosshairHorizontalSlider;
    public Slider CameraDistanceSlider;
    public TMP_InputField SwitchSkinInputField;
    public TMP_InputField SwitchMusicInputField;

    [Header("Other Settings")]

    public GameObject ShadowsPanel;
    public GameObject QualityPanel;
    public AudioMixer MusicMixer;

    [Header("Old Settings")]
    public Toggle FullScreenToggle;
    public TMP_Dropdown ScreenResolutionDropdown;

    private List<AudioClip> AllMusics;
    private int NumberMusic;
    private LoadManager m_LoadManager;
    private MatchSaves m_MatchSettings;
    private PlayerSaves m_PlayerSaves;
    private CanvasGroup m_CanvasGroup;

    #region EVENT

    private void Awake()
    {
        AllMusics = new List<AudioClip>();
        EventManager.AddListener<EndSpawnEvent>(OnEndSpawnEvent);
        m_CanvasGroup = GetComponent<CanvasGroup>();
        m_PlayerSaves = SettingsManager.GetDefaultPlayerSaves();
        m_MatchSettings = SettingsManager.GetDefaultMatchSaves();
        LoadPlayerSettings();
        LoadMatchSettings();
    }

    private void OnDestroy()
    {
        EventManager.RemoveListener<EndSpawnEvent>(OnEndSpawnEvent);
    }

    private void OnEndSpawnEvent(EndSpawnEvent evt)
    {
        m_LoadManager = evt.LoadManager;
        ApplyGameplayPlayerSaves();
    }

    #endregion

    #region LOAD_SAVE_APPLY
    private void LoadMatchSettings()
    {
        GetPlayerPrefs(nameof(m_MatchSettings.DisableInstanceHit), DisableInstanceHitToggle, ref m_MatchSettings.DisableInstanceHit);
        GetPlayerPrefs(nameof(m_MatchSettings.PeacifulMode), PeacifulModeToggle, ref m_MatchSettings.PeacifulMode);

        SettingsManager.SetCurrentMatchSaves(m_MatchSettings);
        //GetPlayerPrefs(nameof(m_MatchSettings.Seed), SeedInputField, ref m_MatchSettings.Seed.ToString());
    }

    private void LoadPlayerSettings()
    {
        GetPlayerPrefs(nameof(m_PlayerSaves.MusicVolume), MusicVolumeSlider, ref m_PlayerSaves.MusicVolume);
        GetPlayerPrefs(nameof(m_PlayerSaves.LookSensitivity), LookSensitivitySlider, ref m_PlayerSaves.LookSensitivity);
        GetPlayerPrefs(nameof(m_PlayerSaves.CrosshairPosition), CrosshairHorizontalSlider, CrosshairVerticalSlider, ref m_PlayerSaves.CrosshairPosition);
        GetPlayerPrefs(nameof(m_PlayerSaves.CameraDistance), CameraDistanceSlider, ref m_PlayerSaves.CameraDistance);
        GetPlayerPrefs(nameof(m_PlayerSaves.Shadows), ref m_PlayerSaves.Shadows);
        GetPlayerPrefs(nameof(m_PlayerSaves.Quality), ref m_PlayerSaves.Quality);
        GetPlayerPrefs(nameof(m_PlayerSaves.Framerate), FramerateToggle, ref m_PlayerSaves.Framerate);
        GetPlayerPrefs(nameof(m_PlayerSaves.VisiblyTrailBullet), VisiblyTrailBulletToggle, ref m_PlayerSaves.VisiblyTrailBullet);
        GetPlayerPrefs(nameof(m_PlayerSaves.Skin), SwitchSkinInputField, ref m_PlayerSaves.Skin);
        GetPlayerPrefs(nameof(m_PlayerSaves.Music), SwitchMusicInputField, ref m_PlayerSaves.Music.Name);

        SettingsManager.SetCurrentPlayerSaves(m_PlayerSaves);

        ApplyPlayerSaves();
        ApplyGameplayPlayerSaves();
    }

    public void SaveMatchSettingsButton()
    {
        PlayerPrefs.SetInt(nameof(m_MatchSettings.DisableInstanceHit), DisableInstanceHitToggle.isOn ? 1 : 0);
        PlayerPrefs.SetInt(nameof(m_MatchSettings.PeacifulMode), PeacifulModeToggle.isOn ? 1 : 0);
        //PlayerPrefs.SetInt(nameof(m_MatchSettings.Seed), Convert.ToInt32(SeedInputField.text));

        SettingsManager.SetCurrentMatchSaves(m_MatchSettings);
    }

    public void SavePlayerSettingsButton() //сохраняем значения объектов в файл
    {


        PlayerPrefs.SetFloat(nameof(m_PlayerSaves.MusicVolume), MusicVolumeSlider.value);
        PlayerPrefs.SetFloat(nameof(m_PlayerSaves.LookSensitivity), LookSensitivitySlider.value);
        PlayerPrefs.SetFloat(nameof(m_PlayerSaves.CrosshairPosition) + "y", CrosshairVerticalSlider.value);
        PlayerPrefs.SetFloat(nameof(m_PlayerSaves.CrosshairPosition) + "x", CrosshairHorizontalSlider.value);
        PlayerPrefs.SetFloat(nameof(m_PlayerSaves.CameraDistance), CameraDistanceSlider.value);
        PlayerPrefs.SetInt(nameof(m_PlayerSaves.Shadows), (int)m_PlayerSaves.Shadows);
        PlayerPrefs.SetInt(nameof(m_PlayerSaves.Quality), m_PlayerSaves.Quality);
        PlayerPrefs.SetInt(nameof(m_PlayerSaves.Framerate), FramerateToggle.isOn ? 1 : 0);
        PlayerPrefs.SetInt(nameof(m_PlayerSaves.VisiblyTrailBullet), VisiblyTrailBulletToggle.isOn ? 1 : 0);
        PlayerPrefs.SetInt(nameof(m_PlayerSaves.Skin), SettingsManager.Customization.GetCurrentIndexSkin());

        SettingsManager.SetCurrentPlayerSaves(m_PlayerSaves);
        ApplyPlayerSaves();
        ApplyGameplayPlayerSaves();
    }

    private void ApplyPlayerSaves()
    {
        QualitySettings.SetQualityLevel(m_PlayerSaves.Quality, true);
        GraphicsQualityText.text = GetQualityAtIndex(m_PlayerSaves.Quality);

        ShadowQuality shadow = m_PlayerSaves.Shadows;
        ShadowsText.text = shadow.ToString();
        QualitySettings.shadows = m_PlayerSaves.Shadows;
    }

    private void ApplyGameplayPlayerSaves()
    {
        if (!m_LoadManager) return;

        /*
        AllMusics.AddRange(SettingsManager.GetMusic(SceneType.Game, MusicType.Battle));

        AudioUtility.SetMasterVolume(1);
        NumberMusic = UnityEngine.Random.Range(0, AllMusics.Count);
        int l = AllMusics.Count;
        string random = "/";
        for (int i = 0; i < 10; i++)
        {
            random += UnityEngine.Random.Range(0, l);
        }

        SetMusic();
        */

        m_LoadManager.PlayerController.UpdatePlayerSkin(SettingsManager.Customization.GetSkin(SkinType.Free, m_PlayerSaves.Skin).Materials);
        m_LoadManager.ThirdPersonController.SetSensitivity(m_PlayerSaves.LookSensitivity);
        m_LoadManager.ThirdPersonController.SetCrosshairPosition(m_PlayerSaves.CrosshairPosition);
        m_LoadManager.ThirdPersonController.SerCameraDistance(m_PlayerSaves.CameraDistance);
        m_LoadManager.GameFlowManager.FramerateCounter.UIText.gameObject.SetActive(m_PlayerSaves.Framerate);
    }
    #endregion

    #region MatchSettings
    public void SetDisableInstanceHit()
    {
        m_MatchSettings.DisableInstanceHit = DisableInstanceHitToggle.isOn;
    }

    public void SetPeaciful()
    {
        m_MatchSettings.PeacifulMode = PeacifulModeToggle.isOn;
    }

    public void SetSeed()
    {
        m_MatchSettings.Seed = Convert.ToInt32(SeedInputField.text);
    }
    #endregion

    #region PlayerSettings
    public void SetFramerate()
    {
        m_PlayerSaves.Framerate = FramerateToggle.isOn;
    }
    public void SetVisiblyTrailBullet()
    {
        m_PlayerSaves.VisiblyTrailBullet = VisiblyTrailBulletToggle.isOn;
    }

    public void OpenShadowsPanel()
    {
        ShadowsPanel.SetActive(!ShadowsPanel.activeSelf);
    }
    public void SetShadows(int shadows)
    {
        ShadowsPanel.SetActive(!ShadowsPanel.activeSelf);
        ShadowQuality shadow = (ShadowQuality)shadows;
        m_PlayerSaves.Shadows = shadow;
        ShadowsText.text = shadow.ToString();
    }

    public void OpenGraphicsQualityPanel()
    {
        QualityPanel.SetActive(!QualityPanel.activeSelf);
    }
    public void SetGraphicsQuality(int quality)
    {
        QualityPanel.SetActive(!QualityPanel.activeSelf);
        string name = GetQualityAtIndex(quality);
        m_PlayerSaves.Quality = quality;
        GraphicsQualityText.text = name;
    }

    public void SetMusicVolume() //установка громкости звука
    {
        //if (valueText) valueText.text = (MusicVolumeSlider.value * 4).ToString();
        MusicMixer.SetFloat("musicVolume", -(15 - MusicVolumeSlider.value));
        m_PlayerSaves.MusicVolume = MusicVolumeSlider.value;
    }

    public void SetLookSensitivity()
    {
        //if (value) value.text = LookSensitivitySlider.value.ToString();
        m_PlayerSaves.LookSensitivity = LookSensitivitySlider.value;
    }

    public void SetCrosshairVertical()
    {
        m_PlayerSaves.CrosshairPosition.y = CrosshairVerticalSlider.value;
    }

    public void SetCrosshairHorizontal()
    {
        m_PlayerSaves.CrosshairPosition.x = CrosshairHorizontalSlider.value;
    }

    public void SetCameraDistance()
    {
        m_PlayerSaves.CameraDistance = CameraDistanceSlider.value;
    }
    #endregion

    #region OTHER

    private string GetQualityAtIndex(int index)
    {
        string[] names = QualitySettings.names;
        string quality = "Fastest";
        for (int i = 0; i < names.Length; i++)
        {
            if (names[i] == names[index]) quality = names[i];
        }
        return quality;
    }

    private int GetIndexQualityAtName(string name)
    {
        string[] names = QualitySettings.names;
        int quality = 0;
        for (int i = 0; i < names.Length; i++)
        {
            if (names[i] == name) quality = i;
        }
        return quality;
    }

    public void SwitchSkinButton(string Arrow)
    {
        switch (Arrow)
        {
            case ("left"):
                SettingsManager.Customization.LeftMaterial();
                SettingsManager.Customization.SetCurrentIndexSkin();
                m_PlayerSaves.Skin = SettingsManager.Customization.GetCurrentIndexSkin();
                UpdateNameSkin();
                break;
            case ("right"):
                SettingsManager.Customization.RightMaterial();
                SettingsManager.Customization.SetCurrentIndexSkin();
                m_PlayerSaves.Skin = SettingsManager.Customization.GetCurrentIndexSkin();
                UpdateNameSkin();
                break;
        }
    }

    public void SetAlphaWindowButton()
    {
        m_CanvasGroup.alpha = m_CanvasGroup.alpha > 0 ? 0f : 1f;
    }

    public void SetShowSeed()
    {
        SeedInputField.text = m_MatchSettings.Seed.ToString();
        SeedInputField.gameObject.SetActive(!SeedInputField.gameObject.activeSelf);
    }

    public void UpdateMusicName(string name)
    {
        if (SwitchMusicInputField != null) SwitchMusicInputField.text = name;
    }

    private void UpdateNameSkin()
    {
        SwitchSkinInputField.text = SettingsManager.Customization.GetCurrentSkin().Name;
    }

    public void SwitchMusicButton(ButtonArrow arrow)
    {
        switch (arrow)
        {
            case (ButtonArrow.Left):
                NumberMusic--;
                SetMusic();
                break;
            case (ButtonArrow.Right):
                NumberMusic++;
                SetMusic();
                break;
        }
    }

    private void SetMusic()
    {
        if (AllMusics.Count == 0) return;

        if (NumberMusic < 0) NumberMusic = AllMusics.Count - 1;
        if (NumberMusic > AllMusics.Count - 1) NumberMusic = 0;
        //MusicSource.clip = AllMusics[NumberMusic];
        UpdateMusicName(AllMusics[NumberMusic].name);
    }
    #endregion

    #region GET_PREFS
    private void GetPlayerPrefs(string key, ref ShadowQuality defaultValue)
    {
        defaultValue = (ShadowQuality)(PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int)defaultValue);
    }

    private void GetPlayerPrefs(string key, ref int defaultValue)
    {
        defaultValue = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : defaultValue;
    }

    private void GetPlayerPrefs(string key, TMP_InputField Inputfield, ref string defaultValue)
    {
        if (Inputfield == null) return;
        defaultValue = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : defaultValue;
        Inputfield.text = defaultValue;
    }

    private void GetPlayerPrefs(string key, TMP_InputField inputfield, ref int defaultValue)
    {
        if (inputfield == null) return;

        defaultValue = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : defaultValue;
        SettingsManager.Customization.SetCurrentIndexSkin(defaultValue);
        inputfield.text = SettingsManager.Customization.GetCurrentSkin().Name;
    }

    private void GetPlayerPrefs(string key, TMP_InputField inputfield, ref AvatarSkin defaultValue)
    {
        if (inputfield == null) return;

        int index = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : 0;
        SettingsManager.Customization.SetCurrentIndexSkin(index);
        defaultValue = SettingsManager.Customization.GetCurrentSkin();
        inputfield.text = defaultValue.Name;
    }

    private void GetPlayerPrefs(string key, Slider sliderX, Slider sliderY, ref Vector2 defaultValue)
    {
        if (sliderX == null) return;
        defaultValue.x = PlayerPrefs.HasKey(key + "x") ? PlayerPrefs.GetFloat(key + "x") : defaultValue.x;
        sliderX.value = defaultValue.x;

        if (sliderY == null) return;
        defaultValue.y = PlayerPrefs.HasKey(key + "y") ? PlayerPrefs.GetFloat(key + "y") : defaultValue.y;
        sliderY.value = defaultValue.y;
    }

    private void GetPlayerPrefs(string key, Slider slider, ref float defaultValue)
    {
        if (slider == null) return;
        defaultValue = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetFloat(key) : defaultValue;
        slider.value = defaultValue;
    }

    private void GetPlayerPrefs(string key, Toggle toggle, ref bool defaultValue)
    {
        if (toggle == null) return;
        defaultValue = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) > 0 : defaultValue;
        toggle.isOn = defaultValue;
    }
    #endregion

}
