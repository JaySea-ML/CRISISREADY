using System;

namespace MRCrisisTrainer.Acts.Act1
{
    public class ProgressTracker
    {
        private readonly int target;
        private int current;

        public event Action<int, int> OnProgressChanged;
        public event Action OnComplete;

        public int Current => current;
        public int Target => target;
        public bool IsComplete => current >= target;
        public float NormalizedProgress => target > 0 ? (float)current / target : 0f;

        public ProgressTracker(int target)
        {
            this.target = System.Math.Max(1, target);
            current = 0;
        }

        public void Increment()
        {
            if (IsComplete) return;
            current++;
            OnProgressChanged?.Invoke(current, target);
            if (IsComplete) OnComplete?.Invoke();
        }

        public void Reset()
        {
            current = 0;
            OnProgressChanged?.Invoke(0, target);
        }
    }
}
