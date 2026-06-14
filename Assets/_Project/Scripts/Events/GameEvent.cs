using System;
using System.Collections.Generic;
using UnityEngine;

namespace MRCrisisTrainer.Events
{
    [CreateAssetMenu(fileName = "GameEvent", menuName = "MRCrisis/Events/Void Event", order = 10)]
    public class GameEvent : ScriptableObject
    {
        private readonly List<Action> listeners = new List<Action>();

        public void Raise()
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
            {
                try { listeners[i]?.Invoke(); }
                catch (Exception e) { Debug.LogError($"[GameEvent {name}] listener threw: {e}"); }
            }
        }

        public void Register(Action listener)
        {
            if (!listeners.Contains(listener)) listeners.Add(listener);
        }

        public void Unregister(Action listener)
        {
            listeners.Remove(listener);
        }
    }
}
