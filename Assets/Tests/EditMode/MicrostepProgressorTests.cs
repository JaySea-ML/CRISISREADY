using NUnit.Framework;
using UnityEngine;
using MRCrisisTrainer.Config;
using MRCrisisTrainer.Core;

namespace MRCrisisTrainer.Tests
{
    public class MicrostepProgressorTests
    {
        private ScenarioConfig config;

        [SetUp]
        public void Setup()
        {
            config = ScriptableObject.CreateInstance<ScenarioConfig>();
            config.scenarioId = "test";
            config.successThreshold = 0.7f;
            config.microsteps = new System.Collections.Generic.List<Microstep>
            {
                new Microstep { id = "step1", label = "Step 1", hintDelaySeconds = 2f, timeoutSeconds = 10f, isMandatory = true, isBlocking = true },
                new Microstep { id = "step2", label = "Step 2", hintDelaySeconds = 2f, timeoutSeconds = 10f, isMandatory = true, isBlocking = true },
                new Microstep { id = "step3", label = "Step 3", hintDelaySeconds = 2f, timeoutSeconds = 10f, isMandatory = true, isBlocking = true }
            };
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(config);
        }

        [Test]
        public void Begin_ActivatesFirstStep()
        {
            var p = new MicrostepProgressor(config, ScaffoldingLevel.None);
            Microstep activated = null;
            p.OnStepActivated += s => activated = s;

            p.Begin(0f);

            Assert.IsNotNull(activated);
            Assert.AreEqual("step1", activated.id);
        }

        [Test]
        public void CompleteCurrent_AdvancesToNextStep()
        {
            var p = new MicrostepProgressor(config, ScaffoldingLevel.None);
            p.Begin(0f);
            p.CompleteCurrent(1f);

            Assert.AreEqual("step2", p.CurrentStep.id);
        }

        [Test]
        public void CompleteIndependent_GivesScore2()
        {
            var p = new MicrostepProgressor(config, ScaffoldingLevel.None);
            p.Begin(0f);
            p.CompleteCurrent(1f, independent: true);

            Assert.AreEqual(MicrostepScore.Independent, p.GetScore("step1"));
        }

        [Test]
        public void CompleteAfterHint_GivesScore1()
        {
            var p = new MicrostepProgressor(config, ScaffoldingLevel.Full);
            p.Begin(0f);
            // ScaffoldingLevel.Full pokazuje hint od razu → score = WithHint
            p.CompleteCurrent(1f, independent: true);

            Assert.AreEqual(MicrostepScore.WithHint, p.GetScore("step1"));
        }

        [Test]
        public void FailCurrent_GivesScore0_AndAdvances()
        {
            var p = new MicrostepProgressor(config, ScaffoldingLevel.None);
            p.Begin(0f);
            p.FailCurrent(1f, "wrong_action");

            Assert.AreEqual(MicrostepScore.Failed, p.GetScore("step1"));
            Assert.AreEqual("step2", p.CurrentStep.id);
        }

        [Test]
        public void Tick_AfterTimeout_FailsStep()
        {
            var p = new MicrostepProgressor(config, ScaffoldingLevel.None);
            p.Begin(0f);
            p.Tick(11f); // > timeoutSeconds

            Assert.AreEqual(MicrostepScore.Failed, p.GetScore("step1"));
            Assert.AreEqual("step2", p.CurrentStep.id);
        }

        [Test]
        public void Tick_PartialScaffolding_HintAfterDelay()
        {
            var p = new MicrostepProgressor(config, ScaffoldingLevel.Partial);
            int hints = 0;
            p.OnHintShown += _ => hints++;
            p.Begin(0f);

            p.Tick(1f); // przed hintDelay
            Assert.AreEqual(0, hints);

            p.Tick(3f); // po hintDelay (2s)
            Assert.AreEqual(1, hints);
        }

        [Test]
        public void CompleteAll_EmitsScenarioCompleted()
        {
            var p = new MicrostepProgressor(config, ScaffoldingLevel.None);
            bool done = false;
            p.OnScenarioCompleted += () => done = true;
            p.Begin(0f);
            p.CompleteCurrent(1f);
            p.CompleteCurrent(2f);
            p.CompleteCurrent(3f);

            Assert.IsTrue(done);
            Assert.IsTrue(p.IsFinished);
        }

        [Test]
        public void GetTotalScore_SumsAllScores()
        {
            var p = new MicrostepProgressor(config, ScaffoldingLevel.None);
            p.Begin(0f);
            p.CompleteCurrent(1f, independent: true);  // 2
            p.CompleteCurrent(2f, independent: false); // 1
            p.FailCurrent(3f);                         // 0

            Assert.AreEqual(3, p.GetTotalScore());
        }
    }
}
