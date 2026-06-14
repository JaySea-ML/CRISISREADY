using UnityEngine;

namespace MRCrisisTrainer.UI
{
    /// <summary>Delikatne pulsowanie skali (np. przycisk GRAJ) + opcjonalny lekki „oddech" pozycji.
    /// Subtelny, profesjonalny akcent życia w menu. Działa na unscaledTime (menu może pauzować czas).</summary>
    public class UIPulse : MonoBehaviour
    {
        [SerializeField] private float scaleAmplitude = 0.04f;
        [SerializeField] private float speed = 2.2f;
        [SerializeField] private float bobAmplitude = 0f;   // ruch w górę/dół (świat), 0 = wyłączony

        private Vector3 baseScale;
        private Vector3 basePos;
        private bool init;

        void OnEnable()
        {
            if (!init) { baseScale = transform.localScale; basePos = transform.localPosition; init = true; }
        }

        void Update()
        {
            float t = Time.unscaledTime * speed;
            transform.localScale = baseScale * (1f + scaleAmplitude * Mathf.Sin(t));
            if (bobAmplitude > 0f)
                transform.localPosition = basePos + Vector3.up * (bobAmplitude * Mathf.Sin(t * 0.7f));
        }
    }
}
