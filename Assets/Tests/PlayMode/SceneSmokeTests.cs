using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using MRCrisisTrainer.Gameplay;

namespace MRCrisisTrainer.Tests
{
    /// <summary>
    /// Smoke-test: ładuje TrainingRoom i odtwarza kilka sekund (Akt II zaczyna jazdę),
    /// sprawdzając że nie leci żaden wyjątek w runtime. To „przetestowanie samemu" bez gogli.
    /// </summary>
    public class SceneSmokeTests
    {
        private readonly List<string> exceptions = new List<string>();

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception) exceptions.Add(condition);
        }

        [UnityTest]
        public IEnumerator TrainingRoom_Runs_Without_Exceptions()
        {
            exceptions.Clear();
            Application.logMessageReceived += OnLog;

            SceneManager.LoadScene("TrainingRoom");
            yield return null; // scena ładuje się w następnej klatce
            yield return null;

            var flow = Object.FindFirstObjectByType<SessionFlowManager>();

            // Odtwórz ~8 s realtime: startDelay + introDelay → Akt II zaczyna jazdę (ForestScroller, kokpit).
            bool act2Activated = false;
            float t = 0f;
            while (t < 9f)
            {
                var act2 = GameObject.Find("Act2_Skid");
                if (act2 != null && act2.activeInHierarchy) act2Activated = true;
                t += 0.25f;
                yield return new WaitForSeconds(0.25f);
            }

            Application.logMessageReceived -= OnLog;

            Debug.Log($"[SmokeTest] SessionFlowManager={(flow != null)} Act2Activated={act2Activated} exceptions={exceptions.Count}");

            Assert.IsNotNull(flow, "SessionFlowManager powinien istnieć w TrainingRoom.");
            Assert.IsTrue(act2Activated, "Akt II (Act2_Skid) powinien się aktywować i zacząć jazdę.");
            Assert.IsEmpty(exceptions, "Runtime nie powinien rzucać wyjątków:\n" + string.Join("\n", exceptions));
        }
    }
}
