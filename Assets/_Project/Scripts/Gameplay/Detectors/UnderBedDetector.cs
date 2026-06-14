using System;
using System.Collections.Generic;
using UnityEngine;
using MRCrisisTrainer.Logging;

namespace MRCrisisTrainer.Gameplay.Detectors
{
    /// <summary>
    /// Mikrokrok „wczołgaj się pod łóżko" — wersja dla SIEDZĄCEGO MR.
    /// Gracz siedzi w fotelu i NIE może podejść do łóżka ani położyć się na podłodze, więc
    /// gestem „chowania się" jest SCHYLENIE GŁOWY NISKO (pochyl się do przodu i w dół, jakbyś
    /// wciskał się pod łóżko). Krok zalicza się, gdy głowa (HMD) jest:
    ///   • w hojnej strefie poziomej wokół łóżka (siedzący i tak jest blisko — pokój „nakłada się"), ORAZ
    ///   • NISKO: albo absolutnie blisko podłogi (stojący/wczołgujący), albo SCHYLONA o `duckDelta`
    ///     względem wyjściowej (wyprostowanej) wysokości głowy zmierzonej na początku kroku.
    /// To czyni krok WYKONALNYM siedząc (wcześniej wymagał fizycznego podejścia i zejścia ≤0.9 m,
    /// czego siedzący gracz nie spełniał → krok zawsze timeoutował).
    /// </summary>
    public class UnderBedDetector : MicrostepDetector
    {
        [SerializeField] private Transform hideZone;             // środek strefy pod łóżkiem (na podłodze)
        [SerializeField] private float horizontalRadius = 1.8f;  // hojnie — siedzący nie podejdzie blisko
        [SerializeField] private float maxHeadHeight = 1.0f;     // ABS: głowa nisko nad podłogą (gdy ktoś realnie się wczołguje)
        [SerializeField] private float duckDelta = 0.28f;        // LUB: schyl głowę o tyle (m) wzgl. pozycji wyjściowej (siedzący)
        [SerializeField] private float baselineWindow = 0.6f;    // ile s zbieramy wyprostowaną wysokość głowy (pozycja wyjściowa)
        [SerializeField] private float requiredHold = 0.6f;      // utrzymaj „schowany" tyle sekund
        [SerializeField] private float failTimeout = 0f;         // 0 = bez porażki własnej (czeka aż się schowa / scenariusz ma swój timeout)

        public event Action<float> OnProgress;  // 0..1 ile trzymane

        private Camera cam;
        private float held;
        private float elapsed;
        private float lastLog;
        private float baselineHead;   // wyprostowana wysokość głowy (pozycja wyjściowa), max z pierwszych baselineWindow s

        protected override void OnBegin()
        {
            held = 0f; elapsed = 0f; lastLog = 0f; baselineHead = 0f;
            cam = ResolveCamera();
            Log("start");
        }

        void Update()
        {
            if (!IsActive) return;
            if (cam == null) cam = ResolveCamera();
            if (cam == null || hideZone == null) return;

            elapsed += Time.deltaTime;

            Vector3 camPos = cam.transform.position;
            Vector3 zonePos = hideZone.position;
            float dx = camPos.x - zonePos.x, dz = camPos.z - zonePos.z;
            float horiz = Mathf.Sqrt(dx * dx + dz * dz);
            float headAbove = camPos.y - zonePos.y;   // wysokość głowy nad strefą (podłogą)

            // Pozycja wyjściowa = najwyższa głowa w pierwszych baselineWindow s (gracz siedzi wyprostowany).
            if (elapsed <= baselineWindow) baselineHead = Mathf.Max(baselineHead, camPos.y);

            bool nearBed = horiz <= horizontalRadius;
            bool duckedAbsolute = headAbove <= maxHeadHeight;                    // realne wczołganie nisko
            bool duckedRelative = (baselineHead - camPos.y) >= duckDelta;        // siedzący: schylił głowę
            bool low = duckedAbsolute || duckedRelative;

            // Liczymy „schowanie" dopiero po ustaleniu pozycji wyjściowej.
            if (elapsed > baselineWindow && nearBed && low) held += Time.deltaTime;
            else held = Mathf.Max(0f, held - Time.deltaTime * 2f);

            OnProgress?.Invoke(Mathf.Clamp01(held / Mathf.Max(0.01f, requiredHold)));

            if (Time.time - lastLog > 1.5f) { lastLog = Time.time; Log("progress", horiz, headAbove); }

            if (held >= requiredHold)
            {
                Log("complete", horiz, headAbove);
                Complete();
                return;
            }
            if (failTimeout > 0f && elapsed > failTimeout)
            {
                Log("timeout", horiz, headAbove);
                Fail("not_hidden_in_time");
            }
        }

        private static Camera ResolveCamera()
        {
            var c = Camera.main;
#if UNITY_2023_1_OR_NEWER
            if (c == null) c = UnityEngine.Object.FindFirstObjectByType<Camera>();
#else
            if (c == null) c = UnityEngine.Object.FindObjectOfType<Camera>();
#endif
            return c;
        }

        private void Log(string phase, float horiz = -1f, float head = -1f)
        {
            JSONLLogger.Instance?.LogEvent("under_bed", new Dictionary<string, object>
            {
                { "phase", phase },
                { "horiz_m", horiz },
                { "head_m", head },
                { "held_s", held }
            });
        }
    }
}
