using System.Collections;
using UnityEngine;
using MRCrisisTrainer.XR;

namespace MRCrisisTrainer.Acts.Act3
{
    /// <summary>
    /// Przejście passthrough → pełne VR z fade. Po zakończeniu uruchamia callback.
    /// </summary>
    public class VRModeTransitioner : MonoBehaviour
    {
        [SerializeField] private Camera xrCamera;
        [SerializeField] private CanvasGroup fadeOverlay;
        [SerializeField] private float duration = 1.2f;
        [SerializeField] private Color fadeColor = Color.black;

        public IEnumerator TransitionToFullVR()
        {
            // Fade to black
            yield return Fade(0f, 1f);

            // Disable passthrough
            if (PassthroughController.Instance != null)
            {
                PassthroughController.Instance.DisablePassthrough();
            }
            else if (xrCamera != null)
            {
                xrCamera.clearFlags = CameraClearFlags.Skybox;
            }

            yield return new WaitForSeconds(0.2f);

            // Fade back in - now in VR environment
            yield return Fade(1f, 0f);
        }

        public IEnumerator TransitionToPassthrough()
        {
            yield return Fade(0f, 1f);
            if (PassthroughController.Instance != null)
                PassthroughController.Instance.EnablePassthrough();
            yield return Fade(1f, 0f);
        }

        private IEnumerator Fade(float from, float to)
        {
            if (fadeOverlay == null) yield break;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                fadeOverlay.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            fadeOverlay.alpha = to;
        }
    }
}
