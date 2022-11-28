using UnityEngine;

namespace Platinum.Menu
{
    public class MapSelect : MonoBehaviour
    {
        public string SceneName;
        public GameObject MapPressedIcon;
        public Sprite[] MapBackgrounds;
        public bool buttonPressed { get; private set; }
        public int Seed { get; private set; }

        public void SetBackgrounds(Sprite[] backgrounds)
        {
            MapBackgrounds = backgrounds;
        }
        
        public void SetSeed(int newSeed)
        {
            Seed = newSeed;
        }
        
        public Sprite GetRandomBackground()
        {
            int indexBackground = Random.Range(0, MapBackgrounds.Length);
            return MapBackgrounds[indexBackground];
        }

        public void SetStatePressed(bool state)
        {
            buttonPressed = state;
            MapPressedIcon.SetActive(state);
        }
    }
}
