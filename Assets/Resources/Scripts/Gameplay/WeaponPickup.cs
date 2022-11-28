using Platinum.Player;
using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    public class WeaponPickup : Pickup
    {
        [Tooltip("The prefab for the weapon that will be added to the player on pickup")]
        public WeaponController WeaponPrefab;

        protected override void Start()
        {
            base.Start();

            // Set all children layers to default (to prefent seeing weapons through meshes)
            foreach (Transform t in GetComponentsInChildren<Transform>())
            {
                if (t != transform)
                    t.gameObject.layer = 0;
            }
        }

        protected override void OnPicked(PlayerController byPlayer)
        {
            PlayerWeaponsManager playerWeaponsManager = byPlayer.LoadManager.PlayerWeaponsManager;
            if (playerWeaponsManager)
            {
                if (playerWeaponsManager.AddWeapon(WeaponPrefab.WeaponName))
                {
                    // Handle auto-switching to weapon if no weapons currently
                    if (playerWeaponsManager.activeWeapon == null || playerWeaponsManager.AutoSwitchNewWeapon)
                    {
                        playerWeaponsManager.SwitchWeapon(true);
                    }

                    PlayPickupFeedback();
                    Destroy(gameObject);
                }
            }
        }
    }
}