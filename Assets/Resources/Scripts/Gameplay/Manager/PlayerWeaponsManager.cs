using Cinemachine;
using Platinum.Settings;
using System.Collections;
using System.Linq;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
#endif

namespace Platinum.Player
{
    public class PlayerWeaponsManager : MonoBehaviour
    {
        public enum WeaponSwitchState
        {
            Up,
            Down,
            PutDownPrevious,
            PutUpNew,
        }
        [Header("References")]
        [Tooltip("Layer to set FPS weapon gameObjects to")]
        public LayerMask CastShootLayer;
        public CinemachineVirtualCamera AimVirtualCamera;
        public bool AutoSwitchNewWeapon = true;

        [Header("Aim Options")]
        [Tooltip("The time at which the enemy rotates")]
        public float AimDuration = 0.1f;

        [Header("Weapon Recoil")]
        [Tooltip("This will affect how fast the recoil moves the weapon, the bigger the value, the fastest")]
        public float RecoilSharpness = 50f;

        [Tooltip("Maximum distance the recoil can affect the weapon")]
        public float MaxRecoilDistance = 0.5f;

        [Tooltip("How fast the weapon goes back to it's original position after the recoil is finished")]
        public float RecoilRestitutionSharpness = 10f;

        [Header("Weapon")]
        [Tooltip("Delay before switching weapon a second time, to avoid recieving multiple inputs from mouse wheel")]
        public float WeaponSwitchDelay = 1f;

        public bool IsAiming { get; private set; }
        public int ActiveWeaponIndex { get; private set; }

        public UnityAction<WeaponController> OnSwitchedToWeapon;
        public UnityAction<WeaponController, int> OnAddedWeapon;
        public UnityAction<WeaponController, int> OnRemovedWeapon;
        public bool hasFired { get; private set; } = false;

        public WeaponController[] WeaponSlots { get; private set; }  // 9 available weapon slots
        PlayerInputHandler m_PlayerInputHandler;
        float m_WeaponBobFactor;
        Vector3 m_LastCharacterPosition;
        Vector3 m_WeaponMainLocalPosition;
        Vector3 m_WeaponBobLocalPosition;
        Vector3 m_WeaponRecoilLocalPosition;
        Vector3 m_AccumulatedRecoil;
        float m_TimeStartedWeaponSwitch;
        WeaponSwitchState m_WeaponSwitchState;
        int m_WeaponSwitchNewWeaponIndex;
        private Transform m_WeaponMuzzle;
        public WeaponController activeWeapon { get; private set; }
        private WeaponController[] WeaponsList;

        public Transform WeaponCamera { get; private set; }

        private Vector3 m_CrosshairLookPosition = Vector3.zero;
        public bool IsPointingAtEnemy { get; private set; }
        public Vector3 MouseWorldPosition { get; private set; } = Vector3.zero;
        private Camera m_MainCamera;

        private Transform m_PlayerBody;
        private Transform m_WeaponPivot;
        private Transform m_RightHandGrip;
        private Transform m_LeftHandGrip;
        private bool ServerPause = true;
        private bool MenuPause = true;

        public PlayerSaves PlayerSettings { get; private set; }
        private ThirdPersonController m_ThirdPersonController;
        private PlayerController m_PlayerController;
        private LoadManager m_LoadManager;
        private bool m_DisableWeapon;

        Ray shootRay;
        RaycastHit hitInfo;
        Ray crosshairRay;
        RaycastHit crosshairHitInfo;

        private void OnDestroy()
        {
            EventManager.RemoveListener<MenuPauseEvent>(OnMenuPauseEvent);
            EventManager.RemoveListener<EndSpawnEvent>(OnPlayerSpawnEvent);
            EventManager.RemoveListener<GamePauseEvent>(OnGamePauseEvent);
        }

        private void Awake()
        {
            WeaponSlots = new WeaponController[9];

            EventManager.AddListener<MenuPauseEvent>(OnMenuPauseEvent);
            EventManager.AddListener<EndSpawnEvent>(OnPlayerSpawnEvent);
            EventManager.AddListener<GamePauseEvent>(OnGamePauseEvent);
        }


        void Update()
        {
            if (ServerPause || MenuPause) return;

            //detectionModule.PlayerHandleTargetDetection(m_Actor, m_SelfColliders);
            GetLookCamera();
            HasShoot();
            HasHandling();
        }

        private void Activate()
        {
            m_PlayerController = m_LoadManager.PlayerController;
            m_ThirdPersonController = m_LoadManager.ThirdPersonController;
            m_MainCamera = m_ThirdPersonController.MainCamera;
            WeaponCamera = m_MainCamera.transform;

            m_PlayerBody = m_PlayerController.transform;
            m_PlayerInputHandler = m_LoadManager.PlayerInputHandler;
            WeaponsList = m_LoadManager.SettingsManager.GetRequredWeapons();
            PlayerSettings = m_LoadManager.SettingsManager.CurrentPlayerSaves;

            m_RightHandGrip = m_PlayerController.RightHandGrip;
            m_LeftHandGrip = m_PlayerController.LeftHandGrip;
            m_WeaponPivot = m_PlayerController.WeaponPivot;

            m_PlayerController.Health.OnDie += OnPlayerDied;

            //m_ThirdPersonController.OnJump += OnJump;

            if (AimVirtualCamera)
            {
                AimVirtualCamera.Follow = m_ThirdPersonController.PlayerFollowObject;
                AimVirtualCamera.LookAt = m_ThirdPersonController.PlayerFollowObject;
            }

            m_DisableWeapon = m_PlayerController.DisableWeapon;

            if (!m_DisableWeapon)
            {
                ActiveWeaponIndex = -1;
                m_WeaponSwitchState = WeaponSwitchState.Down;


                OnSwitchedToWeapon += OnWeaponSwitched;

                // Add starting weapons
                WeaponController[] Weapons = m_PlayerController.StartingWeapons;
                for (int i = 0; i < Weapons.Length; i++)
                {
                    AddWeapon(Weapons[i].WeaponName);
                }

                SwitchWeapon(true);
            }

            ServerPause = false;
        }

        public void ResetWeapons()
        {
            WeaponController[] activeWeapons = WeaponSlots.Where(w => w != null).ToArray();
            for (int i = 0; i < activeWeapons.Length; i++)
            {
                RemoveWeapon(activeWeapons[i]);
            }

            ActiveWeaponIndex = -1;
            m_WeaponSwitchState = WeaponSwitchState.Down;

            // Add starting weapons
            WeaponController[] Weapons = m_PlayerController.StartingWeapons;
            for (int i = 0; i < Weapons.Length; i++)
            {
                AddWeapon(Weapons[i].WeaponName);
            }

            SwitchWeapon(true);
        }

        private void OnPlayerSpawnEvent(EndSpawnEvent evt)
        {
            m_LoadManager = evt.LoadManager;
            Activate();
        }

        private void OnPlayerDied()
        {
            ServerPause = true;
            m_PlayerController.Animator.Play(m_LoadManager.DefaultWeaponClip.name);
        }

        private void OnGamePauseEvent(GamePauseEvent evt)
        {
            ServerPause = evt.ServerPause;
        }
        private void OnMenuPauseEvent(MenuPauseEvent evt)
        {
            MenuPause = evt.MenuPause;
        }

        public bool IsHitEnemy { get; private set; } = false;
        public void OnHitEnemy(bool hit)
        {
            StartCoroutine(WaitHitEnemy(hit ? 0 : 0.5f));
        }

        private IEnumerator WaitHitEnemy(float delay)
        {
            yield return new WaitForSeconds(delay);
            IsHitEnemy = (delay == 0) ? true : false;
        }


        private void HasHandling()
        {
            // weapon switch handling
            if (!IsAiming &&
                (activeWeapon == null || !activeWeapon.IsCharging) &&
                (m_WeaponSwitchState == WeaponSwitchState.Up || m_WeaponSwitchState == WeaponSwitchState.Down))
            {
                int switchWeaponInput = 0;
                if (switchWeaponInput != 0)
                {
                    bool switchUp = switchWeaponInput > 0;
                    SwitchWeapon(switchUp);
                }
                else
                {
                    switchWeaponInput = m_PlayerInputHandler.number;
                    if (switchWeaponInput != 0)
                    {
                        if (GetWeaponAtSlotIndex(switchWeaponInput - 1) != null)
                            SwitchToWeaponIndex(switchWeaponInput - 1);
                    }
                }
            }
        }

        private void HasShoot()
        {
            if (activeWeapon != null && m_WeaponSwitchState == WeaponSwitchState.Up)
            {

                if (!activeWeapon.DisableAiming) CheckAim();

                if (m_PlayerInputHandler.reload && !activeWeapon.IsReloading && !activeWeapon.AutomaticReload && activeWeapon.CurrentAmmoRatio < 1.0f)
                {
                    IsAiming = false;
                    activeWeapon.StartReload();
                    return;
                }

                if (m_PlayerInputHandler.shoot)
                {
                    hasFired = activeWeapon.HandleShootInputs(MouseWorldPosition);

                    // Handle accumulating recoil
                    if (hasFired)
                    {
                        m_AccumulatedRecoil += Vector3.back * activeWeapon.RecoilForce;
                        m_AccumulatedRecoil = Vector3.ClampMagnitude(m_AccumulatedRecoil, MaxRecoilDistance);
                    }
                }
                else
                {
                    activeWeapon.FirstShoot = false;
                }
            }
        }
        
        public Vector3 GetLookCamera()
        {
            /*
            crosshairRay.origin = WeaponCamera.position;
            crosshairRay.direction = WeaponCamera.forward;

            if (Physics.Raycast(crosshairRay, out hitInfo))
            {
                m_CrosshairLookPosition = hitInfo.point;
            }
            else
            {
                m_CrosshairLookPosition = crosshairRay.origin + crosshairRay.direction * 1000.0f;
            }

            shootRay.origin = m_WeaponMuzzle.position;
            shootRay.direction = m_CrosshairLookPosition - m_WeaponMuzzle.position;

            if (Physics.Raycast(shootRay, out hitInfo, CastShootLayer))
            {
                MouseWorldPosition = hitInfo.point;
                Debug.DrawLine(shootRay.origin, hitInfo.point, Color.red, 1.0f);
            }
            return MouseWorldPosition;
            */

            Vector2 screenCenterPoint = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Ray ray = m_MainCamera.ScreenPointToRay(screenCenterPoint);
            ray = m_MainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit raycastHit, 999f, CastShootLayer))
            {
                IsPointingAtEnemy = IsPointingEnemy(raycastHit.collider);
                MouseWorldPosition = raycastHit.point;
            }
            else
            {
                IsPointingAtEnemy = false;
            }
            return MouseWorldPosition;
        }
        private bool IsPointingEnemy(Collider hit)
        {
            return !hit.CompareTag("Player") && hit.GetComponentInParent<Health>();
        }

        private void CheckAim()
        {
            // handle aiming down sights
            IsAiming = m_PlayerInputHandler.aim;
            if (IsAiming)
            {
                AimVirtualCamera.gameObject.SetActive(true);

                /*
                Vector3 worldAimTarget = mouseWorldPosition;
                worldAimTarget.y = _playerBody.position.y;
                Vector3 aimDirection = (worldAimTarget - _playerBody.position).normalized;

                _playerBody.forward = Vector3.Lerp(_playerBody.forward, aimDirection, Time.deltaTime * 20f);
                */
            }
            else
            {
                AimVirtualCamera.gameObject.SetActive(false);
            }
        }
        
        // Update various animated features in LateUpdate because it needs to override the animated arm position
        void LateUpdate()
        {
            if (ServerPause || MenuPause) return;

            //HasAim();

            //UpdateWeaponAiming();
            //UpdateWeaponBob();
            UpdateWeaponRecoil();
            UpdateWeaponSwitching();

            // Set final weapon socket position based on all the combined animation influences
            // WeaponParentSocket.localPosition = m_WeaponMainLocalPosition + m_WeaponBobLocalPosition + m_WeaponRecoilLocalPosition;

        }

        // Iterate on all weapon slots to find the next valid weapon to switch to
        public void SwitchWeapon(bool ascendingOrder)
        {
            int newWeaponIndex = -1;
            int closestSlotDistance = WeaponSlots.Length;
            for (int i = 0; i < WeaponSlots.Length; i++)
            {
                // If the weapon at this slot is valid, calculate its "distance" from the active slot index (either in ascending or descending order)
                // and select it if it's the closest distance yet
                if (i != ActiveWeaponIndex && GetWeaponAtSlotIndex(i) != null)
                {
                    int distanceToActiveIndex = GetDistanceBetweenWeaponSlots(ActiveWeaponIndex, i, ascendingOrder);

                    if (distanceToActiveIndex < closestSlotDistance)
                    {
                        closestSlotDistance = distanceToActiveIndex;
                        newWeaponIndex = i;
                    }
                }
            }

            // Handle switching to the new weapon index
            SwitchToWeaponIndex(newWeaponIndex);
        }

        // Switches to the given weapon index in weapon slots if the new index is a valid weapon that is different from our current one
        public void SwitchToWeaponIndex(int newWeaponIndex, bool force = false)
        {
            if (force || (newWeaponIndex != ActiveWeaponIndex && newWeaponIndex >= 0))
            {
                // Store data related to weapon switching animation
                m_WeaponSwitchNewWeaponIndex = newWeaponIndex;
                m_TimeStartedWeaponSwitch = Time.time;

                // Handle case of switching to a valid weapon for the first time (simply put it up without putting anything down first)
                if (activeWeapon == null)
                {
                    m_WeaponMainLocalPosition = m_WeaponPivot.localPosition;
                    m_WeaponSwitchState = WeaponSwitchState.PutUpNew;
                    ActiveWeaponIndex = m_WeaponSwitchNewWeaponIndex;

                    WeaponController newWeapon = GetWeaponAtSlotIndex(m_WeaponSwitchNewWeaponIndex);
                    if (OnSwitchedToWeapon != null)
                    {
                        OnSwitchedToWeapon.Invoke(newWeapon);
                    }
                }
                // otherwise, remember we are putting down our current weapon for switching to the next one
                else
                {
                    m_WeaponSwitchState = WeaponSwitchState.PutDownPrevious;
                }
            }
        }

        public WeaponController HasWeapon(WeaponController weaponPrefab)
        {
            if (weaponPrefab == null) return null;
            // Checks if we already have a weapon coming from the specified prefab
            for (var index = 0; index < WeaponSlots.Length; index++)
            {
                var w = WeaponSlots[index];
                if (w != null && w.WeaponName == weaponPrefab.WeaponName)
                {
                    return w;
                }
            }

            return null;
        }

        // Updates the weapon recoil animation
        void UpdateWeaponRecoil()
        {
            // if the accumulated recoil is further away from the current position, make the current position move towards the recoil target
            if (m_WeaponRecoilLocalPosition.z >= m_AccumulatedRecoil.z * 0.99f)
            {
                m_WeaponRecoilLocalPosition = Vector3.Lerp(m_WeaponRecoilLocalPosition, m_AccumulatedRecoil,
                    RecoilSharpness * Time.deltaTime);
            }
            // otherwise, move recoil position to make it recover towards its resting pose
            else
            {
                m_WeaponRecoilLocalPosition = Vector3.Lerp(m_WeaponRecoilLocalPosition, Vector3.zero,
                    RecoilRestitutionSharpness * Time.deltaTime);
                m_AccumulatedRecoil = m_WeaponRecoilLocalPosition;
            }
        }

        // Updates the animated transition of switching weapons
        void UpdateWeaponSwitching()
        {
            // Calculate the time ratio (0 to 1) since weapon switch was triggered
            float switchingTimeFactor = 0f;
            if (WeaponSwitchDelay == 0f)
            {
                switchingTimeFactor = 1f;
            }
            else
            {
                switchingTimeFactor = Mathf.Clamp01((Time.time - m_TimeStartedWeaponSwitch) / WeaponSwitchDelay);
            }

            // Handle transiting to new switch state
            if (switchingTimeFactor >= 1f)
            {
                if (m_WeaponSwitchState == WeaponSwitchState.PutDownPrevious)
                {
                    // Deactivate old weapon
                    WeaponController oldWeapon = GetWeaponAtSlotIndex(ActiveWeaponIndex);
                    if (oldWeapon != null)
                    {
                        oldWeapon.ShowWeapon(false);
                    }

                    ActiveWeaponIndex = m_WeaponSwitchNewWeaponIndex;
                    switchingTimeFactor = 0f;

                    // Activate new weapon
                    WeaponController newWeapon = GetWeaponAtSlotIndex(ActiveWeaponIndex);
                    if (OnSwitchedToWeapon != null)
                    {
                        OnSwitchedToWeapon.Invoke(newWeapon);
                    }

                    if (newWeapon)
                    {
                        m_TimeStartedWeaponSwitch = Time.time;
                        m_WeaponSwitchState = WeaponSwitchState.PutUpNew;
                    }
                    else
                    {
                        // if new weapon is null, don't follow through with putting weapon back up
                        m_WeaponSwitchState = WeaponSwitchState.Down;
                    }
                }
                else if (m_WeaponSwitchState == WeaponSwitchState.PutUpNew)
                {
                    m_WeaponSwitchState = WeaponSwitchState.Up;
                }
            }

            // Handle moving the weapon socket position for the animated weapon switching
            if (m_WeaponSwitchState == WeaponSwitchState.PutDownPrevious)
            {
                m_WeaponMainLocalPosition = Vector3.Lerp(m_WeaponPivot.localPosition,
                    m_WeaponPivot.localPosition, switchingTimeFactor);
            }
            else if (m_WeaponSwitchState == WeaponSwitchState.PutUpNew)
            {
                m_WeaponMainLocalPosition = Vector3.Lerp(m_WeaponPivot.localPosition,
                    m_WeaponPivot.localPosition, switchingTimeFactor);
            }
        }

        private int weaponSlot = 0;

        public bool AddWeapon(string weaponName)
        {
            for (int i = 0; i < WeaponsList.Length; i++)
            {
                // if we already hold this weapon type (a weapon coming from the same source prefab), don't add the weapon
                if (WeaponsList[i].WeaponName == weaponName && HasWeapon(WeaponsList[i]) != null) return false;
            }

            // search our weapon slots for the first free one, assign the weapon to it, and return true if we found one. Return false otherwise
            for (int i = 0; i < WeaponSlots.Length; i++)
            {
                // only add the weapon if the slot is free
                if (WeaponSlots[i] == null)
                {
                    weaponSlot = i;
                    m_PlayerController.AddPlayerWeapon(weaponName);
                    return true;
                }
            }

            // Handle auto-switching to weapon if no weapons currently
            if (activeWeapon == null)
            {
                SwitchWeapon(true);
            }

            return false;
        }

        // Adds a weapon to our inventory
        public bool SetWeapon(GameObject weaponObject)
        {
            // spawn the weapon prefab as child of the weapon socket
            WeaponController weaponInstance = weaponObject.GetComponent<WeaponController>();

            /*List<Renderer> weaponRenderer = weaponInstance.WeaponRenderer;
            for (int ii = 0; ii < weaponRenderer.Count; ii++)
            {
                weaponRenderer[ii].enabled = false;
            }*/

            weaponInstance.SourcePrefab = weaponInstance.gameObject;

            weaponInstance.transform.parent = m_WeaponPivot;
            weaponInstance.transform.localPosition = Vector3.zero;
            weaponInstance.transform.localRotation = Quaternion.identity;
            weaponInstance.transform.localScale = new Vector3(1, 1, 1);

            // Set owner to this gameObject so the weapon can alter projectile/damage logic accordingly
            weaponInstance.Owner = m_PlayerBody;

            weaponInstance.SetOptions(weaponSlot, m_PlayerController);

            weaponInstance.ShowWeapon(false);

            WeaponSlots[weaponSlot] = weaponInstance;

            if (OnAddedWeapon != null)
            {
                OnAddedWeapon.Invoke(weaponInstance, weaponSlot);
            }

            SwitchToWeaponIndex(weaponSlot);

            return true;

        }

        public bool SetWeaponRenderers(bool state)
        {
            for (int i = 0; i < WeaponSlots.Length; i++)
            {
                if (WeaponSlots[i] == null) continue;
                for (int ii = 0; ii < WeaponSlots[i].WeaponRenderer.Count; ii++)
                {
                    WeaponSlots[i].WeaponRenderer[ii].enabled = state;
                }
            }
            return true;
        }

        public bool RemoveWeapon(WeaponController weaponInstance)
        {

            // Look through our slots for that weapon
            for (int i = 0; i < WeaponSlots.Length; i++)
            {
                // when weapon found, remove it
                if (WeaponSlots[i] && WeaponSlots[i].WeaponName == weaponInstance.WeaponName)
                {
                    WeaponController RemovedWeapon = WeaponSlots[i];
                    WeaponSlots[i] = null;

                    if (OnRemovedWeapon != null)
                    {
                        OnRemovedWeapon.Invoke(RemovedWeapon, i);
                    }

                    Destroy(RemovedWeapon.gameObject);

                    // Handle case of removing active weapon (switch to next weapon)
                    if (i == ActiveWeaponIndex)
                    {
                        SwitchWeapon(true);
                    }

                    return true;
                }
            }
            if (WeaponSlots.All(w => w == null))
            {
                Debug.Log("DefaultWeaponClip");
                //rigAnimator
                m_PlayerController.Animator.Play(m_LoadManager.DefaultWeaponClip.name);
            }

            return false;
        }

        public WeaponController GetWeaponAtSlotIndex(int index)
        {
            // find the active weapon in our weapon slots based on our active weapon index
            if (index >= 0 &&
                index < WeaponSlots.Length)
            {
                return WeaponSlots[index];
            }

            // if we didn't find a valid active weapon in our weapon slots, return null
            return null;
        }

        // Calculates the "distance" between two weapon slot indexes
        // For example: if we had 5 weapon slots, the distance between slots #2 and #4 would be 2 in ascending order, and 3 in descending order
        int GetDistanceBetweenWeaponSlots(int fromSlotIndex, int toSlotIndex, bool ascendingOrder)
        {
            int distanceBetweenSlots = 0;

            if (ascendingOrder)
            {
                distanceBetweenSlots = toSlotIndex - fromSlotIndex;
            }
            else
            {
                distanceBetweenSlots = -1 * (toSlotIndex - fromSlotIndex);
            }

            if (distanceBetweenSlots < 0)
            {
                distanceBetweenSlots = WeaponSlots.Length + distanceBetweenSlots;
            }

            return distanceBetweenSlots;
        }
        /*
        private void OnJump(bool newState)
        {
            GetActiveWeapon().ShowWeapon(!newState);
        }*/

        private void OnWeaponSwitched(WeaponController newWeapon)
        {
            if (newWeapon != null)
            {
                activeWeapon = WeaponSlots[ActiveWeaponIndex];
                m_PlayerController.Animator.Play("Equep_" + newWeapon.WeaponAnimation.name);
                newWeapon.ShowWeapon(true);
                m_WeaponMuzzle = activeWeapon.WeaponGunMuzzle;
                //m_RightHandGrip.position = newWeapon.RightHandGrip.position;
                //m_LeftHandGrip.position = newWeapon.LeftHandGrip.position;
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Log")]
        void SaveWeaponPose()
        {
            Debug.Log("You serious?");
        }
#endif
    }
}