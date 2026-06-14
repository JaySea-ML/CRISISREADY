using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MRCrisisTrainer.Acts.Act2
{
    public class Act2FeedbackUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text promptLabel;
        [SerializeField] private TMP_Text skidAngleLabel;
        [SerializeField] private Image skidIndicator;
        [SerializeField] private Color correctColor = new Color(0.2f, 0.9f, 0.3f);
        [SerializeField] private Color wrongColor = new Color(1f, 0.3f, 0.3f);
        [SerializeField] private Color neutralColor = new Color(0.7f, 0.7f, 0.7f);

        public void SetPrompt(string text)
        {
            if (promptLabel != null) promptLabel.text = text;
        }

        public void SetSkidAngle(float deg)
        {
            if (skidAngleLabel != null) skidAngleLabel.text = $"{Mathf.RoundToInt(deg)}°";
        }

        public void SetSteeringFeedback(SteeringFeedback feedback)
        {
            if (skidIndicator == null) return;
            switch (feedback)
            {
                case SteeringFeedback.Correct: skidIndicator.color = correctColor; break;
                case SteeringFeedback.Wrong: skidIndicator.color = wrongColor; break;
                default: skidIndicator.color = neutralColor; break;
            }
        }
    }
}
