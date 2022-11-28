using Cinemachine;
using Cinemachine.Examples;
using Platinum.Settings;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Platinum.Player
{
    public class ThirdPersonController : MonoBehaviour
    {
        [Header("References")]
        public Transform PlayerFollowObject;
        public CinemachineVirtualCamera PlayerFollowCamera;
        public CinemachineFreeLook PlayerLockedCamera;
        [Header("Rotation")]
        public float BodyRotationSpeed = 15f;
        public float WeaponRotationSpeed = 5f;
        public Transform WeaponLookAtTarget;
        public Transform HandsHolder;
        public Transform FollowBodyTarget;
        [Header("LookAtCrosshair")]
        public LayerMask CastLookLayer;
        public Camera MainCamera;
        [Header("Cinemachine Locked Camera")]
        public float smoothTime = 0.3F;
        [Header("Cinemachine")]
        [Range(1, 100)]
        public float Sensitivity = 10f;
        public float SensitivityMultiplier = 0.3f;
        [Tooltip("How far in degrees can you move the camera up")]
        public float TopClamp = 30.0f;
        [Tooltip("How far in degrees can you move the camera down")]
        public float BottomClamp = -30.0f;
        [Tooltip("Additional degress to override the camera. Useful for fine tuning camera position when locked")]
        public float CameraAngleOverride = 0.0f;
        [Tooltip("For locking the camera position on all axis")]
        public bool LockCameraPosition = false;

        public UnityAction<Transform> OnSwitchedToCamera;
        //public UnityAction<bool> OnJump;

        public Vector3 MouseWorldPosition { get; private set; } = Vector3.zero;
        private bool GamePause = true;
        private bool MenuPause = true;
        private bool isFollowPlayer;

        private bool IsActive => GamePause || MenuPause || hasPlayerDie
                                           || !m_PlayerInputHandler.isFocus 
                                           || m_PlayerInputHandler.IsMouseInGameWindow;

        // cinemachine
        private float m_CurrentSensitivity;
        private float m_CinemachineTargetYaw;
        private float m_CinemachineTargetPitch;
        public float ClampTargetPitch { get; private set; }

        private const float m_threshold = 0.01f;
        private bool IsCurrentDeviceMouse => m_PlayerInput.currentControlScheme == "KeyboardMouse";
        private PlayerInput m_PlayerInput;
        private bool hasPlayerDie;

        //references
        private PlayerInputHandler m_PlayerInputHandler;
        private PlayerController m_PlayerController;
        private PlayerWeaponsManager m_PlayerWeaponsManager;
        private LoadManager m_LoadManager;
        private Transform m_PlayerBody;
        private Transform m_MainCamera;
        private Transform m_CameraTarget;
        private Transform m_PlayerHips;
        private Transform m_FollowCameraTarget;
        private bool IsRefreshMatch;
        private Cinemachine3rdPersonFollow m_ThirdPersonFollow;
        private SettingsManager m_SettingsManager;
        private Vector2 m_CrosshairPosition;

        private void OnDestroy()
        {
            EventManager.RemoveListener<EndSpawnEvent>(OnEndSpawnEvent);
            EventManager.RemoveListener<MenuPauseEvent>(OnMenuPauseEvent);
            EventManager.RemoveListener<GamePauseEvent>(OnGamePauseEvent);
            //EventManager.RemoveListener<RefreshMatchEvent>(OnRefreshMatchEvent);
        }

        private void Awake()
        {
            m_CameraTarget = PlayerFollowObject;
            m_MainCamera = MainCamera.transform;
            m_PlayerInput = GetComponent<PlayerInput>();
            m_CurrentSensitivity = Sensitivity * SensitivityMultiplier;
            m_ThirdPersonFollow = PlayerFollowCamera.GetComponentInChildren<Cinemachine3rdPersonFollow>();

            EventManager.AddListener<EndSpawnEvent>(OnEndSpawnEvent);
            EventManager.AddListener<MenuPauseEvent>(OnMenuPauseEvent);
            EventManager.AddListener<GamePauseEvent>(OnGamePauseEvent);
            //EventManager.AddListener<RefreshMatchEvent>(OnRefreshMatchEvent);
        }


        private void OnPlayerDie()
        {
            hasPlayerDie = true;
            SwitchingCameraMode(true);
        }
        private void OnPlayerSpawn()
        {
            hasPlayerDie = false;
            SwitchingCameraMode(false);
        }

        private void OnMenuPauseEvent(MenuPauseEvent evt)
        {
            MenuPause = evt.MenuPause;
        }

        private void OnGamePauseEvent(GamePauseEvent evt)
        {
            GamePause = evt.ServerPause;
        }

        private void OnEndSpawnEvent(EndSpawnEvent evt)
        {
            m_LoadManager = evt.LoadManager;
            m_PlayerInputHandler = m_LoadManager.PlayerInputHandler;
            m_SettingsManager = evt.LoadManager.SettingsManager;

            m_PlayerWeaponsManager = m_LoadManager.PlayerWeaponsManager;
            m_PlayerController = m_LoadManager.PlayerController;
            m_PlayerBody = m_PlayerController.transform;
            m_PlayerHips = m_PlayerController.SkeletonHips;
            m_PlayerController.Health.OnDie += OnPlayerDie;
            m_PlayerController.OnSpawn += OnPlayerSpawn;

            //m_CameraTarget.SetParent(m_PlayerBody);

            PlayerFollowCamera.Follow = PlayerFollowObject;
            PlayerFollowCamera.LookAt = PlayerFollowObject;

            PlayerLockedCamera.Follow = PlayerFollowObject;
            PlayerLockedCamera.LookAt = PlayerFollowObject;

            SetCrosshairPosition(m_SettingsManager.CurrentPlayerSaves.CrosshairPosition);
            SwitchingCameraMode(false);

            GamePause = false;
        }

        private void Update()
        {
            if (IsActive) return;
            WeaponLookAt();
            //float xFollow = FollowBodyTarget.eulerAngles.x;
            //WeaponLookAtTarget.rotation = Quaternion.Slerp(WeaponLookAtTarget.rotation, Quaternion.Euler(xFollow, 0, 0), WeaponRotationSpeed * Time.deltaTime);


            //NewLookCharacter();
            //LookAtTarget.position = new Vector3(LookAtTarget.position.x, LookAtTarget.position.y, GetLookCamera().z);
            //OrientTowards(MouseWorldPosition);
        }
        private void LateUpdate()
        {
            //WeaponLookAtTarget.position = BodyLookAtTarget.position;
            if (hasPlayerDie)
            {
                StabilizeCameraRotation();
                return;
            }

            if (IsActive) return;
            CameraRotation();
            NewTowardsCharacter();
            //WeaponLookAt(Time.deltaTime);
        }

        private void WeaponLookAt()
        {
            Vector3 target = m_PlayerWeaponsManager.GetLookCamera();
            float angle = Vector3.Angle(m_PlayerBody.forward, target - m_PlayerBody.position);
            if (angle > 60)
            {
                return;
            }

            FollowBodyTarget.position = HandsHolder.position;
            FollowBodyTarget.LookAt(target);
            WeaponLookAtTarget.position = HandsHolder.position;
            float xFollow = FollowBodyTarget.eulerAngles.x;
            float yBody = FollowBodyTarget.eulerAngles.y;
            WeaponLookAtTarget.rotation = Quaternion.Slerp(WeaponLookAtTarget.rotation, Quaternion.Euler(xFollow, yBody, 0), WeaponRotationSpeed * Time.deltaTime);
        }

        private void NewTowardsCharacter()
        {
            float yawCamera = m_MainCamera.eulerAngles.y;
            m_CameraTarget.position = m_PlayerBody.TransformPoint(m_CrosshairPosition);
            m_PlayerBody.rotation = Quaternion.Slerp(m_PlayerBody.rotation, Quaternion.Euler(0, yawCamera, 0), BodyRotationSpeed * Time.deltaTime);
        }

        private void CameraRotation()
        {
            // if there is an input and camera position is not fixed
            if (m_PlayerInputHandler.look.sqrMagnitude >= m_threshold && !LockCameraPosition)
            {

                //Don't multiply mouse input by Time.deltaTime;
                float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

                m_CinemachineTargetYaw += m_PlayerInputHandler.look.x * deltaTimeMultiplier * m_CurrentSensitivity;
                m_CinemachineTargetPitch += m_PlayerInputHandler.look.y * deltaTimeMultiplier * m_CurrentSensitivity;
            }

            // clamp our rotations so our values are limited 360 degrees
            m_CinemachineTargetYaw = ClampAngle(m_CinemachineTargetYaw, float.MinValue, float.MaxValue);
            m_CinemachineTargetPitch = ClampAngle(m_CinemachineTargetPitch, BottomClamp, TopClamp);
            ClampTargetPitch = m_CinemachineTargetPitch;

            // Cinemachine will follow this target
            m_CameraTarget.position = m_PlayerHips.TransformPoint(m_CrosshairPosition);
            m_CameraTarget.rotation = Quaternion.Euler(m_CinemachineTargetPitch + CameraAngleOverride, m_CinemachineTargetYaw, 0f);
        }

        public void SerCameraDistance(float distance)
        {
            //m_FramingFollowCamera.m_CameraDistance = distance / 10;
            m_ThirdPersonFollow.CameraDistance = distance / 10;
        }

        public void SetCrosshairPosition(Vector2 newPosition)
        {
            m_CrosshairPosition = newPosition / 10;
        }

        public void SetSensitivity(float newAimSensitivity)
        {
            Sensitivity = newAimSensitivity;
            m_CurrentSensitivity = Sensitivity * SensitivityMultiplier;
        }

        public void SwitchingCameraMode(bool locked)
        {
            //m_CameraTarget.SetParent(locked ? null : m_PlayerBody);

            if (!locked)
            {
                Vector3 target = m_PlayerBody.TransformPoint(0,2,20);

                FollowBodyTarget.position = HandsHolder.position;
                FollowBodyTarget.LookAt(target);
                WeaponLookAtTarget.position = HandsHolder.position;
                float xFollow = FollowBodyTarget.eulerAngles.x;
                float yBody = FollowBodyTarget.eulerAngles.y;
                WeaponLookAtTarget.rotation = Quaternion.Euler(xFollow, yBody, 0);
                
                m_CameraTarget.position = m_PlayerHips.TransformPoint(m_CrosshairPosition);
                m_CameraTarget.rotation =  Quaternion.Euler(xFollow, yBody, 0);

                m_CinemachineTargetYaw = 0;
                m_CinemachineTargetPitch = 0;
                ClampTargetPitch = 0;
                
                NewTowardsCharacter();
            }

            if (m_PlayerController.Health.IsInstantDead) return;

            PlayerLockedCamera.gameObject.SetActive(locked);
            PlayerFollowCamera.gameObject.SetActive(!locked);
            //m_CurrentSensitivity = locked ? 0 : Sensitivity * SensitivityMultiplier;
        }

        private Vector3 velocity = Vector3.zero;
        private float timeCount = 0.0f;

        private void StabilizeCameraRotation()
        {
            //Stabilize position target
            // Define a target position above and behind the target transform
            Vector3 targetPosition = m_PlayerHips.TransformPoint(new Vector3(0, 0, 0));
            // Smoothly move the camera towards that target position
            m_CameraTarget.position = Vector3.SmoothDamp(m_CameraTarget.position, targetPosition, ref velocity, smoothTime);
            if (m_PlayerController.Health.IsInstantDead)
            {
                Vector3 newCameraPosition = m_CameraTarget.position;
                newCameraPosition.y = targetPosition.y;
                m_CameraTarget.position = newCameraPosition;
            }

            //Stabilize rotation target (hmm realistic camera?)
            //m_CameraTarget.transform.rotation = Quaternion.Slerp(m_CameraTarget.rotation, m_PlayerHits.rotation, timeCount);
            //timeCount = timeCount + Time.deltaTime;
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }
    }
}