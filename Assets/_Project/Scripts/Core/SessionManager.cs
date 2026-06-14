using System.Collections.Generic;
using UnityEngine;
using MRCrisisTrainer.Config;
using MRCrisisTrainer.Logging;

namespace MRCrisisTrainer.Core
{
    /// <summary>
    /// Śledzi fazę badania (pre-test → trening → post-test → retencja → transfer)
    /// oraz scaffolding level dla bieżącej sesji.
    /// </summary>
    public class SessionManager : MonoBehaviour
    {
        public enum SessionPhase
        {
            PreTest,     // Pierwszy kontakt, bez treningu
            Training,    // Iteracja treningowa (powtarzalna)
            PostTest,    // Bezpośrednio po treningu
            Retention,   // Po kilku dniach
            Transfer     // Inny wariant scenariusza (test transferu)
        }

        public static SessionManager Instance { get; private set; }

        [SerializeField] private SessionPhase currentPhase = SessionPhase.PreTest;
        [SerializeField] private int trainingIterationsCompleted;
        [SerializeField] private string participantId;
        [SerializeField] private string sessionId;

        public SessionPhase CurrentPhase => currentPhase;
        public int TrainingIterations => trainingIterationsCompleted;
        public string ParticipantId => participantId;
        public string SessionId => sessionId;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (string.IsNullOrEmpty(participantId)) participantId = System.Guid.NewGuid().ToString("N").Substring(0, 8);
            if (string.IsNullOrEmpty(sessionId)) sessionId = System.Guid.NewGuid().ToString("N").Substring(0, 12);

            JSONLLogger.Instance?.LogEvent("session_phase", new Dictionary<string, object>
            {
                { "phase", currentPhase.ToString() },
                { "participant", participantId },
                { "session", sessionId }
            });
        }

        /// <summary>Zwraca scaffolding level dla bieżącej fazy + iteracji.</summary>
        public ScaffoldingLevel GetScaffoldingLevel()
        {
            switch (currentPhase)
            {
                case SessionPhase.PreTest:
                case SessionPhase.PostTest:
                case SessionPhase.Retention:
                case SessionPhase.Transfer:
                    return ScaffoldingLevel.None; // Bez pomocy w testach

                case SessionPhase.Training:
                    if (trainingIterationsCompleted == 0) return ScaffoldingLevel.Full;
                    if (trainingIterationsCompleted == 1) return ScaffoldingLevel.Partial;
                    return ScaffoldingLevel.None;

                default: return ScaffoldingLevel.Partial;
            }
        }

        public void RecordTrainingCompleted()
        {
            trainingIterationsCompleted++;
            JSONLLogger.Instance?.LogEvent("training_iteration_done", new Dictionary<string, object>
            {
                { "iteration", trainingIterationsCompleted }
            });
        }

        public void TransitionToPhase(SessionPhase next)
        {
            JSONLLogger.Instance?.LogEvent("session_phase_change", new Dictionary<string, object>
            {
                { "from", currentPhase.ToString() },
                { "to", next.ToString() }
            });
            currentPhase = next;
        }
    }
}
