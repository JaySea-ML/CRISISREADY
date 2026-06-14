using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MRCrisisTrainer.Logging;

namespace MRCrisisTrainer.Research
{
    /// <summary>
    /// Prezentuje kwestionariusz w VR: jedno pytanie na ekranie z przyciskami skali.
    /// Zapisuje odpowiedzi do JSONL. Po ukończeniu wywołuje OnCompleted.
    /// </summary>
    public class QuestionnaireController : MonoBehaviour
    {
        [SerializeField] private QuestionnaireDefinition definition;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text questionLabel;
        [SerializeField] private TMP_Text progressLabel;
        [SerializeField] private TMP_Text leftAnchorLabel;
        [SerializeField] private TMP_Text rightAnchorLabel;
        [SerializeField] private Transform buttonContainer;
        [SerializeField] private GameObject scaleButtonPrefab;

        public event Action<QuestionnaireDefinition, Dictionary<string, int>> OnCompleted;

        private int currentIndex;
        private readonly Dictionary<string, int> answers = new Dictionary<string, int>();
        private readonly List<Button> scaleButtons = new List<Button>();

        public QuestionnaireDefinition Definition => definition;

        void Start()
        {
            if (definition == null) { Debug.LogError("[Questionnaire] No definition."); return; }
            BuildScaleButtons();
            if (titleLabel != null) titleLabel.text = definition.title;
            ShowQuestion(0);
        }

        public void SetDefinition(QuestionnaireDefinition def)
        {
            definition = def;
        }

        private (int min, int max) ScaleRange()
        {
            switch (definition.scaleType)
            {
                case ScaleType.Likert5: return (1, 5);
                case ScaleType.Likert7:
                case ScaleType.Likert7Semantic: return (1, 7);
                case ScaleType.Scale0to20: return (0, 20);
                case ScaleType.Severity0to3: return (0, 3);
                case ScaleType.SAM1to9: return (1, 9);
                default: return (1, 7);
            }
        }

        private void BuildScaleButtons()
        {
            if (buttonContainer == null || scaleButtonPrefab == null) return;
            var (min, max) = ScaleRange();
            // dla 0-20 (NASA-TLX) używamy kroków co 2 żeby zmieścić 11 przycisków
            int step = (max - min) > 10 ? 2 : 1;
            for (int v = min; v <= max; v += step)
            {
                int value = v;
                var go = Instantiate(scaleButtonPrefab, buttonContainer);
                go.SetActive(true);
                var label = go.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = value.ToString();
                var btn = go.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.AddListener(() => Answer(value));
                    scaleButtons.Add(btn);
                }
            }
        }

        private void ShowQuestion(int index)
        {
            currentIndex = index;
            if (index >= definition.items.Count) { Finish(); return; }
            var q = definition.items[index];
            if (questionLabel != null) questionLabel.text = q.text;
            if (progressLabel != null) progressLabel.text = $"{index + 1} / {definition.items.Count}";
            if (leftAnchorLabel != null) leftAnchorLabel.text = q.leftAnchor;
            if (rightAnchorLabel != null) rightAnchorLabel.text = q.rightAnchor;
        }

        private void Answer(int value)
        {
            var q = definition.items[currentIndex];
            int stored = value;
            if (q.reverseScored)
            {
                var (min, max) = ScaleRange();
                stored = (max + min) - value;
            }
            answers[q.id] = stored;
            JSONLLogger.Instance?.LogEvent("questionnaire_answer", new Dictionary<string, object>
            {
                { "questionnaire", definition.questionnaireId },
                { "item", q.id },
                { "raw", value },
                { "scored", stored }
            });
            ShowQuestion(currentIndex + 1);
        }

        private void Finish()
        {
            int total = 0;
            foreach (var v in answers.Values) total += v;
            JSONLLogger.Instance?.LogEvent("questionnaire_completed", new Dictionary<string, object>
            {
                { "questionnaire", definition.questionnaireId },
                { "n_items", answers.Count },
                { "raw_sum", total }
            });
            if (questionLabel != null) questionLabel.text = "Dziękujemy za odpowiedzi.";
            if (progressLabel != null) progressLabel.text = "Ukończono";
            OnCompleted?.Invoke(definition, new Dictionary<string, int>(answers));
        }
    }
}
