using System.Collections;
using UnityEngine;

namespace MRCrisisTrainer.Acts.Act1
{
    /// <summary>
    /// Po dobrej resuscytacji - postać wstaje (animacja Stand Up), po chwili odchodzi
    /// w bok i fade-out (znikaja). Logika ruchu (bez Mixamo walking animation).
    /// </summary>
    public class VictimWalkAway : MonoBehaviour
    {
        [SerializeField] private float standUpDuration = 3f;
        [SerializeField] private float walkSpeed = 0.7f;
        [SerializeField] private float walkDuration = 4f;
        [SerializeField] private Vector3 walkDirectionLocal = new Vector3(1f, 0, 0.3f);
        [SerializeField] private float fadeOutDuration = 1.5f;

        public IEnumerator Sequence()
        {
            // Wait for stand-up animation to finish
            yield return new WaitForSeconds(standUpDuration);

            // Rotate slightly to face the walk direction
            Vector3 worldDir = transform.TransformDirection(walkDirectionLocal.normalized);
            Quaternion targetRot = Quaternion.LookRotation(worldDir);
            float rotElapsed = 0;
            Quaternion startRot = transform.rotation;
            while (rotElapsed < 0.8f)
            {
                rotElapsed += Time.deltaTime;
                transform.rotation = Quaternion.Slerp(startRot, targetRot, rotElapsed / 0.8f);
                yield return null;
            }

            // Walk in that direction
            float walkElapsed = 0;
            while (walkElapsed < walkDuration)
            {
                walkElapsed += Time.deltaTime;
                transform.position += worldDir * walkSpeed * Time.deltaTime;
                yield return null;
            }

            // Fade out
            var renderers = GetComponentsInChildren<Renderer>();
            float fadeElapsed = 0;
            while (fadeElapsed < fadeOutDuration)
            {
                fadeElapsed += Time.deltaTime;
                float alpha = 1f - (fadeElapsed / fadeOutDuration);
                foreach (var r in renderers)
                {
                    foreach (var mat in r.materials)
                    {
                        if (mat.HasProperty("_BaseColor"))
                        {
                            var c = mat.GetColor("_BaseColor");
                            c.a = alpha;
                            mat.SetColor("_BaseColor", c);
                        }
                        else if (mat.HasProperty("_Color"))
                        {
                            var c = mat.color;
                            c.a = alpha;
                            mat.color = c;
                        }
                    }
                }
                yield return null;
            }

            gameObject.SetActive(false);
        }
    }
}
