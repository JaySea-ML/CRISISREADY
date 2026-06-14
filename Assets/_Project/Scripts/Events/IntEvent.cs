using System;
using System.Collections.Generic;
using UnityEngine;

namespace MRCrisisTrainer.Events
{
    [CreateAssetMenu(fileName = "IntEvent", menuName = "MRCrisis/Events/Int Event", order = 11)]
    public class IntEvent : ScriptableObject
    {
        private readonly List<Action<int>> listeners = new List<Action<int>>();

        public void Raise(int value)
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
            {
                try { listeners[i]?.Invoke(value); }
                catch (Exception e) { Debug.LogError($"[IntEvent {name}] listener threw: {e}"); }
            }
        }

        public void Register(Action<int> listener)
        {
            if (!listeners.Contains(listener)) listeners.Add(listener);
        }

        public void Unregister(Action<int> listener)
        {
            listeners.Remove(listener);
        }
    }
}
