using System;
using System.Collections;
using UnityEngine;
using MRCrisisTrainer.Config;

namespace MRCrisisTrainer.Acts.Act3
{
    public class PoliceArrivalSequence : MonoBehaviour
    {
        [SerializeField] private Act3Config config;
        [SerializeField] private AudioSource sirensAudio;
        [SerializeField] private AudioClip sirensClip;
        [SerializeField] private AudioSource doorBreachAudio;
        [SerializeField] private AudioClip doorBreachClip;
        [SerializeField] private Light[] policeLights;
        [SerializeField] private Animator doorAnimator;
        [SerializeField] private GameObject thief;

        public event Action OnSequenceComplete;

        public void StartSequence()
        {
            StartCoroutine(SequenceCoroutine());
        }

        private IEnumerator SequenceCoroutine()
        {
            yield return new WaitForSeconds(config.sirensDelay);

            if (sirensAudio != null && sirensClip != null)
            {
                sirensAudio.clip = sirensClip;
                sirensAudio.spatialBlend = 1f;
                sirensAudio.loop = true;
                sirensAudio.Play();
            }

            StartCoroutine(FlashPoliceLights());

            yield return new WaitForSeconds(config.doorBreachDelay);

            if (doorBreachAudio != null && doorBreachClip != null)
            {
                doorBreachAudio.PlayOneShot(doorBreachClip);
            }

            if (doorAnimator != null)
            {
                doorAnimator.SetTrigger("Breach");
            }

            // Thief flees
            if (thief != null)
            {
                StartCoroutine(ThiefFlees());
            }

            yield return new WaitForSeconds(3f);

            if (sirensAudio != null) sirensAudio.Stop();

            OnSequenceComplete?.Invoke();
        }

        private IEnumerator FlashPoliceLights()
        {
            if (policeLights == null || policeLights.Length == 0) yield break;

            float t = 0;
            int idx = 0;
            while (sirensAudio != null && sirensAudio.isPlaying)
            {
                t += Time.deltaTime;
                if (t > 0.3f)
                {
                    foreach (var l in policeLights) l.enabled = false;
                    policeLights[idx].enabled = true;
                    idx = (idx + 1) % policeLights.Length;
                    t = 0;
                }
                yield return null;
            }
            foreach (var l in policeLights) l.enabled = false;
        }

        private IEnumerator ThiefFlees()
        {
            if (thief == null) yield break;
            Vector3 startPos = thief.transform.position;
            Vector3 fleeTarget = startPos + new Vector3(0, 0, -5f);
            float t = 0;
            while (t < 1f)
            {
                t += Time.deltaTime * 0.7f;
                thief.transform.position = Vector3.Lerp(startPos, fleeTarget, t);
                yield return null;
            }
            thief.SetActive(false);
        }
    }
}
