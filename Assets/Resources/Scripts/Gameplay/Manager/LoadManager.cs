using System;
using System.Collections;
using UnityEngine;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using Platinum.Player;
using Unity.FPS.AI;
using System.Linq;
using Random = UnityEngine.Random;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_EDITOR
// For Unity versions newer than 2017.1
using UnityEditor;

[InitializeOnLoad]
public class DetectPlayModeChanges
{

    static DetectPlayModeChanges()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.ExitingEditMode:
                // Do whatever before entering play mode
                break;
            case PlayModeStateChange.EnteredPlayMode:
                // Do whatever after entering play mode
                break;
            case PlayModeStateChange.ExitingPlayMode:
                PlayerPrefs.DeleteAll();
                // Do whatever before returning to edit mode
                break;
            case PlayModeStateChange.EnteredEditMode:
                // Do whatever after returning to edit mode
                break;
        }
    }
}
#endif

namespace Platinum.Settings
{
    public enum EndLevel
    {
        LoadMenu,
        RefreshGame,
        RestartLevel,
    }

    public enum TypeLevel
    {
        Menu,
        Generator,
        Arena,
        Adventure,
    }

    public enum SpawnMode
    {
        Spawn,
        Find,
    }

    public class LoadManager : MonoBehaviour
    {
        [Header("Mode")]
        public TypeLevel TypeLevel = TypeLevel.Generator;
        public EndLevel EndLevel = EndLevel.RestartLevel;
        public SpawnMode SpawnMode = SpawnMode.Spawn;
        [Range(2f, 5f)]
        public float DelaySpawnPlayerEvent = 2f;
        [Header("Settings")]
        [Tooltip("spawnpoint player in test mode")]
        public GameObject PrefabPlayer;
        public GameObject PrefabBot;
        public AnimationClip DefaultWeaponClip;
        public SettingsManager SettingsManager;
        /// <summary>
        /// LevelGenerator seed
        /// </summary>
        public int Seed = 0;
        [Header("Level References")]
        public Transform EnemyContainer;
        public Transform PlayerSpawnpoint;
        public Transform BotSpawnpoint;
        public SceneController SceneController { get; private set; }
        public ThirdPersonController ThirdPersonController { get; private set; }
        public PlayerInputHandler PlayerInputHandler { get; private set; }
        public PlayerInput PlayerInput { get; private set; }
        public PlayerWeaponsManager PlayerWeaponsManager { get; private set; }
        public GameFlowManager GameFlowManager { get; private set; }
        public ActorsManager ActorsManager { get; private set; }

        public PlayerController PlayerController { get; private set; }
        public EnemyController EnemyController { get; private set; }

        public static LoadManager Instance { get; private set; }
        public int LevelSeed { get; private set; }
        private bool endSpawnBot;
        private bool endSpawnPlayer;
        private int EndMatchCount;
        private string WinnerName = "naumnek";
        private Actor DieActor;

        private void Awake()
        {
            Instance = this;
            SettingsManager.ResetAllItemInfo();
            SettingsManager.RequredRoom.CreateDefaultRoom();
            
            EventManager.AddListener<StartGenerationEvent>(OnStartGenerationEvent);
            EventManager.AddListener<DieEvent>(OnDieEvent);
            
            SceneController = FindObjectOfType<SceneController>();
            ThirdPersonController = FindObjectOfType<ThirdPersonController>();
            PlayerInputHandler = FindObjectOfType<PlayerInputHandler>();
            PlayerInput = FindObjectOfType<PlayerInput>();
            PlayerWeaponsManager = FindObjectOfType<PlayerWeaponsManager>();
            GameFlowManager = FindObjectOfType<GameFlowManager>();
            ActorsManager = FindObjectOfType<ActorsManager>();
            
            if (!EnemyContainer)
            {
                EnemyContainer = new GameObject("EnemyContainer").transform;
                EnemyContainer.SetParent(transform);
            }

            if (!PlayerSpawnpoint)
            {
                PlayerSpawnpoint = new GameObject("PlayerSpawnpoint").transform;
            }
            if (!BotSpawnpoint)
            {
                EnemyContainer = new GameObject("BotSpawnpoint").transform;
            }
        }

        private void Start()
        {
            FindSceneController();
            SetMode();
        }

        private IEnumerator SpawmDelay()
        {
            yield return new WaitForSeconds(DelaySpawnPlayerEvent);
        }

        private void FindSceneController()
        {
            if (SceneController)
            {
                Debug.Log("LoadManager: finish load scene " + SceneController.CurrentScene);
            }
            else
            {
                Debug.Log("Not found SceneController from scene ");
                TypeLevel = TypeLevel.Arena;
                PlayerInfo info = new PlayerInfo();
                info.SetName("Player" + UnityEngine.Random.Range(100, 1000));
                info.AddScore(UnityEngine.Random.Range(20, 75));
                SettingsManager.CreatePlayer(info);

            }
        }

        private void SetMode()
        {

            switch (TypeLevel)
            {
                case (TypeLevel.Menu):
                    break;
                case (TypeLevel.Arena):
                    InitArena();
                    break;
                default:
                    InitArena();
                    break;
            }
        }

        private void InitArena()
        {
            switch (SpawnMode)
            {
                case (SpawnMode.Spawn):
                    SpawnPlayer();
                    SpawnBot();
                    break;

                case (SpawnMode.Find):

                    EnemyController = FindObjectOfType<EnemyController>();
                    PlayerController = FindObjectOfType<PlayerController>();
            
                    EnemyController.Actor.SetAffiliation(EnemyController, SettingsManager, ActorsManager.PlayerAffiliation + 1, "Bot" + Random.Range(1000, 10000));
                    PlayerController.Actor.SetAffiliation(PlayerController, SettingsManager, ActorsManager.PlayerAffiliation, SettingsManager.PlayerInfo.Name);
                    
                    FindPlayer();
                    FindBot();
                    break;
            }
        }

        private void FindPlayer()
        {
            if (PlayerController)
            {
                Transform player = PlayerController.transform;
                PlayerSpawnpoint.position = player.position;
                PlayerSpawnpoint.rotation = player.rotation;
                
                PlayerController.SetSpawn(PlayerSpawnpoint);

                PlayerController.OnSpawn += FinishRefresh;
                PlayerController.OnInitialize += EndSpawn;
                PlayerController.Activation(this);
            }
        }

        private void FindBot()
        {
            if (EnemyController)
            {
                Transform bot = EnemyController.transform;
                BotSpawnpoint.position = bot.position;
                BotSpawnpoint.rotation = bot.rotation;
                
                EnemyController.SetSpawn(BotSpawnpoint);

                EnemyController.OnSpawn += FinishRefresh;
                EnemyController.OnInitialize += EndSpawn;
                EnemyController.Activation(this);
            }

        }

        private void SpawnPlayer()
        {
            Transform player = Instantiate(PrefabPlayer, PlayerSpawnpoint.position, PlayerSpawnpoint.rotation).transform;
            //player.SetParent(EnemyContainer);

            PlayerController = player.GetComponent<PlayerController>();

            if (PlayerController)
            {
                PlayerController.OnSpawn += FinishRefresh;
                PlayerController.OnInitialize += EndSpawn;
                PlayerController.Activation(this);
            }
        }

        private void EndSpawn()
        {
            if (PlayerController.controllable && EnemyController.controllable)
            {
                EndSpawnEvent evt = Events.EndSpawnEvent;
                evt.LoadManager = this;
                EventManager.Broadcast(evt); 
            }
        }

        private void SpawnBot()
        {
            Transform enemy = Instantiate(PrefabBot, BotSpawnpoint.position, BotSpawnpoint.rotation).transform;
            EnemyController = enemy.GetComponent<EnemyController>();
            EnemyController.OnSpawn += FinishRefresh;
            EnemyController.OnInitialize += EndSpawn;
            EnemyController.Activation(this);
        }

        private void OnDestroy()
        {
            EventManager.RemoveListener<StartGenerationEvent>(OnStartGenerationEvent);
            EventManager.RemoveListener<DieEvent>(OnDieEvent);
        }

        public void OnDieEvent(DieEvent evt)
        {
            if (DieActor) return;
            DieActor = evt.Actor; ;
            Actor winActor = ActorsManager.GetEnemyActors(evt.Actor).FirstOrDefault();

            int KillerTeam = winActor.Affiliation;
            WinnerName = winActor.NickName;
            DisplayMessageEvent displayMessage = Events.DisplayMessageEvent;
            displayMessage.Message = "Winner: " + WinnerName;
            EventManager.Broadcast(displayMessage);

            GamePauseEvent gpe = Events.GamePauseEvent;
            gpe.ServerPause = true;
            EventManager.Broadcast(gpe);

            if (TypeLevel != TypeLevel.Arena) return;

            StartCoroutine(EndMatchResult());
        }

        private IEnumerator EndMatchResult()
        {
            if (GameFlowManager.TeamsKillCounter.TeamsKillScores.Any(t => t >= GameSettings.COUNT_KILLS_TO_TEAM_WIN))
            {
                StartCoroutine(WaitForLoadScene());
            }
            else
            {
                GameFlowManager.EndMatchEffect();
                yield return new WaitForSeconds(GameFlowManager.EndMatchAlphaDelay * 2);
                PlayerController.RespawnPlayer();
                EnemyController.RespawnBot();
            }
        }

        private void FinishRefresh()
        {
            if (PlayerController.controllable && EnemyController.controllable)
            {
                StartCoroutine(InitRefreshMatch());
                DieActor = null;
            }
        }

        IEnumerator InitRefreshMatch()
        {
            if (DieActor == null) yield break;
            Debug.Log("InitRefreshMatch");
            EventManager.Broadcast(Events.RefreshMatchEvent);

            yield return new WaitForSeconds(1f + GameFlowManager.EndMatchAlphaDelay);

            GamePauseEvent gpe = Events.GamePauseEvent;
            gpe.ServerPause = false;
            EventManager.Broadcast(gpe);
        }



        IEnumerator WaitForLoadScene()
        {
            yield return new WaitForSeconds(GameSettings.MATCH_RESPAWN_TIME);

            GameFlowManager.ResultEndGame(PlayerController.Health.IsDead ? Result.Lose : Result.Win);
        }
        
        public void OnStartGenerationEvent(StartGenerationEvent evt)
        {
            Seed = evt.Seed;
        }
    }
}

