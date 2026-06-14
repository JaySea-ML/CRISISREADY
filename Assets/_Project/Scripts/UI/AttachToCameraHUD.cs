using UnityEngine;

namespace MRCrisisTrainer.UI
{
    /// <summary>Przyczepia element UI do kamery jako HUD: zawsze na DOLE ekranu, blisko, PIONOWO (nie leży na drodze).
    /// Zastępuje FaceCamera dla podpowiedzi. Retry w LateUpdate, bo Camera.main bywa gotowa dopiero po chwili.</summary>
    public class AttachToCameraHUD : MonoBehaviour
    {
        public Vector3 localOffset = new Vector3(0f, -0.32f, 1.1f);
        private bool attached;

        void OnEnable() { attached = false; TryAttach(); }
        void LateUpdate() { if (!attached) TryAttach(); }

        private void TryAttach()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var fc = GetComponent<FaceCamera>(); if (fc != null) fc.enabled = false;   // jako dziecko kamery już patrzy na gracza
            transform.SetParent(cam.transform, true);   // zachowaj skalę świata
            transform.localPosition = localOffset;
            transform.localRotation = Quaternion.identity;   // PIONOWO, nie na drodze
            attached = true;
        }
    }
}
