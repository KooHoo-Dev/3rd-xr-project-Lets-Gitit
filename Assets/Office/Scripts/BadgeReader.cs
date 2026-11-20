using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace OfficeEscape
{
    public sealed class BadgeReader : MonoBehaviour
    {
        public OfficeRun run;
        public OfficeBadgeSocket socket;
        public XRGrabInteractable badge;
        public Transform door;
        public Renderer statusLight;
        public Material authorizedLight;
        public AudioSource beep;
        public Transform threshold;

        Vector3 doorClosed;
        float openProgress;

        public bool IsAuthorized { get; private set; }

        void Awake()
        {
            doorClosed = door.localPosition;
            socket.selectEntered.AddListener(args =>
            {
                if (args.interactableObject == badge && run.IsPlaying && run.SecondsLeft > 0 && !IsAuthorized)
                {
                    IsAuthorized = true;
                    statusLight.sharedMaterial = authorizedLight;
                    beep.Play();
                }
            });
        }

        void Update()
        {
            if (!run.IsPlaying) return;
            if (!IsAuthorized && badge.isSelected && !socket.IsSelecting(badge) &&
                (badge.transform.position - socket.attachTransform.position).sqrMagnitude < .13f * .13f)
            {
                var hand = badge.firstInteractorSelecting;
                if (!run.player.ReleaseDesktopBadge(hand)) socket.interactionManager.SelectExit(hand, badge);
            }

            if (!IsAuthorized) return;
            openProgress = Mathf.MoveTowards(openProgress, 1, Time.deltaTime / .6f);
            door.localPosition = doorClosed + Vector3.forward * (1.45f * Mathf.SmoothStep(0, 1, openProgress));

            Vector3 atExit = threshold.InverseTransformPoint(run.player.View.position);
            if (atExit.x > 0f && Mathf.Abs(atExit.z) < .8f) run.CrossExit();
        }
    }
}
