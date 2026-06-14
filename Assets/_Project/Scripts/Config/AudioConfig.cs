using UnityEngine;

namespace MRCrisisTrainer.Config
{
    [CreateAssetMenu(fileName = "AudioConfig", menuName = "MRCrisis/Audio Config", order = 1)]
    public class AudioConfig : ScriptableObject
    {
        [Header("Act I - Reanimation")]
        public AudioClip metronomeTick;
        public AudioClip compressionFeedback;
        public AudioClip stabilizedChime;
        public AudioClip victimBreathing;

        [Header("Shared")]
        public AudioClip transitionWhoosh;
        public AudioClip uiPromptAppear;

        [Header("Mixer levels")]
        [Range(0f, 1f)] public float metronomeVolume = 0.6f;
        [Range(0f, 1f)] public float feedbackVolume = 0.8f;
    }
}
