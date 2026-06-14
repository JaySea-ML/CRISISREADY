using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MRCrisisTrainer.Core;
using MRCrisisTrainer.Logging;

namespace MRCrisisTrainer.UI
{
    /// <summary>
    /// Menu główne: ekran zgody (RODO), wybór fazy badania, start sesji.
    /// Panelowy flow: Consent -> Menu -> ładowanie TrainingRoom.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Panele")]
        [SerializeField] private GameObject consentPanel;
        [SerializeField] private GameObject menuPanel;

        [Header("Consent")]
        [SerializeField] private Toggle consentToggle;
        [SerializeField] private Button consentAcceptButton;

        [Header("Menu")]
        [SerializeField] private TMP_Text phaseLabel;
        [SerializeField] private Button startButton;

        [Header("Flow")]
        [SerializeField] private string trainingScene = "TrainingRoom";

        private SessionManager.SessionPhase[] phases = (SessionManager.SessionPhase[])
            System.Enum.GetValues(typeof(SessionManager.SessionPhase));
        private int phaseIndex;

        void Start()
        {
            if (consentPanel != null)
            {
                ShowConsent();
                if (consentAcceptButton != null)
                {
                    consentAcceptButton.onClick.AddListener(AcceptConsent);
                    consentAcceptButton.interactable = false;
                }
                if (consentToggle != null)
                    consentToggle.onValueChanged.AddListener(v =>
                    { if (consentAcceptButton != null) consentAcceptButton.interactable = v; });
            }
            else if (menuPanel != null) menuPanel.SetActive(true);   // brak panelu zgody → od razu menu (GRAJ)
            if (startButton != null) startButton.onClick.AddListener(StartSession);
            UpdatePhaseLabel();
        }

        private void ShowConsent()
        {
            if (consentPanel != null) consentPanel.SetActive(true);
            if (menuPanel != null) menuPanel.SetActive(false);
        }

        private void AcceptConsent()
        {
            JSONLLogger.Instance?.LogEvent("consent_given", new System.Collections.Generic.Dictionary<string, object>
            { { "rodo", true }, { "timestamp_local", System.DateTime.Now.ToString("o") } });
            if (consentPanel != null) consentPanel.SetActive(false);
            if (menuPanel != null) menuPanel.SetActive(true);
        }

        public void NextPhase()
        {
            phaseIndex = (phaseIndex + 1) % phases.Length;
            UpdatePhaseLabel();
        }

        public void PrevPhase()
        {
            phaseIndex = (phaseIndex - 1 + phases.Length) % phases.Length;
            UpdatePhaseLabel();
        }

        private void UpdatePhaseLabel()
        {
            if (phaseLabel != null) phaseLabel.text = PhaseDisplayName(phases[phaseIndex]);
        }

        private string PhaseDisplayName(SessionManager.SessionPhase p)
        {
            switch (p)
            {
                case SessionManager.SessionPhase.PreTest: return "Pre-test (bez treningu)";
                case SessionManager.SessionPhase.Training: return "Trening";
                case SessionManager.SessionPhase.PostTest: return "Post-test";
                case SessionManager.SessionPhase.Retention: return "Test retencji";
                case SessionManager.SessionPhase.Transfer: return "Test transferu";
                default: return p.ToString();
            }
        }

        private void StartSession()
        {
            if (SessionManager.Instance != null)
                SessionManager.Instance.TransitionToPhase(phases[phaseIndex]);
            JSONLLogger.Instance?.LogEvent("session_start_pressed", new System.Collections.Generic.Dictionary<string, object>
            { { "phase", phases[phaseIndex].ToString() } });
            if (SceneLoader.Instance != null)
                SceneLoader.Instance.LoadActAdditive(trainingScene, "MainMenu");
        }
    }
}
