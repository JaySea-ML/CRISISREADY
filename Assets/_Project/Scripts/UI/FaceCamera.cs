using UnityEngine;

namespace MRCrisisTrainer.UI
{
    /// <summary>Obraca obiekt (np. HUD) w stronę kamery gracza (billboard).</summary>
    public class FaceCamera : MonoBehaviour
    {
        [SerializeField] private bool lockY = true;
        private Transform cam;

        void LateUpdate()
        {
            if (cam == null)
            {
                if (Camera.main != null) cam = Camera.main.transform;
                else return;
            }
            Vector3 dir = transform.position - cam.position;
            if (lockY) dir.y = 0;
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.LookRotation(dir.normalized);
        }
    }
}
