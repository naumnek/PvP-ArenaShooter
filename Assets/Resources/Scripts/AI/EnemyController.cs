using System;
using Platinum.Settings;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;
using UnityEngine.Events;
using Random = UnityEngine.Random;

namespace Unity.FPS.AI
{
    [RequireComponent(typeof(Health), typeof(Actor))]
    public class EnemyController : MonoBehaviour
    {
        public enum WeaponSwitchState
        {
            Up,
            Down,
            PutDownPrevious,
            PutUpNew,
        }
        [Header("General")]
        public bool DisableCustomization;
        public bool DisplayHealthBar;

        [Header("Info")]
        [Tooltip("Strenght the enemy")]
        public Transform SkeletonHips;
        public Collider[] RagdollColliders;
        public Rigidbody[] RagdollRigidbodys;
        public SkinnedMeshRenderer CharacterSkinned;
        public GameObject HealthBarPivot;
        public Transform WeaponPivot;

        [Header("Die VFX Effect")]
        [Tooltip("The VFX prefab spawned when the enemy dies")]
        public float delayStartDieEffect = 3f;
        public Material DissolveMaterial;
        public ParticleSystem VFXParticleEnergyExplosion;
        public ParticleSystem VFXParticleFlakes;
        public float spawnEffectTime = 2;
        public float pause = 1;
        public AnimationCurve fadeIn;

        [Header("Parameters")]
        [Tooltip("Height at which the enemy dies instantly when falling off the map")]
        public float KillHeight = -50f;

        [Tooltip("The distance at which the enemy considers that it has reached its current path destination point")]
        public float PathReachingRadius = 2f;

        [Header("Weapons Parameters")]
        [Tooltip("Allow weapon swapping for this enemy")]
        public bool SwapToNextWeapon = false;

        [Tooltip("Time delay between a weapon swap and the next attack")]
        public float DelayAfterWeaponSwap = 0f;

        [Header("Sounds")]
        [Tooltip("Sound played when recieving damages")]
        public AudioClip DamageTick;

        private GameObject LootPrefab;
        private float DropRate = 1f;

        private Transform BotSpawnpoint;

        public UnityAction onAttack;
        public UnityAction<Actor> onDetectedTarget;
        public UnityAction onLostTarget;
        public UnityAction onDamaged;
        public UnityAction OnInitialize;
        public UnityAction OnSpawn;
        public UnityAction onDie;

        private Vector3 defaultHipsPosition;
        private float timer = 0;
        private int shaderProperty;
        private bool updateDieEffect;

        float m_LastTimeDamaged = float.NegativeInfinity;

        public PatrolPath CurrentPatrolPath { get; set; }
        public PatrolPath[] PatrolPaths { get; set; }
        public DetectionModule DetectionModule { get; private set; }
        public int ActiveWeaponIndex { get; private set; }

        public UnityAction<WeaponController> OnSwitchedToWeapon;
        public UnityAction<WeaponController, int> OnAddedWeapon;
        public UnityAction<WeaponController, int> OnRemovedWeapon;
        public bool hasFired { get; private set; } = false;

        Vector3 m_LastCharacterPosition;
        Vector3 m_WeaponMainLocalPosition;
        Vector3 m_WeaponRecoilLocalPosition;
        Vector3 m_AccumulatedRecoil;
        float m_TimeStartedWeaponSwitch;
        WeaponSwitchState m_WeaponSwitchState;
        int m_WeaponSwitchNewWeaponIndex;

        private Transform TransformSpawnpoint;
        public Vector3 mouseWorldPosition { get; private set; } = Vector3.zero;

        public Actor Actor { get; private set; }
        public Animator Animator { get; private set; }
        public ActorsManager ActorsManager { get; private set; }
        GameFlowManager m_GameFlowManager;
        public Health Health { get; private set; }
        public SettingsManager SettingsManager { get; private set; }
        public EnemyMobile EnemyMobile { get; private set; }
        public AudioSource AudioSource { get; private set; }

        private WeaponController[] WeaponsList;
        private Vector3 lookPositionTarget;
        private int PatrolPathIndex;

        private NavMeshAgent m_NavMeshAgent;
        int m_PathDestinationNodeIndex;
        Collider[] m_SelfColliders;
        bool m_WasDamagedThisFrame;
        float m_LastTimeWeaponSwapped = Mathf.NegativeInfinity;
        public WeaponController activeWeapon { get; private set; }
        private WeaponController[] m_Weapons;
        public WeaponController[] WeaponSlots { get; private set; } = new WeaponController[9]; // 9 available weapon slots
        public WeaponController CurrentWeapon { get; private set; }
        NavigationModule m_NavigationModule;

        public LoadManager LoadManager { get; private set; }
        public CharacterController CharacterController { get; private set; }

        public bool ServerPause { get; private set; } = true;
        public bool controllable { get; private set; } = false;
        bool m_WasDead = true;

        public MatchSaves MatchSettings { get; private set; }

        private void Awake()
        {
            EventManager.AddListener<GamePauseEvent>(OnGamePauseEvent);
            EventManager.AddListener<PlayerDeathEvent>(OnPlayerDeathEvent);
            
            shaderProperty = Shader.PropertyToID("_cutoff");
            defaultHipsPosition = SkeletonHips.position;
            
            for (int i = 0; i < RagdollRigidbodys.Length; i++)
            {
                RagdollRigidbodys[i].isKinematic = true;
            }

            HealthBarPivot.SetActive(DisplayHealthBar);
            var main = VFXParticleFlakes.main;
            main.duration = spawnEffectTime;

            Health = GetComponent<Health>();
            Actor = GetComponent<Actor>();
            m_SelfColliders = GetComponentsInChildren<Collider>();
            EnemyMobile = GetComponent<EnemyMobile>();
            Animator = GetComponent<Animator>();
            AudioSource = GetComponent<AudioSource>();
            CharacterController = GetComponent<CharacterController>();
            DetectionModule = GetComponent<DetectionModule>();
            
            DetectionModule.onDetectedTarget += OnDetectedTarget;
            DetectionModule.onLostTarget += OnLostTarget;
            onAttack += DetectionModule.OnAttack;
            OnSwitchedToWeapon += OnWeaponSwitched;
        }
        
        private void OnDestroy()
        {
            EventManager.RemoveListener<GamePauseEvent>(OnGamePauseEvent);
            EventManager.RemoveListener<PlayerDeathEvent>(OnPlayerDeathEvent);
        }
        public void SetSpawnpoint(Transform spawnpoint)
        {
            BotSpawnpoint = spawnpoint;
        }

        public void SetDrop(GameObject prefab, float rate)
        {
            LootPrefab = prefab;
            DropRate = rate;
        }

        public void Activation(LoadManager loadManager)
        {
            if (LoadManager) return;
            LoadManager = loadManager;
            
            RigBuilder rig = GetComponent<RigBuilder>();
            rig.enabled = false;
            rig.enabled = true;

            m_GameFlowManager = LoadManager.GameFlowManager;
            ActorsManager = LoadManager.ActorsManager;
            SettingsManager = LoadManager.SettingsManager;

            // Subscribe to damage & death actions
            Health.OnDie += OnDie;
            Health.OnDamaged += OnDamaged;
            // Initialize detection module
            
            SetBotSettings();
            StartCoroutine(IsKillHeightPlayer());
            
            Debug.Log("Activate Bot: " + Actor.NickName);
            OnInitialize?.Invoke();
        }

        private void OnPlayerDeathEvent(PlayerDeathEvent evt)
        {
            if (!evt.Die) return;

            //controllable = false;
        }

        private void OnGamePauseEvent(GamePauseEvent evt)
        {
            ServerPause = evt.ServerPause;
        }

        private bool waitCheck = false;
        private IEnumerator WaitForCheck(float time)
        {
            waitCheck = true;
            yield return new WaitForSeconds(time);
            Debug.Log("Live-LookPositionTarget: " + lookPositionTarget);
            waitCheck = false;
        }

        void Update()
        {
            if (updateDieEffect && !VFXParticleEnergyExplosion.IsAlive()) DieEffect();

            if (ServerPause && !controllable) return;

            DetectionModule.HandleTargetDetection(Actor, m_SelfColliders);

            m_WasDamagedThisFrame = false;
        }

        public void SetSpawn(Transform spawnpoint)
        {
            TransformSpawnpoint = spawnpoint;
        }

        #region COROUTINES

        private IEnumerator WaitForRespawn()
        {
            controllable = false;

            HealthBarPivot.SetActive(false);

            //CharacterSkinned.enabled = false;

            yield return new WaitForSeconds(0.1f);

            VFXParticleEnergyExplosion.gameObject.GetComponent<AudioSource>().Play();
            VFXParticleEnergyExplosion.Play();
            CharacterSkinned.material = DissolveMaterial;

            if (activeWeapon != null) activeWeapon.SetRigidbody(true);

            Animator.enabled = false;
            CharacterController.enabled = false;
            for (int i = 0; i < RagdollRigidbodys.Length; i++)
            {
                RagdollRigidbodys[i].isKinematic = false;
            }

            yield return new WaitForSeconds(delayStartDieEffect);

            updateDieEffect = true;
        }

        private IEnumerator WaitForDisableInvulnerable()
        {
            yield return new WaitForSeconds(GameSettings.PLAYER_INVULNERABLE_TIME);
            Health.DisableInvulnerable();
        }

        #endregion

        private void SetBotSettings()
        {
            if (LoadManager == null)
            {
                LoadManager = LoadManager.Instance;
                SettingsManager = LoadManager.SettingsManager;
            }
            MatchSettings = SettingsManager.CurrentMatchSaves;
            if (!DisableCustomization)
            {
                CharacterSkinned.sharedMesh = SettingsManager.Customization.GetRandomModel();

                Material[] skins = SettingsManager.Customization.GetRandomSkin().Materials;
                CharacterSkinned.materials = skins;
            }

            ActiveWeaponIndex = -1;
            m_WeaponSwitchState = WeaponSwitchState.Down;

            WeaponsList = SettingsManager.GetRequredWeapons();
            //int index = Random.Range(0, WeaponsList.Count());

            AddWeapon(SettingsManager.GetPublicRandomWeapon().WeaponName);
            SwitchWeapon(true);

            ResetPathDestination();

            if (PatrolPaths != null)
            {
                PatrolPathIndex++;
                if (PatrolPathIndex > PatrolPaths.Length - 1) PatrolPathIndex = 0;
                CurrentPatrolPath = PatrolPaths[Random.Range(0, PatrolPaths.Length)];
            }

            if (ActorsManager.PlayerActors.Count > 0)
                controllable = !MatchSettings.PeacifulMode;
        }

        private void DieEffect()
        {
            if (timer == 0)
            {
                VFXParticleFlakes.Play();
            }
            if (timer < spawnEffectTime + pause)
            {
                timer += Time.deltaTime;
            }
            else
            {
                timer = 0;
                updateDieEffect = false;
                CharacterSkinned.gameObject.SetActive(false);

                DieEvent ke = Events.DieEvent;
                ke.Actor = Actor;
                EventManager.Broadcast(ke);
            }

            CharacterSkinned.material.SetFloat(shaderProperty, fadeIn.Evaluate(Mathf.InverseLerp(0, spawnEffectTime, timer)));
        }

        public void RespawnBot()
        {
            controllable = false;

            transform.position = TransformSpawnpoint.position;
            transform.rotation = TransformSpawnpoint.rotation;

            for (int i = 0; i < RagdollRigidbodys.Length; i++)
            {
                RagdollRigidbodys[i].isKinematic = true;
            }
            SkeletonHips.position = defaultHipsPosition;
            CharacterSkinned.gameObject.SetActive(true);
            CharacterController.enabled = true;
            Animator.enabled = true;
            
            HealthBarPivot.SetActive(DisplayHealthBar);

            for (int i = 0; i < WeaponSlots.Length; i++)
            {
                RemoveWeapon(WeaponSlots[i]);
            }

            Health.Resurrection();
            StartCoroutine(WaitForDisableInvulnerable());
            StartCoroutine(IsKillHeightPlayer());

            SetBotSettings();

            OnSpawn?.Invoke();
        }

        private IEnumerator IsKillHeightPlayer()
        {
            while (!Health.IsDead)
            {
                // check for Y kill
                if (transform.position.y < KillHeight)
                {
                    Health.Kill();
                    break;
                }
                yield return new WaitForSeconds(0.25f);
            }
        }

        void OnLostTarget()
        {
            onLostTarget?.Invoke();
        }

        void OnDetectedTarget(Actor target)
        {
            //onDetectedTarget.Invoke(); don't work...
            onDetectedTarget?.Invoke(target);
        }
        bool IsPathValid()
        {
            return CurrentPatrolPath && CurrentPatrolPath.PathNodes.Count > 0;
        }

        public void ResetPathDestination()
        {
            m_PathDestinationNodeIndex = 0;
        }

        public void SetPathDestinationToClosestNode()
        {
            if (IsPathValid())
            {
                int closestPathNodeIndex = 0;
                for (int i = 0; i < CurrentPatrolPath.PathNodes.Count; i++)
                {
                    float distanceToPathNode = CurrentPatrolPath.GetDistanceToNode(transform.position, i);
                    if (distanceToPathNode < CurrentPatrolPath.GetDistanceToNode(transform.position, closestPathNodeIndex))
                    {
                        closestPathNodeIndex = i;
                    }
                }

                m_PathDestinationNodeIndex = closestPathNodeIndex;
            }
            else
            {
                m_PathDestinationNodeIndex = 0;
            }
        }

        public Vector3 GetDestinationOnPath()
        {
            if (IsPathValid())
            {
                return CurrentPatrolPath.GetPositionOfPathNode(m_PathDestinationNodeIndex);
            }
            else
            {
                return Vector3.zero;
            }
        }

        private float m_AnimationBlend;

        public void UpdatePathDestination(bool inverseOrder = false)
        {
            if (IsPathValid())
            {
                // Check if reached the path destination
                if (Vector3.Distance(transform.position, GetDestinationOnPath()) <= PathReachingRadius)
                {
                    // increment path destination index
                    m_PathDestinationNodeIndex =
                        inverseOrder ? (m_PathDestinationNodeIndex - 1) : (m_PathDestinationNodeIndex + 1);
                    if (m_PathDestinationNodeIndex < 0)
                    {
                        m_PathDestinationNodeIndex += CurrentPatrolPath.PathNodes.Count;
                    }

                    if (m_PathDestinationNodeIndex >= CurrentPatrolPath.PathNodes.Count)
                    {
                        m_PathDestinationNodeIndex -= CurrentPatrolPath.PathNodes.Count;
                    }
                }
            }
        }

        void OnDamaged(float damage, GameObject damageSource)
        {
            // test if the damage source is the player
            if (damageSource && damageSource.GetComponent<Health>())
            {
                // pursue the player
                DetectionModule.OnDamaged(damageSource);

                onDamaged?.Invoke();
                m_LastTimeDamaged = Time.time;

                // play the damage tick sound
                if (DamageTick && !m_WasDamagedThisFrame)
                    AudioSource.PlayOneShot(DamageTick);

                m_WasDamagedThisFrame = true;
            }
        }


        void OnDie()
        {
            onDie?.Invoke();
            // spawn a particle system when dying
            //var vfx = Instantiate(DeathVfx, DeathVfxSpawnPoint.position, Quaternion.identity);
            //Destroy(vfx, 5f);
            // tells the game flow manager to handle the enemy destuction
            //m_EnemyManager.UnregisterEnemy(this);
            // loot an object
            if (TryDropItem())
            {
                //Instantiate(LootPrefab, transform.position, Quaternion.identity);
            }
            StartCoroutine(WaitForRespawn());
        }

        public void OrientWeaponsTowards(Vector3 lookPosition)
        {
            // orient weapon towards player
            Vector3 weaponForward = (lookPosition - CurrentWeapon.WeaponRoot.transform.position).normalized;
            CurrentWeapon.transform.forward = weaponForward;
        }
        public void OnEnemyShoot(int weaponIndex, Vector3 targetWorldPosition)
        {
            EnemyFire(weaponIndex, targetWorldPosition);
        }

        public void EnemyFire(int weaponIndex, Vector3 targetWorldPosition)
        {
            CurrentWeapon.HandleShoot(targetWorldPosition);
        }

        public bool TryAtack(Vector3 enemyPosition)
        {
            if (m_GameFlowManager.GameIsEnding || enemyPosition == Vector3.zero)
                return false;

            //OrientWeaponsTowards(enemyPosition);

            if ((m_LastTimeWeaponSwapped + DelayAfterWeaponSwap) >= Time.time)
                return false;

            // Shoot the weapon
            bool didFire = activeWeapon.HandleShootInputs(enemyPosition);

            if (didFire && onAttack != null)
            {
                onAttack.Invoke();
            }

            return didFire;
        }

        public bool TryDropItem()
        {
            if (DropRate == 0 || LootPrefab == null)
                return false;
            else if (DropRate == 1)
                return true;
            else
                return (Random.value <= DropRate);
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
                if (activeWeapon != null) Debug.Log("SwitchToWeaponIndex: 0 | " + activeWeapon.name);
                // Store data related to weapon switching animation
                m_WeaponSwitchNewWeaponIndex = newWeaponIndex;
                m_TimeStartedWeaponSwitch = Time.time;

                // Handle case of switching to a valid weapon for the first time (simply put it up without putting anything down first)
                if (activeWeapon == null)
                {
                    m_WeaponMainLocalPosition = WeaponPivot.localPosition;
                    m_WeaponSwitchState = WeaponSwitchState.PutUpNew;
                    ActiveWeaponIndex = m_WeaponSwitchNewWeaponIndex;

                    if (SwapToNextWeapon)
                    {
                        m_LastTimeWeaponSwapped = Time.time;
                    }
                    else
                    {
                        m_LastTimeWeaponSwapped = Mathf.NegativeInfinity;
                    }

                    WeaponController newWeapon = GetWeaponAtSlotIndex(m_WeaponSwitchNewWeaponIndex);
                    CurrentWeapon = newWeapon;
                    if (OnSwitchedToWeapon != null)
                    {
                        OnSwitchedToWeapon?.Invoke(newWeapon);
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

        // Adds a weapon to our inventory
        public bool AddWeapon(string weaponName)
        {
            for (int i = 0; i < WeaponsList.Length; i++)
            {
                // if we already hold this weapon type (a weapon coming from the same source prefab), don't add the weapon
                if (WeaponsList[i].name == weaponName && HasWeapon(WeaponsList[i]) != null)
                {
                    Debug.Log("Bot: detect duplicate weapon " + weaponName);
                    return false;
                }
            }

            // search our weapon slots for the first free one, assign the weapon to it, and return true if we found one. Return false otherwise
            for (int i = 0; i < WeaponSlots.Length; i++)
            {
                // only add the weapon if the slot is free
                if (WeaponSlots[i] == null)
                {
                    // spawn the weapon prefab as child of the weapon socket
                    GameObject weapon = SettingsManager.GetRequredWeapons().
                        Where(w => w.WeaponName == weaponName).FirstOrDefault().gameObject;

                    GameObject weaponObject = Instantiate(weapon, WeaponPivot.position, WeaponPivot.rotation);
                    WeaponController weaponInstance = weaponObject.GetComponent<WeaponController>();

                    weaponInstance.SourcePrefab = weaponInstance.gameObject;

                    weaponInstance.transform.SetParent(WeaponPivot);
                    weaponInstance.transform.localPosition = Vector3.zero;
                    weaponInstance.transform.localRotation = Quaternion.identity;
                    weaponInstance.transform.localScale = new Vector3(1, 1, 1);

                    // Set owner to this gameObject so the weapon can alter projectile/damage logic accordingly
                    weaponInstance.Owner = transform;

                    weaponInstance.SetOptions(i, this);
                    weaponInstance.AutomaticReload = true;
                    weaponInstance.InfinityAmmo = true;

                    weaponInstance.ShowWeapon(false);

                    WeaponSlots[i] = weaponInstance;

                    if (OnAddedWeapon != null)
                    {
                        OnAddedWeapon.Invoke(weaponInstance, i);
                    }

                    SwitchToWeaponIndex(i);

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

        public bool RemoveWeapon(WeaponController weaponInstance)
        {
            // Look through our slots for that weapon
            for (int i = 0; i < WeaponSlots.Length; i++)
            {
                // when weapon found, remove it
                if (WeaponSlots[i] && WeaponSlots[i] == weaponInstance)
                {
                    if (activeWeapon = WeaponSlots[i]) activeWeapon = null;
                    WeaponSlots[i] = null;

                    if (OnRemovedWeapon != null)
                    {
                        OnRemovedWeapon.Invoke(weaponInstance, i);
                    }

                    Destroy(weaponInstance.gameObject);

                    // Handle case of removing active weapon (switch to next weapon)
                    if (i == ActiveWeaponIndex)
                    {
                        SwitchWeapon(true);
                    }

                    return true;
                }
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

        public void SetPatrolPath(PatrolPath[] patrols, int PatrolPathIndex)
        {
            PatrolPaths = patrols;
            PatrolPaths[PatrolPathIndex].EnemiesToAssign.Add(this);
            CurrentPatrolPath = PatrolPaths[PatrolPathIndex];
        }
        void OnWeaponSwitched(WeaponController newWeapon)
        {
            if (newWeapon != null)
            {
                activeWeapon = WeaponSlots[ActiveWeaponIndex];

                Animator.Play("Equep_" + newWeapon.WeaponAnimation.name);

                newWeapon.ShowWeapon(true);
            }
        }
    }
}