using System.Collections;
using UnityEngine;

namespace MRCrisisTrainer.UI
{
    /// <summary>
    /// Ustawia kierownicę (holder) RAZ, w zasięgu ręki przed graczem przy starcie samouczka —
    /// niezależnie od tego gdzie gracz stoi/patrzy po założeniu gogli. Bez podążania (stały cel do chwytu).
    /// Koło (dziecko holdera) jest ~0.42 m do przodu i ~1.0 m wysoko w lokalnych wsp., więc:
    /// holder.y = camY - eyeToWheelDrop → koło ~0.45 m poniżej oczu (klatka piersiowa), 0.42 m przed graczem.
    /// </summary>
    public class PlaceWheelInFront : MonoBehaviour
    {
        [SerializeField] private float eyeToWheelDrop = 1.45f;

        private IEnumerator Start()
        {
            Camera cam = null;
            // czekaj aż HMD faktycznie się ustawi (kamera wyjdzie z origin), inaczej koło ląduje daleko
            for (int i = 0; i < 180; i++)
            {
                cam = Camera.main;
                if (cam != null && cam.transform.position.y > 0.3f) break;
                yield return null;
            }
            if (cam == null) yield break;

            // ustawiaj koło PRZED graczem przez ~2 s (HMD się dosadza, gracz zwraca się do koła), potem zablokuj
            float t = 0f;
            while (t < 2f) { Place(cam); t += Time.deltaTime; yield return null; }
            Place(cam);
        }

        private void Place(Camera cam)
        {
            Vector3 fwd = cam.transform.forward; fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
            // koło ~0.40 m przed graczem (przyciągnięte bliżej), na wysokości klatki piersiowej
            transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y - eyeToWheelDrop, cam.transform.position.z)
                                 - fwd * 0.05f;
            transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }
    }
}
