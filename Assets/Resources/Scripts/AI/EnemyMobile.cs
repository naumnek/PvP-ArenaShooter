using Platinum.Settings;
using System.Collections;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.AI;

namespace Unity.FPS.AI
{
    public enum MoveState
    {
        Idle,
        Walk,
        Sprint,
        Crouch
    }

    [RequireComponent(typeof(EnemyController))]
    public class EnemyMobile : MonoBehaviour
    {

        public enum AIState
        {
            Patrol,
            Follow,
            Attack,
        }

        [Header("General")]
        public bool MoveWhileShoot;
        [Header("References")]
        public NavMeshAgent navMeshAgent;
        public Transform LookAtTarget;

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

        [Header("WeaponLook")]
        public float WeaponMaxShootAngle = 10f;
        public float WeaponMaxLookAngle = 60f;
        public float AimMaxAngle = 80f;
        public float WeaponRotationSpeed = 15f;
        public int MuzzleMaxCastAttempts = 5;
        public float SurfaceUnderCastLength = 3f;
        [Header("NavAgent Jump")]
        public float ObstacleOffsetY = 0.75f;
        public float ObstacleCastLength = 3f;
        public float RadiusActiveOffMeshLink = 5f;
        [Header("NavAgent Move")]
        public float OrientationSpeed = 30f;
        public float AgentRangeActive = 3f;
        public float AgentVelocityMagnitude = 4f;
        public float RadiusNavRandomSearch = 10.0f;
        public float DelayBetweenMovementAndStop = 3f;

        [Header("Audio")]
        public float audioFootstepSpeedMultiplier = 2f;
        public AudioClip FootstepSfx;
        public AudioClip JumpSfx;
        public AudioClip LandSfx;
        public AudioClip FallDamageSfx;

        private bool waitJump;
        private float currentGroundSpeed;
        private Vector3 rootMotion;
        private Vector3 velocity;
        private Vector3 desiredVelocity;
        private bool isJumping;

        private bool inputJump;
        private bool inputCrouch;
        private Vector2 input;
        private float m_FootstepDistanceCounter;
        private bool isHits;
        private Vector3 lastHitDirection;
        public MoveState MoveState { get; private set; }
        private CharacterController m_CharacterController;

        private Animator m_Animator;

        [Header("Weapons Options")]

        [Tooltip("Pointing target ray")]
        public LayerMask WeaponCastLayer;
        public LayerMask GroundCastLayer;

        public float DynamicAttackRange { get; private set; } = 50f;
        public float DynamicStopAttackRange { get; private set; } = 10f;
        public AIState AiState { get; private set; }
        private EnemyController m_EnemyController;
        private DetectionModule m_DetectionModule;
        private SettingsManager m_SettingsManager;
        private AudioSource m_AudioSource;
        private Actor KnownDetectedTarget;

        private LoadManager LoadManager;
        private float DelayBetweenShots = 0.2f;
        private bool CheckBetweenShots;
        private Vector3 lastTargetPosition;
        private Vector3 offMeshLinkPosition;
        private Vector3 normalObstaclePosition;

        private bool ServerPause = true;

        private Transform TransformAgent;
        private Transform TransformBody;
        public float agentDistance { get; private set; }
        private bool isAgentMove;
        private bool isAgentJump;

        private Transform m_AimPoint;
        private Transform m_LookWeaponMuzzle;
        private LoadManager m_LoadManager;

        private void OnDestroy()
        {
            EventManager.RemoveListener<GamePauseEvent>(OnGamePauseEvent);
        }

        private void Awake()
        {
            EventManager.AddListener<GamePauseEvent>(OnGamePauseEvent);
            
            TransformBody = transform;
            TransformAgent = navMeshAgent.transform;

            navMeshAgent.angularSpeed = OrientationSpeed * 10f;
            
            requiredPointing = ForwardBody();

            m_EnemyController = GetComponent<EnemyController>();
            m_Animator = GetComponent<Animator>();
            m_DetectionModule = GetComponent<DetectionModule>();
            m_CharacterController = GetComponent<CharacterController>();

            m_DetectionModule.onDetectedTarget += OnDetectedTarget;
            m_DetectionModule.onLostTarget += OnLostTarget;
            m_EnemyController.OnSwitchedToWeapon += OnWeaponSwitched;
            m_EnemyController.onAttack += OnAttack;
            m_EnemyController.SetPathDestinationToClosestNode();
            m_EnemyController.onDamaged += OnDamaged;
            m_EnemyController.OnSpawn += OnSpawnBot;
            m_EnemyController.OnInitialize += Activate;

            OnDieBot();
        }

        private void OnGamePauseEvent(GamePauseEvent evt)
        {
            ServerPause = evt.ServerPause;
        }

        private void Activate()
        {
            m_DetectionModule = m_EnemyController.DetectionModule;
            m_AimPoint = m_EnemyController.Actor.AimPoint;
            //m_NavMeshAgent.updateRotation = false;

            //m_NavMeshAgent.isStopped = true;
            m_AudioSource = m_EnemyController.AudioSource;
            m_EnemyController.Health.OnDie += OnDieBot;
            OnSpawnBot();

            ServerPause = m_EnemyController.SettingsManager.CurrentMatchSaves.PeacifulMode;
        }

        public void OnDieBot()
        {
            input = new Vector2(0, 0);
            AnimationMoveCharacter();
        }

        public void OnSpawnBot()
        {
            LookAtTarget.position = ForwardBody();
            waitJump = true;
            OnWeaponSwitched(m_EnemyController.CurrentWeapon);
            AiState = AIState.Patrol;
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
            if (hit.moveDirection.y < -0.3f) return;

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
            rootMotion += m_Animator.deltaPosition;
        }
        
        public void SetNavDestination(Vector3 destination)
        {
            if (navMeshAgent && navMeshAgent.enabled)
            {
                navMeshAgent.SetDestination(destination);
            }
        }

        public void UpdateAgentMovement()
        {
            agentDistance = Vector3.Distance(TransformAgent.position, TransformBody.position);
            isAgentMove = navMeshAgent.velocity.magnitude >= AgentVelocityMagnitude || agentDistance >= 1f;

            navMeshAgent.enabled = agentDistance <= AgentRangeActive && m_CharacterController.isGrounded;
            TransformAgent.position = TransformBody.position;

            if (navMeshAgent.isOnOffMeshLink)
            {
                isAgentJump = true;
                offMeshLinkPosition = TransformAgent.position;
            }

            //navMeshAgent.enabled = m_CharacterController.isGrounded && agentDistance < 1f;
            //Debug.Log("isAgentMove: " + isAgentMove + " | " + agentDistance + " | " + isAgentJump + " | ");
            //Debug.Log("navMeshAgent: " +  navMeshAgent.velocity + " | " + navMeshAgent.nextPosition + " | ");
            //Debug.Log("lastTargetPosition: " + lastTargetPosition + " | ");

            input = new Vector2(0, isAgentMove ? 1 : 0);
        }

        void Update()
        {
            UpdateSpeed();
            AudioFootstep();
            if (ServerPause || !m_CharacterController.enabled) return;
            
            AnimationMoveCharacter();
            UpdateAgentMovement();
            
            m_Animator.SetBool("isCrouching", inputCrouch);

            UpdateAiStateTransitions();
            UpdateCurrentAiState();
        }

        private void UpdateSpeed()
        {
            float requiredSpeed;
            switch (MoveState)
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


            currentGroundSpeed = input.sqrMagnitude > 1 ?
                requiredSpeed * lateralSpeedMultiplier :
                requiredSpeed;
            m_Animator.speed = currentGroundSpeed * animationSpeedMultiplier;
            navMeshAgent.speed = currentGroundSpeed * 5f;
        }

        private void AnimationMoveCharacter()
        {
            m_Animator.SetFloat("InputX", input.x);
            m_Animator.SetFloat("InputY", input.y);
        }

        private void AudioFootstep()
        {
            if (isJumping || input == Vector2.zero) return;

            m_AudioSource.volume = currentGroundSpeed / 2f;
            // keep track of distance traveled for footsteps sound
            float characterVelocity = currentGroundSpeed * audioFootstepSpeedMultiplier * Time.deltaTime;

            m_FootstepDistanceCounter += characterVelocity;

            if (m_FootstepDistanceCounter >= 1f)
            {
                m_FootstepDistanceCounter = 0f;
                m_AudioSource.PlayOneShot(FootstepSfx);
            }
        }

        private void FixedUpdate()
        {
            if (!m_CharacterController.enabled) return;

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
            m_CharacterController.Move(displacement);
            //NavMeshAgent.velocity = m_CharacterController.velocity;

            isJumping = !m_CharacterController.isGrounded;
            rootMotion = Vector3.zero;

            if (waitFall)
            {
                waitFall = false;
                StartCoroutine(FallAnimation());
            }
            if (!isJumping && m_Animator.GetBool("isJumping"))
            {
                obstacleCheck = false;
                isHits = false;
                m_Animator.SetBool("isJumping", isJumping);
                m_AudioSource.PlayOneShot(LandSfx);
            }
        }

        private bool waitFall = true;
        private IEnumerator FallAnimation()
        {
            yield return new WaitForSeconds(delayFallAnimation);
            m_Animator.SetBool("isJumping", isJumping);
            waitFall = true;
        }

        private void UpdateOnGround()
        {
            if (isAgentJump && waitJump)
            {
                isAgentJump = IsForwardObstacle(TransformBody, out Vector3 normal);
                if (isAgentJump)
                {
                    normalObstaclePosition = normal;
                    float linkDistance = Vector3.Distance(offMeshLinkPosition, TransformAgent.position);
                    if (linkDistance < RadiusActiveOffMeshLink) StartCoroutine(Jump());
                }
            }
            
            Vector3 stepForwardAmount = rootMotion * currentGroundSpeed;
            Vector3 stepDownAmount = Vector3.down * stepDown;

            m_CharacterController.Move(stepForwardAmount + stepDownAmount);
            //m_NavMeshAgent.velocity = m_CharacterController.velocity;

            rootMotion = Vector3.zero;

            if (!m_CharacterController.isGrounded)
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
            m_AudioSource.PlayOneShot(JumpSfx);
            m_Animator.SetBool("isJumping", true);
            yield return new WaitForSeconds(delayObstacleCheck);
            obstacleCheck = true;
            yield return new WaitForSeconds(intervalJumps);
            waitJump = true;
        }

        private void SetInAir(float jumpVelocity)
        {
            isJumping = true;
            velocity = m_Animator.velocity * currentGroundSpeed * jumpDump;
            velocity.y = jumpVelocity;
            if (jumpVelocity == 0) ReturnHitImpulsePlayer(lastHitDirection);
        }

        private Vector3 CalculateAir()
        {
            return ((transform.forward * input.y) + (transform.right * input.x)) * (airControl / 100);
        }

        public void LookTowardsPosition(Vector3 lookPosition)
        {
            Debug.Log("LookTowardsPosition: " + lookPosition);
            Quaternion targetRotation = Quaternion.LookRotation(lookPosition);
            float yawLook = targetRotation.eulerAngles.y;
            TransformBody.rotation = Quaternion.Slerp(TransformBody.rotation, Quaternion.Euler(0, yawLook, 0), OrientationSpeed * Time.deltaTime);
        }

        public void OrientTowards(Vector3 lookPosition)
        {
            Quaternion targetRotation = TransformBody.rotation;

            Vector3 lookDirection = Vector3.ProjectOnPlane(lookPosition - TransformBody.position, Vector3.up).normalized;
            if (lookDirection.sqrMagnitude != 0f)
            {
                targetRotation = Quaternion.LookRotation(lookDirection);
                float yawLook = targetRotation.eulerAngles.y;
                TransformBody.rotation = Quaternion.Slerp(TransformBody.rotation, Quaternion.Euler(0, yawLook, 0), OrientationSpeed * Time.deltaTime);
            }
        }

        public void TurnTowardsAgent()
        {
            //Debug.Log("TurnTowardsAgent: " + agentDistance);
            if (isAgentMove)
            {
                Quaternion rotate = isAgentJump && isJumping
                    ? Quaternion.Euler(0,180f,0) * Quaternion.LookRotation(normalObstaclePosition)
                    : TransformAgent.rotation;
                OrientTowards(rotate);
            }
        } 
        
        public void OrientTowards(Quaternion lookRotation)
        {
            Quaternion targetRotation = Quaternion.identity;

            float yawCamera = lookRotation.eulerAngles.y;
            TransformBody.rotation = Quaternion.Slerp(TransformBody.rotation, Quaternion.Euler(0, yawCamera, 0), OrientationSpeed * Time.deltaTime);
        }

        void UpdateAiStateTransitions()
        {
            // Handle transitions 
            switch (AiState)
            {
                case AIState.Follow:
                    // Transition to attack when there is a line of sight to the target
                    if (IsTargetInAttack)
                    {
                        AiState = AIState.Attack;
                    }

                    break;
                case AIState.Attack:
                    // Transition to follow when no longer a target in attack range
                    if (!IsTargetInAttack)
                    {
                        AiState = AIState.Follow;
                    }

                    break;
            }
        }

        private void FindNearestWaypoint(Vector3 target, float radius, bool force)
        {
            //if (waitMoveNav == false) return;
            if (force || IsDistanceLastTarget > 2f &&  (IsDistanceLastTarget >= RadiusNavRandomSearch || !isAgentMove))
            {
                Vector3 point = target + Random.insideUnitSphere * radius;
                if (NavMesh.SamplePosition(point, out NavMeshHit hit, 1.0f, NavMesh.AllAreas))
                {
                    lastTargetPosition = hit.position;
                    //Debug.Log("lastFollowPosition: " + lastTargetPosition);
                }
            }
        }

        private Vector3 weaponPointing = Vector3.zero;
        private Vector3 aimPointing = Vector3.zero;
        private Vector3 targetPosition = Vector3.zero;
        private Vector3 requiredPointing = Vector3.zero;
        private float aimAngle;
        private float IsDistanceTarget;
        private float IsDistanceLastTarget;
        private bool IsTargetInAttack;
        private bool IsDistanceStop;
        private bool IsHeightTarget;
        private bool IsRequiredPointing;
        private bool IsAimPointing;
        private bool IsWeaponPointing;

        private void DetectionTargetInfo()
        {

            if (KnownDetectedTarget)
            {
                targetPosition = KnownDetectedTarget.AimPoint.position;
                IsDistanceTarget = Vector3.Distance(targetPosition, m_AimPoint.position);
                IsDistanceLastTarget = Vector3.Distance(targetPosition, lastTargetPosition);
                //bool IsEnemyInAttackRange = IsDistanceTarget >= (m_DynamicAttackStopDistanceRatio * DynamicAttackRange);
                IsDistanceStop = IsDistanceTarget > DynamicStopAttackRange;
                IsHeightTarget = targetPosition.y < m_AimPoint.position.y || IsDistanceTarget > targetPosition.y - m_AimPoint.position.y;

                IsTargetInAttack = IsDistanceTarget < DynamicAttackRange;
                //Debug.Log("IsTargetInAttack: " + IsTargetInAttack + " | " + IsDistanceTarget + " < " + DynamicAttackRange);
                FindNearestWaypoint(targetPosition, RadiusNavRandomSearch, false);
            }
        }
        private void WeaponLookAt(Vector3 point)
        {
            float angle = Vector3.Angle(m_AimPoint.forward, point - m_AimPoint.position);
            //Debug.Log(enemyActor.NickName + ": " + angle);
            if (angle > WeaponMaxLookAngle) return;

            LookAtTarget.position = Vector3.Slerp(LookAtTarget.position, point, WeaponRotationSpeed * Time.deltaTime);
        }

        private Vector3 ForwardBody()
        {
            return TransformBody.TransformPoint(new Vector3(0, 1, 20));
        }

        private Vector3 ForwardAgent()
        {
            return TransformAgent.TransformPoint(new Vector3(0, 1, 20));
        }
        
        void UpdateCurrentAiState()
        {
            // Handle logic 
            Vector3 destinationPath = m_EnemyController.GetDestinationOnPath();

            DetectionTargetInfo();

            Debug.DrawLine(lastTargetPosition, lastTargetPosition + Vector3.up, Color.magenta, 1f, false);

            switch (AiState)
            {
                case AIState.Patrol:
                    if (destinationPath == Vector3.zero)
                    {
                        lastTargetPosition = TransformBody.position;
                        SetNavDestination(lastTargetPosition);
                        AiState = AIState.Follow;
                        return;
                    }

                    m_EnemyController.UpdatePathDestination();

                    WeaponLookAt(ForwardBody());
                    TurnTowardsAgent();

                    SetNavDestination(destinationPath);
                    break;
                case AIState.Follow:
                    if (KnownDetectedTarget == null) break;
                    //Debug.Log("AIState.Follow: " + lastTargetPosition);
                    
                    WeaponLookAt(ForwardBody());
                    TurnTowardsAgent();

                    SetNavDestination(lastTargetPosition);

                    break;
                case AIState.Attack:
                    if (KnownDetectedTarget == null) break;
                
                    aimPointing = IsAimTarget(KnownDetectedTarget);
                    IsAimPointing = aimPointing != Vector3.zero;
                    
                    weaponPointing = IsWeaponTarget();
                    IsWeaponPointing = weaponPointing != Vector3.zero;
                    
                    requiredPointing = IsWeaponPointing ? weaponPointing : IsAimPointing ? aimPointing : ForwardBody();
                    IsRequiredPointing = requiredPointing != ForwardBody();

                    if(wasWaitMoveNav && waitMoveNav == false)
                    {
                        endMoveNav = Time.time;
                        FindNearestWaypoint(aimPointing, 1f, true);
                    }
                    wasWaitMoveNav = waitMoveNav;

                    //Debug.Log("Pointing: " + IsWeaponPointing + " | " + IsAimPointing);
                    if (IsAimPointing && aimAngle <= WeaponMaxShootAngle || IsWeaponPointing)
                    {
                        bool isMove = IsHeightTarget && IsDistanceStop && MoveWhileShoot && IsSurfaceUnder(TransformBody);
                        Vector3 destination = isMove ? lastTargetPosition : TransformBody.position;
                        SetNavDestination(destination);

                        m_EnemyController.TryAtack(aimPointing);
                    }
                    else
                    {
                        //Debug.Log("WaitNav: " + waitMoveNav + " | " + (endMoveNav + DelayBetweenMovementAndStop) + " < " + Time.time);
                        if (IsAimPointing && !waitMoveNav || IsAimPointing && waitMoveNav && endMoveNav + DelayBetweenMovementAndStop < Time.time)
                        {
                            SetMoveNav();
                            WeaponLookAt(requiredPointing);
                            OrientTowards(aimPointing);
                            SetNavDestination(TransformBody.position);
                            //Debug.Log("RotateAim");
                        } 

                        if(waitMoveNav == false) 
                            break;
                        
                        //Debug.Log("waitMoveNav: true");
                        WeaponLookAt(requiredPointing);
                        TurnTowardsAgent();
                        SetNavDestination(lastTargetPosition);
                    }

                    break;
            }
        }

        private Vector3 IsAimTarget(Actor enemyActor)
        {

            aimAngle = Vector3.Angle(m_AimPoint.forward, enemyActor.AimPoint.position - m_AimPoint.position);
            if (aimAngle > AimMaxAngle) return Vector3.zero;
            //Debug.Log(enemyActor.NickName + ": " + aimAngle);

            // Pointing at enemy handling
            Vector3 randomOffset = Vector3.zero;
            Vector3 startPos = m_AimPoint.position;
            Vector3 endPos = enemyActor.AimPoint.position;
            RaycastHit castInfo = RaycastMuzzle(startPos, endPos);

            for (int attempts = 1; ImpactRequiredTarget(castInfo) != Vector3.zero && attempts <= MuzzleMaxCastAttempts; attempts++)
            {
                if (attempts >= MuzzleMaxCastAttempts)
                {
                    Debug.DrawLine(startPos, endPos, attempts == 0 ? Color.red : Color.yellow, 0.2f, false);
                    return castInfo.point;
                }

                randomOffset = new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(-0.2f, 0.2f), 0);
                endPos = enemyActor.AimPoint.TransformPoint(new Vector3(randomOffset.x, randomOffset.y, 0));
                castInfo = RaycastMuzzle(startPos, endPos);
            }

            // Pointing at enemy handling
            return Vector3.zero;
        }

        private bool IsSurfaceUnder(Transform body)
        {
            Vector3 startPos = body.TransformPoint(Vector3.forward * SurfaceUnderCastLength);
            Vector3 endPos = startPos;
            startPos.y += ObstacleOffsetY;
            endPos.y -= 3f;
            RaycastHit castInfo = RaycastGround(startPos, endPos);
            Debug.DrawLine(startPos, endPos, castInfo.collider != null ? Color.blue : Color.cyan, 0.05f, false);
            return castInfo.collider != null;
        }
        private bool IsForwardObstacle(Transform body, out Vector3 normal)
        {
            Vector3 startPos = body.position;
            Vector3 endPos = body.TransformPoint(Vector3.forward * ObstacleCastLength);
            startPos.y += ObstacleOffsetY;
            endPos.y += ObstacleOffsetY;
            RaycastHit castInfo = RaycastGround(startPos, endPos);
            Debug.DrawLine(startPos, endPos, castInfo.collider != null ? Color.blue : Color.cyan, 0.05f, false);
            normal = castInfo.normal;
            return castInfo.collider != null;
        }

        private bool IsCastTarget()
        {
            // Pointing at enemy handling
            RaycastHit castInfo = RaycastMuzzle(m_LookWeaponMuzzle.position, targetPosition);
            bool isSuccesCast = ImpactRequiredTarget(castInfo) != Vector3.zero;

            if(isSuccesCast)Debug.DrawLine(m_LookWeaponMuzzle.position, castInfo.point, isSuccesCast ? Color.black : Color.white, 0.2f, false);

            return isSuccesCast;
        }

        private Vector3 IsWeaponTarget()
        {
            // Pointing at enemy handling
            Vector3 startPos = m_AimPoint.position;
            Vector3 endPos = m_LookWeaponMuzzle.TransformPoint(Vector3.forward * DynamicAttackRange);
            RaycastHit castInfo = RaycastMuzzle(startPos, endPos);
            bool isSuccesCast = ImpactRequiredTarget(castInfo) != Vector3.zero;

            //if(isSuccesCast)Debug.DrawLine(startPos, castInfo.point, isSuccesCast ? Color.black : Color.white, 0.2f, false);

            return isSuccesCast ? castInfo.point : Vector3.zero;
        }

        private bool IsRequiredActor(Actor actor)
        { 
            return actor.Affiliation != m_EnemyController.Actor.Affiliation;
        }
       
        private Vector3 ImpactRequiredTarget(RaycastHit cast)
        {
            if (cast.collider == null) return Vector3.zero;

            Actor actor = cast.collider.GetComponentInParent<Actor>();
            if (actor && IsRequiredActor(actor))
            {
                return cast.point;
            }
            return Vector3.zero;
        }
        
        private RaycastHit RaycastGround(Vector3 origin, Vector3 target)
        {
            if (Physics.Linecast(origin, target, out RaycastHit cast, GroundCastLayer))
            {
                return cast;
            }
            return new RaycastHit();
        }

        private RaycastHit RaycastMuzzle(Vector3 origin, Vector3 target)
        {
            if (Physics.Linecast(origin, target, out RaycastHit cast, WeaponCastLayer))
            {
                return cast;
            }
            return new RaycastHit();
        }

        private void resetMoveNav() => waitMoveNav = true;
        private bool waitMoveNav = true;
        private bool wasWaitMoveNav = true;
        private float endMoveNav;
        private void SetMoveNav()
        {
            if (waitMoveNav == false) return;
            waitMoveNav = false;

            Invoke(nameof(resetMoveNav), DelayBetweenMovementAndStop);
        }

        public void OnHitEnemy(bool hit)
        {
            if (CheckBetweenShots) return;
            CheckBetweenShots = true;

            StartCoroutine(WaitBetweenShot());
        }

        public void OnWeaponSwitched(WeaponController newWeapon)
        {
            m_LookWeaponMuzzle = newWeapon.WeaponGunMuzzle;

            // WeaponTypes: Rifle, Pistol, ShotGun, MiniGun, Machine, MachineGun, Grenade, Rocket

            DelayBetweenShots = newWeapon.DelayBetweenShots;
            DynamicAttackRange = newWeapon.RecomendAttackRange;
            DynamicStopAttackRange = newWeapon.RecomendStopAttackRange;
        }

        private IEnumerator WaitBetweenShot()
        {
            yield return new WaitForSeconds(DelayBetweenShots);
            CheckBetweenShots = false;
        }

        void OnAttack() { }

        void OnDetectedTarget(Actor target)
        {
            KnownDetectedTarget = target;
            //Debug.Log("OnDetectedTarget: " + KnownDetectedTarget.NickName);

            if (AiState == AIState.Patrol)
            {
                AiState = AIState.Follow;
            }
        }

        void OnLostTarget()
        {
            KnownDetectedTarget = null;
            
            if (AiState == AIState.Follow || AiState == AIState.Attack)
            {
                AiState = AIState.Patrol;
            }
        }

        void OnDamaged() { }
    }
}