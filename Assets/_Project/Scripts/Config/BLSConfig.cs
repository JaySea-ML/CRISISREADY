using UnityEngine;

namespace MRCrisisTrainer.Config
{
    [CreateAssetMenu(fileName = "BLSConfig", menuName = "MRCrisis/BLS Config", order = 0)]
    public class BLSConfig : ScriptableObject
    {
        [Header("Rhythm")]
        [Tooltip("Target compressions per minute (BLS guideline: 100-120)")]
        public float targetBPM = 110f;

        [Tooltip("Acceptable deviation from target (e.g. 10 = 100-120 BPM)")]
        public float bpmTolerance = 10f;

        [Tooltip("Number of compressions a player must perform to complete Act I")]
        public int targetCompressions = 30;

        [Header("Detection")]
        [Tooltip("Minimum vertical movement (meters) to register a compression cycle")]
        public float minCompressionDepth = 0.04f;

        [Tooltip("Maximum time (seconds) between down and up for a valid cycle")]
        public float maxCycleDuration = 0.6f;

        [Tooltip("Rolling window for BPM smoothing")]
        public int rollingWindowSize = 5;

        [Header("Scaffolding")]
        [Tooltip("Seconds out-of-range before showing tempo hint to player")]
        public float outOfRangeHintDelay = 3f;

        [Tooltip("Seconds with hands off chest before highlighting hotspot")]
        public float handsOffHintDelay = 5f;

        public bool IsInRange(float bpm)
        {
            return Mathf.Abs(bpm - targetBPM) <= bpmTolerance;
        }

        public TempoClassification Classify(float bpm)
        {
            if (bpm < targetBPM - bpmTolerance) return TempoClassification.TooSlow;
            if (bpm > targetBPM + bpmTolerance) return TempoClassification.TooFast;
            return TempoClassification.OK;
        }
    }

    public enum TempoClassification
    {
        TooSlow,
        OK,
        TooFast
    }
}
