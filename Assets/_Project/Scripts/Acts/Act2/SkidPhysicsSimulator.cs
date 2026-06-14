using MRCrisisTrainer.Config;

namespace MRCrisisTrainer.Acts.Act2
{
    /// <summary>
    /// Symuluje kąt poślizgu (yaw) auta jako funkcję czasu i sterowania.
    /// Kąt > 0 = ślizg w prawo (tył w prawo), kąt < 0 = ślizg w lewo.
    /// Counter-steering = sterowanie w stronę ślizgu (taki sam znak jak kąt).
    /// </summary>
    public class SkidPhysicsSimulator
    {
        private readonly VehicleConfig config;
        private float currentAngleDeg;
        private float belowThresholdTime;
        private float totalElapsed;
        private bool catastrophe;
        private bool recovered;

        public float CurrentAngleDeg => currentAngleDeg;
        public float TotalElapsed => totalElapsed;
        public bool IsCatastrophe => catastrophe;
        public bool IsRecovered => recovered;

        public SkidPhysicsSimulator(VehicleConfig config, float initialAngleDeg)
        {
            this.config = config;
            currentAngleDeg = initialAngleDeg;
            belowThresholdTime = 0f;
            totalElapsed = 0f;
        }

        /// <param name="steeringNormalized">-1..1, -1 = full left, +1 = full right</param>
        /// <param name="dt">delta time seconds</param>
        public void Step(float steeringNormalized, float dt)
        {
            if (recovered) return;   // bez „katastrofy"/timeoutu — z poślizgu ZAWSZE da się wyjść skrętem (koniec „auto stoi na ukos")

            totalElapsed += dt;

            float sign = currentAngleDeg >= 0 ? 1f : -1f;
            float mag = System.Math.Abs(steeringNormalized);

            if (mag > 0.12f)
            {
                // KAŻDY zdecydowany skręt PROSTUJE auto, z PEŁNĄ siłą w intuicyjnym kierunku:
                // przechylony w prawo → skręć w lewo → odbijasz na lewo do prostej (i odwrotnie).
                float correction = mag * config.steeringCorrectionRate * dt;
                if (currentAngleDeg > 0f) currentAngleDeg = System.Math.Max(0f, currentAngleDeg - correction);
                else currentAngleDeg = System.Math.Min(0f, currentAngleDeg + correction);
            }
            else if (System.Math.Abs(currentAngleDeg) > config.recoveryAngleThresholdDeg)
            {
                // Brak reakcji → poślizg lekko narasta (trzeba zareagować), ale z bezpiecznym limitem (bez „crasha").
                currentAngleDeg += config.skidGrowthRateDeg * sign * dt;
                float cap = config.catastrophicAngleDeg > 1f ? config.catastrophicAngleDeg : 80f;
                if (currentAngleDeg > cap) currentAngleDeg = cap;
                else if (currentAngleDeg < -cap) currentAngleDeg = -cap;
            }

            float abs = System.Math.Abs(currentAngleDeg);
            if (abs <= config.recoveryAngleThresholdDeg)
            {
                belowThresholdTime += dt;
                if (belowThresholdTime >= config.recoveryHoldDuration)
                    recovered = true;
            }
            else
            {
                belowThresholdTime = 0f;
            }
        }

        public void Reset(float initialAngleDeg)
        {
            currentAngleDeg = initialAngleDeg;
            belowThresholdTime = 0f;
            totalElapsed = 0f;
            catastrophe = false;
            recovered = false;
        }
    }
}
