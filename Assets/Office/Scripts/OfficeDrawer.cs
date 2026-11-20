using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace OfficeEscape
{
    public sealed class OfficeDrawer : MonoBehaviour
    {
        public OfficeRun run;
        public XRSimpleInteractable handle;
        public XRGrabInteractable badge;
        public AudioSource railAudio;
        [Min(.05f)] public float travel = .36f;
        [Min(.05f)] public float badgeReveal = .24f;
        [Min(.05f)] public float noisyPullSpeed = .52f;

        IXRSelectInteractor hand;
        Vector3 closed;
        Vector3 badgeAtRest;
        float handAtGrab;
        float openAtGrab;
        float opening;
        float nextRattle;
        bool badgeTaken;

        void Awake()
        {
            closed = transform.localPosition;
            badgeAtRest = badge.transform.position;
            handle.selectEntered.AddListener(GrabHandle);
            handle.selectExited.AddListener(_ => hand = null);
            badge.selectEntered.AddListener(_ => badgeTaken = true);
            var badgeBody = badge.GetComponent<Rigidbody>();
            badge.selectExited.AddListener(_ =>
            {
                if (!badge.isSelected && run.IsPlaying) badgeBody.isKinematic = false;
            });
            badge.gameObject.SetActive(false);
        }

        void GrabHandle(SelectEnterEventArgs args)
        {
            hand = args.interactorObject;
            handAtGrab = HandZ();
            openAtGrab = opening;
        }

        void Update()
        {
            if (hand == null || !run.IsPlaying) return;
            float previous = opening;
            opening = Mathf.Clamp(openAtGrab + handAtGrab - HandZ(), 0, travel);
            transform.localPosition = closed + Vector3.back * opening;

            if (!badgeTaken)
            {
                badge.transform.position = badgeAtRest - transform.parent.forward * opening;
                badge.gameObject.SetActive(opening >= badgeReveal);
            }

            if (Time.time >= nextRattle && Mathf.Abs(opening - previous) > noisyPullSpeed * Time.deltaTime)
            {
                nextRattle = Time.time + .42f;
                railAudio.Play();
                run.ReportNoise(transform.position);
            }
        }

        float HandZ() => transform.parent.InverseTransformPoint(hand.GetAttachTransform(handle).position).z;
    }
}
