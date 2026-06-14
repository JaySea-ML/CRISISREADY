using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using MRCrisisTrainer.Acts.Act3;
using MRCrisisTrainer.Logging;

namespace MRCrisisTrainer.Gameplay.Detectors
{
    /// <summary>
    /// PRAWDZIWE rozpoznawanie mowy przez Wit.ai (HTTP POST /dictation). Nagrywa wypowiedź gracza,
    /// wysyła audio (16-bit PCM) z tokenem, odbiera transkrypcję i sprawdza, czy padły SŁOWA KLUCZOWE
    /// formułki (stemy, odporne na odmianę). Pokazuje, co usłyszał. Awaryjnie (brak sieci / timeout)
    /// i tak przechodzi dalej — gra NIGDY nie utknie na formułce.
    /// </summary>
    public class WitDictationDetector : MicrostepDetector
    {
        [SerializeField] private MicrophoneInputProvider mic;
        [SerializeField] private string witToken;
        [SerializeField] private string[] keywords;            // stemy, np. "właman","piętr"
        [SerializeField] private int requiredMatches = 1;
        [SerializeField] private string operatorVoiceClip;     // linia dyspozytora po zaliczeniu
        [SerializeField] private float volumeThreshold = 0.014f; // niżej = łatwiej złapać mowę
        [SerializeField] private float minSpeech = 0.45f;      // min. czas mówienia, by uznać wypowiedź
        [SerializeField] private float phraseEndSilence = 0.8f;// cisza po mowie = koniec wypowiedzi → wyślij
        [SerializeField] private float maxSeconds = 24f;       // awaryjne przejście (gra nie utyka)

        private float elapsed, speechAccum, quiet;
        private bool posting, finished;
        private TextMeshPro heardTmp;

        protected override void OnBegin()
        {
            elapsed = 0; speechAccum = 0; quiet = 0; posting = false; finished = false;
            // Dyspozytor PYTA na początku kroku → gracz odpowiada formułką (spójny dialog Q→A, nie odwrotnie)
            if (!string.IsNullOrEmpty(operatorVoiceClip) && Core.AudioManager.Instance != null)
                Core.AudioManager.Instance.QueueVoice(operatorVoiceClip);
        }

        void Update()
        {
            if (!IsActive || finished) return;
            elapsed += Time.deltaTime;
            float vol = mic != null ? mic.CurrentVolume : 0f;
            if (vol > volumeThreshold) { speechAccum += Time.deltaTime; quiet = 0f; }
            else if (speechAccum > minSpeech) quiet += Time.deltaTime;

            if (!posting && speechAccum >= minSpeech && quiet >= phraseEndSilence)
            {
                posting = true;
                StartCoroutine(Recognize());
            }
            if (elapsed >= maxSeconds && !finished) Finish();   // awaryjnie dalej (np. brak sieci)
        }

        private IEnumerator Recognize()
        {
            if (mic == null || string.IsNullOrEmpty(witToken) ||
                !mic.TryGetRecentSamples(speechAccum + 0.8f, out var samples, out int rate))
            { posting = false; speechAccum = 0; quiet = 0; yield break; }

            byte[] pcm = ToPcm16(samples);
            using (var req = new UnityWebRequest("https://api.wit.ai/dictation?v=20240304", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(pcm);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Authorization", "Bearer " + witToken);
                req.SetRequestHeader("Content-Type", $"audio/raw;encoding=signed-integer;bits=16;rate={rate};endian=little");
                req.timeout = 12;
                yield return req.SendWebRequest();

                bool httpOk = req.result == UnityWebRequest.Result.Success;
                string heard = httpOk ? ExtractText(req.downloadHandler.text) : "";
                if (!httpOk) Debug.LogWarning($"[Wit] HTTP błąd: {req.error}");
                bool matched = Matches(heard);
                Debug.Log($"[Wit] usłyszano: '{heard}' (match={matched})");
                // Log do metrics.jsonl — dzięki temu NA URZĄDZENIU widać, czy Wit REALNIE rozpoznaje mowę
                // (transkrypcja + czy trafił w słowa kluczowe), a nie tylko czy krok „zszedł" w ~0 s.
                JSONLLogger.Instance?.LogEvent("wit_heard", new Dictionary<string, object>
                {
                    { "step_id", stepId }, { "heard", heard }, { "matched", matched }, { "http_ok", httpOk }
                });

                if (matched)
                {
                    ShowHeard(heard, new Color(0.25f, 1f, 0.4f));   // ZIELONO = formułka rozpoznana poprawnie
                    yield return new WaitForSeconds(0.9f);
                    Finish(); yield break;
                }
                ShowHeard(string.IsNullOrEmpty(heard) ? "(nie rozpoznano — powtórz)" : heard, new Color(1f, 0.85f, 0.3f));   // żółto = słyszę, ale nie te słowa kluczowe
            }
            // brak dopasowania → pozwól powtórzyć wypowiedź
            posting = false; speechAccum = 0; quiet = 0;
        }

        private bool Matches(string heard)
        {
            if (string.IsNullOrEmpty(heard) || keywords == null || keywords.Length == 0) return false;
            string h = heard.ToLowerInvariant();
            int n = 0;
            foreach (var k in keywords)
                if (!string.IsNullOrEmpty(k) && h.Contains(k.ToLowerInvariant())) n++;
            return n >= Mathf.Max(1, requiredMatches);
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            if (heardTmp != null) Destroy(heardTmp.gameObject);
            Complete();
        }

        private static byte[] ToPcm16(float[] s)
        {
            var b = new byte[s.Length * 2];
            for (int i = 0; i < s.Length; i++)
            {
                short sh = (short)(Mathf.Clamp(s[i], -1f, 1f) * 32767f);
                b[i * 2] = (byte)(sh & 0xff);
                b[i * 2 + 1] = (byte)((sh >> 8) & 0xff);
            }
            return b;
        }

        private static string ExtractText(string body)
        {
            if (string.IsNullOrEmpty(body)) return "";
            var ms = Regex.Matches(body, "\"text\"\\s*:\\s*\"([^\"]*)\"");
            return ms.Count > 0 ? ms[ms.Count - 1].Groups[1].Value : "";
        }

        private void ShowHeard(string txt, Color col)
        {
            var c = Camera.main; if (c == null) return;
            if (heardTmp == null)
            {
                var go = new GameObject("WitHeard"); go.transform.SetParent(c.transform, false);
                go.transform.localPosition = new Vector3(0f, -0.24f, 0.5f);   // dół ekranu, BLISKO, pod formułką
                go.transform.localRotation = Quaternion.identity;
                heardTmp = go.AddComponent<TextMeshPro>();
                heardTmp.alignment = TextAlignmentOptions.Center;
                heardTmp.fontSize = 0.055f;
                heardTmp.fontStyle = FontStyles.Bold;
                heardTmp.rectTransform.sizeDelta = new Vector2(0.66f, 0.2f);
            }
            heardTmp.color = col;
            heardTmp.text = "Usłyszano: „" + txt + "\"";
        }
    }
}
