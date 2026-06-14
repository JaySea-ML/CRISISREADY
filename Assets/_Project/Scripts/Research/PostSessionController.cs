using System.Collections.Generic;
using UnityEngine;
using TMPro;
using MRCrisisTrainer.Logging;

namespace MRCrisisTrainer.Research
{
    /// <summary>
    /// Po sesji: prezentuje kolejno wszystkie kwestionariusze (NASA-TLX, SUS, UEQ-S, SSQ, IPQ, SAM),
    /// a na końcu pokazuje podsumowanie wyników (debrief) zgodnie z wytycznymi.
    /// </summary>
    public class PostSessionController : MonoBehaviour
    {
        [SerializeField] private QuestionnaireController questionnaireUI;
        [SerializeField] private QuestionnaireDefinition[] questionnaires;
        [SerializeField] private GameObject questionnairePanel;
        [SerializeField] private GameObject debriefPanel;
        [SerializeField] private TMP_Text debriefText;

        private int index;

        void Start()
        {
            // TESTY/KWESTIONARIUSZE WYŁĄCZONE (życzenie użytkownika) — gra kończy się ekranem
            // „UDAŁO SIĘ PRZEJŚĆ GRĘ" (Akt III). Bez NASA-TLX/SUS/debrief.
            if (questionnairePanel != null) questionnairePanel.SetActive(false);
            if (debriefPanel != null) debriefPanel.SetActive(false);
        }

        private void StartNext()
        {
            if (questionnaires == null || index >= questionnaires.Length)
            {
                ShowDebrief();
                return;
            }
            if (questionnairePanel != null) questionnairePanel.SetActive(true);
            questionnaireUI.SetDefinition(questionnaires[index]);
            // restart controller logic by re-enabling
            questionnaireUI.SendMessage("Start", SendMessageOptions.DontRequireReceiver);
        }

        private void OnQuestionnaireDone(QuestionnaireDefinition def, Dictionary<string, int> answers)
        {
            index++;
            StartNext();
        }

        private void ShowDebrief()
        {
            if (questionnairePanel != null) questionnairePanel.SetActive(false);
            if (debriefPanel != null) debriefPanel.SetActive(true);

            int total = PlayerPrefs.GetInt("last_total_score", 0);
            int max = PlayerPrefs.GetInt("last_max_score", 0);
            float pct = max > 0 ? 100f * total / max : 0f;

            JSONLLogger.Instance?.LogEvent("debrief_shown", new Dictionary<string, object>
            { { "total", total }, { "max", max }, { "pct", pct } });

            if (debriefText != null)
            {
                debriefText.text =
                    $"Sesja zakończona\n\n" +
                    $"Wynik scenariuszy: {total} / {max}  ({pct:F0}%)\n\n" +
                    "Dziękujemy za udział w badaniu.\n" +
                    "Twoje dane zostały zapisane anonimowo (pseudonimizacja).\n\n" +
                    "To było środowisko treningowe VR. W realnej sytuacji\n" +
                    "zawsze dzwoń pod 112 i postępuj zgodnie z instrukcjami dyspozytora.";
            }
        }
    }
}
