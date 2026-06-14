using NUnit.Framework;
using UnityEngine;
using MRCrisisTrainer.Acts.Act3;
using MRCrisisTrainer.Config;

namespace MRCrisisTrainer.Tests
{
    public class SpeechCueDetectorTests
    {
        private Act3Config config;

        [SetUp]
        public void Setup()
        {
            config = ScriptableObject.CreateInstance<Act3Config>();
            config.speechVolumeThreshold = 0.08f;
            config.minSpeechDuration = 3.0f;
            config.maxTimeForDispatch = 25f;
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(config);
        }

        [Test]
        public void Sustained_Speech_Detects()
        {
            var d = new SpeechCueDetector(config);
            float t = 0;
            for (int i = 0; i < 200 && !d.HasDetectedSpeech; i++)
            {
                t += 0.02f;
                d.Tick(0.15f, 0.02f, t);
            }
            Assert.IsTrue(d.HasDetectedSpeech);
            Assert.GreaterOrEqual(d.SpeechElapsed, config.minSpeechDuration);
        }

        [Test]
        public void Silence_Does_Not_Trigger()
        {
            var d = new SpeechCueDetector(config);
            float t = 0;
            for (int i = 0; i < 200; i++)
            {
                t += 0.02f;
                d.Tick(0.01f, 0.02f, t);
            }
            Assert.IsFalse(d.HasDetectedSpeech);
        }

        [Test]
        public void Short_Burst_Below_Min_Duration_Does_Not_Trigger()
        {
            var d = new SpeechCueDetector(config);
            float t = 0;
            for (int i = 0; i < 50; i++)
            {
                t += 0.02f;
                d.Tick(0.2f, 0.02f, t);
            }
            // 50 * 0.02 = 1s < 3s minimum
            Assert.IsFalse(d.HasDetectedSpeech);
        }

        [Test]
        public void Timeout_Fires_When_NoSpeech_In_Window()
        {
            var d = new SpeechCueDetector(config);
            bool timedOut = false;
            d.OnTimeout += () => timedOut = true;

            float t = 0;
            for (int i = 0; i < 2000 && !timedOut; i++)
            {
                t += 0.02f;
                d.Tick(0.01f, 0.02f, t);
            }
            Assert.IsTrue(timedOut);
        }

        [Test]
        public void Reset_Allows_New_Detection()
        {
            var d = new SpeechCueDetector(config);
            float t = 0;
            for (int i = 0; i < 200; i++) { t += 0.02f; d.Tick(0.2f, 0.02f, t); }
            Assert.IsTrue(d.HasDetectedSpeech);

            d.Reset();
            Assert.IsFalse(d.HasDetectedSpeech);
        }
    }
}
