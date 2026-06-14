using UnityEngine;

namespace MRCrisisTrainer.Acts.Act2
{
    /// <summary>
    /// Nieskończona jazda: przesuwa segmenty drogi+lasu w stronę gracza (lokalne -Z) i zawija
    /// te, które go minęły, na koniec łańcucha. Daje wrażenie ciągłej jazdy przez las.
    /// </summary>
    public class ForestScroller : MonoBehaviour
    {
        [SerializeField] private Transform[] segments;
        [SerializeField] private float segmentLength = 30f;
        [SerializeField] private float speed = 9f;          // m/s (~32 km/h)
        [SerializeField] private float recycleBehind = 35f; // ile za graczem zanim zawinie

        private float speedMul = 1f;
        private bool running;

        public float Speed => speed * speedMul;

        public void Begin() => running = true;
        public void Stop() => running = false;
        public void SetSpeedMultiplier(float m) => speedMul = Mathf.Max(0f, m);

        void Update()
        {
            if (!running || segments == null || segments.Length == 0) return;
            float total = segments.Length * segmentLength;
            float dz = speed * speedMul * Time.deltaTime;
            foreach (var s in segments)
            {
                if (s == null) continue;
                var p = s.localPosition;
                p.z -= dz;
                if (p.z <= -recycleBehind) p.z += total;
                s.localPosition = p;
            }
        }
    }
}
