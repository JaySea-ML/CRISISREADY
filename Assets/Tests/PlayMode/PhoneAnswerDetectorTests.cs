using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using MRCrisisTrainer.Gameplay.Detectors;

namespace MRCrisisTrainer.Tests
{
    /// <summary>
    /// Weryfikuje NIEZAWODNY odbiór telefonu przez SPOJRZENIE (bez śledzenia dłoni/padów):
    /// gracz patrzy na telefon → krok się zalicza; patrzy w bok → nie.
    /// </summary>
    public class PhoneAnswerDetectorTests
    {
        private static void Set(object o, string name, float v)
            => o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);

        private static PhoneAnswerDetector MakeDetector(Transform phone)
        {
            var det = new GameObject("PhoneAnswer").AddComponent<PhoneAnswerDetector>();
            det.GetType().GetField("phone", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(det, phone);
            Set(det, "gazeHold", 0.5f);
            Set(det, "gazeAngle", 26f);
            Set(det, "gazeDistance", 3f);
            Set(det, "maxSeconds", 0f);   // bez timeoutu — tylko spojrzenie może zaliczyć
            return det;
        }

        [UnityTest]
        public IEnumerator Gaze_AtPhone_Answers()
        {
            foreach (var existing in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                existing.gameObject.SetActive(false);
            var camGo = new GameObject("TestHMD"); camGo.tag = "MainCamera"; camGo.AddComponent<Camera>();
            camGo.transform.position = Vector3.zero; camGo.transform.rotation = Quaternion.identity; // patrzy +Z

            var phone = new GameObject("Phone").transform;
            phone.position = new Vector3(0f, 0f, 1f);   // 1 m PROSTO przed kamerą

            var det = MakeDetector(phone);
            bool done = false; det.OnCompleted += () => done = true;
            det.Begin();

            float t = 0f;
            while (t < 1.2f && !done) { t += Time.deltaTime; yield return null; }
            Assert.IsTrue(done, "Patrzenie na telefon powinno go odebrać.");

            Object.Destroy(camGo); Object.Destroy(phone.gameObject); Object.Destroy(det.gameObject);
        }

        [UnityTest]
        public IEnumerator Gaze_AwayFromPhone_DoesNotAnswer()
        {
            foreach (var existing in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                existing.gameObject.SetActive(false);
            var camGo = new GameObject("TestHMD2"); camGo.tag = "MainCamera"; camGo.AddComponent<Camera>();
            camGo.transform.position = Vector3.zero; camGo.transform.rotation = Quaternion.identity; // patrzy +Z

            var phone = new GameObject("Phone2").transform;
            phone.position = new Vector3(2f, 0f, 0.3f);   // mocno w BOK (kąt > 26°)

            var det = MakeDetector(phone);
            bool done = false; det.OnCompleted += () => done = true;
            det.Begin();

            float t = 0f;
            while (t < 1.0f) { t += Time.deltaTime; yield return null; }
            Assert.IsFalse(done, "Patrzenie w bok NIE powinno odbierać telefonu.");

            det.Cancel();
            Object.Destroy(camGo); Object.Destroy(phone.gameObject); Object.Destroy(det.gameObject);
        }
    }
}
