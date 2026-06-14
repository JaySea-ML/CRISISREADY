using System;
using System.Collections.Generic;
using UnityEngine;
using MRCrisisTrainer.Config;
using MRCrisisTrainer.Logging;

namespace MRCrisisTrainer.Core
{
    /// <summary>
    /// Logika progresji przez mikrokroki scenariusza. Czysta C# (testowalna) z eventami.
    /// Każdy krok dostaje status (Pending → Active → Completed/Failed) i score (0/1/2).
    /// </summary>
    public class MicrostepProgressor
    {
        private readonly ScenarioConfig config;
        private readonly ScaffoldingLevel scaffolding;
        private readonly Dictionary<string, MicrostepRuntime> runtime;
        private int currentIndex = -1;
        private float scenarioStartTime;

        public event Action<Microstep> OnStepActivated;
        public event Action<Microstep, MicrostepScore> OnStepCompleted;
        public event Action<Microstep> OnHintShown;
        public event Action OnScenarioCompleted;

        public bool IsFinished => currentIndex >= config.microsteps.Count;
        public Microstep CurrentStep => currentIndex >= 0 && currentIndex < config.microsteps.Count
            ? config.microsteps[currentIndex] : null;

        public MicrostepProgressor(ScenarioConfig config, ScaffoldingLevel scaffolding)
        {
            this.config = config;
            this.scaffolding = scaffolding;
            this.runtime = new Dictionary<string, MicrostepRuntime>();
            foreach (var step in config.microsteps)
            {
                runtime[step.id] = new MicrostepRuntime { status = MicrostepStatus.Pending };
            }
        }

        public void Begin(float currentTime)
        {
            scenarioStartTime = currentTime;
            AdvanceToNext(currentTime);
        }

        /// <summary>Oznacza aktualny krok jako pomyślnie ukończony.</summary>
        public void CompleteCurrent(float currentTime, bool independent = true)
        {
            if (CurrentStep == null) return;
            var step = CurrentStep;
            var rt = runtime[step.id];

            rt.status = MicrostepStatus.Completed;
            rt.endTime = currentTime;
            rt.score = independent && !rt.hintShown ? MicrostepScore.Independent : MicrostepScore.WithHint;

            OnStepCompleted?.Invoke(step, rt.score);
            JSONLLogger.Instance?.LogEvent("microstep_completed", new Dictionary<string, object>
            {
                { "scenario", config.scenarioId },
                { "step_id", step.id },
                { "score", (int)rt.score },
                { "duration_s", currentTime - rt.startTime },
                { "hint_shown", rt.hintShown }
            });
            AdvanceToNext(currentTime);
        }

        /// <summary>Oznacza aktualny krok jako wykonany błędnie / pominięty.</summary>
        public void FailCurrent(float currentTime, string reason = null)
        {
            if (CurrentStep == null) return;
            var step = CurrentStep;
            var rt = runtime[step.id];

            rt.status = MicrostepStatus.Failed;
            rt.endTime = currentTime;
            rt.score = MicrostepScore.Failed;

            OnStepCompleted?.Invoke(step, rt.score);
            JSONLLogger.Instance?.LogEvent("microstep_failed", new Dictionary<string, object>
            {
                { "scenario", config.scenarioId },
                { "step_id", step.id },
                { "reason", reason ?? "unspecified" },
                { "duration_s", currentTime - rt.startTime }
            });
            AdvanceToNext(currentTime);
        }

        /// <summary>Wywoływane co frame - sprawdza czy pokazać hint lub force-timeout.</summary>
        public void Tick(float currentTime)
        {
            if (CurrentStep == null) return;
            var step = CurrentStep;
            var rt = runtime[step.id];
            if (rt.status != MicrostepStatus.Active) return;

            float elapsed = currentTime - rt.startTime;

            if (!rt.hintShown && ShouldShowHint(elapsed, step))
            {
                ShowHint(step, rt, elapsed);
            }

            if (elapsed >= step.timeoutSeconds)
            {
                FailCurrent(currentTime, "timeout");
            }
        }

        private bool ShouldShowHint(float elapsed, Microstep step)
        {
            switch (scaffolding)
            {
                case ScaffoldingLevel.Full: return true; // Hint widoczny od początku
                case ScaffoldingLevel.Partial: return elapsed >= step.hintDelaySeconds;
                case ScaffoldingLevel.None: return false;
                default: return false;
            }
        }

        private void AdvanceToNext(float currentTime)
        {
            currentIndex++;
            if (CurrentStep == null)
            {
                FinalizeScenario(currentTime);
                return;
            }
            var step = CurrentStep;
            var rt = runtime[step.id];
            rt.status = MicrostepStatus.Active;
            rt.startTime = currentTime;
            rt.hintShown = false;

            OnStepActivated?.Invoke(step);
            JSONLLogger.Instance?.LogEvent("microstep_started", new Dictionary<string, object>
            {
                { "scenario", config.scenarioId },
                { "step_id", step.id },
                { "index", currentIndex },
                { "scaffolding", scaffolding.ToString() }
            });

            if (scaffolding == ScaffoldingLevel.Full)
            {
                ShowHint(step, rt, 0f);
            }
        }

        private void ShowHint(Microstep step, MicrostepRuntime rt, float elapsed)
        {
            rt.hintShown = true;
            OnHintShown?.Invoke(step);
            JSONLLogger.Instance?.LogEvent("microstep_hint_shown", new Dictionary<string, object>
            {
                { "scenario", config.scenarioId },
                { "step_id", step.id },
                { "elapsed_s", elapsed }
            });
        }

        private void FinalizeScenario(float currentTime)
        {
            int total = 0;
            foreach (var step in config.microsteps)
            {
                total += (int)Math.Max(0, (int)runtime[step.id].score);
            }
            float normalized = config.MaxScore > 0 ? (float)total / config.MaxScore : 0f;
            bool success = normalized >= config.successThreshold;

            OnScenarioCompleted?.Invoke();
            JSONLLogger.Instance?.LogEvent("scenario_completed", new Dictionary<string, object>
            {
                { "scenario", config.scenarioId },
                { "total_score", total },
                { "max_score", config.MaxScore },
                { "normalized_score", normalized },
                { "success", success },
                { "duration_s", currentTime - scenarioStartTime },
                { "scaffolding", scaffolding.ToString() }
            });
        }

        public MicrostepScore GetScore(string stepId)
        {
            return runtime.TryGetValue(stepId, out var rt) ? rt.score : MicrostepScore.NotStarted;
        }

        public int GetTotalScore()
        {
            int total = 0;
            foreach (var rt in runtime.Values) total += (int)Math.Max(0, (int)rt.score);
            return total;
        }

        private class MicrostepRuntime
        {
            public MicrostepStatus status;
            public MicrostepScore score = MicrostepScore.NotStarted;
            public float startTime;
            public float endTime;
            public bool hintShown;
        }
    }
}
