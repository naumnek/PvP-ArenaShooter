using Platinum.Menu;
using Platinum.Player;
using Platinum.Settings;
using System.Collections;
using TMPro;
using Unity.FPS.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Unity.FPS.Game
{
    public class GameFlowManager : MonoBehaviour
    {
        [Header("Generals")]
        [SerializeField] private bool DisableStaticticsPanel;
        [Header("References")]
        public SwitchItemMenu SwitchItemMenu;
        public FramerateCounter FramerateCounter;
        public TeamsKillCounter TeamsKillCounter;
        [Header("Statictics")]
        public CanvasGroup StaticticsPanel;
        public TMP_Text ResultTitle;
        public TMP_Text CoinCounter;
        public Slider ScoreSlider;
        public TMP_Text LevelCounter;
        [Header("UI")]
        public CanvasGroup[] AllMenu;
        public CanvasGroup InGameMenu;
        public CanvasGroup TabMenu;
        public CanvasGroup PlayerHUD;
        public CanvasGroup SettingsWindow;
        public CanvasGroup ItemListSwitch;
        public GameObject FeedbackFlashCanvas;
        public GameObject SpectatorInfo;
        [Header("Parameters")]
        public AudioSource AudioWeaponButtonClick;
        public AudioSource AudioButtonClick;
        public AudioSource MusicSource;

        [Tooltip("Duration of the fade-to-black at the end of the game")]
        public float EndMatchAlphaDelay = 2.5f;


        [Tooltip("The canvas group of the fade-to-black screen")]
        public CanvasGroup EndGameFadeCanvasGroup;

        [Header("Win")]

        [Tooltip("Duration of delay before the fade-to-black, if winning")]
        public float DelayBeforeFadeToBlack = 4f;

        [Tooltip("Win game message")]
        public string WinGameMessage;
        [Tooltip("Duration of delay before the win message")]
        public float DelayBeforeWinMessage = 2f;

        [Tooltip("Sound played on win")] public AudioClip VictorySound;
        [Tooltip("Sound played on defeat")] public AudioClip DefeatSound;

        public bool GameIsEnding { get; private set; }
        public bool HasMenuFocused { get; private set; }

        float m_TimeLoadEndGameScene;
        private bool ServerPause = true;
        private LoadManager m_LoadManager;
        private PlayerController m_PlayerController;
        private SettingsManager m_SettingsManager;
        private PlayerInputHandler m_PlayerInputHandler;
        private bool MenuPause = true;
        private AudioSource m_AudioSource;

        private static GameFlowManager instance;
        public static GameFlowManager GetInstance() => instance;

        void OnDestroy()
        {
            EventManager.RemoveListener<EndSpawnEvent>(OnPlayerSpawnEvent);
            EventManager.RemoveListener<MenuPauseEvent>(OnMenuPauseEvent);
            EventManager.RemoveListener<GamePauseEvent>(OnGamePauseEvent);
            EventManager.RemoveListener<AllObjectivesCompletedEvent>(OnAllObjectivesCompleted);
            EventManager.RemoveListener<RefreshMatchEvent>(OnRefreshMatchEvent);
            EventManager.RemoveListener<StartGenerationEvent>(OnStartGenerationEvent);
            //EventManager.RemoveListener<PlayerDeathEvent>(OnPlayerDeath);
        }

        void Awake()
        {
            EventManager.AddListener<RefreshMatchEvent>(OnRefreshMatchEvent);
            EventManager.AddListener<EndSpawnEvent>(OnPlayerSpawnEvent);
            EventManager.AddListener<MenuPauseEvent>(OnMenuPauseEvent);
            EventManager.AddListener<GamePauseEvent>(OnGamePauseEvent);
            EventManager.AddListener<AllObjectivesCompletedEvent>(OnAllObjectivesCompleted);
            EventManager.AddListener<StartGenerationEvent>(OnStartGenerationEvent);
            
            instance = this;
            m_AudioSource = GetComponent<AudioSource>();
            SwitchItemMenu.WeaponLimitReached += OnWeaponLimitReached;

            //EventManager.AddListener<PlayerDeathEvent>(OnPlayerDeath);
        }

        private void OnWeaponLimitReached()
        {
            m_PlayerInputHandler.SelectWeapon = false;
            SetItemListSwitch(false);
        }

        public void EndMatchEffect()
        {
            StartCoroutine(WaitFade(1));
        }

        bool fade() => Time.time >= m_TimeLoadEndGameScene;
        IEnumerator WaitFade(float alpha)
        {
            m_TimeLoadEndGameScene = Time.time + EndMatchAlphaDelay;
            float timeRatio;
            while (!fade())
            {
                yield return new WaitForFixedUpdate();
                timeRatio = alpha - (m_TimeLoadEndGameScene - Time.time) / EndMatchAlphaDelay;
                EndGameFadeCanvasGroup.alpha = timeRatio;
            }
        }

        private void OnRefreshMatchEvent(RefreshMatchEvent evt)
        {
            SetItemListSwitch(true);
            StartCoroutine(WaitFade(0));
            /*m_TimeLoadEndGameScene = Time.time + EndSceneLoadDelay;

            while (Time.time >= m_TimeLoadEndGameScene)
            {
                float timeRatio = 1 - (m_TimeLoadEndGameScene - Time.time) / EndSceneLoadDelay;
                EndGameFadeCanvasGroup.alpha = timeRatio;
            }*/

            // See if it's time to load the end scene (after the delay)
            //if (Time.time >= m_TimeLoadEndGameScene)
        }
        private void OnPlayerDead()
        {
            ServerPause = true;
            SetTabMenu(false);
            SetActivePlayerHUD(false);
        }

        private void OnPlayerSpawnEvent(EndSpawnEvent evt)
        {
            m_LoadManager = evt.LoadManager;

            m_PlayerController = m_LoadManager.PlayerController;
            m_PlayerInputHandler = m_LoadManager.PlayerInputHandler;
            m_SettingsManager = m_LoadManager.SettingsManager;

            m_PlayerController.Health.OnDie += OnPlayerDead;

            Initialize();
        }

        public void ContinueButton()
        {
            SetTabMenu(false);
        }

        public void RestartButton()
        {
            StaticticsPanel.blocksRaycasts = false;
            if (m_LoadManager.SceneController)
            {
                m_LoadManager.SceneController.LoadingScreenController.RestartScene();
            }
            else
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
        }

        public void GoHomeButton()
        {
            //SetPauseMenuActivation(false);
            Debug.Log("GoHome!");
            
            ServerPause = true;
            SetTabMenu(false);
            SetActivePlayerHUD(false);
            StaticticsPanel.blocksRaycasts = false;
            GameIsEnding = false;
            switch (m_LoadManager.EndLevel)
            {
                case (EndLevel.RefreshGame):

                    break;
                case (EndLevel.LoadMenu):
                    if (m_LoadManager.SceneController)
                    {
                        m_LoadManager.SceneController.LoadingScreenController.LoadMainMenu();
                    }
                    else
                    {
                        Debug.Log("ScemeManager: " + GameSettings.MAINMENU_SCENE);
                        SceneManager.LoadScene(GameSettings.MAINMENU_SCENE);
                    }
                    break;
                default:
                    SceneManager.LoadScene(GameSettings.DEFAULT_GAME_SCENE);
                    break;
            }
        }

        public void ExitButton()
        {
            //SetPauseMenuActivation(false);
            ResultEndGame(Result.None);
        }

        public void SetMusic()
        {
            MusicSource.Pause();
        }

        private void SetMenuAlpha(CanvasGroup menu, bool active)
        {
            if (active)
            {
                SelectMenu(menu);
            }
            else
            {
                ClosedAllMenu();
            }
        }

        public void ClosedAllMenu()
        {
            foreach (CanvasGroup copy in AllMenu)
            {
                copy.alpha = 0f;
                copy.blocksRaycasts = false;
            }
        }
        public void SelectMenu(CanvasGroup menu) //открыть главное меню и закрыть все остальные
        {
            foreach (CanvasGroup copy in AllMenu)
            {
                if (copy != menu) copy.alpha = 0f;
                if (copy != menu) copy.blocksRaycasts = false;
            }
            if (menu == InGameMenu) return;
            menu.alpha = 1f;
            menu.blocksRaycasts = true;
        }

        public void OpenWindowMenu(CanvasGroup menu) //открыть главное меню и закрыть все остальные
        {
            foreach (CanvasGroup copy in AllMenu)
            {
                if (copy != menu) copy.blocksRaycasts = false;
            }
            menu.alpha = 1f;
            menu.blocksRaycasts = true;
        }

        private void Initialize()
        {
            //m_PlayerInputHandler.SelectWeapon = true;
            SetItemListSwitch(true);
            ServerPause = false;
        }

        private void OnMenuPauseEvent(MenuPauseEvent evt)
        {
            //CursorState(evt.MenuPause);
            if (evt.MenuPause) m_AudioSource.Pause();
            else m_AudioSource.Play(); 
        }
        private void OnGamePauseEvent(GamePauseEvent evt)
        {
            ServerPause = evt.ServerPause;
        }

        public void SetActiveSpectatorInfo()
        {
            SpectatorInfo.SetActive(true);
        }

        public void SetActivePlayerHUD(bool active)
        {
            PlayerHUD.alpha = active ? 1f : 0f;
            FeedbackFlashCanvas.SetActive(active);
        }

        public void SetItemListSwitch(bool active)
        {
            HasMenuFocused = active;

            float distanceFromSpawn =
                Vector3.Distance(m_PlayerController.Spawnpoint.position, m_PlayerController.transform.position);

            //Debug.Log((m_SwitchItemMenu.FixedPanel && distanceFromSpawn > AsteroidsGame.DISTANCE_OPEN_SELECT_WEAPON) + "/" + (m_SwitchItemMenu.FixedPanel));

            if (distanceFromSpawn > GameSettings.DISTANCE_OPEN_SELECT_WEAPON
                || TabMenu.alpha == 1f)
                return;

            SetActiveMenu(active);
            SetMenuAlpha(ItemListSwitch, active);
        }

        public void SetTabMenu(bool active)
        {
            HasMenuFocused = active;

            if (ItemListSwitch.alpha == 1f)
            {
                SetItemListSwitch(false);
                return;
            }

            SetActiveMenu(active);
            SetMenuAlpha(TabMenu, active);
        }

        private void SetActiveMenu(bool active)
        {
            SetActivePlayerHUD(!active);
            //CursorState(active);
            SetPauseMenuActivation(active);
        }

        private void CursorState(bool state)
        {
            Cursor.visible = state;
            Cursor.lockState = state ? CursorLockMode.None : CursorLockMode.Locked;
        }

        void SetPauseMenuActivation(bool pause)
        {
            MenuPauseEvent evt = Events.MenuPauseEvent;
            evt.MenuPause = pause;
            EventManager.Broadcast(evt);
        }

        void Update()
        {
            if (ServerPause || m_PlayerInputHandler == null || GameIsEnding) return;

            if ((TabMenu.alpha == 1f || ItemListSwitch.alpha == 1f) && m_PlayerInputHandler.click)
            {
                m_PlayerInputHandler.click = false;
                //CursorState(true);
            }

            if (m_PlayerInputHandler.tab)
            {
                m_PlayerInputHandler.tab = false;
                SetTabMenu(TabMenu.alpha == 1f ? false : true);
            }

            if (m_PlayerInputHandler.SelectWeapon)
            {
                m_PlayerInputHandler.SelectWeapon = false;
                SetItemListSwitch(ItemListSwitch.alpha == 1f ? false : true);
            }
        }
        void OnStartGenerationEvent(StartGenerationEvent evt)
        {
            Debug.Log("GFM: StartGenerationEvent");
        }

        void OnAllObjectivesCompleted(AllObjectivesCompletedEvent evt)
        {
            ResultEndGame(Result.Win);
        }


        public void ResultEndGame(Result EndLevel)
        {
            GamePauseEvent gpe = Events.GamePauseEvent;
            gpe.ServerPause = true;
            EventManager.Broadcast(gpe);
            
            GameIsEnding = true;
            SetActivePlayerHUD(false);
            SetTabMenu(false);
            // unlocks the cursor before leaving the scene, to be able to click buttons
            //Cursor.lockState = CursorLockMode.None;
            // Cursor.visible = true;

            // Remember that we need to load the appropriate end scene after a delay
            //EndGameFadeCanvasGroup.gameObject.SetActive(true);


            MusicSource.Pause();
            int resultValue = 0;
            switch (EndLevel)
            {
                case (Result.Win):

                    m_AudioSource.PlayOneShot(VictorySound);
                    // play a sound on win
                    /*var audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.clip = VictorySound;
                    audioSource.playOnAwake = false;
                    audioSource.outputAudioMixerGroup = AudioUtility.GetAudioGroup(AudioUtility.AudioGroups.HUDVictory);
                    audioSource.PlayScheduled(AudioSettings.dspTime + DelayBeforeWinMessage);*/

                    #region Message
                    // create a game message
                    //var message = Instantiate(WinGameMessagePrefab).GetComponent<DisplayMessage>();
                    //if (message)
                    //{
                    //    message.delayBeforeShowing = delayBeforeWinMessage;
                    //    message.GetComponent<Transform>().SetAsLastSibling();
                    //}

                    //DisplayMessageEvent displayMessage = Events.DisplayMessageEvent;
                    //displayMessage.Message = WinGameMessage;
                    //displayMessage.DelayBeforeDisplay = DelayBeforeWinMessage;
                    //EventManager.Broadcast(displayMessage);
                    #endregion

                    resultValue = GameSettings.PLAYER_SCORE_FOR_WIN;
                    ResultTitle.text = "VICTORY!";
                    break;
                case (Result.Lose):
                    m_AudioSource.PlayOneShot(DefeatSound);
                    resultValue = GameSettings.PLAYER_SCORE_FOR_LOSE;
                    ResultTitle.text = "DEFEAT";
                    break;
                case (Result.None):
                    m_AudioSource.PlayOneShot(DefeatSound);
                    resultValue = GameSettings.PLAYER_SCORE_FOR_EXIT;
                    ResultTitle.text = "WASTED";
                    break;
            }

            GameStatisctics(resultValue);
            //PhotonNetwork.LeaveLobby();
        }

        private void GameStatisctics(int result)
        {
            if (DisableStaticticsPanel) return;

            int killing = m_PlayerController.Actor.AmountKill;
            int total = (result + (killing * GameSettings.PLAYER_SCORE_FOR_KILL));
            int coins = killing * GameSettings.PLAYER_COIN_FOR_KILL;

            m_SettingsManager.PlayerInfo.AddCoin(coins);
            m_SettingsManager.PlayerInfo.AddScore(total);
            m_SettingsManager.PlayerInfo.AddCountKill(killing);

            CoinCounter.text = coins.ToString();

            LevelCounter.text = m_SettingsManager.PlayerInfo.Level.ToString();
            ScoreSlider.value = m_SettingsManager.PlayerInfo.GetRatioScore();

            SetActiveMenu(true);
            SetMenuAlpha(StaticticsPanel, true);
        }
    }
}