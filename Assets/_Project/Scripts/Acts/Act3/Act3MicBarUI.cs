using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MRCrisisTrainer.Acts.Act3
{
    /// <summary>
    /// Pionowy pasek poziomu mikrofonu pokazywany w fazie ciszy. Zielony gdy cicho,
    /// czerwony gdy gracz przekracza próg hałasu. Pokazuje też pozostały czas ciszy.
    /// </summary>
    public class Act3MicBarUI : MonoBehaviour
    {
        [SerializeField] private MicrophoneInputProvider mic;
        [SerializeField] private Image fill;            // Image typu Filled, Vertical
        [SerializeField] private TMP_Text label;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private float volumeThreshold = 0.05f;
        [SerializeField] private float maxVolume = 0.22f;

        private readonly Color quiet = new Color(0.25f, 0.9f, 0.4f);
        private readonly Color loud = new Color(0.95f, 0.2f, 0.15f);
        private bool active;
        private float remaining;
        private float flash;

        void Awake()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            Hide();
        }

        public void Show()
        {
            active = true;
            gameObject.SetActive(true);
            var cam = Camera.main;
            if (cam != null)
            {
                var fc = GetComponent<MRCrisisTrainer.UI.FaceCamera>(); if (fc != null) fc.enabled = false;   // jako dziecko kamery już patrzy na gracza
                transform.SetParent(cam.transform, true);   // PRZYCZEP DO KAMERY → zawsze blisko gracza (nie na biurku)
                transform.localPosition = new Vector3(0.26f, -0.12f, 0.62f);   // prawy-dolny róg widoku, blisko
                transform.localRotation = Quaternion.identity;
            }
            if (group != null) group.alpha = 1f;
        }

        public void Hide()
        {
            active = false;
            if (group != null) group.alpha = 0f;
        }

        public void SetRemaining(float secondsLeft, float normalized)
        {
            remaining = secondsLeft;
        }

        public void FlashAlarm() => flash = 1f;

        void Update()
        {
            if (!active) return;

            float vol = mic != null ? mic.CurrentVolume : 0f;
            float norm = Mathf.Clamp01(vol / Mathf.Max(0.001f, maxVolume));
            bool tooLoud = vol > volumeThreshold;

            if (fill != null)
            {
                fill.fillAmount = Mathf.Lerp(fill.fillAmount, norm, Time.deltaTime * 12f);
                Color c = tooLoud ? loud : quiet;
                if (flash > 0f) { c = Color.Lerp(c, loud, flash); flash = Mathf.Max(0f, flash - Time.deltaTime * 2f); }
                fill.color = c;
            }

            if (label != null)
            {
                int s = Mathf.CeilToInt(remaining);
                label.text = tooLoud ? "ZA GŁOŚNO!" : $"CISZA  {s / 60}:{(s % 60):00}";
                label.color = tooLoud ? loud : new Color(0.9f, 0.9f, 0.95f);
            }
        }
    }
}
