using NUnit.Framework;
using MRCrisisTrainer.Acts.Act2;

namespace MRCrisisTrainer.Tests
{
    public class CounterSteerEvaluatorTests
    {
        [TestCase(35f, 1f, SteeringFeedback.Correct)]   // skid right, steer right
        [TestCase(-35f, -1f, SteeringFeedback.Correct)] // skid left, steer left
        [TestCase(35f, -1f, SteeringFeedback.Wrong)]    // skid right, steer left
        [TestCase(-35f, 1f, SteeringFeedback.Wrong)]    // skid left, steer right
        [TestCase(35f, 0f, SteeringFeedback.Neutral)]   // no input
        [TestCase(35f, 0.05f, SteeringFeedback.Neutral)] // deadzone
        [TestCase(0f, 1f, SteeringFeedback.Neutral)]    // no skid yet
        public void Evaluate_CategorizesCorrectly(float skid, float steering, SteeringFeedback expected)
        {
            Assert.AreEqual(expected, CounterSteerEvaluator.Evaluate(skid, steering));
        }
    }
}
