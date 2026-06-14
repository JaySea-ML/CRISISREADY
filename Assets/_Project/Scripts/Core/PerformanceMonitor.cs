using System.Collections.Generic;
using UnityEngine;
using MRCrisisTrainer.Logging;

namespace MRCrisisTrainer.Core
{
    /// <summary>
    /// Monitoruje wydajność (FPS, drop klatek) zgodnie z wytycznymi (testowanie techniczne VR/MR).
    /// Loguje co interwał do JSONL. Wykrywa spadki poniżej progu.
    /// </summary>
    public class PerformanceMonitor : MonoBehaviour
    {
        public static PerformanceMonitor Instance { get; private set; }

        [SerializeField] private float logInterval = 5f;
        [SerializeField] private float lowFpsThreshold = 60f;

        private float timer;
        private int frames;
        private float accumDt;
        private int dropCount;
        private float worstFrameMs;

        public float CurrentFps { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            frames++;
            accumDt += dt;
            float ms = dt * 1000f;
            if (ms > worstFrameMs) worstFrameMs = ms;
            if (1f / dt < lowFpsThreshold) dropCount++;

            timer += dt;
            if (timer >= logInterval)
            {
                CurrentFps = frames / accumDt;
                JSONLLogger.Instance?.LogEvent("perf", new Dictionary<string, object>
                {
                    { "fps", CurrentFps },
                    { "frame_drops", dropCount },
                    { "worst_frame_ms", worstFrameMs },
                    { "scene", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name }
                });
                timer = 0; frames = 0; accumDt = 0; dropCount = 0; worstFrameMs = 0;
            }
        }
    }
}
