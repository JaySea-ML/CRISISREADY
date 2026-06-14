using NUnit.Framework;
using UnityEngine;
using MRCrisisTrainer.Acts.Act2;
using MRCrisisTrainer.Config;

namespace MRCrisisTrainer.Tests
{
    public class SkidPhysicsTests
    {
        private VehicleConfig config;

        [SetUp]
        public void Setup()
        {
            config = ScriptableObject.CreateInstance<VehicleConfig>();
            config.initialSkidAngleDeg = 35f;
            config.skidGrowthRateDeg = 8f;
            config.catastrophicAngleDeg = 80f;
            config.maxSteeringAngleDeg = 540f;
            config.steeringCorrectionRate = 45f;
            config.recoveryAngleThresholdDeg = 5f;
            config.recoveryHoldDuration = 1.5f;
            config.skidTimeout = 12f;
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(config);
        }

        [Test]
        public void Initial_Angle_Is_Set()
        {
            var s = new SkidPhysicsSimulator(config, 35f);
            Assert.AreEqual(35f, s.CurrentAngleDeg, 0.01f);
            Assert.IsFalse(s.IsRecovered);
            Assert.IsFalse(s.IsCatastrophe);
        }

        [Test]
        public void NoInput_SkidGrows_HitsCatastrophe()
        {
            var s = new SkidPhysicsSimulator(config, 35f);
            for (int i = 0; i < 600 && !s.IsCatastrophe && !s.IsRecovered; i++)
            {
                s.Step(0f, 0.02f);
            }
            Assert.IsTrue(s.IsCatastrophe, "Without correction, skid should grow until catastrophe or timeout.");
        }

        [Test]
        public void CorrectSteering_LeadsToRecovery()
        {
            var s = new SkidPhysicsSimulator(config, 35f);
            for (int i = 0; i < 600 && !s.IsCatastrophe && !s.IsRecovered; i++)
            {
                float steering = s.CurrentAngleDeg > 0 ? 1f : -1f;
                s.Step(steering, 0.02f);
            }
            Assert.IsTrue(s.IsRecovered);
            Assert.IsFalse(s.IsCatastrophe);
        }

        [Test]
        public void WrongSteering_MakesItWorse()
        {
            var s = new SkidPhysicsSimulator(config, 35f);
            for (int i = 0; i < 100 && !s.IsCatastrophe; i++)
            {
                float wrongSteering = s.CurrentAngleDeg > 0 ? -1f : 1f;
                s.Step(wrongSteering, 0.02f);
            }
            Assert.IsTrue(s.IsCatastrophe, "Wrong direction should accelerate skid.");
        }

        [Test]
        public void LeftSkid_LeftSteeringCorrects()
        {
            var s = new SkidPhysicsSimulator(config, -35f);
            for (int i = 0; i < 600 && !s.IsCatastrophe && !s.IsRecovered; i++)
            {
                s.Step(-1f, 0.02f);
            }
            Assert.IsTrue(s.IsRecovered);
        }

        [Test]
        public void Reset_RestoresInitialAngle()
        {
            var s = new SkidPhysicsSimulator(config, 35f);
            for (int i = 0; i < 50; i++) s.Step(0f, 0.02f);
            s.Reset(20f);
            Assert.AreEqual(20f, s.CurrentAngleDeg, 0.01f);
            Assert.IsFalse(s.IsCatastrophe);
            Assert.IsFalse(s.IsRecovered);
        }
    }
}
