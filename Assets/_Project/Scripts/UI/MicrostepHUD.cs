using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MRCrisisTrainer.Config;
using MRCrisisTrainer.Gameplay;

namespace MRCrisisTrainer.UI
{
    /// <summary>
    /// World-space HUD pokazujący bieżący mikrokrok, podpowiedź, postęp i feedback.
    /// Podłącza się do ScenarioRunner i reaguje na jego eventy.
    /// </summary>
    public class MicrostepHUD : MonoBehaviour
    {
        [SerializeField] private ScenarioRunner runner;
        [SerializeField] private TMP_Text stepLabel;
        [SerializeField] private TMP_Text hintLabel;
        [SerializeField] private TMP_Text progressLabel;
        [SerializeField] private Image progressBar;
        [SerializeField] private TMP_Text scoreLabel;

        [SerializeField] private Color colorOk = new Color(0.2f, 0.9f, 0.3f);
        [SerializeField] private Color colorHint = new Color(1f, 0.8f, 0.2f);
        [SerializeField] private Color colorFail = new Color(1f, 0.3f, 0.3f);

        private int stepIndex;
        private int totalSteps;
        private int runningScore;

        void OnEnable()
        {
            if (runner == null) runner = FindRunner();
            if (runner == null) return;
            runner.OnStepActivated += HandleActivated;
            runner.OnStepScored += HandleScored;
            runner.OnHint += HandleHint;
            runner.OnScenarioFinished += HandleFinished;
            if (runner.Scenario != null) totalSteps = runner.Scenario.microsteps.Count;
        }

        void OnDisable()
        {
            if (runner == null) return;
            runner.OnStepActivated -= HandleActivated;
            runner.OnStepScored -= HandleScored;
            runner.OnHint -= HandleHint;
            runner.OnScenarioFinished -= HandleFinished;
        }

        private ScenarioRunner FindRunner()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<ScenarioRunner>();
#else
            return Object.FindObjectOfType<ScenarioRunner>();
#endif
        }

        private void HandleActivated(Microstep step)
        {
            stepIndex++;
            if (stepLabel != null) stepLabel.text = step.label;
            if (hintLabel != null) hintLabel.text = "";
            if (progressLabel != null) progressLabel.text = $"Krok {stepIndex}/{totalSteps}";
            if (progressBar != null)
            {
                progressBar.fillAmount = totalSteps > 0 ? (float)(stepIndex - 1) / totalSteps : 0;
                progressBar.color = colorOk;
            }
        }

        private void HandleHint(Microstep step)
        {
            if (hintLabel != null)
            {
                hintLabel.text = "> " + step.hintText;
                hintLabel.color = colorHint;
            }
        }

        private void HandleScored(Microstep step, MicrostepScore score)
        {
            runningScore += Mathf.Max(0, (int)score);
            if (scoreLabel != null) scoreLabel.text = $"Punkty: {runningScore}";
            if (progressBar != null)
            {
                progressBar.color = score == MicrostepScore.Failed ? colorFail
                    : score == MicrostepScore.WithHint ? colorHint : colorOk;
                progressBar.fillAmount = totalSteps > 0 ? (float)stepIndex / totalSteps : 1;
            }
        }

        private void HandleFinished(int total, int max)
        {
            if (stepLabel != null) stepLabel.text = "Scenariusz ukończony";
            if (hintLabel != null) hintLabel.text = "";
            if (progressLabel != null) progressLabel.text = $"Wynik: {total}/{max}";
            if (progressBar != null) { progressBar.fillAmount = 1; progressBar.color = colorOk; }
        }

        /// <summary>Pozwala ustawić prompt z zewnątrz (np. dla kroków bez detektora).</summary>
        public void SetCustomPrompt(string text)
        {
            if (stepLabel != null) stepLabel.text = text;
        }
    }
}
