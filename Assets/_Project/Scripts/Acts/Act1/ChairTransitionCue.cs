using System.Collections;
using UnityEngine;

namespace MRCrisisTrainer.Acts.Act1
{
    public class ChairTransitionCue : MonoBehaviour
    {
        [SerializeField] private Renderer[] chairRenderers;
        [SerializeField] private Light glowLight;
        [SerializeField] private Color glowColor = new Color(1f, 0.15f, 0.1f);
        [SerializeField] private float pulseHz = 1.2f;
        [SerializeField] private float fadeInDuration = 1.0f;

        private bool active;

        public void Activate()
        {
            if (active) return;
            active = true;
            StartCoroutine(FadeIn());
        }

        private IEnumerator FadeIn()
        {
            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeInDuration;
                ApplyColor(glowColor * t);
                yield return null;
            }
            while (active)
            {
                float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * pulseHz * Mathf.PI * 2f);
                ApplyColor(glowColor * pulse);
                yield return null;
            }
        }

        private void ApplyColor(Color c)
        {
            if (chairRenderers != null)
            {
                foreach (var r in chairRenderers)
                {
                    if (r == null) continue;
                    foreach (var mat in r.materials)
                    {
                        if (mat.HasProperty("_EmissionColor"))
                        {
                            mat.EnableKeyword("_EMISSION");
                            mat.SetColor("_EmissionColor", c);
                        }
                    }
                }
            }
            if (glowLight != null)
            {
                glowLight.color = c;
                glowLight.intensity = c.maxColorComponent * 4f;
            }
        }
    }
}
