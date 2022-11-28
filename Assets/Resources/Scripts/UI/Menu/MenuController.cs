using Platinum.Settings;
using System.Collections.Generic;
using TMPro;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

namespace Platinum.Menu
{
    public class MenuController : MonoBehaviour
    {
        [Header("General")]
        public SettingsManager SettingsManager;
        public CanvasGroup[] allMenu;
        public AudioMixer MusicMixer;
        public AudioSource MusicSource;
        [Header("Login Menu")]
        public CanvasGroup LoginMenu;
        public TMP_InputField InputUsername;
        [Header("PlayerStats")]
        public TMP_Text Username;
        public TMP_Text Coin;
        public Slider ScoreSlider;
        public TMP_Text Level;
        [Header("Start Menu")]
        public CanvasGroup StartMenu;
        public Image Background;
        public List<Sprite> BackgroundSprites;
        public TMP_Text GameVersionText;
        public string GameVersionTitle = "Version: ";
        [Header("Select Map Menu")]
        public CanvasGroup ParametersWindows;
        //PRIVATE
        private LoadingScreenController m_LoadingScreenController;
        private Sprite m_DefaultSpriteLoadMapButton;
        private MapSelect m_CurrentMap;
        private AudioClip[] WinMusics;
        private AudioClip[] LoseMusics;
        private int NumberMusic;

        private void Awake()
        {
            WinMusics = SettingsManager.GetMusic(SceneType.Menu, MusicType.Happy);
            LoseMusics = SettingsManager.GetMusic(SceneType.Menu, MusicType.Sad);
            Background.sprite = BackgroundSprites[Random.Range(0, BackgroundSprites.Count)];
            m_LoadingScreenController = FindObjectOfType<LoadingScreenController>();
            GameVersionText.text = GameVersionTitle + "<#afd9e9>" + Application.version;


            if (SettingsManager.PlayerInfo == null)
            {
                PlayerInfo info = new PlayerInfo();
                info.SetName("Player" + UnityEngine.Random.Range(100, 1000));
                info.AddScore(UnityEngine.Random.Range(20, 75));
                SettingsManager.CreatePlayer(info);
                InputUsername.text = info.Name;
                LoginMenu.alpha = 1f;
            }
            else
            {
                InputUsername.text = SettingsManager.PlayerInfo.Name;
                LoginButton();
                SelectMenu(StartMenu);
            }
            
            ResetCursorPosition();
        }
        
        private void ResetCursorPosition()
        {
            Vector2 screenCenterPoint = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Mouse.current.WarpCursorPosition(screenCenterPoint);
            InputState.Change(Mouse.current.position, screenCenterPoint);
            
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void LoginButton()
        {
            SetMusic();
            LoadPlayerData();
            SelectMenu(StartMenu);
        }

        private void SetMusic()
        {
            if (!MusicSource || WinMusics.Length == 0) return;

            if (m_LoadingScreenController != null && m_LoadingScreenController.EndLevel == Result.Win)
            {
                NumberMusic = UnityEngine.Random.Range(0, WinMusics.Length);
                MusicSource.clip = WinMusics[NumberMusic];
            }
            else
            {
                NumberMusic = UnityEngine.Random.Range(0, LoseMusics.Length);
                MusicSource.clip = LoseMusics[NumberMusic];
            }
            if (MusicSource.clip.name == "Cafofo - AMB - Muffled Pop Music") MusicSource.volume = 1;
            MusicSource.Play();
        }

        private void LoadPlayerData()
        {
            Username.text = InputUsername.text;
            Coin.text = SettingsManager.PlayerInfo.Coin.ToString();
            Level.text = SettingsManager.PlayerInfo.Level.ToString();
            ScoreSlider.value = SettingsManager.PlayerInfo.GetRatioScore();
        }

        public void SelectMap(MapSelect map)
        {
            m_CurrentMap = !m_CurrentMap ? map : m_CurrentMap;
            m_CurrentMap.SetStatePressed(false);
            map.SetStatePressed(true);
            m_CurrentMap = map;
            OpenWindowMenu(ParametersWindows);
        }
        
        public void LoadScene() //загрузка уровня
        {
            foreach (CanvasGroup copy in allMenu)
            {
                copy.alpha = 0f;
            }

            FindObjectOfType<SceneController>().LoadingScreenController.LoadScene(m_CurrentMap);
        }

        public void SelectMenu(CanvasGroup menu) //открыть главное меню и закрыть все остальные
        {
            foreach (CanvasGroup copy in allMenu)
            {
                if (copy != menu) copy.alpha = 0f;
                if (copy != menu) copy.blocksRaycasts = false;
            }
            menu.alpha = 1f;
            menu.blocksRaycasts = true;
        }

        public void OpenWindowMenu(CanvasGroup menu) //открыть главное меню и закрыть все остальные
        {
            foreach (CanvasGroup copy in allMenu)
            {
                if (copy != menu) copy.blocksRaycasts = false;
            }
            menu.alpha = 1f;
            menu.blocksRaycasts = true;
        }

        public void Exit() //выход из игры
        {
            Application.Quit();
        }
    }
}

