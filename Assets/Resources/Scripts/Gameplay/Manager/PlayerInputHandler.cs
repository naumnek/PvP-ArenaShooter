using Unity.FPS.Game;
using Platinum.Settings;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

namespace Platinum.Player
{
    public enum MoveState
    {
        Idle,
        Walk,
        Sprint,
        Crouch
    }

    public class PlayerInputHandler : MonoBehaviour
    {
        [Header("KeyCode")]
        public bool jump;
        public bool crouch;
        public int number;
        public bool reload;
        public bool SelectWeapon;
        public bool tab;
        public bool click;

        public bool isFocus { get; private set; } = true;
        public MoveState MoveState { get; private set; }
        public Vector2 movearrow { get; private set; }
        public float MoveAxisRaw { get; private set; }
        public Vector2 move { get; private set; }
        public Vector2 look { get; private set; }
        public bool shoot { get; private set; }
        public bool aim { get; private set; }
        public bool sprint { get; private set; }

        //spectator camera 
        public bool uparrow { get; private set; }
        public bool downarrow { get; private set; }
        public bool rightarrow { get; private set; }
        public bool leftarrow { get; private set; }
        public bool upwards { get; private set; }
        public bool downwards { get; private set; }

        public bool aimode { get; private set; }
        public bool ToggleTexture { get; private set; }
        public bool SpectatorMode { get; private set; }
        public bool LockCameraPosition { get; private set; }
        public bool HideGameHUD { get; private set; }

        [Header("Movement Settings")]
        public bool analogMovement;

#if !UNITY_IOS || !UNITY_ANDROID
        [Header("Mouse Cursor Settings")]
        public bool cursorLocked = true;
        public bool cursorInputForLook = true;
#endif

        [Tooltip("Additional sensitivity multiplier for WebGL")]
        public float WebglLookSensitivityMultiplier = 0.25f;

        [Tooltip("Limit to consider an input when using a trigger on a controller")]
        public float TriggerAxisThreshold = 0.4f;

        [Tooltip("Used to flip the vertical input axis")]
        public bool InvertYAxis = false;

        [Tooltip("Used to flip the horizontal input axis")]
        public bool InvertXAxis = false;

        public InputPlayerAssets InputPlayerAssets;
        private GameFlowManager m_GameFlowManager;

        private bool ServerPause = true;
        private bool MenuPause = true;

        private LoadManager m_LoadManager;
        private ThirdPersonController m_ThirdPersonController;

        private bool IgnorePause;
        private static PlayerInputHandler instance;

#if ENABLE_INPUT_SYSTEM

        public static PlayerInputHandler GetInstance() => instance;

        private void Awake()
        {
            instance = this;
            InputPlayerAssets = new InputPlayerAssets();

            EventManager.AddListener<MenuPauseEvent>(OnMenuPauseEvent);
            EventManager.AddListener<GamePauseEvent>(OnGamePauseEvent);
            EventManager.AddListener<EndSpawnEvent>(OnPlayerSpawnEvent);

            m_LoadManager = FindObjectOfType<LoadManager>();
            if (!m_LoadManager)
            {
                Activate();
                MenuPause = false;
            }
        }

        private void OnDestroy()
        {
            EventManager.RemoveListener<MenuPauseEvent>(OnMenuPauseEvent);
            EventManager.RemoveListener<GamePauseEvent>(OnGamePauseEvent);
            EventManager.RemoveListener<EndSpawnEvent>(OnPlayerSpawnEvent);
        }

        private void OnMenuPauseEvent(MenuPauseEvent evt)
        {
            MenuPause = evt.MenuPause;
        }
        private void OnGamePauseEvent(GamePauseEvent evt)
        {
            //ServerPause = evt.ServerPause;
        }
        private void OnPlayerSpawnEvent(EndSpawnEvent evt)
        {
            m_LoadManager = evt.LoadManager;
            m_GameFlowManager = m_LoadManager.GameFlowManager;
            m_ThirdPersonController = m_LoadManager.ThirdPersonController;

            switch (m_LoadManager.TypeLevel)
            {
                case TypeLevel.Generator:
                    if (SceneController.Instance.LoadingScreenController.LevelSeed == 12345678)
                    {
                        aimode = true;
                        m_GameFlowManager.SetActiveSpectatorInfo();
                    }
                    break;
                case TypeLevel.Arena:
                    break;
            }

            Activate();
        }

        private void Activate()
        {
            /*
            _InputPlayerAssets.Player.Sprint.performed += ctx =>
            {
                SprintInput(true);
            };
            _InputPlayerAssets.Player.Sprint.canceled += ctx =>
            {
                SprintInput(false);
            };

            _InputPlayerAssets.Player.Crouch.performed += ctx =>
            {
                SprintInput(true);
            };
            _InputPlayerAssets.Player.Crouch.canceled += ctx =>
            {
                SprintInput(false);
            };
            */
            InputPlayerAssets.Player.ChangeWeapons.performed += ctx =>
            {
                if (HasPause) int.TryParse(ctx.control.name, out number);
            };

            InputPlayerAssets.Player.AiMode.performed += ctx =>
            {
                if (HasPause) aimode = !aimode;
            };

            InputPlayerAssets.Player.SpectatorMode.performed += ctx =>
            {
                if (HasPause) SpectatorMode = !SpectatorMode;
            };

            InputPlayerAssets.Player.LockCameraPosition.performed += ctx =>
            {
                if (HasPause) LockCameraPosition = !LockCameraPosition;
            };

            InputPlayerAssets.Player.ToggleTexture.performed += ctx =>
            {
                if (HasPause) ToggleTexture = true;
            };

            InputPlayerAssets.Player.HideGameHUD.performed += ctx =>
            {
                if (HasPause)
                {
                    HideGameHUD = !HideGameHUD;
                    if (m_LoadManager)
                    {
                        m_GameFlowManager.SetActivePlayerHUD(!HideGameHUD);
                    }
                }
            };
            ServerPause = false;
        }

        private void OnEnable()
        {
            InputPlayerAssets.Enable();
        }

        private void OnDisable()
        {
            InputPlayerAssets.Disable();
        }

        public void OnUpwards(InputValue value)
        {
            UpwardsInput(value.isPressed);
        }

        public void OnDownwards(InputValue value)
        {
            DownwardsInput(value.isPressed);
        }

        public void OnUpArrow(InputValue value)
        {
            UpArrowInput(value.isPressed);
        }

        public void OnDownArrow(InputValue value)
        {
            DownArrowInput(value.isPressed);
        }

        public void OnRightArrow(InputValue value)
        {
            RightArrowInput(value.isPressed);
        }

        public void OnLeftArrow(InputValue value)
        {
            LeftArrowInput(value.isPressed);
        }
        public void OnSelectWeapon(InputValue value)
        {
            SelectWeaponInput(value.isPressed);
        }

        public void OnTab(InputValue value)
        {
            TabInput(value.isPressed);
        }


        public void OnMove(InputValue value)
        {
            MoveInput(value.Get<Vector2>());
        }
        public void OnMoveArrows(InputValue value)
        {
            MoveArrowsInput(value.Get<Vector2>());
        }

        public void OnLook(InputValue value)
        {
            if (cursorInputForLook)
            {
                LookInput(value.Get<Vector2>());
            }
        }

        public void OnShoot(InputValue value)
        {
            ShootInput(value.isPressed);
        }

        public void OnAim(InputValue value)
        {
            AimInput(value.isPressed);
        }

        public void OnReload(InputValue value)
        {
            ReloadInput(value.isPressed);
        }

        public void OnJump(InputValue value)
        {
            JumpInput(value.isPressed);
        }

        public void OnSprint(InputValue value)
        {
            SprintInput(value.isPressed);
        }

        public void OnCrouch(InputValue value)
        {
            CrouchInput(value.isPressed);
        }

#else
	// old input sys if we do decide to have it (most likely wont)...
#endif

        public void UpwardsInput(bool newState)
        {
            upwards = HasPause ? newState : false;
        }
        public void DownwardsInput(bool newState)
        {
            downwards = HasPause ? newState : false;
        }
        public void UpArrowInput(bool newState)
        {
            uparrow = HasPause ? newState : false;
        }
        public void DownArrowInput(bool newState)
        {
            downarrow = HasPause ? newState : false;
        }
        public void RightArrowInput(bool newState)
        {
            rightarrow = HasPause ? newState : false;
        }
        public void LeftArrowInput(bool newState)
        {
            leftarrow = HasPause ? newState : false;
        }
        public void SelectWeaponInput(bool newState)
        {
            SelectWeapon = ServerPause ? false : newState;
        }

        public void TabInput(bool newState)
        {
            tab = ServerPause ? false : newState;
        }

        public void MoveArrowsInput(Vector2 newDirection)
        {
            movearrow = HasPause ? newDirection : Vector2.zero;
        }

        public void LookInput(Vector2 newDirection)
        {
            look = HasPause ? newDirection : Vector2.zero;
        }

        public void ShootInput(bool newState)
        {
            shoot = HasPause ? newState : false;
            click = newState;
        }

        public void AimInput(bool newState)
        {
            aim = HasPause ? newState : false;
        }

        public void JumpInput(bool newState)
        {
            jump = HasPause ? newState : false;
        }

        public void MoveInput(Vector2 newDirection)
        {
            move = HasPause ? newDirection : Vector2.zero;
            MoveState = !crouch && !sprint ? MoveState.Walk : MoveState;
        }

        public void CrouchInput(bool newState)
        {
            crouch = HasPause ? newState : false;
            MoveState = crouch ? MoveState.Crouch : MoveState.Idle;
        }

        public void SprintInput(bool newState)
        {
            sprint = HasPause ? newState : false;
            MoveState = sprint ? MoveState.Sprint : MoveState.Idle;
        }

        public void ReloadInput(bool newState)
        {
            reload = HasPause ? newState : false;
        }

        private bool HasPause { get { return !MenuPause && !ServerPause; } }

#if !UNITY_IOS || !UNITY_ANDROID

        private void OnApplicationFocus(bool hasFocus)
        {
            if(hasFocus)ResetCursorPosition();
            isFocus = hasFocus;
        }
        public bool IsMouseInGameWindow;// { get { return !(0 > Input.mousePosition.x || 0 > Input.mousePosition.y || Screen.width < Input.mousePosition.x || Screen.height < Input.mousePosition.y); } }
        private bool isCursorLocked => isFocus && !MenuPause;

        private void UpdateMouseOverGameWindow()
        {
            Vector3 mousePos = Input.mousePosition;
            IsMouseInGameWindow = (0 > mousePos.x || 0 > mousePos.y || Screen.width < mousePos.x || Screen.height < mousePos.y);
        }

        private void ResetCursorPosition()
        {
            Vector2 screenCenterPoint = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Mouse.current.WarpCursorPosition(screenCenterPoint);
            InputState.Change(Mouse.current.position, screenCenterPoint);
        }
        
        private void Update()
        {
            UpdateMouseOverGameWindow();
            Cursor.visible = !isCursorLocked;
            Cursor.lockState = isCursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
            if (false)
            {
            }
        }

#endif
    }
}