using Platinum.Player;
using Platinum.Settings;
using TMPro;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.UI
{
    public class WorldspaceHealthBar : MonoBehaviour
    {
        [Tooltip("Health component to track")] public Health Health;
        [Tooltip("Actor component to track")] public Actor Actor;

        public TMP_Text NickName;

        public Color EnemyNickNameColor;
        public Color TeamNickNameColor;

        public GameObject TeamIcon;

        public GameObject HealthBar;

        [Tooltip("Image component displaying health left")]
        public Image HealthBarImage;

        [Tooltip("The floating healthbar pivot transform")]
        public Transform HealthBarPivot;

        [Tooltip("Whether the health bar is visible when at full health or not")]
        public bool HideFullHealthBar = true;

        private int Affiliation = 0;
        private int PlayerAffiliation = 0;
        private PlayerController PlayerController;
        private Transform targetLookAt;
        private LoadManager m_LoadManager;

        private void Awake()
        {
            EventManager.AddListener<EndSpawnEvent>(OnPlayerSpawnEvent);
        }

        private void OnPlayerSpawnEvent(EndSpawnEvent evt)
        {
            m_LoadManager = evt.LoadManager;

            targetLookAt = Camera.main.transform;

            Activate();
        }

        private void Activate()
        {
            PlayerController = m_LoadManager.PlayerController;
            Affiliation = Actor.Affiliation;
            PlayerAffiliation = PlayerController.Actor.Affiliation;
            NickName.text = Actor.NickName;

            if (PlayerController)
            {
                HealthBarPivot.gameObject.SetActive(false);
            }
        }

        void Update()
        {
            if (TeamIcon.activeSelf && PlayerAffiliation != Affiliation)
            {
                TeamIcon.SetActive(false);
                NickName.gameObject.SetActive(false);
                NickName.color = EnemyNickNameColor;
            }
            else
            {
                NickName.color = TeamNickNameColor;
            }

            // update health bar value
            HealthBarImage.fillAmount = Health.CurrentHealth / Health.MaxHealth;

            // rotate health bar to face the camera/player
            HealthBarPivot.LookAt(targetLookAt);

            // hide health bar if needed
            if (HideFullHealthBar)
                HealthBar.gameObject.SetActive(HealthBarImage.fillAmount != 1);
        }

        public void DisableTeamIcon()
        {

        }

        private void OnDestroy()
        {

            EventManager.RemoveListener<EndSpawnEvent>(OnPlayerSpawnEvent);
        }
    }
}