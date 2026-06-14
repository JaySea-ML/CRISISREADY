using UnityEngine;

namespace MRCrisisTrainer.XR
{
    /// <summary>
    /// Utrzymuje obiekt (np. zestaw passthrough: OVRManager + OVRPassthroughLayer) przy życiu przez
    /// WSZYSTKIE sceny i gwarantuje jeden egzemplarz. Dzięki temu warstwa passthrough rejestruje się raz
    /// i nie znika przy przejściu menu→gra (koniec numLayers:0 / czarnego ekranu po GRAJ).
    /// </summary>
    public class PersistAcrossScenes : MonoBehaviour
    {
        private static PersistAcrossScenes instance;

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
}
