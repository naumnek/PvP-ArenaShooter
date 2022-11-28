using Platinum.Player;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.UI;
using Platinum.Settings;

namespace Platinum.UI
{
    public class CrosshairManager : MonoBehaviour
    {
        public Image CrosshairImage;
        public Sprite NullCrosshairSprite;
        public float CrosshairUpdateshrpness = 5f;

        PlayerWeaponsManager m_PlayerWeaponsManager;
        bool m_WasPointingAtEnemy;
        bool m_WasHitEnemy;
        RectTransform m_CrosshairRectTransform;
        CrosshairData m_CrosshairDataDefault;
        CrosshairData m_CrosshairDataTarget;
        CrosshairData m_CrosshairDataHitTarget;

        CrosshairData m_CurrentCrosshair;
        Transform m_WeaponCamera;
        private bool ServerPause = true;

        private void Start()
        {
            m_PlayerWeaponsManager = LoadManager.Instance.PlayerWeaponsManager;

            OnWeaponChanged(m_PlayerWeaponsManager.activeWeapon);

            m_PlayerWeaponsManager.OnSwitchedToWeapon += OnWeaponChanged;
            m_WeaponCamera = m_PlayerWeaponsManager.WeaponCamera;

            ServerPause = false;
        }

        void Update()
        {
            if (ServerPause) return;
            UpdateCrosshairPointingAtEnemy(false);
            m_WasPointingAtEnemy = m_PlayerWeaponsManager.IsPointingAtEnemy;
            m_WasHitEnemy = m_PlayerWeaponsManager.IsHitEnemy;
        }

        bool PointingAtEnemy => m_PlayerWeaponsManager.IsPointingAtEnemy;
        bool HitEnemy => m_PlayerWeaponsManager.IsHitEnemy;

        void UpdateCrosshairPointingAtEnemy(bool force)
        {
            if (m_CrosshairDataDefault.CrosshairSprite == null) return;

            if (force || m_WasHitEnemy)
            {
                m_PlayerWeaponsManager.OnHitEnemy(false);
                SetCrosshair(m_CrosshairDataHitTarget);
            }
            if ((force || !m_WasPointingAtEnemy) && PointingAtEnemy && !m_WasHitEnemy)
            {
                SetCrosshair(m_CrosshairDataTarget);
            }
            else if ((force || m_WasPointingAtEnemy || m_WasHitEnemy) && !PointingAtEnemy && !HitEnemy)
            {
                SetCrosshair(m_CrosshairDataDefault);
            }

            CrosshairImage.color = Color.Lerp(CrosshairImage.color, m_CurrentCrosshair.CrosshairColor,
                Time.deltaTime * CrosshairUpdateshrpness);

            m_CrosshairRectTransform.sizeDelta = Mathf.Lerp(m_CrosshairRectTransform.sizeDelta.x,
                m_CurrentCrosshair.CrosshairSize,
                Time.deltaTime * CrosshairUpdateshrpness) * Vector2.one;
        }

        private void SetCrosshair(CrosshairData crosshair)
        {
            m_CurrentCrosshair = crosshair;
            CrosshairImage.sprite = m_CurrentCrosshair.CrosshairSprite;
            m_CrosshairRectTransform.sizeDelta = m_CurrentCrosshair.CrosshairSize * Vector2.one;
        }

        void OnWeaponChanged(WeaponController newWeapon)
        {
            if (newWeapon)
            {
                CrosshairImage.enabled = true;
                m_CrosshairDataDefault = newWeapon.CrosshairDataDefault;
                m_CrosshairDataTarget = newWeapon.CrosshairDataTargetInSight;
                m_CrosshairDataHitTarget = newWeapon.CrosshairDataHitTarget;
                m_CrosshairRectTransform = CrosshairImage.GetComponent<RectTransform>();
                DebugUtility.HandleErrorIfNullGetComponent<RectTransform, CrosshairManager>(m_CrosshairRectTransform,
                    this, CrosshairImage.gameObject);
            }
            else
            {
                if (NullCrosshairSprite)
                {
                    CrosshairImage.sprite = NullCrosshairSprite;
                }
                else
                {
                    CrosshairImage.enabled = false;
                }
            }

            UpdateCrosshairPointingAtEnemy(true);
        }
    }
}