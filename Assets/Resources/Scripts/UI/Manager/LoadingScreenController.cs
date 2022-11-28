using TMPro;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Platinum.Menu;
using UnityEngine.Serialization;

namespace Platinum.Settings
{

    public class LoadingScreenController : MonoBehaviour
    {
        [FormerlySerializedAs("MenuBackground")] [Header("MainMenu")] 
        public Sprite[] MenuBackgrounds;
        
        [Header("References")]
        public AnimationClip ScreenVisibility;
        public AnimationClip ScreenUnvisibility;

        public float WaitLoadScene = 1f;
        [Range(0.1f, 1f)]
        public float BarFillSpeed = 0.5f;
        [Range(0, 0.9f)]
        public float BarFillStartLoadScene = 0.95f;
        public TMP_Text ValueLoading;
        public string TitleValueLoading;
        public Image LoadingBackground;
        public Slider LoadingBar;
        public Result EndLevel { get; private set; } = Result.None;
        //PUBLIC GET
        public int LevelSeed { get; private set; }
        //PRIVATE
        private MapSelect CurrentMap;
        private AsyncOperation loadingSceneOperation;
        public bool LoadCurrentScene = false;
        private Animator ScreenAnimator;
        private float time = 0f;
        private SceneController m_SceneController;

        void Awake() //запускаем самый первый процесс
        {
            m_SceneController = GetComponentInParent<SceneController>();
            ScreenAnimator = GetComponent<Animator>();
        }

        public void OnLevelFinishedLoading()
        {
            ScreenAnimator.Play("ScreenUnvisibility");
        }

        public int SetSeed(int seed)
        {
            return LevelSeed = seed;
        }
        
        public void LoadMainMenu()
        {
            CurrentMap = new MapSelect();
            CurrentMap.SetSeed(0);
            CurrentMap.SceneName = GameSettings.MAINMENU_SCENE;
            CurrentMap.SetBackgrounds(MenuBackgrounds);
            
            LoadingBackground.sprite = CurrentMap.GetRandomBackground();
            LevelSeed = CurrentMap.Seed;

            ScreenAnimator.Play("ScreenVisibility");
        }

        public void LoadScene(MapSelect map)
        {
            CurrentMap = map;
            LoadingBackground.sprite = map.GetRandomBackground();
            LevelSeed = map.Seed;

            ScreenAnimator.Play("ScreenVisibility");
        }

        public void RestartScene()
        {
            LoadingBackground.sprite = CurrentMap.GetRandomBackground();
            LevelSeed = CurrentMap.Seed;

            ScreenAnimator.Play("ScreenVisibility");
        }
        
        public void EndScreenVisibility(StateScreen screen)
        {
            switch (screen)
            {
                case (StateScreen.Visibly):
                    loadingSceneOperation = SceneManager.LoadSceneAsync(CurrentMap.SceneName);
                    loadingSceneOperation.allowSceneActivation = false;
                    LoadCurrentScene = true;
                    break;
                case (StateScreen.Unvisibly):
                    EndLoadScene();
                    LoadCurrentScene = false;
                    break;
            }
        }

        private void EndLoadScene()
        {
            Debug.Log("EndLoadingPanel: " + CurrentMap.SceneName);
        }

        void Update()
        {
            if (LoadCurrentScene)
            {
                if (time < loadingSceneOperation.progress)
                {
                    //print("Load: " + time + "/" + loadingSceneOperation.progress + "/" + BarFillStartLoadScene);
                    time += Time.deltaTime * BarFillSpeed;
                    LoadingBar.value = time;
                    ValueLoading.text = (Mathf.RoundToInt(time * 100)).ToString() + "%";
                }
                if (time > BarFillStartLoadScene)
                {
                    loadingSceneOperation.allowSceneActivation = true;
                }
            }
        }
    }

}


