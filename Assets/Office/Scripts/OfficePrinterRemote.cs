using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace OfficeEscape
{
    public sealed class OfficePrinterRemote : XRGrabInteractable
    {
        public OfficeRun run;
        public AudioSource impactAudio;

        Rigidbody body;
        public bool WasDropped { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            body = GetComponent<Rigidbody>();
        }

        public override bool IsSelectableBy(IXRSelectInteractor interactor) =>
            !WasDropped && run.IsPlaying && base.IsSelectableBy(interactor);

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);
            string reason = run.player.DesktopRemoteReleaseReason;
            if (run.player.Desktop && run.IsPlaying && !isSelected && string.IsNullOrEmpty(reason))
                Debug.LogWarning($"[RemoteGrip] 우클릭 해제 명령 없이 리모컨 선택이 풀림 (frame={Time.frameCount}, canceled={args.isCanceled})", this);
            if (isSelected || WasDropped || !run.IsPlaying) return;

            WasDropped = true;
            body.useGravity = true;
            body.isKinematic = false;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!WasDropped || !run.IsPlaying) return;
            impactAudio.Play();
            run.Caught("리모컨을 떨어뜨렸어!");
        }
    }
}
