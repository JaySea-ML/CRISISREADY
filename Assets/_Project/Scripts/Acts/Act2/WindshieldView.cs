using UnityEngine;

namespace MRCrisisTrainer.Acts.Act2
{
    /// <summary>
    /// Symuluje widok przez przednią szybę - obraca/przesuwa świat (drogę) reagując
    /// na skid angle. Statyczna kamera gracza, ruchomy świat - cheap dla Quest 3.
    /// Poślizg = obrót (yaw) + przechył (roll) + WSTRZĄS (shake) + „fishtail" (zarzucanie tyłem),
    /// żeby utrata przyczepności była wyraźnie czytelna.
    /// </summary>
    public class WindshieldView : MonoBehaviour
    {
        [SerializeField] private Transform worldRoot;
        [SerializeField] private float yawSensitivity = 1.5f;
        [SerializeField] private float yawSmoothing = 6f;
        [SerializeField] private float rollSensitivity = 0.55f;   // mocniejszy przechył w poślizgu (filmowo)
        [SerializeField] private float maxRollDeg = 18f;
        [SerializeField] private float fishtailDeg = 6f;          // amplituda zarzucania tyłem podczas poślizgu
        [SerializeField] private float fishtailSpeed = 7f;        // jak szybko auto „macha" tyłem
        [SerializeField] private float shakeAmp = 0.06f;          // amplituda wstrząsu (m) przy pełnej energii
        [SerializeField] private float shakeDecay = 1.8f;         // jak szybko wstrząs wygasa

        private float worldYaw, worldRoll;
        private float targetYaw, targetRoll;
        private float skidMag;        // 0..1 jak mocny jest aktualny poślizg (do fishtaila/shake)
        private float shakeEnergy;    // 0..~1 bieżąca energia wstrząsu
        private float seed;
        private float laneX;          // wygładzony tor jazdy (bok) — patrz LateralOffset

        /// <summary>Boczne przesunięcie świata (m) = tor jazdy sterowany kierownicą (Act2DriveDirector to ustawia).
        /// WindshieldView jest JEDYNYM pisarzem worldRoot.localPosition — koniec konfliktu o ten sam transform.</summary>
        public float LateralOffset { get; set; }

        void Awake() { seed = Mathf.Repeat(transform.position.x * 13.17f + 4.2f, 100f); }

        public void SetActive(bool active)
        {
            enabled = active;
            if (worldRoot != null)
            {
                worldRoot.gameObject.SetActive(active);
                if (active) { laneX = worldRoot.localPosition.x; LateralOffset = laneX; }   // start z bieżącego pasa (bez skoku przy starcie jazdy)
            }
        }

        void Update()
        {
            if (worldRoot == null) return;
            worldYaw = Mathf.Lerp(worldYaw, targetYaw, Time.deltaTime * yawSmoothing);
            worldRoll = Mathf.Lerp(worldRoll, targetRoll, Time.deltaTime * yawSmoothing);

            // Fishtail: gdy trwa poślizg, tył „macha" — dodatkowy oscylujący yaw/roll proporcjonalny do siły poślizgu.
            float wob = Mathf.Sin(Time.time * fishtailSpeed + seed) * fishtailDeg * skidMag;
            float wobRoll = Mathf.Cos(Time.time * fishtailSpeed * 0.85f + seed) * (fishtailDeg * 0.4f) * skidMag;
            worldRoot.localRotation = Quaternion.Euler(0f, worldYaw + wob, worldRoll + wobRoll);

            // Pozycja świata = TOR JAZDY (LateralOffset, sterowany kierownicą) + WSTRZĄS (Perlin).
            // JEDEN pisarz localPosition → koniec konfliktu z Act2DriveDirector (wcześniej oba pisały i kasowały się,
            // przez co „ruszało się autem, ale nic to nie dawało"). Teraz tor jazdy naprawdę działa.
            laneX = Mathf.MoveTowards(laneX, LateralOffset, Time.deltaTime * 6f);
            float shakeX = 0f, shakeY = 0f;
            if (shakeEnergy > 0.0001f)
            {
                float t = Time.time * 22f;
                float k = shakeEnergy * shakeAmp;
                shakeX = (Mathf.PerlinNoise(seed, t) - 0.5f) * 2f * k;
                shakeY = (Mathf.PerlinNoise(seed + 9.1f, t) - 0.5f) * 2f * k * 0.6f;
                shakeEnergy = Mathf.Max(0f, shakeEnergy - Time.deltaTime * shakeDecay);
            }
            worldRoot.localPosition = new Vector3(laneX + shakeX, shakeY, 0f);
        }

        /// <summary>Ustawia kąt obrotu świata wynikający z poślizgu. Większy kąt = mocniejszy przechył, fishtail i wstrząs.</summary>
        public void SetSkidYaw(float skidAngleDeg)
        {
            targetYaw = -skidAngleDeg * yawSensitivity;
            targetRoll = Mathf.Clamp(skidAngleDeg * rollSensitivity, -maxRollDeg, maxRollDeg);
            skidMag = Mathf.Clamp01(Mathf.Abs(skidAngleDeg) / 22f);
            // delikatny ciągły wstrząs podczas poślizgu (utrata przyczepności)
            if (skidMag > 0.15f && shakeEnergy < skidMag * 0.5f) shakeEnergy = skidMag * 0.5f;
        }

        /// <summary>Mocny, jednorazowy wstrząs (np. moment wpadnięcia w poślizg). 0..1.</summary>
        public void AddShake(float amount) => shakeEnergy = Mathf.Clamp01(shakeEnergy + amount);

        /// <summary>Zachowane dla SteeringRecoveryDetector — teraz tylko obrót (bez podwójnego przewijania).</summary>
        public void Step(float dt, float skidAngleDeg) => SetSkidYaw(skidAngleDeg);

        public void ResetWorld()
        {
            worldYaw = 0; targetYaw = 0; worldRoll = 0; targetRoll = 0; skidMag = 0; shakeEnergy = 0;
            if (worldRoot != null) { worldRoot.localRotation = Quaternion.identity; worldRoot.localPosition = Vector3.zero; }
        }
    }
}
