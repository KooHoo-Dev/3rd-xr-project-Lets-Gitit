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
