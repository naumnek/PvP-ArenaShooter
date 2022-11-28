using Platinum.Player;
using Platinum.Settings;
using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Platinum.Menu
{
    public class SwitchItemMenu : MonoBehaviour
    {
        [Header("General")]
        [Tooltip("Root GameObject of the menu used to toggle its activation")]
        public GameObject MenuRoot;

        [Header("SwitchItems List Panel")]
        public GameObject ItemListContent;
        public Transform GridLayotMachine;
        public Transform GridLayotShotgun;
        public Transform GridLayotRifle;
        public GameObject WeaponsElementPrefab;
        public Button CloseItemMenuButton;
        public List<WeaponController> PaidWeapons { get; private set; }
        public UnityAction WeaponLimitReached;

        private Items[] ItemList;

        private Dictionary<string, Items> cachedRoomList;
        private Dictionary<string, GameObject> roomListEntries;

        public SettingsManager SettingsManager { get; private set; }
        private PlayerController m_PlayerController;
        private PlayerWeaponsManager m_PlayerWeaponsManager;
        private LoadManager m_LoadManager;
        private GameFlowManager m_FlowManager;
        private bool IsActive = true;
        private List<WeaponAttributes> m_WeaponsAttributes;

        private static SwitchItemMenu instance;
        public static SwitchItemMenu GetInstance() => instance;

        private void OnDestroy()
        {
            EventManager.RemoveListener<EndSpawnEvent>(OnEndSpawnEvent);
            EventManager.AddListener<RefreshMatchEvent>(OnRefreshMatchEvent);
        }

        private void Awake()
        {
            instance = this;

            EventManager.AddListener<EndSpawnEvent>(OnEndSpawnEvent);
            EventManager.AddListener<RefreshMatchEvent>(OnRefreshMatchEvent);

            m_WeaponsAttributes = new List<WeaponAttributes>();
            cachedRoomList = new Dictionary<string, Items>();
            roomListEntries = new Dictionary<string, GameObject>();
            PaidWeapons = new List<WeaponController> { };
        }

        private void OnEndSpawnEvent(EndSpawnEvent evt)
        {
            m_LoadManager = evt.LoadManager;

            m_PlayerController = m_LoadManager.PlayerController;
            m_FlowManager = m_LoadManager.GameFlowManager;
            m_PlayerWeaponsManager = m_LoadManager.PlayerWeaponsManager;
            SettingsManager = m_LoadManager.SettingsManager;

            ItemList = SettingsManager.GetRequredItems();

            for (int i = 0; i < ItemList.Length; i++)
            {
                m_WeaponsAttributes.Add(ItemList[i].Attributes);
            }

            Initialize();
        }

        private void Initialize()
        {
            //WeaponsLists.Add(Instantiate(WeaponsListPrefab, ItemListContent.transform).transform);c
            OnRoomListUpdate(ItemList);
        }

        private void OnRefreshMatchEvent(RefreshMatchEvent evt)
        {
            ResetItems();
            StartCoroutine(MaxWaitPaid());
        }

        public void OnRoomListUpdate(Items[] itemList)
        {
            ClearRoomListView();

            UpdateCachedRoomList(itemList);
            UpdateRoomListView();
        }

        private void ClearRoomListView()
        {
            foreach (GameObject entry in roomListEntries.Values)
            {
                DestroyImmediate(entry.gameObject);
            }

            roomListEntries.Clear();
        }

        public void ResetItems()
        {
            IsActive = true;

            OnRoomListUpdate(ItemList);
            //m_FlowManager.SetItemListSwitch(true);
        }

        private void UpdateCachedRoomList(Items[] roomList)
        {
            foreach (Items info in roomList)
            {
                // Remove menu from cached menu list if it got closed, became invisible or was marked as removed
                if (info.RemovedFromList || info.IsUnvisibly)
                {
                    if (cachedRoomList.ContainsKey(info.Name()))
                    {
                        cachedRoomList.Remove(info.Name());
                    }

                    continue;
                }

                // Update cached menu info
                if (cachedRoomList.ContainsKey(info.Name()))
                {
                    cachedRoomList[info.Name()] = info;
                }
                // Add new menu info to cache
                else
                {
                    cachedRoomList.Add(info.Name(), info);
                }
            }
        }

        private void UpdateRoomListView()
        {
            foreach (Items info in cachedRoomList.Values)
            {
                Transform weaponGridSlot = GridLayotMachine;
                switch (info.Weapon.Type)
                {
                    case (WeaponType.Machine):
                        weaponGridSlot = GridLayotMachine;
                        break;
                    case (WeaponType.ShotGun):
                        weaponGridSlot = GridLayotShotgun;
                        break;
                    case (WeaponType.Rifle):
                        weaponGridSlot = GridLayotRifle;
                        break;
                }
                GameObject entry = Instantiate(WeaponsElementPrefab, weaponGridSlot);
                //entry.transform.localScale = Vector3.one;

                ItemListSwitch ItemListSwitch = entry.GetComponent<ItemListSwitch>();
                ItemListSwitch.ItemChooseButton.onClick.AddListener
                    (delegate () { m_FlowManager.AudioWeaponButtonClick.Play(); });

                ItemListSwitch.Initialize(info, this, IsActive);

                roomListEntries.Add(info.Name(), entry);
            }
        }

        public void GiveItemPlayer(Items itemInfo, ItemListSwitch itemSwitch)
        {
            if (!itemInfo.IsPaid && !itemInfo.IsBlocked && !m_PlayerController.Health.IsDead)
            {
                if (IsActive)
                {
                    for (int i = 0; i < PaidWeapons.Count; i++)
                    {
                        m_PlayerWeaponsManager.RemoveWeapon(PaidWeapons[i]);
                        PaidWeapons.RemoveAt(i);
                    }
                    foreach (Items info in ItemList)
                    {
                        info.Reset();
                    }
                }

                PaidWeapons.Add(itemInfo.Weapon);
                CloseItemMenuButton.gameObject.SetActive(true);
                m_PlayerWeaponsManager.AddWeapon(itemInfo.Weapon.WeaponName);

                itemInfo.OnPaid();

                IsActive = PaidWeapons.Count < GameSettings.MAX_PAID_WEAPONS;
                if(!IsActive) WeaponLimitReached?.Invoke();

                OnRoomListUpdate(ItemList);
            }
        }

        private IEnumerator MaxWaitPaid()
        {
            yield return new WaitForSeconds(GameSettings.TIME_TO_BLOCK_SELECTION);
            IsActive = false;
            OnRoomListUpdate(ItemList);
        }

        public void CloseItemMenu()
        {
            m_FlowManager.SetItemListSwitch(false);
        }
    }
}