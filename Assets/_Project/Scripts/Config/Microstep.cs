using System;
using UnityEngine;

namespace MRCrisisTrainer.Config
{
    /// <summary>
    /// Pojedynczy mikrokrok scenariusza (np. "Sprawdź przytomność", "Wezwij pomoc").
    /// Punktowany 0/1/2 zgodnie z wytycznymi projektu badawczego.
    /// </summary>
    [Serializable]
    public class Microstep
    {
        [Tooltip("Unikalny identyfikator do logowania (np. 'check_consciousness')")]
        public string id;

        [Tooltip("Wyświetlana etykieta po polsku (np. 'Sprawdź przytomność')")]
        public string label;

        [Tooltip("Krótki tekst podpowiedzi pokazany graczowi w razie potrzeby")]
        [TextArea(2, 4)]
        public string hintText;

        [Tooltip("Czas (s) od momentu w którym krok jest aktywny do automatycznej podpowiedzi")]
        public float hintDelaySeconds = 5f;

        [Tooltip("Maksymalny czas (s) - po nim krok = 0 punktów, przejście wymuszone")]
        public float timeoutSeconds = 30f;

        [Tooltip("Czy krok jest obowiązkowy. Jeśli nie - można pominąć (np. dodatkowe info do dyspozytora)")]
        public bool isMandatory = true;

        [Tooltip("Czy ten krok blokuje przejście dalej (musi być ukończony żeby iść dalej)")]
        public bool isBlocking = true;
    }

    public enum MicrostepScore
    {
        NotStarted = -1,
        Failed = 0,     // Brak wykonania lub działanie szkodliwe
        WithHint = 1,   // Wykonanie z podpowiedzią/po czasie
        Independent = 2 // Wykonanie samodzielne i poprawne
    }

    public enum MicrostepStatus
    {
        Pending,    // Jeszcze nie aktywny
        Active,     // Aktualnie w toku
        Completed,  // Zakończony pomyślnie
        Failed,     // Zakończony z błędem (0 punktów)
        Skipped     // Pominięty (opcjonalny krok)
    }
}
