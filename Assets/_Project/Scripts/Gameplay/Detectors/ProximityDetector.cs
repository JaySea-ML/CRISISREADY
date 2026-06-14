using UnityEngine;

namespace MRCrisisTrainer.Gameplay.Detectors
{
    /// <summary>Ukończenie gdy kamera gracza zbliży się do targetu (np. podejście do ofiary).</summary>
    public class ProximityDetector : MicrostepDetector
    {
        [SerializeField] private Transform target;
        [SerializeField] private float radius = 1.2f;
        [SerializeField] private float holdSeconds = 0.5f;

        private Transform cam;
        private float inRangeTime;

        protected override void OnBegin()
        {
            inRangeTime = 0f;
            if (cam == null && Camera.main != null) cam = Camera.main.transform;
        }

        void Update()
        {
            if (!IsActive || target == null) return;
            if (cam == null && Camera.main != null) cam = Camera.main.transform;
            if (cam == null) return;

            Vector3 a = cam.position; a.y = 0;
            Vector3 b = target.position; b.y = 0;
            if ((a - b).magnitude <= radius)
            {
                inRangeTime += Time.deltaTime;
                if (inRangeTime >= holdSeconds) Complete();
            }
            else inRangeTime = 0f;
        }
    }
}
