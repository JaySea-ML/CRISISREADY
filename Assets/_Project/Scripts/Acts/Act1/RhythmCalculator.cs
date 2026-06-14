using System.Collections.Generic;
using MRCrisisTrainer.Config;

namespace MRCrisisTrainer.Acts.Act1
{
    public class RhythmCalculator
    {
        private readonly Queue<float> timestamps;
        private readonly int windowSize;
        private float lastTimestamp = float.NaN;

        public RhythmCalculator(int windowSize)
        {
            this.windowSize = System.Math.Max(2, windowSize);
            timestamps = new Queue<float>(this.windowSize);
        }

        public void RegisterCompression(float timestamp)
        {
            if (!float.IsNaN(lastTimestamp) && timestamp <= lastTimestamp)
            {
                timestamps.Clear();
            }

            timestamps.Enqueue(timestamp);
            lastTimestamp = timestamp;
            while (timestamps.Count > windowSize) timestamps.Dequeue();
        }

        public bool HasEnoughData => timestamps.Count >= 2;

        public float CurrentBPM
        {
            get
            {
                if (!HasEnoughData) return 0f;
                var arr = timestamps.ToArray();
                float totalInterval = arr[arr.Length - 1] - arr[0];
                int intervals = arr.Length - 1;
                if (totalInterval <= 0f) return 0f;
                float avgInterval = totalInterval / intervals;
                return 60f / avgInterval;
            }
        }

        public TempoClassification Classify(BLSConfig config)
        {
            if (!HasEnoughData) return TempoClassification.OK;
            return config.Classify(CurrentBPM);
        }

        public void Reset()
        {
            timestamps.Clear();
            lastTimestamp = float.NaN;
        }
    }
}
