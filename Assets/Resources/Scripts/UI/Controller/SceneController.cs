using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Platinum.Settings
{
    public class SceneController : MonoBehaviour
    {

        [Header("General References")]
        public SettingsManager SettingsManager;
        public GameObject AllManagers;
        public LoadingScreenController LoadingScreenController;
        public string CurrentScene { get; private set; }

        public static SceneController Instance { get; private set; }
        private bool FirstStart;

        private void Awake()
        {
            SceneController[] conductors = FindObjectsOfType<SceneController>();
            if (conductors.Length > 1)
            {
                Destroy(this.gameObject);
            }
            else
            {
                Instance = this;
                SceneManager.sceneLoaded += OnLevelFinishedLoading;
                AllManagers.SetActive(true);
                DontDestroyOnLoad(this);
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnLevelFinishedLoading;
        }

        public void OnLevelFinishedLoading(Scene scene, LoadSceneMode mode)
        {
            if (FirstStart)
            {
                CurrentScene = scene.name;
                LoadingScreenController.OnLevelFinishedLoading();
            }
            FirstStart = true;
        }
    }
}
