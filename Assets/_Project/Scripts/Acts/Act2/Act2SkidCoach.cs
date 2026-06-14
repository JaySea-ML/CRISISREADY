using UnityEngine;
using TMPro;
using MRCrisisTrainer.Config;
using MRCrisisTrainer.Gameplay;
using MRCrisisTrainer.Gameplay.Detectors;

namespace MRCrisisTrainer.Acts.Act2
{
    /// <summary>
    /// Profesjonalny "trener" Aktu II — duże, czytelne podpowiedzi prowadzące przez poślizg:
    ///   rozpoznaj → złap kierownicę → kontrasteruj (kierunek na żywo, zielono=dobrze / czerwono=źle) →
    ///   stabilizuj → odzyskano. Reaguje na ScenarioRunner i SteeringRecoveryDetector.OnSkidUpdate.
    /// </summary>
    public class Act2SkidCoach : MonoBehaviour
    {
        [SerializeField] private ScenarioRunner runner;
        [SerializeField] private SteeringRecoveryDetector recovery;
        [SerializeField] private TMP_Text bigPrompt;
        [SerializeField] private TMP_Text subPrompt;
        [Tooltip("Kierunek poślizgu: +1 = w prawo, -1 = w lewo (zwykle sign(initialSkidAngleDeg))")]
        [SerializeField] private float skidDirection = 1f;

        [SerializeField] private Color warn = new Color(1f, 0.78f, 0.2f);
        [SerializeField] private Color good = new Color(0.25f, 0.95f, 0.35f);
        [SerializeField] private Color bad = new Color(1f, 0.35f, 0.3f);
        [SerializeField] private Color calm = new Color(0.85f, 0.9f, 1f);

        private string currentStep = "";
        private float pulseT;

        void OnEnable()
        {
            if (runner != null)
            {
                runner.OnStepActivated += OnStep;
                runner.OnScenarioFinished += OnFinished;
            }
            if (recovery != null) recovery.OnSkidUpdate += OnSkid;
            Show("", "", calm);
        }

        void OnDisable()
        {
            if (runner != null)
            {
                runner.OnStepActivated -= OnStep;
                runner.OnScenarioFinished -= OnFinished;
            }
            if (recovery != null) recovery.OnSkidUpdate -= OnSkid;
        }

        private string DirArrow(float dir) => dir >= 0f ? "w PRAWO  →" : "←  w LEWO";

        private void OnStep(Microstep step)
        {
            currentStep = step != null ? step.id : "";
            switch (currentStep)
            {
                case "drive_calm":
                    Show("JEDŹ SPOKOJNIE — PRAWY PAS", "Trzymaj kierownicę obiema rękami. Za chwilę poślizg.", calm);
                    break;
                case "recognize_skid":
                    Show("! POŚLIZG W PRAWO", "Przygotuj się — zaraz ODBIJESZ W LEWO", warn);
                    break;
                case "grip_wheel":
                    Show("ZŁAP KIEROWNICĘ", "Sięgnij dłońmi do kierownicy (działają też kontrolery)", warn);
                    break;
                case "counter_steer":
                    Show($"ODBIJ  {DirArrow(-skidDirection)}", "Skręć kierownicą W LEWO, aż auto się wyprostuje", warn);
                    break;
                case "stabilize":
                case "stabilize_l":
                    Show("TRZYMAJ PROSTO", "Utrzymaj kierownicę aż auto się wyrówna", good);
                    break;
                case "drive_between":
                    Show("JEDŹ DALEJ — UWAGA", "Dobrze! Bądź gotów na kolejny poślizg.", calm);
                    break;
                case "recognize_skid_l":
                    Show("! POŚLIZG W LEWO", "Przygotuj się — zaraz ODBIJESZ W PRAWO", warn);
                    break;
                case "counter_steer_l":
                    Show($"ODBIJ  {DirArrow(skidDirection)}", "Skręć kierownicą W PRAWO, aż auto się wyprostuje", warn);
                    break;
                case "resume":
                    Show("OK — KONTROLA ODZYSKANA", "Świetnie! Wracaj do spokojnej jazdy", good);
                    break;
                default:
                    Show("", "", calm);
                    break;
            }
        }

        // Na żywo podczas kontrasterowania: kierunek + kolor wg poprawności + kąt poślizgu
        private void OnSkid(float angleDeg, SteeringFeedback fb)
        {
            if (currentStep != "counter_steer" && currentStep != "counter_steer_l") return;
            float skidSign = angleDeg >= 0f ? 1f : -1f;
            string arrow = DirArrow(-skidSign);   // ODBIJ przeciwnie do poślizgu (prawy poślizg → w lewo)
            int absA = Mathf.RoundToInt(Mathf.Abs(angleDeg));
            bool almost = absA < 9;
            Show($"ODBIJ  {arrow}", almost ? $"Już prawie prosto ({absA}°)" : $"Skręcaj, aż auto się wyrówna ({absA}°)", almost ? good : warn);
        }

        private void OnFinished(int total, int max) => Show("", "", calm);

        private void Show(string big, string sub, Color c)
        {
            if (bigPrompt != null) { bigPrompt.text = big; bigPrompt.color = c; }
            if (subPrompt != null) { subPrompt.text = sub; subPrompt.color = c; }
            pulseT = 0f;
        }

        void Update()
        {
            // delikatne pulsowanie ostrzeżeń, by przyciągały wzrok
            if (bigPrompt == null) return;
            bool urgent = currentStep == "recognize_skid" || currentStep == "recognize_skid_l"
                          || currentStep == "grip_wheel"
                          || currentStep == "counter_steer" || currentStep == "counter_steer_l";
            if (!urgent) { bigPrompt.transform.localScale = Vector3.one; return; }
            pulseT += Time.deltaTime * 4f;
            float s = 1f + Mathf.Sin(pulseT) * 0.05f;
            bigPrompt.transform.localScale = new Vector3(s, s, s);
        }
    }
}
