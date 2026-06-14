using UnityEngine;

namespace MRCrisisTrainer.Config
{
    [CreateAssetMenu(fileName = "VehicleConfig", menuName = "MRCrisis/Vehicle Config", order = 2)]
    public class VehicleConfig : ScriptableObject
    {
        [Header("Skid event")]
        [Tooltip("Sekundy od startu Aktu II do wywołania poślizgu")]
        public float secondsBeforeSkid = 8f;

        [Tooltip("Początkowy kąt poślizgu (stopnie) - znak ujemny = poślizg w lewo")]
        public float initialSkidAngleDeg = 35f;

        [Tooltip("Prędkość narastania poślizgu jeśli gracz nie reaguje (deg/s)")]
        public float skidGrowthRateDeg = 8f;

        [Tooltip("Próg utraty kontroli - kąt powyżej którego scena jest porażką")]
        public float catastrophicAngleDeg = 80f;

        [Header("Steering")]
        [Tooltip("Maksymalny kąt obrotu kierownicy WIZUALNIE (stopnie w jedną stronę)")]
        public float maxSteeringAngleDeg = 540f;

        [Tooltip("Kąt obrotu kierownicy = PEŁNE sterowanie (NormalizedSteering=±1). Mały, by ręka (łuk ~90°) dawała mocny skręt → poślizg da się opanować dłońmi.")]
        public float steeringFullLockDeg = 95f;

        [Tooltip("Jak mocno skręt kierownicy redukuje kąt poślizgu (deg/s przy max sterowaniu)")]
        public float steeringCorrectionRate = 45f;

        [Tooltip("Minimalna proporcja correct-direction steering (0..1) potrzebna do recovery")]
        [Range(0.4f, 1f)] public float minCorrectionRatio = 0.55f;

        [Header("Recovery")]
        [Tooltip("Kąt poślizgu poniżej którego scena jest uznana za odzyskaną")]
        public float recoveryAngleThresholdDeg = 5f;

        [Tooltip("Czas (sekundy) jaki gracz musi utrzymać niski kąt by ukończyć akt")]
        public float recoveryHoldDuration = 1.5f;

        [Tooltip("Maksymalny czas (sekundy) na opanowanie poślizgu - po nim porażka")]
        public float skidTimeout = 12f;

        [Header("Hand grip detection")]
        [Tooltip("Promień (m) wokół kierownicy w którym ręka jest uznana za chwytającą")]
        public float gripRadius = 0.18f;

        [Tooltip("Jak długo ręka musi być w gripie zanim 'driving' się włączy")]
        public float gripHoldToStart = 0.4f;
    }
}
