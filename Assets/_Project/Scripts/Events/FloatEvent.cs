using System;
using System.Collections.Generic;
using UnityEngine;

namespace MRCrisisTrainer.Events
{
    [CreateAssetMenu(fileName = "FloatEvent", menuName = "MRCrisis/Events/Float Event", order = 12)]
    public class FloatEvent : ScriptableObject
    {
        private readonly List<Action<float>> listeners = new List<Action<float>>();

        public void Raise(float value)
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
            {
                try { listeners[i]?.Invoke(value); }
                catch (Exception e) { Debug.LogError($"[FloatEvent {name}] listener threw: {e}"); }
            }
        }

        public void Register(Action<float> listener)
        {
            if (!listeners.Contains(listener)) listeners.Add(listener);
        }

        public void Unregister(Action<float> listener)
        {
            listeners.Remove(listener);
        }
    }
}
