namespace MRCrisisTrainer.Acts.Act2
{
    public enum SteeringFeedback
    {
        Neutral,
        Correct,
        Wrong
    }

    /// <summary>
    /// Klasyfikuje, czy gracz steruje w stronę poślizgu (counter-steer = w stronę poślizgu)
    /// czy w przeciwną (instynktowna, błędna).
    /// </summary>
    public static class CounterSteerEvaluator
    {
        public static SteeringFeedback Evaluate(float skidAngleDeg, float steeringNormalized, float deadzone = 0.1f)
        {
            if (System.Math.Abs(steeringNormalized) < deadzone) return SteeringFeedback.Neutral;
            if (System.Math.Abs(skidAngleDeg) < 1f) return SteeringFeedback.Neutral;

            float skidSign = skidAngleDeg > 0 ? 1f : -1f;
            float steerSign = steeringNormalized > 0 ? 1f : -1f;

            return steerSign == skidSign ? SteeringFeedback.Correct : SteeringFeedback.Wrong;
        }
    }
}
