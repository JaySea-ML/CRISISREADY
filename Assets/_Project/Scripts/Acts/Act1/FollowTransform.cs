using UnityEngine;

namespace MRCrisisTrainer.Acts.Act1
{
    /// <summary>
    /// Utrzymuje pozycję obiektu nad targetem (np. UI prompt nad głową ofiary).
    /// Opcjonalnie obraca się w stronę kamery (billboard).
    /// </summary>
    public class FollowTransform : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0, 0.6f, 0);
        [SerializeField] private bool billboardToCamera = true;
        [SerializeField] private float positionLerp = 8f;

        private Transform cameraTransform;

        public void SetTarget(Transform t) { target = t; }

        void Awake()
        {
            if (Camera.main != null) cameraTransform = Camera.main.transform;
        }

        void LateUpdate()
        {
            if (target == null) return;

            Vector3 desired = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desired, Time.deltaTime * positionLerp);

            if (billboardToCamera && cameraTransform != null)
            {
                Vector3 toCam = transform.position - cameraTransform.position;
                toCam.y = 0;
                if (toCam.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(toCam.normalized);
                }
            }
        }
    }
}
