using UnityEngine;
using UnityEngine.Events;

namespace Unity.FPS.Game
{
    public abstract class ProjectileBase : MonoBehaviour
    {
        public WeaponController Weapon { get; private set; }

        public Transform Owner { get; private set; }
        public Actor OwnerActor { get; private set; }

        public Vector3 InitialPosition { get; private set; }
        public Vector3 InitialDirection { get; private set; }
        public Vector3 InheritedMuzzleVelocity { get; private set; }
        public float InitialCharge { get; private set; }

        public Vector3 TargetPosition { get; private set; }

        public UnityAction OnShoot;

        public void Shoot(WeaponController controller, Vector3 targetPosition)
        {
            if (Weapon) return;
            Weapon = controller;
            TargetPosition = targetPosition;
            Owner = controller.Owner;
            OwnerActor = Owner.GetComponent<Actor>();
            InitialPosition = transform.position;
            InitialDirection = transform.forward;
            InheritedMuzzleVelocity = controller.MuzzleWorldVelocity;
            InitialCharge = controller.CurrentCharge;

            OnShoot?.Invoke();
        }
    }
}