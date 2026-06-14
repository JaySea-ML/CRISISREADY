using UnityEngine;

namespace MRCrisisTrainer.Config
{
    [CreateAssetMenu(fileName = "Act3Config", menuName = "MRCrisis/Act 3 Config", order = 3)]
    public class Act3Config : ScriptableObject
    {
        [Header("Phone")]
        [Tooltip("Sekundy od startu Aktu III do uruchomienia dzwonka")]
        public float secondsBeforePhoneRing = 2.5f;

        [Tooltip("Promień (m) wokół słuchawki w którym ręka jest uznana za chwytającą")]
        public float receiverGrabRadius = 0.15f;

        [Header("VR transition")]
        [Tooltip("Czas (s) fade-out passthrough → pełne VR")]
        public float vrTransitionDuration = 1.2f;

        [Header("Speech detection - komunikat 112")]
        [Tooltip("Próg głośności (0..1 z mikrofonu) uznawany za 'mówienie'")]
        [Range(0f, 1f)] public float speechVolumeThreshold = 0.08f;

        [Tooltip("Minimalny czas (s) ciągłego mówienia żeby uznać 'wiadomość 112'")]
        public float minSpeechDuration = 3.0f;

        [Tooltip("Maksymalny czas (s) na zaalarmowanie 112")]
        public float maxTimeForDispatch = 25f;

        [Header("Silence phase")]
        [Tooltip("Wymagany czas ciszy (s) po komunikacie do 112")]
        public float silenceRequiredDuration = 120f;

        [Tooltip("Próg głośności (0..1) ponad który gracz 'wydaje hałas'")]
        [Range(0f, 1f)] public float silenceVolumeThreshold = 0.05f;

        [Tooltip("Ile razy gracz może 'zostać usłyszany' zanim przegra")]
        public int allowedNoiseStrikes = 2;

        [Header("Thief AI")]
        [Tooltip("Prędkość poruszania się złodzieja (m/s)")]
        public float thiefSpeed = 0.6f;

        [Tooltip("Czas (s) jaki złodziej spędza w pokoju zanim wyjdzie")]
        public float thiefLingerDuration = 25f;

        [Header("Police arrival")]
        [Tooltip("Czas (s) od końca ciszy do nadejścia syren")]
        public float sirensDelay = 1f;

        [Tooltip("Czas (s) od syren do wyłamania drzwi")]
        public float doorBreachDelay = 3f;
    }
}
