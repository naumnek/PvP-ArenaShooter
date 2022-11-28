using TMPro;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.UI
{
    public class PlayerHealthBar : MonoBehaviour
    {
        public TMP_Text PlayerHealthAmount;
        public Slider PlayerHealthSlider;
        public TMP_Text EnemyHealthAmount;
        public Slider EnemyHealthSlider;

        [Tooltip("Image component dispplaying current health")]
        public Image HealthFillImage;

        Health m_PlayerHealth;
        Health m_EnemyHealth;
        private bool Pause = true;

        private void OnDestroy()
        {
            EventManager.RemoveListener<EndSpawnEvent>(OnPlayerSpawnEvent);
            EventManager.RemoveListener<GamePauseEvent>(OnGamePauseEvent);
        }
        private void Awake()
        {
            EventManager.AddListener<GamePauseEvent>(OnGamePauseEvent);
            EventManager.AddListener<EndSpawnEvent>(OnPlayerSpawnEvent);
        }

        private void OnGamePauseEvent(GamePauseEvent evt)
        {
            Pause = evt.ServerPause;
        }

        private void OnPlayerSpawnEvent(EndSpawnEvent evt)
        {
            m_PlayerHealth = evt.LoadManager.PlayerController.Health;
            m_EnemyHealth = evt.LoadManager.EnemyController.Health;
            Pause = false;
        }

        void Update()
        {
            if (!Pause && m_PlayerHealth && m_EnemyHealth)
            {
                string playerHealthAmount = m_PlayerHealth.CurrentHealth.ToString().Split(',')[0] + " <#afd9e9>/ " + m_PlayerHealth.MaxHealth.ToString();
                string enemyHealthAmount = m_EnemyHealth.CurrentHealth.ToString().Split(',')[0] + " /<#ffffff> " + m_EnemyHealth.MaxHealth.ToString();
                // update health bar value
                PlayerHealthSlider.value = m_PlayerHealth.GetRatio();
                PlayerHealthAmount.text = m_PlayerHealth.IsDead ? "0" : playerHealthAmount;

                EnemyHealthSlider.value = m_EnemyHealth.GetRatio();
                EnemyHealthAmount.text = m_EnemyHealth.IsDead ? "0" : enemyHealthAmount;
                //HealthFillImage.fillAmount = m_PlayerHealth.CurrentHealth / m_PlayerHealth.MaxHealth;
            }
        }
    }
}