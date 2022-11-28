using Platinum.Player;
using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    public class ProjectileStandard : ProjectileBase
    {
        [Header("General")]
        [Header("Instant hit up target")]
        public bool InstantHit = true;

        [Tooltip("Radius of this projectile's collision detection")]
        public float Radius = 0.01f;

        [Tooltip("Transform representing the root of the projectile (used for accurate collision detection)")]
        public Transform Root;

        [Tooltip("Mesh Renderer this projectile")]
        public MeshRenderer[] MeshProjectile;

        [Tooltip("Transform representing the tip of the projectile (used for accurate collision detection)")]
        public Transform Tip;

        [Tooltip("Radius of this projectile's collision detection")]
        public float MaxShotDistance = 100f;

        [Tooltip("LifeTime of the projectile")]
        public float MaxLifeTime = 5f;

        [Tooltip("VFX prefab to spawn line projectile")]
        public TrailRenderer TrailEffect;

        [Tooltip("VFX prefab to spawn upon impact")]
        public GameObject ImpactVfx;

        [Tooltip("LifeTime of the VFX before being destroyed")]
        public float ImpactVfxLifetime = 5f;

        [Tooltip("Offset along the hit normal where the VFX will be spawned")]
        public float ImpactVfxSpawnOffset = 0.1f;

        [Tooltip("Clip to play on impact")]
        public AudioClip ImpactSfxClip;

        [Tooltip("Layers this projectile can collide with")]
        public LayerMask HittableLayers = -1;

        [Tooltip("Layers this projectile can collide with")]
        public LayerMask PlayerLayers = -1;

        [Header("Movement")]
        [Tooltip("Speed of the projectile")]
        public float Speed = 20f;

        [Tooltip("Downward acceleration from gravity")]
        public float GravityDownAcceleration = 0f;

        //[Tooltip("Distance over which the projectile will correct its course to fit the intended trajectory (used to drift projectiles towards center of screen in First Person view). At values under 0, there is no correction")]
        //public float TrajectoryCorrectionDistance = -1;

        [Tooltip("Determines if the projectile inherits the velocity that the weapon's muzzle had when firing")]
        public bool InheritWeaponVelocity = false;

        [Header("Damage")]
        [Tooltip("Damage of the projectile")]
        public float Damage = 40f;

        [Tooltip("Area of damage. Keep empty if you don<t want area damage")]
        public DamageArea AreaOfDamage;

        [Header("Debug")]
        [Tooltip("Color of the projectile radius debug view")]
        public Color RadiusColor = Color.cyan * 0.2f;

        PlayerWeaponsManager playerWeaponsManager;
        ProjectileBase m_ProjectileBase;
        Vector3 m_LastRootPosition;
        Vector3 m_Velocity;
        //bool m_HasTrajectoryOverride;
        //float m_ShootTime;
        //Vector3 m_TrajectoryCorrectionVector;
        //Vector3 m_ConsumedTrajectoryCorrectionVector;
        List<Collider> m_IgnoredColliders;

        const QueryTriggerInteraction k_TriggerInteraction = QueryTriggerInteraction.Collide;
        private TrailRenderer m_Tracer;
        private bool m_VisiblyTrailBullet;

        void OnEnable()
        {
            m_ProjectileBase = GetComponent<ProjectileBase>();
            DebugUtility.HandleErrorIfNullGetComponent<ProjectileBase, ProjectileStandard>(m_ProjectileBase, this,
                gameObject);

            m_ProjectileBase.OnShoot += OnShoot;

            Destroy(gameObject, MaxLifeTime);
        }
        new void OnShoot()
        {
            playerWeaponsManager = m_ProjectileBase.Weapon.PlayerWeaponsManager;

            InstantHit = m_ProjectileBase.Weapon.MatchSettings.DisableInstanceHit ? false : InstantHit;
            m_VisiblyTrailBullet = m_ProjectileBase.Weapon.PlayerSettings.VisiblyTrailBullet;

            //m_ShootTime = Time.time;
            m_LastRootPosition = Root.position;
            m_Velocity = transform.forward * Speed;
            m_IgnoredColliders = new List<Collider>();
            transform.position += m_ProjectileBase.InheritedMuzzleVelocity * Time.deltaTime;

            // Ignore colliders of owner
            m_IgnoredColliders.AddRange(m_ProjectileBase.gameObject.
                GetComponentsInChildren<Collider>());

            m_IgnoredColliders.AddRange(m_ProjectileBase.Owner.gameObject.
                GetComponentsInChildren<Collider>());

            // Handle case of player shooting (make projectiles not go through walls, and remember center-of-screen trajectory)
            if (playerWeaponsManager)
            {
                HittableLayers -= PlayerLayers;
                //m_HasTrajectoryOverride = true;

                Vector3 cameraToMuzzle = (m_ProjectileBase.InitialPosition -
                                          playerWeaponsManager.WeaponCamera.position);

                //m_TrajectoryCorrectionVector = Vector3.ProjectOnPlane(-cameraToMuzzle, playerWeaponsManager.WeaponCamera.transform.forward);

                Ray ray = new Ray();
                ray.origin = playerWeaponsManager.WeaponCamera.position;
                ray.direction = cameraToMuzzle.normalized;
                if (Physics.Raycast(ray,
                    out RaycastHit hitInfo, cameraToMuzzle.magnitude, HittableLayers, k_TriggerInteraction))
                {
                    if (IsHitValid(hitInfo))
                    {
                        OnHit(hitInfo);
                    }
                }
            }



            if (InstantHit)
            {
                /*if (Physics.Linecast(m_ProjectileBase.InitialPosition, m_ProjectileBase.TargetPosition, out RaycastHit hit, HittableLayers))
                {
                    OnHit(hit.point, hit.normal, hit.collider);
                }*/

                Ray ray = new Ray();
                Vector3 startPos = m_ProjectileBase.InitialPosition;
                Vector3 endPos = m_ProjectileBase.TargetPosition;
                ray.origin = startPos;
                ray.direction = m_ProjectileBase.InitialDirection;

                TrailRenderer tracer = new TrailRenderer();
                if (m_VisiblyTrailBullet)
                {
                    tracer = Instantiate(TrailEffect, startPos, Quaternion.identity, null);
                    tracer.AddPosition(startPos);
                }
                if (Vector3.Distance(startPos, endPos) > MaxShotDistance)
                {
                    Vector3 dir = endPos - startPos;
                    float dist = Mathf.Clamp(Vector3.Distance(startPos, endPos), 0, MaxShotDistance);
                    Vector3 finishPos = startPos + (dir.normalized * dist);
                    if (m_VisiblyTrailBullet) tracer.transform.position = finishPos;
                    //HitEffect(finishPos, finishPos);
                }
                else
                {
                    if (Physics.Raycast(ray,
                        out RaycastHit hitInfo, 200f, HittableLayers, k_TriggerInteraction))
                    {
                        if (IsHitValid(hitInfo))
                        {
                            OnHit(hitInfo);
                        }
                        if (m_VisiblyTrailBullet) tracer.transform.position = hitInfo.point;
                    }
                }

                /*
                if (Physics.Linecast(m_ProjectileBase.InitialPosition, m_ProjectileBase.TargetPosition, out RaycastHit hit, HittableLayers))
                {
                    OnHit(hit.point, hit.normal, hit.collider);
                }*/
            }
            else
            {
                if (m_VisiblyTrailBullet)
                {
                    for (int i = 0; i < MeshProjectile.Length; i++)
                    {
                        MeshProjectile[i].enabled = true;
                    }
                }
            }
        }

        void Update()
        {
            if (InstantHit) return;
            // Move
            transform.position += m_Velocity * Time.deltaTime;
            if (InheritWeaponVelocity)
            {
                transform.position += m_ProjectileBase.InheritedMuzzleVelocity * Time.deltaTime;
            }

            // Orient towards velocity
            transform.forward = m_Velocity.normalized;

            // Gravity
            if (GravityDownAcceleration > 0)
            {
                // add gravity to the projectile velocity for ballistic effect
                m_Velocity += Vector3.down * GravityDownAcceleration * Time.deltaTime;
            }

            // Hit detection
            {
                RaycastHit closestHit = new RaycastHit();
                closestHit.distance = Mathf.Infinity;
                bool foundHit = false;

                // Sphere cast
                Vector3 displacementSinceLastFrame = Tip.position - m_LastRootPosition;
                RaycastHit[] hits = Physics.SphereCastAll(m_LastRootPosition, Radius,
                    displacementSinceLastFrame.normalized, displacementSinceLastFrame.magnitude, HittableLayers,
                    k_TriggerInteraction);
                foreach (var hit in hits)
                {
                    if (IsHitValid(hit) && hit.distance < closestHit.distance)
                    {
                        foundHit = true;
                        closestHit = hit;
                    }
                }

                if (foundHit)
                {
                    // Handle case of casting while already inside a collider
                    if (closestHit.distance <= 0f)
                    {
                        closestHit.point = Root.position;
                        closestHit.normal = -transform.forward;
                    }

                    OnHit(closestHit);
                }
            }

            m_LastRootPosition = Root.position;
        }

        bool IsHitValid(RaycastHit hit)
        {
            // ignore hits with an ignore component
            if (hit.collider.GetComponentInParent<IgnoreHitDetection>())
            {
                return false;
            }

            // ignore hits with triggers that don't have a Damageable component
            if (hit.collider.isTrigger && hit.collider.GetComponentInParent<Damageable>() == null)
            {
                return false;
            }

            // ignore hits with specific ignored colliders (self colliders, by default)
            if (m_IgnoredColliders != null && m_IgnoredColliders.Contains(hit.collider))
            {
                return false;
            }

            return true;
        }

        void OnHit(RaycastHit hitInfo)
        {
            Vector3 point = hitInfo.point;
            Vector3 normal = hitInfo.normal;
            Collider collider = hitInfo.collider;

            // damage
            if (AreaOfDamage)
            {
                // area damage
                AreaOfDamage.InflictDamageInArea(Damage, point, HittableLayers, k_TriggerInteraction,
                    m_ProjectileBase.gameObject);
            }
            else
            {
                // point damage
                Damageable damageable = collider.GetComponentInParent<Damageable>();

                if (damageable && !damageable.Health.IsDead && damageable.Actor.Affiliation != m_ProjectileBase.OwnerActor.Affiliation)
                {
                    Actor player = m_ProjectileBase.OwnerActor;
                    m_ProjectileBase.OwnerActor.OnHitEnemy(true);
                    if (damageable.InflictDamage(Damage, false, m_ProjectileBase.gameObject, collider))
                    {
                        KillEvent evt = Events.KillEvent;
                        evt.killed = damageable.Actor;
                        evt.killer = m_ProjectileBase.OwnerActor;
                        EventManager.Broadcast(evt);

                        if (player != null) player.AddKill();
                    }
                }
            }

            //var tracer = Instantiate(TrailEffect, shootOrigin, Quaternion.identity, null);
            //tracer.AddPosition(shootOrigin);

            HitEffect(point, normal).SetParent(collider.transform);

            //tracer.transform.position = point;

            // impact sfx
            if (ImpactSfxClip)
            {
                AudioUtility.CreateSFX(ImpactSfxClip, point, AudioUtility.AudioGroups.Impact, 1f, 3f);
            }

            // Self Destruct
            Destroy(this.gameObject, 1f);
        }

        private Transform HitEffect(Vector3 point, Vector3 normal)
        {
            Vector3 shootOrigin = point + (normal * ImpactVfxSpawnOffset);

            Transform impactVfxInstance = Instantiate(ImpactVfx, shootOrigin,
                Quaternion.LookRotation(normal)).transform;
            if (ImpactVfxLifetime > 0)
            {
                Destroy(impactVfxInstance.gameObject, ImpactVfxLifetime);
            }
            return impactVfxInstance;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = RadiusColor;
            Gizmos.DrawSphere(transform.position, Radius);
        }
    }
}