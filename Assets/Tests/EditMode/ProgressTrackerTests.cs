using NUnit.Framework;
using MRCrisisTrainer.Acts.Act1;

namespace MRCrisisTrainer.Tests
{
    public class ProgressTrackerTests
    {
        [Test]
        public void Increment_EmitsProgressChanged()
        {
            var tracker = new ProgressTracker(5);
            int latestCurrent = -1;
            int latestTarget = -1;
            tracker.OnProgressChanged += (c, t) => { latestCurrent = c; latestTarget = t; };

            tracker.Increment();

            Assert.AreEqual(1, latestCurrent);
            Assert.AreEqual(5, latestTarget);
        }

        [Test]
        public void Increment_30Times_EmitsComplete()
        {
            var tracker = new ProgressTracker(30);
            bool completed = false;
            tracker.OnComplete += () => completed = true;

            for (int i = 0; i < 30; i++) tracker.Increment();

            Assert.IsTrue(completed);
            Assert.IsTrue(tracker.IsComplete);
            Assert.AreEqual(1f, tracker.NormalizedProgress, 0.001f);
        }

        [Test]
        public void Increment_PastTarget_StillCompleteOnlyOnce()
        {
            var tracker = new ProgressTracker(3);
            int completeCount = 0;
            tracker.OnComplete += () => completeCount++;

            for (int i = 0; i < 10; i++) tracker.Increment();

            Assert.AreEqual(1, completeCount);
            Assert.AreEqual(3, tracker.Current);
        }

        [Test]
        public void Reset_EmitsZeroProgress()
        {
            var tracker = new ProgressTracker(5);
            tracker.Increment();
            tracker.Increment();

            int latestCurrent = -1;
            tracker.OnProgressChanged += (c, t) => latestCurrent = c;

            tracker.Reset();

            Assert.AreEqual(0, tracker.Current);
            Assert.AreEqual(0, latestCurrent);
        }
    }
}
