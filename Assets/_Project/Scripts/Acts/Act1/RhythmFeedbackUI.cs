using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MRCrisisTrainer.Config;

namespace MRCrisisTrainer.Acts.Act1
{
    public class RhythmFeedbackUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private BLSConfig config;
        [SerializeField] private Image tempoBar;
        [SerializeField] private TMP_Text bpmLabel;
        [SerializeField] private TMP_Text counterLabel;
        [SerializeField] private TMP_Text promptLabel;

        [Header("Colors")]
        [SerializeField] private Color colorOK = new Color(0.2f, 0.9f, 0.3f);
        [SerializeField] private Color colorTooSlow = new Color(0.2f, 0.5f, 1f);
        [SerializeField] private Color colorTooFast = new Color(1f, 0.3f, 0.3f);
        [SerializeField] private Color colorNeutral = new Color(0.7f, 0.7f, 0.7f);

        public void SetPrompt(string text)
        {
            if (promptLabel != null) promptLabel.text = text;
        }

        public void SetProgress(int current, int target)
        {
            if (counterLabel != null) counterLabel.text = $"{current} / {target}";
        }

        public void SetBPM(float bpm, TempoClassification classification)
        {
            if (bpmLabel != null) bpmLabel.text = bpm > 0 ? $"{Mathf.RoundToInt(bpm)} BPM" : "-- BPM";

            if (tempoBar != null && config != null)
            {
                tempoBar.color = ClassificationColor(classification);
                if (bpm > 0)
                {
                    float normalized = Mathf.Clamp01((bpm - 60f) / 120f);
                    tempoBar.fillAmount = normalized;
                }
                else
                {
                    tempoBar.color = colorNeutral;
                    tempoBar.fillAmount = 0.5f;
                }
            }
        }

        private Color ClassificationColor(TempoClassification c)
        {
            switch (c)
            {
                case TempoClassification.OK: return colorOK;
                case TempoClassification.TooSlow: return colorTooSlow;
                case TempoClassification.TooFast: return colorTooFast;
                default: return colorNeutral;
            }
        }
    }
}
