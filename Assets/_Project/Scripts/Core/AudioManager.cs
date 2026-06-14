using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MRCrisisTrainer.Core
{
    /// <summary>
    /// Centralny manager audio. Ładuje klipy z Resources/Audio i odtwarza je po nazwie.
    /// Obsługuje SFX (one-shot), ambient (loop) i voice (kolejkowane).
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private AudioSource sfxSource;
        private AudioSource ambientSource;
        private AudioSource voiceSource;
        private readonly Queue<string> voiceQueue = new Queue<string>();
        private bool voicePlaying;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            ambientSource = gameObject.AddComponent<AudioSource>();
            ambientSource.playOnAwake = false;
            ambientSource.loop = true;
            ambientSource.volume = 0.4f;
            voiceSource = gameObject.AddComponent<AudioSource>();
            voiceSource.playOnAwake = false;
            voiceSource.volume = 1f;

            LoadAll();
        }

        private void LoadAll()
        {
            foreach (var c in Resources.LoadAll<AudioClip>("Audio"))
            {
                clips[c.name] = c;
            }
            Debug.Log($"[AudioManager] Loaded {clips.Count} clips from Resources/Audio");
        }

        public AudioClip Get(string name)
        {
            return clips.TryGetValue(name, out var c) ? c : null;
        }

        public void PlaySfx(string name, float volume = 1f)
        {
            var c = Get(name);
            if (c != null) sfxSource.PlayOneShot(c, volume);
        }

        /// <summary>Odtwarza SFX w przestrzeni 3D w danym punkcie.</summary>
        public void PlaySfxAt(string name, Vector3 position, float volume = 1f)
        {
            var c = Get(name);
            if (c != null) AudioSource.PlayClipAtPoint(c, position, volume);
        }

        public void PlayAmbient(string name, float volume = 0.4f)
        {
            var c = Get(name);
            if (c == null) return;
            ambientSource.clip = c;
            ambientSource.volume = volume;
            ambientSource.Play();
        }

        public void StopAmbient() => ambientSource.Stop();

        /// <summary>Kolejkuje linię głosową (np. operator 112). Gra sekwencyjnie.</summary>
        public void QueueVoice(string name)
        {
            voiceQueue.Enqueue(name);
            if (!voicePlaying) StartCoroutine(VoiceLoop());
        }

        public bool IsVoicePlaying => voicePlaying || voiceQueue.Count > 0;

        public float PlayVoiceImmediate(string name)
        {
            var c = Get(name);
            if (c == null) return 0f;
            voiceSource.Stop();
            voiceSource.clip = c;
            voiceSource.Play();
            return c.length;
        }

        private IEnumerator VoiceLoop()
        {
            voicePlaying = true;
            while (voiceQueue.Count > 0)
            {
                var name = voiceQueue.Dequeue();
                var c = Get(name);
                if (c != null)
                {
                    voiceSource.clip = c;
                    voiceSource.Play();
                    yield return new WaitForSeconds(c.length + 0.25f);
                }
            }
            voicePlaying = false;
        }
    }
}
