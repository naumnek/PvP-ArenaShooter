using Platinum.Settings;
using System.Collections;
using System.Linq;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Events;

namespace Platinum.Player
{
    public class PlayerController : MonoBehaviour
    {
        [Header("General Settings")]
        public bool DisableCustomization;
        public bool DisableWeapon;

        [Header("Physics")]
        public float returnForce = 2.0f;
        public float pushPower = 2.0f;

        [Header("Fall")]
        public float gravity = 20f;
        public float delayFallAnimation = 0.5f;

        [Header("Jump")]
        public float delayObstacleCheck = 0.2f;
        public float intervalJumps = 1.25f;
        public float jumpHeight = 2f;
        public float airControl = 2.5f;
        public float jumpDump = 0.5f;

        [Header("Movement")]
        public float stepDown = 0.1f;
        public float groundSpeed = 1.2f;
        public float lateralSpeedMultiplier = 1.4f;
        public float sprintSpeedMultiplier = 1.4f;
        public float crouchSpeedMultiplier = 0.8f;
        public float animationSpeedMultiplier = 0.7f;

        [Header("Audio")]
        public float audioFootstepSpeedMultiplier = 2f;
        public AudioClip FootstepSfx;
        public AudioClip JumpSfx;
        public AudioClip LandSfx;
        public AudioClip FallDamageSfx;

        [Header("General References")]
        public Transform SkeletonHips;
        public SkinnedMeshRenderer CharacterSkinned;
        public Collider[] RagdollColliders;
        public Rigidbody[] RagdollRigidbodys;
        public WeaponController[] StartingWeapons;
        public Transform AimPoint;

        [Header("Die Effect")]
        [Tooltip("The VFX prefab spawned when the enemy dies")]
        public float delayStartDieEffect = 3f;
        public Material DissolveMaterial;
        public ParticleSystem VFXParticleEnergyExplosion;
        public ParticleSystem VFXParticleFlakes;
        public float spawnEffectTime = 2;
        public float pause = 1;
        public AnimationCurve fadeIn;

        [Header("Sounds")]
        [Tooltip("Sound played when recieving damages")]
        public AudioClip DamageTick;

        [Header("VFX")]
        [Tooltip("The VFX prefab spawned when the enemy dies")]
        public GameObject DeathVfx;

        [Tooltip("The point at which the death VFX is spawned")]
        public Transform DeathVfxSpawnPoint;

        [Header("Settings")]
        [Tooltip("Height at which the player dies instantly when falling off the map")]
        public float KillHeight = -50f;
        [Tooltip("Parent transform where all weapon will be added in the hierarchy")]
        public Transform WeaponPivot;
        public Transform RightHandGrip;
        public Transform LeftHandGrip;
        public Actor Actor { get; private set; }
        public Health Health { get; private set; }
        public Damageable Damageable { get; private set; }
        public CharacterController CharacterController { get; private set; }
        public AudioSource AudioSource { get; private set; }
        public CapsuleCollider CharacterCollider { get; private set; }
        public Transform Body { get; private set; }
        public LoadManager LoadManager { get; private set; }

        public bool controllable;

        public Transform Spawnpoint { get; private set; }
        public Animator Animator { get; private set; }

        private WeaponController[] m_Weapons;

        private CustomizationInfo Customization;
        private ThirdPersonController ThirdPersonController;
        private PlayerWeaponsManager PlayerWeaponsManager;
        private SettingsManager SettingsManager;
        private PlayerInputHandler m_PlayerInputHandler;
        private ActorsManager m_ActorsManager;

        public UnityAction OnSpawn;
        public UnityAction OnInitialize;

        private bool waitJump;
        private float currentGroundSpeed;
        private Vector3 rootMotion;
        private Vector3 velocity;
        private bool isJumping;
        private Vector2 input;
        private float m_FootstepDistanceCounter;
        private bool isHits;
        private Vector3 lastHitDirection;
        private bool m_WasDamagedThisFrame;
        private PlayerSaves m_PlayerSaves;

        private Vector3 defaultHipsPosition;
        private float timer;
        private int shaderProperty;
        private bool updateDieEffect;
        private bool ServerPause = true;

        public void SetSpawn(Transform spawnpoint)
        {
            Spawnpoint = spawnpoint;
        }
        private void OnGamePauseEvent(GamePauseEvent evt)
        {
            ServerPause = evt.ServerPause;
        }
        private void OnRefreshMatchEvent(RefreshMatchEvent evt)
        {

        }

        private void OnDestroy()
        {
            EventManager.RemoveListener<GamePauseEvent>(OnGamePauseEvent);
            EventManager.RemoveListener<RefreshMatchEvent>(OnRefreshMatchEvent);
        }

        // Start is called before the first frame update
        private void Awake()
        {
            EventManager.AddListener<GamePauseEvent>(OnGamePauseEvent);
            EventManager.AddListener<RefreshMatchEvent>(OnRefreshMatchEvent);

            shaderProperty = Shader.PropertyToID("_cutoff");
            defaultHipsPosition = SkeletonHips.position;

            for (int i = 0; i < RagdollRigidbodys.Length; i++)
            {
                RagdollRigidbodys[i].isKinematic = true;
            }

            var main = VFXParticleFlakes.main;
            main.duration = spawnEffectTime;

            Body = transform;
            currentGroundSpeed = groundSpeed;
            waitJump = true;

            Actor = GetComponent<Actor>();
            Health = GetComponent<Health>();
            Damageable = GetComponent<Damageable>();
            CharacterController = GetComponent<CharacterController>();
            Animator = GetComponent<Animator>();
            AudioSource = GetComponent<AudioSource>();
            CharacterCollider = GetComponent<CapsuleCollider>();

            Health.OnDie += OnDie;
            Health.OnDamaged += OnDamaged;
        }

        public void Activation(LoadManager loadManager)
        {
            if (LoadManager) return;
            LoadManager = loadManager;

            ThirdPersonController = LoadManager.ThirdPersonController;
            PlayerWeaponsManager = LoadManager.PlayerWeaponsManager;
            SettingsManager = LoadManager.SettingsManager;
            m_ActorsManager = LoadManager.ActorsManager;
            Customization = SettingsManager.Customization;
            m_PlayerInputHandler = LoadManager.PlayerInputHandler;
            m_PlayerSaves = SettingsManager.CurrentPlayerSaves;

            SetPlayerSettings(Customization.GetCurrentIndexModel(), m_PlayerSaves.Skin, Customization.Type);
            StartCoroutine(IsKillHeightPlayer());
            
            controllable = true;
            ServerPause = false;
                
            OnInitialize?.Invoke();
            Debug.Log("New Player: " + Actor.NickName);
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            lastHitDirection = hit.moveDirection;
            Rigidbody body = hit.collider.attachedRigidbody;

            // no rigidbody
            if (body == null || body.isKinematic)
                if (isJumping && !isHits && obstacleCheck)
                {
                    isHits = true;
                    ReturnHitImpulsePlayer(lastHitDirection);
                }
            return;

            // We dont want to push objects below us
            if (hit.moveDirection.y < -0.3f)
                return;

            // Calculate push direction from move direction,
            // we only push objects to the sides never up and down
            Vector3 pushDir = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);

            // If you know how fast your character is trying to move,
            // then you can also multiply the push velocity by that.

            // Apply the push
            body.velocity = pushDir * pushPower;
        }

        private void ReturnHitImpulsePlayer(Vector3 moveDirection)
        {
            Vector3 pushDirection = new Vector3(moveDirection.x, 0, moveDirection.z);
            velocity = pushDirection * returnForce;
        }

        private void OnAnimatorMove()
        {
            rootMotion += Animator.deltaPosition;
        }

        private void Update()
        {
            if (updateDieEffect && !VFXParticleEnergyExplosion.IsAlive()) DieEffect();

            AnimationMoveCharacter();
            UpdateSpeed();
            AudioFootstep();

            if (!controllable || ServerPause)
            {
                input = Vector2.zero;
                return;
            }

            input = m_PlayerInputHandler.move;
            Animator.SetBool("isCrouching", m_PlayerInputHandler.crouch);

            bool jump = (m_PlayerInputHandler.jump && waitJump && !isJumping);
            if (jump) StartCoroutine(Jump());
            m_WasDamagedThisFrame = false;
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

        private void UpdateSpeed()
        {
            float requiredSpeed;
            switch (m_PlayerInputHandler.MoveState)
            {
                case MoveState.Sprint:
                    requiredSpeed = groundSpeed * sprintSpeedMultiplier;
                    break;
                case MoveState.Crouch:
                    requiredSpeed = groundSpeed * crouchSpeedMultiplier;
                    break;
                default:
                    requiredSpeed = groundSpeed;
                    break;
            }

            Animator.speed = currentGroundSpeed * animationSpeedMultiplier;

            currentGroundSpeed = input.sqrMagnitude > 1 ?
                requiredSpeed * lateralSpeedMultiplier :
                requiredSpeed;
        }

        private void AnimationMoveCharacter()
        {
            if (Animator.enabled == false) return;

            Animator.SetFloat("InputX", input.x);
            Animator.SetFloat("InputY", input.y);
        }

        private void AudioFootstep()
        {
            if (isJumping || input == Vector2.zero) return;

            AudioSource.volume = currentGroundSpeed / 2f;
            // keep track of distance traveled for footsteps sound
            float characterVelocity = currentGroundSpeed * audioFootstepSpeedMultiplier * Time.deltaTime;

            m_FootstepDistanceCounter += characterVelocity;

            if (m_FootstepDistanceCounter >= 1f)
            {
                m_FootstepDistanceCounter = 0f;
                AudioSource.PlayOneShot(FootstepSfx);
            }
        }

        private void FixedUpdate()
        {
            if (!CharacterController.enabled) return;

            if (isJumping) //IsAir state
            {
                UpdateInAir();
            }
            else //IsGrounded State
            {
                UpdateOnGround();
            }

        }

        private void UpdateInAir()
        {
            velocity.y -= gravity * Time.fixedDeltaTime;
            Vector3 displacement = velocity * Time.fixedDeltaTime;
            if (!isHits) displacement += CalculateAir();
            CharacterController.Move(displacement);
            isJumping = !CharacterController.isGrounded;
            rootMotion = Vector3.zero;

            if (waitFall)
            {
                waitFall = false;
                StartCoroutine(FallAnimation());
            }
            if (!isJumping && Animator.GetBool("isJumping"))
            {
                obstacleCheck = false;
                isHits = false;
                Animator.SetBool("isJumping", isJumping);
                AudioSource.PlayOneShot(LandSfx);
            }
        }

        private bool waitFall = true;
        private IEnumerator FallAnimation()
        {
            yield return new WaitForSeconds(delayFallAnimation);
            Animator.SetBool("isJumping", isJumping);
            waitFall = true;
        }

        private void UpdateOnGround()
        {
            Vector3 stepForwardAmount = rootMotion * currentGroundSpeed;
            Vector3 stepDownAmount = Vector3.down * stepDown;

            CharacterController.Move(stepForwardAmount + stepDownAmount);
            rootMotion = Vector3.zero;

            if (!CharacterController.isGrounded)
            {
                SetInAir(0);
            }
        }

        private bool obstacleCheck;
        private IEnumerator Jump()
        {
            waitJump = false;
            float jumpVelocity = Mathf.Sqrt(2 * gravity * jumpHeight);
            SetInAir(jumpVelocity);
            AudioSource.PlayOneShot(JumpSfx);
            Animator.SetBool("isJumping", true);
            yield return new WaitForSeconds(delayObstacleCheck);
            obstacleCheck = true;
            yield return new WaitForSeconds(intervalJumps);
            waitJump = true;
        }

        private void SetInAir(float jumpVelocity)
        {
            isJumping = true;
            velocity = Animator.velocity * currentGroundSpeed * jumpDump;
            velocity.y = jumpVelocity;
            if (jumpVelocity == 0) ReturnHitImpulsePlayer(lastHitDirection);
        }

        private Vector3 CalculateAir()
        {
            return ((transform.forward * input.y) + (transform.right * input.x)) * (airControl / 100);
        }

        public void OnPlayerShoot(int weaponIndex, Vector3 mouseWorldPosition)
        {
            Fire(mouseWorldPosition);
        }

        public void AddPlayerWeapon(string weaponName)
        {
            SettingsManager settings = LoadManager.SettingsManager;
            GameObject weapon = settings.GetRequredWeapons().Where(w => w.WeaponName == weaponName).FirstOrDefault().gameObject;
            // spawn the weapon prefab as child of the weapon socket
            GameObject weaponObject = Instantiate(weapon, WeaponPivot.position, WeaponPivot.rotation);
            PlayerWeaponsManager.SetWeapon(weaponObject);
        }

        private void SetPlayerSettings(int indexModel, int indexMaterial, SkinType type)
        {
            SettingsManager settings = LoadManager.SettingsManager;

            StartingWeapons = settings.ExceptNotRequredWeapon(StartingWeapons);

            if (!DisableCustomization)
            {
                CharacterSkinned.sharedMesh = settings.Customization.GetModel(type, indexModel);
                CharacterSkinned.materials = settings.Customization.GetSkin(type, indexMaterial).Materials;
            }
        }

        public void UpdatePlayerSkin(Material[] skin)
        {
            CharacterSkinned.materials = skin;
        }

        private void Fire(Vector3 mouseWorldPosition)
        {
            WeaponController weapon = PlayerWeaponsManager.activeWeapon;
            weapon.HandleShoot(mouseWorldPosition);
        }
        void OnDamaged(float damage, GameObject damageSource)
        {
            // test if the damage source is the player
            if (damageSource && damageSource.GetComponent<Health>())
            {
                // play the damage tick sound
                if (DamageTick && !m_WasDamagedThisFrame)
                    AudioSource.PlayOneShot(DamageTick);

                m_WasDamagedThisFrame = true;
            }
        }

        private void OnDie()
        {
            DiePlayer();
        }

        #region COROUTINES

        private IEnumerator WaitForRespawn()
        {
            controllable = false;

            yield return new WaitForSeconds(0.1f);

            VFXParticleEnergyExplosion.gameObject.GetComponent<AudioSource>().Play();
            VFXParticleEnergyExplosion.Play();
            CharacterSkinned.material = DissolveMaterial;

            CharacterController.enabled = false;
            Animator.enabled = false;
            for (int i = 0; i < RagdollRigidbodys.Length; i++)
            {
                RagdollRigidbodys[i].isKinematic = false;
            }
            WeaponController weapon = PlayerWeaponsManager.activeWeapon;
            if (weapon != null) weapon.SetRigidbody(true);

            yield return new WaitForSeconds(delayStartDieEffect);

            updateDieEffect = true;
        }

        private IEnumerator WaitForDisableInvulnerable()
        {
            yield return new WaitForSeconds(GameSettings.PLAYER_INVULNERABLE_TIME);
            Health.DisableInvulnerable();
        }

        #endregion


        private void DiePlayer()
        {
            PlayerDeathEvent evt = Events.PlayerDeathEvent;
            evt.Die = true;
            EventManager.Broadcast(evt);

            StartCoroutine(WaitForRespawn());
        }


        public void RespawnPlayer()
        {
            Debug.Log("RespawnPlayer");
            controllable = false;
            transform.position = Spawnpoint.position;
            transform.rotation = Spawnpoint.rotation;

            for (int i = 0; i < RagdollRigidbodys.Length; i++)
            {
                RagdollRigidbodys[i].isKinematic = true;
            }
            SkeletonHips.position = defaultHipsPosition;
            CharacterSkinned.gameObject.SetActive(true);
            CharacterController.enabled = true;
            Animator.enabled = true;

            m_PlayerSaves = SettingsManager.CurrentPlayerSaves;
            SetPlayerSettings(Customization.GetCurrentIndexModel(), m_PlayerSaves.Skin, Customization.Type);

            PlayerWeaponsManager.ResetWeapons();

            Health.Resurrection();
            StartCoroutine(WaitForDisableInvulnerable());
            StartCoroutine(IsKillHeightPlayer());

            controllable = true;

            PlayerDeathEvent evt = Events.PlayerDeathEvent;
            evt.Die = false;
            EventManager.Broadcast(evt);

            OnSpawn?.Invoke();
        }

    }
}
