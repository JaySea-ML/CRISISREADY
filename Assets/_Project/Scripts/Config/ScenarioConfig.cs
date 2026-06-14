using System.Collections.Generic;
using UnityEngine;

namespace MRCrisisTrainer.Config
{
    /// <summary>
    /// Pełna definicja scenariusza (Akt II / III) z listą mikrokroków.
    /// </summary>
    [CreateAssetMenu(fileName = "ScenarioConfig", menuName = "MRCrisis/Scenario Config", order = 5)]
    public class ScenarioConfig : ScriptableObject
    {
        [Header("Identyfikacja")]
        public string scenarioId;
        public string displayName;
        [TextArea(2, 4)] public string description;

        [Header("Mikrokroki (w kolejności wykonania)")]
        public List<Microstep> microsteps = new List<Microstep>();

        [Header("Scaffolding (poziom podpowiedzi)")]
        [Tooltip("Sesja 1 = Full, kolejne maleją: Partial, None. Sterowany przez SessionManager.")]
        public ScaffoldingLevel defaultScaffolding = ScaffoldingLevel.Full;

        [Header("Próg sukcesu")]
        [Tooltip("Minimalna suma punktów / max points dla sukcesu scenariusza (0..1)")]
        [Range(0f, 1f)] public float successThreshold = 0.7f;

        public int MaxScore => microsteps.Count * 2;
    }

    /// <summary>
    /// Poziom adaptacyjnego scaffoldingu - określa jak agresywnie system pomaga graczowi.
    /// </summary>
    public enum ScaffoldingLevel
    {
        Full,    // Wszystkie podpowiedzi widoczne od początku, długi czas zanim timeout
        Partial, // Podpowiedzi dopiero po czasie hint_delay
        None     // Brak podpowiedzi - tylko feedback po zakończeniu
    }
}
