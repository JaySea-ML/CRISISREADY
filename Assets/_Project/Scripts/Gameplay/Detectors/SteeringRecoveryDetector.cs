using System;
using UnityEngine;
using MRCrisisTrainer.Config;
using MRCrisisTrainer.Acts.Act2;

namespace MRCrisisTrainer.Gameplay.Detectors
{
    /// <summary>
    /// Mikrokrok kontrasterowania: symuluje poślizg, gracz steruje kierownicą.
    /// Sukces gdy odzyska kontrolę (kąt < próg przez czas). Porażka przy katastrofie/timeout.
    /// </summary>
    public class SteeringRecoveryDetector : MicrostepDetector
    {
        [SerializeField] private VehicleConfig config;
        [SerializeField] private SteeringWheelController wheel;
        [SerializeField] private WindshieldView windshield;
        [SerializeField] private float initialDirection = 1f; // +1 prawo, -1 lewo

        public event Action<float, SteeringFeedback> OnSkidUpdate; // angle, feedback

        private SkidPhysicsSimulator skid;

        protected override void OnBegin()
        {
            float dir = Mathf.Sign(initialDirection == 0 ? 1 : initialDirection);
            skid = new SkidPhysicsSimulator(config, config.initialSkidAngleDeg * dir);
            windshield?.SetActive(true);
        }

        void Update()
        {
            if (!IsActive || skid == null) return;
            float steering = wheel != null ? wheel.NormalizedSteering : 0f;
            skid.Step(steering, Time.deltaTime);
            var fb = CounterSteerEvaluator.Evaluate(skid.CurrentAngleDeg, steering);
            OnSkidUpdate?.Invoke(skid.CurrentAngleDeg, fb);
            windshield?.Step(Time.deltaTime, skid.CurrentAngleDeg);

            if (skid.IsRecovered) Complete();
            else if (skid.IsCatastrophe) Fail("crash");
        }
    }
}
