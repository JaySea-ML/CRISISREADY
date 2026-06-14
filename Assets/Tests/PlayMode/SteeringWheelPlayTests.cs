using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using MRCrisisTrainer.Acts.Act2;
using MRCrisisTrainer.Config;
using MRCrisisTrainer.XR;

namespace MRCrisisTrainer.Tests
{
    /// <summary>
    /// Weryfikuje mechanikę kierownicy (sterowanie WZGLĘDNE: chwyt = neutralny, obrót ręki = skręt)
    /// oraz że poślizg da się opanować dłońmi (NormalizedSteering wystarczająco mocny).
    /// </summary>
    public class SteeringWheelPlayTests
    {
        private class MockHands : IHandPoseProvider
        {
            public Vector3 pos;
            public bool tracked;
            public bool TryGetHandPosition(HandSide side, out Vector3 worldPosition) { worldPosition = pos; return tracked; }
            public bool TryGetPalmRotation(HandSide side, out Quaternion worldRotation) { worldRotation = Quaternion.identity; return false; }
            public bool IsTracked(HandSide side) => tracked;
        }

        private static void SetField(object o, string name, object val) =>
            o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, val);

        private static (SteeringWheelController, MockHands, VehicleConfig, GameObject) MakeWheel()
        {
            var cfg = ScriptableObject.CreateInstance<VehicleConfig>();
            cfg.maxSteeringAngleDeg = 540f;
            cfg.steeringFullLockDeg = 95f;
            cfg.gripRadius = 0.2f;
            cfg.gripHoldToStart = 0.05f;
            var go = new GameObject("WheelTest");
            go.transform.position = Vector3.zero;
            // Osobny obiekt na wizualny obrót (jak w grze: wheelCenter stały, wheelTransform osobny),
            // żeby obrót koła NIE obracał układu odniesienia środka.
            var visual = new GameObject("WheelVisual");
            visual.transform.SetParent(go.transform, false);
            var ctrl = go.AddComponent<SteeringWheelController>();
            var mock = new MockHands { tracked = true };
            SetField(ctrl, "config", cfg);
            SetField(ctrl, "wheelCenter", go.transform);     // stabilny środek (forward=+Z, up=+Y)
            SetField(ctrl, "wheelTransform", visual.transform);
            SetField(ctrl, "handProvider", mock);
            return (ctrl, mock, cfg, go);
        }

        // Chwyt u góry (12) = neutralny; obrót ręki w PRAWO => skręt w PRAWO (dodatni), w LEWO => ujemny.
        [UnityTest]
        public IEnumerator HandRotation_RightIsPositive_LeftIsNegative()
        {
            var (ctrl, mock, cfg, go) = MakeWheel();

            mock.pos = new Vector3(0f, 0.16f, 0f);  // chwyt na górze koła (neutralny punkt)
            yield return null; yield return null;
            Assert.IsTrue(ctrl.IsGripped, "Ręka przy kierownicy powinna chwycić.");
            Assert.Less(Mathf.Abs(ctrl.NormalizedSteering), 0.1f, "Sam chwyt (bez obrotu) = neutralnie.");

            mock.pos = new Vector3(0.16f, 0f, 0f);  // obrót ręki do PRAWEJ (3 godzina)
            yield return null; yield return null;
            float right = ctrl.NormalizedSteering;

            mock.pos = new Vector3(0f, 0.16f, 0f);  // z powrotem na górę
            yield return null; yield return null;
            mock.pos = new Vector3(-0.16f, 0f, 0f); // obrót do LEWEJ (9 godzina)
            yield return null; yield return null;
            float left = ctrl.NormalizedSteering;

            Object.DestroyImmediate(go); Object.DestroyImmediate(cfg);

            Assert.Greater(right, 0.3f, $"Ręka w prawo => skręt w prawo (dodatni). right={right}");
            Assert.Less(left, -0.3f, $"Ręka w lewo => skręt w lewo (ujemny). left={left}");
        }

        // Puszczenie kierownicy => auto-center do 0.
        [UnityTest]
        public IEnumerator ReleasingWheel_AutoCenters()
        {
            var (ctrl, mock, cfg, go) = MakeWheel();
            mock.pos = new Vector3(0f, 0.16f, 0f); yield return null; yield return null;
            mock.pos = new Vector3(0.16f, 0f, 0f); yield return null; yield return null;
            Assert.Greater(Mathf.Abs(ctrl.NormalizedSteering), 0.3f);

            mock.tracked = false; // puszczenie kierownicy
            for (int i = 0; i < 300; i++) yield return null; // auto-center
            Object.DestroyImmediate(go); Object.DestroyImmediate(cfg);
            Assert.Less(Mathf.Abs(ctrl.NormalizedSteering), 0.06f, "Po puszczeniu kierownica wraca do środka.");
        }

        [UnityTest]
        public IEnumerator ApplySteeringByValue_Sets_Normalized()
        {
            var cfg = ScriptableObject.CreateInstance<VehicleConfig>();
            cfg.maxSteeringAngleDeg = 540f; cfg.steeringFullLockDeg = 95f;
            var go = new GameObject("WheelTest2");
            var ctrl = go.AddComponent<SteeringWheelController>();
            SetField(ctrl, "config", cfg);
            SetField(ctrl, "wheelTransform", go.transform);
            yield return null;
            ctrl.ApplySteeringByValue(0.5f);
            float n = ctrl.NormalizedSteering;
            Object.DestroyImmediate(go); Object.DestroyImmediate(cfg);
            Assert.AreEqual(0.5f, n, 0.02f);
        }
    }
}
