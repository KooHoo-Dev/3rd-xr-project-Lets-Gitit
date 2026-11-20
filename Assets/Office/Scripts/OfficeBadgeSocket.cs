using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace OfficeEscape
{
    public sealed class OfficeBadgeSocket : XRSocketInteractor
    {
        public XRGrabInteractable badge;

        public override bool CanHover(IXRHoverInteractable interactable) =>
            interactable == badge && base.CanHover(interactable);

        public override bool CanSelect(IXRSelectInteractable interactable) =>
            interactable == badge && base.CanSelect(interactable);
    }
}
