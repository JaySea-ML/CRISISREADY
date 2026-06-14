using UnityEngine;

namespace MRCrisisTrainer.Core
{
    public enum ActId
    {
        None,
        Bootstrap,
        Act1_Reanimation,
        Act2_Car,
        Act3_Hide,
        Finished
    }

    public class GameStateManager : MonoBehaviour
    {
        public static GameStateManager Instance { get; private set; }

        [SerializeField] private ActId currentAct = ActId.Bootstrap;
        [SerializeField] private float sessionStartTime;

        public ActId CurrentAct => currentAct;
        public float SessionElapsed => Time.realtimeSinceStartup - sessionStartTime;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            sessionStartTime = Time.realtimeSinceStartup;
        }

        public void SetCurrentAct(ActId act)
        {
            currentAct = act;
            Debug.Log($"[GameStateManager] Act changed -> {act}");
        }
    }
}
