using Platinum.Player;

namespace Unity.FPS.Gameplay
{
    public class JetpackPickup : Pickup
    {
        protected override void OnPicked(PlayerController byPlayer)
        {
            var jetpack = byPlayer.GetComponent<Jetpack>();
            if (!jetpack)
                return;

            if (jetpack.TryUnlock())
            {
                PlayPickupFeedback();
                Destroy(gameObject);
            }
        }
    }
}