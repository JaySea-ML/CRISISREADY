using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MRCrisisTrainer.Core
{
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        [SerializeField] private float fadeDuration = 0.5f;
        private CanvasGroup fadeOverlay;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void RegisterFadeOverlay(CanvasGroup overlay)
        {
            fadeOverlay = overlay;
        }

        public void LoadActAdditive(string sceneName, string previousActScene = null)
        {
            StartCoroutine(LoadCoroutine(sceneName, previousActScene));
        }

        private IEnumerator LoadCoroutine(string sceneName, string previousActScene)
        {
            yield return Fade(0f, 1f);

            var asyncLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            while (!asyncLoad.isDone)
            {
                yield return null;
            }

            var newScene = SceneManager.GetSceneByName(sceneName);
            if (newScene.IsValid())
            {
                SceneManager.SetActiveScene(newScene);
            }

            if (!string.IsNullOrEmpty(previousActScene))
            {
                var prevScene = SceneManager.GetSceneByName(previousActScene);
                if (prevScene.IsValid())
                {
                    yield return SceneManager.UnloadSceneAsync(prevScene);
                }
            }

            yield return Fade(1f, 0f);
        }

        private IEnumerator Fade(float from, float to)
        {
            if (fadeOverlay == null) yield break;

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                fadeOverlay.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
                yield return null;
            }
            fadeOverlay.alpha = to;
        }
    }
}
