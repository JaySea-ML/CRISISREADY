using NUnit.Framework;
using UnityEngine;
using MRCrisisTrainer.Acts.Act3;
using MRCrisisTrainer.Config;

namespace MRCrisisTrainer.Tests
{
    public class SilenceMonitorTests
    {
        private Act3Config config;

        [SetUp]
        public void Setup()
        {
            config = ScriptableObject.CreateInstance<Act3Config>();
            config.silenceRequiredDuration = 5f;
            config.silenceVolumeThreshold = 0.05f;
            config.allowedNoiseStrikes = 2;
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(config);
        }

        [Test]
        public void Silence_For_Required_Time_Completes()
        {
            var m = new SilenceMonitor(config);
            float t = 0;
            for (int i = 0; i < 300 && m.Outcome == SilenceOutcome.InProgress; i++)
            {
                t += 0.02f;
                m.Tick(0.01f, 0.02f, t);
            }
            Assert.AreEqual(SilenceOutcome.Success, m.Outcome);
        }

        [Test]
        public void Loud_Volume_Above_Strikes_Fails()
        {
            var m = new SilenceMonitor(config);
            float t = 0;
            // Several loud "bursts" separated by 3 seconds
            for (int strike = 0; strike < 4; strike++)
            {
                t += 3f;
                m.Tick(0.5f, 0.02f, t);
            }
            Assert.AreEqual(SilenceOutcome.Failed, m.Outcome);
        }

        [Test]
        public void Noise_Below_Threshold_Does_Not_Strike()
        {
            var m = new SilenceMonitor(config);
            float t = 0;
            for (int i = 0; i < 200 && m.Outcome == SilenceOutcome.InProgress; i++)
            {
                t += 0.02f;
                m.Tick(0.03f, 0.02f, t); // below 0.05 threshold
            }
            Assert.AreEqual(0, m.NoiseStrikes);
        }

        [Test]
        public void Strike_Reduces_Progress_But_Does_Not_Reset()
        {
            var m = new SilenceMonitor(config);
            float t = 0;
            // Build up some silence
            for (int i = 0; i < 100; i++)
            {
                t += 0.02f;
                m.Tick(0f, 0.02f, t);
            }
            float beforeStrike = m.SilenceElapsed;
            // One loud event
            t += 0.5f;
            m.Tick(0.5f, 0.02f, t);
            Assert.AreEqual(1, m.NoiseStrikes);
            Assert.Less(m.SilenceElapsed, beforeStrike);
            Assert.GreaterOrEqual(m.SilenceElapsed, 0f);
        }

        [Test]
        public void Reset_Restores_State()
        {
            var m = new SilenceMonitor(config);
            float t = 0;
            for (int i = 0; i < 50; i++) { t += 0.02f; m.Tick(0f, 0.02f, t); }
            t += 3f; m.Tick(0.5f, 0.02f, t);

            m.Reset();
            Assert.AreEqual(0, m.NoiseStrikes);
            Assert.AreEqual(0f, m.SilenceElapsed, 0.001f);
            Assert.AreEqual(SilenceOutcome.InProgress, m.Outcome);
        }
    }
}
