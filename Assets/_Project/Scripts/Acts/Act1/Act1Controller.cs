using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MRCrisisTrainer.Config;
using MRCrisisTrainer.Core;
using MRCrisisTrainer.Logging;

namespace MRCrisisTrainer.Acts.Act1
{
    public enum Act1State
    {
        Idle,
        Intro,
        ApproachVictim,
        HandPlacement,
        Compressions,
        Stabilized,
        Complete
    }

    public class Act1Controller : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private BLSConfig blsConfig;

        [Header("Refs")]
        [SerializeField] private VictimAnimator victim;
        [SerializeField] private VictimWalkAway victimWalkAway;
        [SerializeField] private Transform victimSpawnPoint;
        [SerializeField] private HandPlacementValidator placementValidator;
        [SerializeField] private CompressionDetector compressionDetector;
        [SerializeField] private MetronomeController metronome;
        [SerializeField] private RhythmFeedbackUI feedbackUI;
        [SerializeField] private ChairTransitionCue chairCue;

        [Header("Flow")]
        [SerializeField] private float introDelay = 2.5f;
        [SerializeField] private float postStabilizedDelay = 4f;
        [SerializeField] private string nextSceneName = "Act2_Car";

        public Act1State CurrentState { get; private set; } = Act1State.Idle;

        private RhythmCalculator rhythm;
        private ProgressTracker progress;
        private float lastInRangeTime;
        private float lastOutOfRangeStart = -1f;

#if UNITY_EDITOR
        void OnValidate()
        {
            if (blsConfig == null)
            {
                blsConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<BLSConfig>("Assets/_Project/ScriptableObjects/BLSConfig.asset");
            }
        }
#endif

        void Start()
        {
            if (blsConfig == null)
            {
                Debug.LogError("[Act1Controller] BLSConfig not assigned. Aborting.");
                return;
            }

            rhythm = new RhythmCalculator(blsConfig.rollingWindowSize);
            progress = new ProgressTracker(blsConfig.targetCompressions);
            progress.OnProgressChanged += HandleProgressChanged;
            progress.OnComplete += HandleAllCompressionsDone;

            if (placementValidator != null)
                placementValidator.OnPlacementChanged += HandlePlacementChanged;
            if (compressionDetector != null)
                compressionDetector.OnCompression += HandleCompressionDetected;

            JSONLLogger.Instance?.LogEvent("act1_start", new Dictionary<string, object>
            {
                { "target_bpm", blsConfig.targetBPM },
                { "target_compressions", blsConfig.targetCompressions }
            });

            StartCoroutine(IntroSequence());
        }

        void OnDestroy()
        {
            if (placementValidator != null)
                placementValidator.OnPlacementChanged -= HandlePlacementChanged;
            if (compressionDetector != null)
                compressionDetector.OnCompression -= HandleCompressionDetected;
            if (progress != null)
            {
                progress.OnProgressChanged -= HandleProgressChanged;
                progress.OnComplete -= HandleAllCompressionsDone;
            }
        }

        private IEnumerator IntroSequence()
        {
            TransitionTo(Act1State.Intro);
            feedbackUI?.SetPrompt("");
            feedbackUI?.SetProgress(0, blsConfig.targetCompressions);
            feedbackUI?.SetBPM(0, TempoClassification.OK);

            if (victim != null)
            {
                if (victimSpawnPoint != null) victim.transform.position = victimSpawnPoint.position;
                victim.PlayFall();
            }

            // Show drop prompt briefly
            yield return new WaitForSeconds(0.6f);
            feedbackUI?.SetPrompt("Ofiara nieprzytomna!");
            yield return new WaitForSeconds(introDelay);

            TransitionTo(Act1State.ApproachVictim);
            feedbackUI?.SetPrompt("Przeprowadź poprawną resuscytację\nPodejdź do ofiary");
            yield return new WaitForSeconds(2.5f);

            TransitionTo(Act1State.HandPlacement);
            feedbackUI?.SetPrompt("Połóż obie dłonie na klatce piersiowej");
        }

        private void HandlePlacementChanged(bool placed)
        {
            if (CurrentState == Act1State.HandPlacement && placed)
            {
                EnterCompressions();
            }
            else if (CurrentState == Act1State.Compressions && !placed)
            {
                feedbackUI?.SetPrompt("Trzymaj ręce na klatce piersiowej");
            }
            else if (CurrentState == Act1State.Compressions && placed)
            {
                feedbackUI?.SetPrompt("Uciskaj w tempie metronomu");
            }
        }

        private void EnterCompressions()
        {
            TransitionTo(Act1State.Compressions);
            feedbackUI?.SetPrompt("Uciskaj w tempie metronomu");
            metronome?.StartTicking();
            compressionDetector?.SetGateOpen(true);
            lastInRangeTime = Time.time;
        }

        private void HandleCompressionDetected(float timestamp, float depth)
        {
            rhythm.RegisterCompression(timestamp);
            var classification = rhythm.Classify(blsConfig);
            float bpm = rhythm.CurrentBPM;

            feedbackUI?.SetBPM(bpm, classification);

            bool inRange = classification == TempoClassification.OK;
            if (inRange) lastInRangeTime = Time.time;

            progress.Increment();

            JSONLLogger.Instance?.LogEvent("compression", new Dictionary<string, object>
            {
                { "n", progress.Current },
                { "bpm", bpm },
                { "depth_m", depth },
                { "classification", classification.ToString() }
            });
        }

        private void HandleProgressChanged(int current, int target)
        {
            feedbackUI?.SetProgress(current, target);
        }

        void Update()
        {
            if (CurrentState != Act1State.Compressions) return;
            if (rhythm == null || !rhythm.HasEnoughData) return;

            var classification = rhythm.Classify(blsConfig);
            if (classification != TempoClassification.OK)
            {
                if (lastOutOfRangeStart < 0) lastOutOfRangeStart = Time.time;
                else if (Time.time - lastOutOfRangeStart > blsConfig.outOfRangeHintDelay)
                {
                    string hint = classification == TempoClassification.TooSlow ? "Szybciej!" : "Wolniej!";
                    feedbackUI?.SetPrompt(hint);
                }
            }
            else
            {
                lastOutOfRangeStart = -1f;
            }
        }

        private void HandleAllCompressionsDone()
        {
            StartCoroutine(StabilizedSequence());
        }

        private IEnumerator StabilizedSequence()
        {
            TransitionTo(Act1State.Stabilized);
            compressionDetector?.SetGateOpen(false);
            metronome?.StopTicking();
            feedbackUI?.SetPrompt("Ofiara stabilna - wstaje");

            victim?.PlayStabilized();

            JSONLLogger.Instance?.LogEvent("act1_complete", new Dictionary<string, object>
            {
                { "total_compressions", progress.Current }
            });

            // Wait for stand-up + walk away
            if (victimWalkAway != null)
            {
                yield return StartCoroutine(victimWalkAway.Sequence());
            }
            else
            {
                yield return new WaitForSeconds(postStabilizedDelay);
            }

            feedbackUI?.SetPrompt("Słychać syreny w oddali");
            chairCue?.Activate();

            yield return new WaitForSeconds(2.5f);

            TransitionTo(Act1State.Complete);
            if (SceneLoader.Instance != null && !string.IsNullOrEmpty(nextSceneName))
            {
                SceneLoader.Instance.LoadActAdditive(nextSceneName, "Act1_Reanimation");
                GameStateManager.Instance?.SetCurrentAct(ActId.Act2_Car);
            }
        }

        private void TransitionTo(Act1State next)
        {
            Debug.Log($"[Act1] {CurrentState} -> {next}");
            CurrentState = next;
        }
    }
}
