using UnityEngine;

namespace MRCrisisTrainer.Gameplay.Detectors
{
    /// <summary>Ukończenie gdy gracz patrzy na target przez wymagany czas (ocena sceny, rozpoznanie).</summary>
    public class GazeDetector : MicrostepDetector
    {
        [SerializeField] private Transform target;
        [SerializeField] private float maxAngle = 15f;
        [SerializeField] private float holdSeconds = 1.5f;

        private Transform cam;
        private float gazeTime;

        protected override void OnBegin()
        {
            gazeTime = 0f;
            if (cam == null && Camera.main != null) cam = Camera.main.transform;
        }

        void Update()
        {
            if (!IsActive || target == null) return;
            if (cam == null && Camera.main != null) cam = Camera.main.transform;
            if (cam == null) return;

            Vector3 toTarget = (target.position - cam.position).normalized;
            float angle = Vector3.Angle(cam.forward, toTarget);
            if (angle <= maxAngle)
            {
                gazeTime += Time.deltaTime;
                if (gazeTime >= holdSeconds) Complete();
            }
            else gazeTime = Mathf.Max(0, gazeTime - Time.deltaTime);
        }
    }
}
