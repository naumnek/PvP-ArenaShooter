using Platinum.Settings;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Unity.FPS.Game
{
    public class Health : MonoBehaviour
    {
        [Tooltip("Maximum amount of health")] public float MaxHealth = 100f;
        [Tooltip("Heal by passive regeneration")] public float PassiveHeal = 5f;

        [Tooltip("Health ratio at which the critical health vignette starts appearing")]
        public float CriticalHealthRatio = 0.3f;
        [Tooltip("Multiplier to apply to the received damage")]
        public float DamageMultiplier = 1f;
        [Range(0, 1)]
        [Tooltip("Multiplier to apply to self damage")]
        public float SensibilityToSelfdamage = 0.5f;

        public UnityAction<float, GameObject> OnDamaged;
        public UnityAction<float> OnHealed;
        public UnityAction OnDie;

        public float CurrentHealth { get; set; }
        public bool CanPickup() => CurrentHealth < MaxHealth;

        public float GetRatio() => CurrentHealth / MaxHealth;
        public bool IsCritical() => GetRatio() <= CriticalHealthRatio;

        public bool invulnerable { get; private set; } = false;
        public void DisableInvulnerable()
        {
            invulnerable = false;
        }

        public bool IsDead { get; private set; }
        public bool IsInstantDead { get; private set; }
        public CauseDeath IsCauseDeath;
        public enum CauseDeath
        {
            Player,
            Enemy,
            Environment,
        }

        void Start()
        {
            CurrentHealth = MaxHealth;
            StartCoroutine(PassiveRegeneration());
        }

        private IEnumerator PassiveRegeneration()
        {
            while (true)
            {
                yield return new WaitForSeconds(GameSettings.PLAYER_PASSIVE_REGENERATION_INTERVAL);
                if (!IsDead) Heal(GameSettings.PLAYER_PASSIVE_REGENERATION_AMOUNT);
            }
        }

        public void Heal(float healAmount)
        {
            float healthBefore = CurrentHealth;
            CurrentHealth += healAmount;
            CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, MaxHealth);

            // call OnHeal action
            float trueHealAmount = CurrentHealth - healthBefore;
            if (trueHealAmount > 0f)
            {
                OnHealed?.Invoke(trueHealAmount);
            }
        }

        public void InflictDamage(float damage, bool isExplosionDamage, GameObject damageSource)
        {
            var totalDamage = damage;

            // skip the crit multiplier if it's from an explosion
            if (!isExplosionDamage)
            {
                totalDamage *= DamageMultiplier;
            }

            // potentially reduce damages if inflicted by self
            if (gameObject == damageSource)
            {
                totalDamage *= SensibilityToSelfdamage;
            }

            // apply the damages
            TakeDamage(totalDamage, damageSource);
        }

        public bool TakeDamage(float damage, GameObject damageSource)
        {
            if (invulnerable) damage = 0.01f;
            float healthBefore = CurrentHealth;
            CurrentHealth -= damage;
            CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, MaxHealth);

            // call OnDamage action
            float trueDamageAmount = healthBefore - CurrentHealth;
            if (trueDamageAmount > 0f)
            {
                OnDamaged?.Invoke(trueDamageAmount, damageSource);
            }

            return HandleDeath();
        }

        public void Kill()
        {
            IsInstantDead = true;
            CurrentHealth = 0f;

            //call OnDamage action
            OnDamaged?.Invoke(MaxHealth, null);
            HandleDeath();
        }

        bool HandleDeath()
        {
            // call OnDie action
            if (!IsDead && CurrentHealth <= 0f)
            {
                Debug.Log("HealthIsDead: " + CurrentHealth);
                IsDead = true;
                invulnerable = true;
                OnDie?.Invoke();
            }
            return IsDead;
        }

        public void Resurrection()
        {
            CurrentHealth = MaxHealth;
            IsInstantDead = false;
            IsDead = false;
        }
    }
}