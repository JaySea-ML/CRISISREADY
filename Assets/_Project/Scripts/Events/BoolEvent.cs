using System;
using System.Collections.Generic;
using UnityEngine;

namespace MRCrisisTrainer.Events
{
    [CreateAssetMenu(fileName = "BoolEvent", menuName = "MRCrisis/Events/Bool Event", order = 13)]
    public class BoolEvent : ScriptableObject
    {
        private readonly List<Action<bool>> listeners = new List<Action<bool>>();

        public void Raise(bool value)
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
            {
                try { listeners[i]?.Invoke(value); }
                catch (Exception e) { Debug.LogError($"[BoolEvent {name}] listener threw: {e}"); }
            }
        }

        public void Register(Action<bool> listener)
        {
            if (!listeners.Contains(listener)) listeners.Add(listener);
        }

        public void Unregister(Action<bool> listener)
        {
            listeners.Remove(listener);
        }
    }
}
