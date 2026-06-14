using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace MRCrisisTrainer.Core
{
    /// <summary>
    /// Prosi o uprawnienia systemowe na Androidzie (Quest): mikrofon (Akt III - mowa/cisza).
    /// Passthrough/scene obsługuje Meta XR SDK osobno.
    /// </summary>
    public class PermissionRequester : MonoBehaviour
    {
        void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                Permission.RequestUserPermission(Permission.Microphone);
            }
#endif
        }
    }
}
