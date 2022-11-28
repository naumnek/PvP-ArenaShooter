using Platinum.Settings;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Events;

namespace Unity.FPS.AI
{
    public class DetectionModule : MonoBehaviour
    {
        public LayerMask IgnoryLayor;

        [Tooltip("The point representing the source of target-detection raycasts for the enemy AI")]
        public Transform DetectionSourcePoint;

        public bool IgnoreDetectionRange = true;
        public bool AutoSetRandomTarget = true;
        public bool IsSeeingThroughWalls = true;
        [Tooltip("The max distance at which the enemy can see targets")]
        public float DetectionRange = 100f;

        [Tooltip("Time before an enemy abandons a known target that it can't see anymore")]
        public float KnownTargetTimeout = 4f;

        [Tooltip("Optional animator for OnShoot animations")]
        private Animator Animator;

        public UnityAction<Actor> onDetectedTarget;
        public UnityAction onLostTarget;

        public Actor KnownDetectedTarget { get; private set; }
        public bool IsTargetInAttackRange { get; private set; }
        public bool IsSeeingTarget { get; private set; }
        public bool HadKnownTarget { get; private set; }

        protected float TimeLastSeenTarget = Mathf.NegativeInfinity;

        ActorsManager m_ActorsManager;
        EnemyMobile m_EnemyMobile;
        private float m_StartDetectionRange;
        private Actor m_Actor;
        private LoadManager m_LoadManager;
        private EnemyController EnemyController;

        private void Awake()
        {
            m_StartDetectionRange = DetectionRange;

            EnemyController = GetComponent<EnemyController>();
            EnemyController.OnInitialize += Activate;
            EnemyController.onDie += OnDieBot;
            EnemyController.OnSpawn += OnSpawnBot;
        }

        private void Activate()
        {
            m_Actor = EnemyController.Actor;
            m_EnemyMobile = EnemyController.EnemyMobile;
            m_ActorsManager = EnemyController.ActorsManager;
            OnSpawnBot();
        }
        
        private void OnSpawnBot()
        {
            if (AutoSetRandomTarget)
            {
                KnownDetectedTarget = GetNearestActor(m_ActorsManager.GetEnemyActors(m_Actor));
                IsSeeingTarget = true;
            }
            if (IgnoreDetectionRange) DetectionRange = 1000;
        }

        private void OnDieBot()
        {
            KnownDetectedTarget = null;
            HadKnownTarget = false;
        }

        public float HasDistanceNearestForwardActor(float angleActors)
        {
            List<Actor> actors = new List<Actor>();
            actors.AddRange(m_ActorsManager.GetFriendlyActors(m_Actor).Where(a => a != m_Actor));

            for (int i = 0; i < actors.Count; i++)
            {
                float angle = HasAngleActor(actors[i]);

                if (angle > angleActors)
                {
                    actors.RemoveAt(i);
                }
            }

            return GetDistanceNearestActor(actors);
        }

        public Actor GetNearestActor(List<Actor> actors)
        {
            Actor nearestActor = actors.First();

            float minDistance = GetDistanceFromActor(nearestActor);
            float currentMinDistance = minDistance;
            for (int i = 0; i < actors.Count; i++)
            {
                currentMinDistance = GetDistanceFromActor(actors[i]);

                if (currentMinDistance < minDistance)
                {
                    minDistance = currentMinDistance;
                    nearestActor = actors[i];
                }
            }

            //Debug.Log(m_Actor.Nickname + " check distance " + nearestActor.Nickname + ": " + minDistance);
            return nearestActor;
        }

        public float GetDistanceNearestActor(List<Actor> actors)
        {
            Actor nearestActor = actors.First();

            float minDistance = GetDistanceFromActor(nearestActor);
            float currentMinDistance = minDistance;
            for (int i = 0; i < actors.Count; i++)
            {
                currentMinDistance = GetDistanceFromActor(actors[i]);

                if (currentMinDistance < minDistance)
                {
                    minDistance = currentMinDistance;
                    nearestActor = actors[i];
                }
            }

            //Debug.Log(m_Actor.Nickname + " check distance " + nearestActor.Nickname + ": " + minDistance);
            return minDistance;
        }



        public float HasAngleActor(Actor actor)
        {
            float angle = Vector3.Angle(DetectionSourcePoint.forward, actor.AimPoint.position - DetectionSourcePoint.position);
            //Debug.Log(actor.Nickname + " angle " + angle.ToString() + " " + m_Actor.Nickname);

            return angle;
        }

        public float GetDistanceFromActor(Actor actor)
        {
            float distance = Vector3.Distance(DetectionSourcePoint.position, actor.AimPoint.position);
            //Debug.Log(actor.Nickname + " distance: " + distance + " / " + m_Actor.Nickname);

            return distance;
        }

        private bool IsDiedTarget => KnownDetectedTarget && KnownDetectedTarget.Health.IsDead;

        public virtual void HandleTargetDetection(Actor actor, Collider[] selfColliders)
        {
            // Handle known target detection timeout
            if (IsDiedTarget || KnownDetectedTarget && !AutoSetRandomTarget && !IsSeeingTarget && (Time.time - TimeLastSeenTarget) > KnownTargetTimeout)
            {
                KnownDetectedTarget = null;
            }

            IsSeeingTarget = false;

            if (!m_ActorsManager) return;
            List<Actor> enemyActors = m_ActorsManager.GetEnemyActors(actor);

            // Find the closest visible hostile actor
            float sqrDetectionRange = DetectionRange * DetectionRange;
            float closestSqrDistance = Mathf.Infinity;

            foreach (Actor otherActor in enemyActors)
            {
                if (otherActor.Affiliation != actor.Affiliation)
                {
                    float sqrDistance = (otherActor.transform.position - DetectionSourcePoint.position).sqrMagnitude;
                    if (sqrDistance < sqrDetectionRange && sqrDistance < closestSqrDistance)
                    {
                        // Check for obstructions
                        RaycastHit[] hits = Physics.RaycastAll(DetectionSourcePoint.position,
                            (otherActor.AimPoint.position - DetectionSourcePoint.position).normalized, DetectionRange,
                           ~IgnoryLayor);
                        RaycastHit closestValidHit = new RaycastHit();
                        closestValidHit.distance = Mathf.Infinity;
                        bool foundValidHit = false;
                        foreach (var hit in hits)
                        {
                            if (!selfColliders.Contains(hit.collider) && hit.distance < closestValidHit.distance)
                            {
                                closestValidHit = hit;
                                foundValidHit = true;
                            }
                        }

                        if (foundValidHit || IsSeeingThroughWalls)
                        {

                            Actor hitActor = foundValidHit ?
                                closestValidHit.collider.GetComponentInParent<Actor>() :
                                m_ActorsManager.GetEnemyActors(actor).OrderBy
                                (x => Vector3.Distance(otherActor.transform.position, x.transform.position)).FirstOrDefault();


                            //Actor hitActor = closestValidHit.collider.GetComponentInParent<Actor>();

                            if (hitActor == otherActor)
                            {
                                IsSeeingTarget = true;
                                closestSqrDistance = sqrDistance;

                                TimeLastSeenTarget = Time.time;
                                KnownDetectedTarget = otherActor;
                            }
                        }
                    }
                }
            }

            IsTargetInAttackRange = KnownDetectedTarget != null &&
                                    Vector3.Distance(m_Actor.AimPoint.position, KnownDetectedTarget.AimPoint.position) <=
                                    m_EnemyMobile.DynamicAttackRange;

            // Detection events
            if (!HadKnownTarget &&
                KnownDetectedTarget != null)
            {
                OnDetect();
            }

            if (HadKnownTarget &&
                KnownDetectedTarget == null)
            {
                OnLostTarget();
            }

            // Remember if we already knew a target (for next frame)
            HadKnownTarget = KnownDetectedTarget != null;
        }

        public virtual void OnLostTarget() => onLostTarget?.Invoke();

        public virtual void OnDetect() => onDetectedTarget?.Invoke(KnownDetectedTarget);


        private bool WaitOnDamaged;
        public virtual void OnDamaged(GameObject damageSource)
        {
            TimeLastSeenTarget = Time.time;

            if (WaitOnDamaged) return;
            StartCoroutine(WaitSetTargetDamaged(damageSource));
        }

        private IEnumerator WaitSetTargetDamaged(GameObject damageSource)
        {
            WaitOnDamaged = true;
            yield return new WaitForSeconds(KnownTargetTimeout);
            KnownDetectedTarget = damageSource.GetComponent<ProjectileBase>().OwnerActor;
            WaitOnDamaged = false;

        }

        public virtual void OnAttack() { }
    }
}