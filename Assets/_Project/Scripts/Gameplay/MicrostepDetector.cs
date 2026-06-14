using System;
using UnityEngine;

namespace MRCrisisTrainer.Gameplay
{
    /// <summary>
    /// Bazowa klasa detektora ukończenia mikrokroku. Każdy konkretny detektor
    /// rozpoznaje jeden typ akcji gracza (podejście, ułożenie rąk, chwyt, mowa, cisza...).
    /// ScenarioRunner aktywuje detektor dla bieżącego mikrokroku i czeka na OnCompleted/OnFailed.
    /// </summary>
    public abstract class MicrostepDetector : MonoBehaviour
    {
        [Tooltip("ID mikrokroku które ten detektor obsługuje (musi pasować do Microstep.id)")]
        public string stepId;

        public event Action OnCompleted;
        public event Action<string> OnFailed;

        protected bool IsActive { get; private set; }

        /// <summary>Rozpoczyna nasłuchiwanie na ukończenie kroku.</summary>
        public virtual void Begin()
        {
            IsActive = true;
            OnBegin();
        }

        /// <summary>Przerywa nasłuchiwanie (np. timeout lub przejście dalej).</summary>
        public virtual void Cancel()
        {
            IsActive = false;
            OnCancel();
        }

        protected virtual void OnBegin() { }
        protected virtual void OnCancel() { }

        protected void Complete()
        {
            if (!IsActive) return;
            IsActive = false;
            OnCompleted?.Invoke();
        }

        protected void Fail(string reason)
        {
            if (!IsActive) return;
            IsActive = false;
            OnFailed?.Invoke(reason);
        }
    }
}
