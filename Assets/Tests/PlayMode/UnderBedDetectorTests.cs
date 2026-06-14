using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using MRCrisisTrainer.Gameplay.Detectors;

namespace MRCrisisTrainer.Tests
{
    /// <summary>
    /// Weryfikuje, że „wczołganie się pod łóżko" jest WYKONALNE w SIEDZĄCYM MR:
    /// gracz NIE podchodzi do łóżka (nie może), tylko SCHYLA głowę w dół — i krok się zalicza.
    /// </summary>
    public class UnderBedDetectorTests
    {
        private static void SetField(object o, string name, float v)
            => o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);

        [UnityTest]
        public IEnumerator SeatedDuck_NearBed_Completes()
        {
            // IZOLACJA: inne testy (SceneSmokeTests) zostawiają załadowaną scenę TrainingRoom z własnymi kamerami,
            // przez co Camera.main wskazywałaby nie tę kamerę. Dezaktywujemy istniejące kamery → Camera.main = nasza.
            foreach (var existing in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                existing.gameObject.SetActive(false);

            // Kamera „HMD" na wysokości oczu siedzącego gracza
            var camGo = new GameObject("TestHMD"); camGo.tag = "MainCamera";
            camGo.AddComponent<Camera>();
            camGo.transform.position = new Vector3(0f, 1.2f, 0f);

            // Strefa pod łóżkiem ~1.2 m przed graczem (siedzący NIE dojdzie tam pieszo)
            var zone = new GameObject("Zone").transform;
            zone.position = new Vector3(0f, 0.10f, 1.2f);

            var det = new GameObject("UnderBed").AddComponent<UnderBedDetector>();
            det.GetType().GetField("hideZone", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(det, zone);
            SetField(det, "horizontalRadius", 1.8f);
            SetField(det, "maxHeadHeight", 1.0f);
            SetField(det, "duckDelta", 0.25f);
            SetField(det, "baselineWindow", 0.4f);
            SetField(det, "requiredHold", 0.5f);

            bool done = false;
            det.OnCompleted += () => done = true;
            det.Begin();

            // Faza 1 (0.6 s): siedzi wyprostowany — krok NIE powinien się zaliczyć
            float t = 0f;
            while (t < 0.6f) { t += Time.deltaTime; yield return null; }
            Assert.IsFalse(done, "Sam siedzenie (bez schylenia) NIE powinno zaliczać kroku.");

            // Faza 2: SCHYLA głowę o 0.40 m (siedzący gest „chowam się pod łóżko")
            camGo.transform.position = new Vector3(0f, 0.80f, 0f);
            float t2 = 0f;
            while (t2 < 2.0f && !done) { t2 += Time.deltaTime; yield return null; }

            Debug.Log($"[UBTest] Camera.main={(Camera.main != null ? Camera.main.name : "NULL")} camY={camGo.transform.position.y} done={done}");
            Assert.IsTrue(done, "Schylenie głowy (siedząc) w pobliżu łóżka powinno zaliczyć 'wczołganie się pod łóżko'.");

            Object.Destroy(camGo); Object.Destroy(zone.gameObject); Object.Destroy(det.gameObject);
        }
    }
}
