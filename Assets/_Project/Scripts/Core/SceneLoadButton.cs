using UnityEngine;

namespace MRCrisisTrainer.Core
{
    /// <summary>Pomocnik: ładuje wskazaną scenę po wywołaniu Load() (np. z przycisku UI).</summary>
    public class SceneLoadButton : MonoBehaviour
    {
        public string sceneName;

        public void Load()
        {
            string current = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (SceneLoader.Instance != null)
                SceneLoader.Instance.LoadActAdditive(sceneName, current);
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        }
    }
}
