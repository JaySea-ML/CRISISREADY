using System.Collections;
using UnityEngine;

namespace MRCrisisTrainer.UI
{
    /// <summary>
    /// Ustawia panel (np. menu) PRZED graczem przy starcie, niezależnie w którą stronę patrzy po
    /// założeniu gogli. Bez tego world-space UI w stałej pozycji bywa z boku/za graczem = „nie widzę menu".
    /// Opcjonalnie delikatnie podąża, gdy gracz mocno odwróci głowę (recenter), żeby zawsze był w zasięgu.
    /// </summary>
    public class PlaceInFrontOfUser : MonoBehaviour
    {
        [SerializeField] private float distance = 1.6f;
        [SerializeField] private float height = 1.45f;
        [SerializeField] private bool keepInView = true;     // wróć przed gracza, gdy odejdzie z pola widzenia
        [SerializeField] private float recenterAngle = 55f;  // > tyle stopni od środka → przesuń przed gracza
        [SerializeField] private float followLerp = 3f;

        private Camera cam;
        private bool placed;

        void OnEnable() { placed = false; StartCoroutine(PlaceSoon()); }

        private IEnumerator PlaceSoon()
        {
            // poczekaj aż head-tracking ustawi kamerę (kilka klatek po wejściu do sceny)
            for (int i = 0; i < 6 && ResolveCam() == null; i++) yield return null;
            yield return null;
            Snap();
            placed = true;
        }

        private Camera ResolveCam()
        {
            if (cam == null) cam = Camera.main;
            return cam;
        }

        private void Snap()
        {
            var c = ResolveCam(); if (c == null) return;
            Vector3 fwd = c.transform.forward; fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
            Vector3 p = c.transform.position + fwd * distance; p.y = height;
            transform.position = p;
            // zwrócony do gracza, pionowo (bez przechyłu)
            Vector3 look = p - c.transform.position; look.y = 0f;
            if (look.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        }

        void Update()
        {
            if (!placed || !keepInView) return;
            var c = ResolveCam(); if (c == null) return;
            Vector3 toPanel = transform.position - c.transform.position; toPanel.y = 0f;
            Vector3 fwd = c.transform.forward; fwd.y = 0f;
            if (toPanel.sqrMagnitude < 0.001f || fwd.sqrMagnitude < 0.001f) return;
            float ang = Vector3.Angle(fwd.normalized, toPanel.normalized);
            if (ang > recenterAngle)
            {
                // płynnie sprowadź panel z powrotem przed gracza
                Vector3 target = c.transform.position + fwd.normalized * distance; target.y = height;
                transform.position = Vector3.Lerp(transform.position, target, Time.unscaledDeltaTime * followLerp);
                Vector3 look = transform.position - c.transform.position; look.y = 0f;
                if (look.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                        Quaternion.LookRotation(look.normalized, Vector3.up), Time.unscaledDeltaTime * followLerp);
            }
        }
    }
}
