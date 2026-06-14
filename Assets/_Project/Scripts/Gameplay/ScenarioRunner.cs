using System;
using System.Collections.Generic;
using UnityEngine;
using MRCrisisTrainer.Config;
using MRCrisisTrainer.Core;

namespace MRCrisisTrainer.Gameplay
{
    /// <summary>
    /// Steruje przebiegiem scenariusza (Akt) przez mikrokroki. Łączy ScenarioConfig
    /// z MicrostepProgressor i aktywuje odpowiedni MicrostepDetector dla każdego kroku.
    /// </summary>
    public class ScenarioRunner : MonoBehaviour
    {
        [SerializeField] private ScenarioConfig scenario;
        [SerializeField] private MicrostepDetector[] detectors;
        [SerializeField] private bool autoStart = false;
        [SerializeField] private float startDelay = 1f;

        public ScenarioConfig Scenario => scenario;
        public bool IsRunning { get; private set; }

        public event Action<Microstep> OnStepActivated;
        public event Action<Microstep, MicrostepScore> OnStepScored;
        public event Action<Microstep> OnHint;
        public event Action<int, int> OnScenarioFinished; // (totalScore, maxScore)

        private MicrostepProgressor progressor;
        private readonly Dictionary<string, MicrostepDetector> byId = new Dictionary<string, MicrostepDetector>();
        private MicrostepDetector activeDetector;

        void Awake()
        {
            // WAŻNE: Unity serializuje nieprzypisaną tablicę jako PUSTĄ (nie null), więc samo "== null"
            // pomijało auto-wyszukiwanie i ŻADEN detektor się nie rejestrował (poślizg nigdy nie wychodził,
            // chwyt koła nie działał). Dlatego sprawdzamy też Length == 0.
            if (detectors == null || detectors.Length == 0) detectors = GetComponentsInChildren<MicrostepDetector>(true);
            foreach (var d in detectors)
            {
                if (d != null && !string.IsNullOrEmpty(d.stepId)) byId[d.stepId] = d;
            }
        }

        void Start()
        {
            if (autoStart) Invoke(nameof(Run), startDelay);
        }

        public void Run()
        {
            if (scenario == null) { Debug.LogError("[ScenarioRunner] No scenario assigned."); return; }
            if (IsRunning) return;
            IsRunning = true;

            var scaffolding = SessionManager.Instance != null
                ? SessionManager.Instance.GetScaffoldingLevel()
                : scenario.defaultScaffolding;

            progressor = new MicrostepProgressor(scenario, scaffolding);
            progressor.OnStepActivated += HandleStepActivated;
            progressor.OnStepCompleted += HandleStepCompleted;
            progressor.OnHintShown += s => OnHint?.Invoke(s);
            progressor.OnScenarioCompleted += HandleScenarioCompleted;
            progressor.Begin(Time.time);
        }

        private void HandleStepActivated(Microstep step)
        {
            OnStepActivated?.Invoke(step);
            if (byId.TryGetValue(step.id, out var det))
            {
                activeDetector = det;
                det.OnCompleted -= OnDetectorCompleted;
                det.OnFailed -= OnDetectorFailed;
                det.OnCompleted += OnDetectorCompleted;
                det.OnFailed += OnDetectorFailed;
                det.Begin();
            }
            else
            {
                Debug.LogWarning($"[ScenarioRunner] No detector for step '{step.id}' - waiting for timeout/manual.");
                activeDetector = null;
            }
        }

        private void OnDetectorCompleted()
        {
            DetachActive();
            progressor?.CompleteCurrent(Time.time, independent: true);
        }

        private void OnDetectorFailed(string reason)
        {
            DetachActive();
            progressor?.FailCurrent(Time.time, reason);
        }

        private void DetachActive()
        {
            if (activeDetector != null)
            {
                activeDetector.OnCompleted -= OnDetectorCompleted;
                activeDetector.OnFailed -= OnDetectorFailed;
                activeDetector.Cancel();
                activeDetector = null;
            }
        }

        private void HandleStepCompleted(Microstep step, MicrostepScore score)
        {
            OnStepScored?.Invoke(step, score);
        }

        private void HandleScenarioCompleted()
        {
            IsRunning = false;
            int total = progressor.GetTotalScore();
            int max = scenario.MaxScore;
            OnScenarioFinished?.Invoke(total, max);
            if (SessionManager.Instance != null &&
                SessionManager.Instance.CurrentPhase == SessionManager.SessionPhase.Training)
            {
                SessionManager.Instance.RecordTrainingCompleted();
            }
        }

        void Update()
        {
            if (IsRunning) progressor?.Tick(Time.time);
        }

        /// <summary>Pozwala detektorowi/innemu kodowi wymusić sukces bieżącego kroku.</summary>
        public void ForceCompleteCurrent() => OnDetectorCompleted();

        /// <summary>Pozwala wymusić porażkę bieżącego kroku.</summary>
        public void ForceFailCurrent(string reason) => OnDetectorFailed(reason);
    }
}
