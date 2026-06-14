using NUnit.Framework;
using MRCrisisTrainer.Config;
using UnityEngine;

namespace MRCrisisTrainer.Tests
{
    public class BLSConfigTests
    {
        private BLSConfig config;

        [SetUp]
        public void Setup()
        {
            config = ScriptableObject.CreateInstance<BLSConfig>();
            config.targetBPM = 110f;
            config.bpmTolerance = 10f;
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(config);
        }

        [TestCase(100f, true)]
        [TestCase(110f, true)]
        [TestCase(120f, true)]
        [TestCase(99f, false)]
        [TestCase(121f, false)]
        [TestCase(80f, false)]
        [TestCase(140f, false)]
        public void IsInRange_BehavesPerSpec(float bpm, bool expected)
        {
            Assert.AreEqual(expected, config.IsInRange(bpm));
        }

        [TestCase(99f, TempoClassification.TooSlow)]
        [TestCase(110f, TempoClassification.OK)]
        [TestCase(125f, TempoClassification.TooFast)]
        public void Classify_BehavesPerSpec(float bpm, TempoClassification expected)
        {
            Assert.AreEqual(expected, config.Classify(bpm));
        }
    }
}
