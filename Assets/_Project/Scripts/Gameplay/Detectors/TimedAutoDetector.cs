using UnityEngine;

namespace MRCrisisTrainer.Gameplay.Detectors
{
    /// <summary>Auto-ukończenie po czasie - dla kroków narracyjnych (np. odtworzenie cutscene/audio).</summary>
    public class TimedAutoDetector : MicrostepDetector
    {
        [SerializeField] private float duration = 2f;
        private float t;

        protected override void OnBegin() => t = 0f;

        void Update()
        {
            if (!IsActive) return;
            t += Time.deltaTime;
            if (t >= duration) Complete();
        }
    }
}
