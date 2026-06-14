using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MRCrisisTrainer.Core;
using MRCrisisTrainer.Logging;

namespace MRCrisisTrainer.Gameplay
{
    /// <summary>
    /// Orkiestruje przebieg sesji w TrainingRoom: kolejno uruchamia 2 akty (ScenarioRunner),
    /// przełącza ich obiekty (aktywne tylko bieżący akt), po wszystkich ładuje PostSession.
    /// </summary>
    public class SessionFlowManager : MonoBehaviour
    {
        [System.Serializable]
        public class Act
        {
            public string actName;
            public GameObject actRoot;       // obiekty tego aktu (włączane gdy aktywny)
            public ScenarioRunner runner;
            public float introDelay = 2f;
            public string introVoice;        // opcjonalny klip głosowy/narrator
        }

        [SerializeField] private List<Act> acts = new List<Act>();
        [SerializeField] private float betweenActsDelay = 3f;
        [SerializeField] private string postSessionScene = "PostSession";
        [SerializeField] private bool autoStart = true;
        [SerializeField] private float startDelay = 2f;

        private int currentAct = -1;
        private int totalScore;
        private int maxScore;

        void Start()
        {
            // Wyłącz wszystkie akty na start
            foreach (var a in acts) if (a.actRoot != null) a.actRoot.SetActive(false);
            if (autoStart) StartCoroutine(RunSession());
        }

        public void BeginSession() => StartCoroutine(RunSession());

        private IEnumerator RunSession()
        {
            yield return new WaitForSeconds(startDelay);
            JSONLLogger.Instance?.LogEvent("training_room_session_start", null);

            // RESTART po przegranej: „Spróbuj ponownie" wraca do AKTU Z POKOJEM (ostatni), nie do początku całej gry.
            int startAct = 0;
            if (PlayerPrefs.GetInt("retry_room_act", 0) == 1)
            {
                PlayerPrefs.DeleteKey("retry_room_act"); PlayerPrefs.Save();
                startAct = acts.Count - 1;   // ostatni akt = pokój (Akt III)
            }
            for (int i = startAct; i < acts.Count; i++)
            {
                currentAct = i;
                var act = acts[i];
                if (act.actRoot != null) act.actRoot.SetActive(true);

                JSONLLogger.Instance?.LogEvent("act_begin", new Dictionary<string, object>
                { { "act", act.actName }, { "index", i } });

                if (!string.IsNullOrEmpty(act.introVoice) && AudioManager.Instance != null)
                    AudioManager.Instance.QueueVoice(act.introVoice);

                yield return new WaitForSeconds(act.introDelay);

                bool finished = false;
                if (act.runner != null)
                {
                    act.runner.OnScenarioFinished += (t, m) => { totalScore += t; maxScore += m; finished = true; };
                    act.runner.Run();
                    while (!finished) yield return null;
                }
                else
                {
                    Debug.LogWarning($"[SessionFlow] Act {act.actName} has no runner.");
                }

                JSONLLogger.Instance?.LogEvent("act_end", new Dictionary<string, object>
                { { "act", act.actName } });

                yield return new WaitForSeconds(betweenActsDelay);
                // OSTATNIEGO aktu NIE wyłączamy — reżyser Aktu III dokańcza (syreny/ucieczka) i pokazuje ekran „UDAŁO SIĘ PRZEJŚĆ GRĘ".
                if (act.actRoot != null && i < acts.Count - 1) act.actRoot.SetActive(false);
            }

            JSONLLogger.Instance?.LogEvent("training_room_session_end", new Dictionary<string, object>
            { { "total_score", totalScore }, { "max_score", maxScore } });

            // Zapisz wynik do przekazania do PostSession
            PlayerPrefs.SetInt("last_total_score", totalScore);
            PlayerPrefs.SetInt("last_max_score", maxScore);
            PlayerPrefs.Save();

            // TESTY KOŃCOWE (PostSession: NASA-TLX/SUS/UEQ-S/...) USUNIĘTE (życzenie użytkownika) — gra kończy się
            // ekranem „UDAŁO SIĘ PRZEJŚĆ GRĘ" pokazywanym przez reżysera Aktu III. Nie ładujemy sceny PostSession.
            yield break;
        }
    }
}
