using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MRCrisisTrainer.Acts.Act3
{
    public class Act3FeedbackUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text promptLabel;
        [SerializeField] private TMP_Text silenceTimerLabel;
        [SerializeField] private Image silenceBar;
        [SerializeField] private TMP_Text strikesLabel;
        [SerializeField] private Color barNormal = new Color(0.2f, 0.7f, 1f);
        [SerializeField] private Color barWarning = new Color(1f, 0.8f, 0.2f);

        public void SetPrompt(string text)
        {
            if (promptLabel != null) promptLabel.text = text;
        }

        public void SetSilenceProgress(float normalized, float remainingSeconds)
        {
            if (silenceBar != null)
            {
                silenceBar.fillAmount = normalized;
                silenceBar.color = remainingSeconds < 10f ? barWarning : barNormal;
            }
            if (silenceTimerLabel != null)
            {
                silenceTimerLabel.text = $"{Mathf.CeilToInt(remainingSeconds)} s ciszy";
            }
        }

        public void SetStrikes(int strikes, int max)
        {
            if (strikesLabel != null)
            {
                strikesLabel.text = $"Hałas: {strikes} / {max}";
                strikesLabel.color = strikes >= max ? Color.red : Color.white;
            }
        }

        public void HideSilenceUI()
        {
            if (silenceBar != null) silenceBar.gameObject.SetActive(false);
            if (silenceTimerLabel != null) silenceTimerLabel.gameObject.SetActive(false);
            if (strikesLabel != null) strikesLabel.gameObject.SetActive(false);
        }

        public void ShowSilenceUI()
        {
            if (silenceBar != null) silenceBar.gameObject.SetActive(true);
            if (silenceTimerLabel != null) silenceTimerLabel.gameObject.SetActive(true);
            if (strikesLabel != null) strikesLabel.gameObject.SetActive(true);
        }
    }
}
