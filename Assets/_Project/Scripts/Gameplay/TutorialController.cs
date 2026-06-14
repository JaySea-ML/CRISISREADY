using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using MRCrisisTrainer.Acts.Act2;
using MRCrisisTrainer.Acts.Act3;
using MRCrisisTrainer.Core;

namespace MRCrisisTrainer.Gameplay
{
    /// <summary>
    /// Sesja treningowa (samouczek) PRZED główną grą — tylko ręce. Trzy ćwiczenia po kolei:
    ///   1) KIEROWNICA: złap koło i skręć w prawo, potem w lewo.
    ///   2) MOWA: przeczytaj na głos pokazaną formułkę (offline wykrywamy głos, nie dokładne słowa).
    ///   3) CISZA: utrzymaj ciszę przez 30 s.
    /// Po zaliczeniu wszystkich pojawia się przycisk GRAJ (SceneLoadButton → główna gra).
    /// </summary>
    public class TutorialController : MonoBehaviour
    {
        [SerializeField] private SteeringWheelController wheel;
        [SerializeField] private MicrophoneInputProvider mic;
        [SerializeField] private TMP_Text prompt;
        [SerializeField] private GameObject playButton;     // GRAJ — aktywny dopiero po treningu
        [SerializeField] private string nextScene = "TrainingRoom";
        [SerializeField] private float speechThreshold = 0.02f;
        [SerializeField] private float silenceThreshold = 0.03f;
        [SerializeField] private float speechSeconds = 2.5f;
        [SerializeField] private float silenceSeconds = 30f;
        [SerializeField] [TextArea] private string phrase =
            "Dzwonię pod 112. Ktoś włamuje się do mojego domu, jestem sam w mieszkaniu.";

        void Start() => StartCoroutine(Run());

        private IEnumerator Run()
        {
            if (playButton != null) playButton.SetActive(false);
            Set("SESJA TRENINGOWA\n\nZanim zaczniesz, przećwiczysz 3 rzeczy.\nGrasz tylko dłońmi.");
            yield return new WaitForSeconds(2.5f);
            yield return StartCoroutine(Steer());
            yield return StartCoroutine(Speak());
            yield return StartCoroutine(Silence());
            Set("TRENING UKOŃCZONY!\n\nNaciśnij GRAJ, aby rozpocząć.\n(gra ruszy też sama za chwilę)");
            AudioManager.Instance?.PlaySfx("success_chime", 1f);
            if (playButton != null)
            {
                playButton.SetActive(true);
                var btn = playButton.GetComponent<Button>();
                if (btn != null) btn.onClick.AddListener(LoadNext);
            }
            // SAFETY: gwarancja wejścia do gry — auto-start po 15 s, gdyby klik GRAJ nie zadziałał w VR
            float waited = 0f;
            while (waited < 15f && !loaded) { waited += Time.deltaTime; yield return null; }
            if (!loaded) LoadNext();
        }

        private bool loaded;
        private void LoadNext()
        {
            if (loaded) return; loaded = true;
            string cur = SceneManager.GetActiveScene().name;
            if (SceneLoader.Instance != null) SceneLoader.Instance.LoadActAdditive(nextScene, cur);
            else SceneManager.LoadScene(nextScene);
        }

        private IEnumerator Steer()
        {
            Set("ĆWICZENIE 1 z 3 — KIEROWNICA\n\nZłap kierownicę dłonią\ni SKRĘĆ W PRAWO.");
            yield return Hold(() => wheel != null && wheel.NormalizedSteering > 0.5f, 0.4f);
            AudioManager.Instance?.PlaySfx("ui_click", 0.8f);
            Set("Dobrze! Teraz SKRĘĆ W LEWO.");
            yield return Hold(() => wheel != null && wheel.NormalizedSteering < -0.5f, 0.4f);
            Set("Kierownica opanowana — zaliczone!");
            AudioManager.Instance?.PlaySfx("success_chime", 0.8f);
            yield return new WaitForSeconds(1.5f);
        }

        private IEnumerator Speak()
        {
            Set($"ĆWICZENIE 2 z 3 — MOWA\n\nPrzeczytaj na głos, wyraźnie:\n\n„{phrase}\"");
            float acc = 0f, t = 0f;
            while (acc < speechSeconds)
            {
                t += Time.deltaTime;
                if (t > 20f) break;   // auto-przejdź (nie blokuj gry jeśli mikrofon nie łapie)
                if (mic != null && mic.CurrentVolume > speechThreshold) acc += Time.deltaTime;
                else acc = Mathf.Max(0f, acc - Time.deltaTime * 0.4f);
                yield return null;
            }
            Set("Głos wyraźny — zaliczone!");
            AudioManager.Instance?.PlaySfx("success_chime", 0.8f);
            yield return new WaitForSeconds(1.5f);
        }

        private IEnumerator Silence()
        {
            float silent = 0f, t = 0f;
            while (silent < silenceSeconds)
            {
                t += Time.deltaTime;
                if (t > silenceSeconds + 30f) break;   // twardy limit — nie blokuj gry, gdyby mikrofon ciągle łapał hałas
                bool quiet = mic == null || mic.CurrentVolume < silenceThreshold;
                silent = quiet ? silent + Time.deltaTime : Mathf.Max(0f, silent - 1f);   // hałas cofa postęp
                int left = Mathf.CeilToInt(silenceSeconds - silent);
                Set($"ĆWICZENIE 3 z 3 — CISZA\n\nBądź cicho i nie ruszaj się.\nPozostało: {left} s");
                yield return null;
            }
            Set("Cisza utrzymana — zaliczone!");
            AudioManager.Instance?.PlaySfx("success_chime", 0.8f);
            yield return new WaitForSeconds(1.2f);
        }

        private void Set(string t) { if (prompt != null) prompt.text = t; }

        private IEnumerator Hold(System.Func<bool> cond, float hold)
        {
            float h = 0f, t = 0f;
            while (h < hold)
            {
                t += Time.deltaTime;
                if (t > 22f) yield break;   // auto-przejdź jeśli nie wykryto (NIGDY nie blokuj wejścia do gry)
                h = cond() ? h + Time.deltaTime : 0f;
                yield return null;
            }
        }
    }
}
