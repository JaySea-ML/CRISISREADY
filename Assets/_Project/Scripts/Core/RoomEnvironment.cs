using UnityEngine;

namespace MRCrisisTrainer.Core
{
    /// <summary>
    /// Korzeń wirtualnego pokoju zaimportowanego z Polycam/photogrametrii.
    /// Definiuje anchory dla scenariuszy oraz przełącza między trybem MR (passthrough)
    /// a VR (pokój widoczny zamiast realnego otoczenia).
    /// </summary>
    public class RoomEnvironment : MonoBehaviour
    {
        public enum EnvironmentMode
        {
            VirtualRoom,   // Pełne VR - widać model Polycam
            Passthrough,   // MR - widać realny pokój, model ukryty (anchory używane do alignment)
            Mixed          // MR + wirtualne meble (model pokoju widoczny ale przezroczysty/oświetlenie)
        }

        public static RoomEnvironment Instance { get; private set; }

        [Header("Mode")]
        [SerializeField] private EnvironmentMode startMode = EnvironmentMode.VirtualRoom;
        public EnvironmentMode CurrentMode { get; private set; }

        [Header("Visual root")]
        [Tooltip("GameObject zawierający geometrię pokoju (mesh, materiały). Ukrywany w trybie Passthrough.")]
        [SerializeField] private GameObject roomVisualRoot;

        [Header("Anchory dla scenariuszy")]
        [SerializeField] private Transform victimSpawnAnchor;
        [SerializeField] private Transform chairAnchor;
        [SerializeField] private Transform deskAnchor;
        [SerializeField] private Transform phoneAnchor;
        [SerializeField] private Transform wardrobeAnchor;
        [SerializeField] private Transform playerStartAnchor;

        public Transform VictimSpawn => victimSpawnAnchor;
        public Transform Chair => chairAnchor;
        public Transform Desk => deskAnchor;
        public Transform Phone => phoneAnchor;
        public Transform Wardrobe => wardrobeAnchor;
        public Transform PlayerStart => playerStartAnchor;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void Start()
        {
            // W Start (po wszystkich Awake) — PassthroughController.Instance już istnieje.
            ApplyMode(startMode);
        }

        public void ApplyMode(EnvironmentMode mode)
        {
            CurrentMode = mode;
            switch (mode)
            {
                case EnvironmentMode.VirtualRoom:
                    SetRoomVisible(true);
                    if (XR.PassthroughController.Instance != null)
                        XR.PassthroughController.Instance.DisablePassthrough();
                    break;

                case EnvironmentMode.Passthrough:
                    SetRoomVisible(false);
                    if (XR.PassthroughController.Instance != null)
                        XR.PassthroughController.Instance.EnablePassthrough();
                    break;

                case EnvironmentMode.Mixed:
                    SetRoomVisible(true);
                    if (XR.PassthroughController.Instance != null)
                        XR.PassthroughController.Instance.EnablePassthrough();
                    break;
            }
            Debug.Log($"[RoomEnvironment] Mode -> {mode}");
        }

        public void SetRoomVisible(bool visible)
        {
            if (roomVisualRoot != null) roomVisualRoot.SetActive(visible);
        }

        public void ToggleMode()
        {
            int next = ((int)CurrentMode + 1) % System.Enum.GetValues(typeof(EnvironmentMode)).Length;
            ApplyMode((EnvironmentMode)next);
        }
    }
}
