using NUnit.Framework;
using MRCrisisTrainer.Acts.Act1;
using MRCrisisTrainer.Config;
using UnityEngine;

namespace MRCrisisTrainer.Tests
{
    public class RhythmCalculatorTests
    {
        private BLSConfig config;

        [SetUp]
        public void Setup()
        {
            config = ScriptableObject.CreateInstance<BLSConfig>();
            config.targetBPM = 110f;
            config.bpmTolerance = 10f;
            config.rollingWindowSize = 5;
        }

        [TearDown]
        public void Teardown()
        {
            if (config != null) Object.DestroyImmediate(config);
        }

        [Test]
        public void Empty_Buffer_HasNoData()
        {
            var calc = new RhythmCalculator(5);
            Assert.IsFalse(calc.HasEnoughData);
            Assert.AreEqual(0f, calc.CurrentBPM, 0.001f);
        }

        [Test]
        public void SingleCompression_StillNotEnough()
        {
            var calc = new RhythmCalculator(5);
            calc.RegisterCompression(0f);
            Assert.IsFalse(calc.HasEnoughData);
        }

        [Test]
        public void TwoCompressionsAt110BPM_ProducesCorrectBPM()
        {
            var calc = new RhythmCalculator(5);
            float interval = 60f / 110f;
            calc.RegisterCompression(0f);
            calc.RegisterCompression(interval);
            Assert.AreEqual(110f, calc.CurrentBPM, 0.5f);
            Assert.AreEqual(TempoClassification.OK, calc.Classify(config));
        }

        [Test]
        public void TooSlow_IsClassified()
        {
            var calc = new RhythmCalculator(5);
            float interval = 60f / 80f;
            calc.RegisterCompression(0f);
            calc.RegisterCompression(interval);
            Assert.AreEqual(TempoClassification.TooSlow, calc.Classify(config));
        }

        [Test]
        public void TooFast_IsClassified()
        {
            var calc = new RhythmCalculator(5);
            float interval = 60f / 140f;
            calc.RegisterCompression(0f);
            calc.RegisterCompression(interval);
            Assert.AreEqual(TempoClassification.TooFast, calc.Classify(config));
        }

        [Test]
        public void RollingWindow_KeepsOnlyLastN()
        {
            var calc = new RhythmCalculator(3);
            calc.RegisterCompression(0f);
            calc.RegisterCompression(10f);
            calc.RegisterCompression(20f);
            calc.RegisterCompression(60f / 110f);
            calc.RegisterCompression(60f / 110f + 60f / 110f);
            Assert.AreEqual(110f, calc.CurrentBPM, 1f);
        }

        [Test]
        public void Reset_ClearsBuffer()
        {
            var calc = new RhythmCalculator(5);
            calc.RegisterCompression(0f);
            calc.RegisterCompression(0.5f);
            calc.Reset();
            Assert.IsFalse(calc.HasEnoughData);
        }
    }
}
