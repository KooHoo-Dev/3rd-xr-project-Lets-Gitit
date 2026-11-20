using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace OfficeEscape
{
    public sealed class OfficePlayer : MonoBehaviour
    {
        public OfficeRun run;
        public CharacterController body;
        public Transform trackingSpace;
        public Transform view;
        public TrackedPoseDriver headTracking;
        public TrackedPoseDriver leftTracking;
        public TrackedPoseDriver rightTracking;
        public XRDirectInteractor leftHand;
        public XRDirectInteractor rightHand;
        public Transform watchMount;
        public Transform desktopWatchMount;
        public LayerMask desktopAimLayers;
        public GameObject desktopReticle;
        public GameObject desktopStart;
        public AudioSource stepAudio;

        readonly List<XRInputSubsystem> inputSubsystems = new();
        bool ready;
        bool floorTracking;
        bool retryPressed;
        bool retryHeld;
        bool desktopControlsActive;
        float crouchOffset;
        float stepDistance;
        float yaw;
        float pitch;
        float drawerPull;
        int drawerPullFrame;
        Vector3 previewDrawerOrigin;
        Transform previewDrawerAxis;
        BadgeReader previewReader;

        public Transform View => view;
        public bool Desktop { get; private set; }
        public bool Ready => ready;
        public bool RetryPressed => retryPressed;

        void Awake()
        {
            Desktop = Application.platform != RuntimePlatform.Android;
            desktopReticle.SetActive(Desktop);
            desktopStart.SetActive(Desktop);
            if (!Desktop) return;

            headTracking.enabled = false;
            leftTracking.enabled = false;
            rightTracking.enabled = false;
            leftHand.gameObject.SetActive(false);
            run.watchDisplay.transform.SetParent(desktopWatchMount, false);
            run.watchDisplay.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            view.localPosition = new Vector3(0, 1.63f, 0);
            yaw = transform.eulerAngles.y;
        }

        void Update()
        {
            if (Desktop)
            {
                UpdateDesktop();
                return;
            }

            var hmd = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            retryPressed = Button(right, CommonUsages.primaryButton) && !retryHeld;
            retryHeld = Button(right, CommonUsages.primaryButton);

            if (!ready && Button(hmd, CommonUsages.isTracked) && left.isValid && right.isValid)
            {
                SubsystemManager.GetSubsystems(inputSubsystems);
                foreach (var subsystem in inputSubsystems)
                {
                    if (subsystem.running)
                        floorTracking = subsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
                }
                ready = true;
            }

            if (!run.IsPlaying || !Button(hmd, CommonUsages.isTracked)) return;
            bool crouch = Button(right, CommonUsages.primaryButton);
            bool watch = Button(left, CommonUsages.primaryButton);
            ApplyStance(crouch, watch);

            if (crouch || watch || leftHand.IsSelecting(run.drawer.handle) || rightHand.IsSelecting(run.drawer.handle)) return;

            var moveDevice = leftHand.IsSelecting(run.drawer.badge) ? right : left;
            if (!moveDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick)) return;

            Vector3 forward = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = transform.forward;
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            Vector3 movement = (side * stick.x + forward * stick.y).normalized;
            float amount = Mathf.InverseLerp(.17f, .96f, stick.magnitude);
            Walk(movement, amount * 1.9f, stick.magnitude >= .68f);
        }

        static bool Button(UnityEngine.XR.InputDevice device, InputFeatureUsage<bool> usage) =>
            device.TryGetFeatureValue(usage, out bool value) && value;

        void UpdateDesktop()
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (mouse == null || keyboard == null) return;
            if (mouse.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                ready = true;
                desktopControlsActive = true;
                desktopStart.SetActive(false);
            }
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                desktopControlsActive = false;
            }
            retryPressed = keyboard.rKey.wasPressedThisFrame;
            if (!run.IsPlaying || !desktopControlsActive) return;

            Vector2 look = mouse.delta.ReadValue() * .12f;
            yaw += look.x;
            pitch = Mathf.Clamp(pitch - look.y, -82f, 82f);
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            view.localRotation = Quaternion.Euler(pitch, 0, 0);
            bool crouch = keyboard.cKey.isPressed;
            bool watch = keyboard.tabKey.isPressed;
            ApplyStance(crouch, watch);
            UpdateDesktopHand(keyboard);
            if (crouch || watch || rightHand.IsSelecting(run.drawer.handle)) return;

            Vector2 direction = new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
            bool loud = keyboard.shiftKey.isPressed;
            Vector3 worldDirection = transform.TransformDirection(new Vector3(direction.x, 0, direction.y).normalized);
            Walk(worldDirection, loud ? 1.9f : .98f, loud);
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused) desktopControlsActive = false;
        }

        void UpdateDesktopHand(Keyboard keyboard)
        {
            bool hasTarget = Physics.Raycast(view.position, view.forward, out RaycastHit target,
                2f, desktopAimLayers, QueryTriggerInteraction.Collide);
            var handle = run.drawer.handle;
            var badge = run.drawer.badge;

            if (rightHand.IsSelecting(handle))
            {
                if (!keyboard.eKey.isPressed)
                {
                    rightHand.EndManualInteraction();
                    return;
                }

                if (Time.frameCount > drawerPullFrame)
                    drawerPull += Time.deltaTime * (keyboard.shiftKey.isPressed ? 1.4f : .24f);
                rightHand.transform.position = previewDrawerOrigin - previewDrawerAxis.forward * drawerPull;
                return;
            }

            if (rightHand.IsSelecting(badge) && keyboard.eKey.wasPressedThisFrame)
            {
                previewReader = hasTarget ? target.collider.GetComponentInParent<BadgeReader>() : null;
                if (!previewReader) rightHand.EndManualInteraction();
            }

            Transform hand = rightHand.transform;
            hand.SetPositionAndRotation(previewReader ? previewReader.socket.attachTransform.position :
                view.TransformPoint(new Vector3(.23f, -.19f, .43f)), view.rotation);
            if (rightHand.hasSelection || !keyboard.eKey.wasPressedThisFrame || !hasTarget) return;

            if (target.collider.GetComponentInParent<XRSimpleInteractable>() == handle)
            {
                previewDrawerOrigin = target.point;
                previewDrawerAxis = run.drawer.transform.parent;
                drawerPull = 0;
                drawerPullFrame = Time.frameCount;
                hand.position = previewDrawerOrigin;
                rightHand.StartManualInteraction((IXRSelectInteractable)handle);
            }
            else if (badge.gameObject.activeInHierarchy && target.collider.GetComponentInParent<XRGrabInteractable>() == badge)
            {
                hand.position = badge.transform.position;
                rightHand.StartManualInteraction((IXRSelectInteractable)badge);
            }
        }

        public bool ReleaseDesktopBadge(IXRSelectInteractor hand)
        {
            if (!Desktop || hand != rightHand) return false;
            rightHand.EndManualInteraction();
            previewReader = null;
            return true;
        }

        void ApplyStance(bool crouch, bool watch)
        {
            float trackingHeight = Desktop || floorTracking ? 0f : 1.6f;
            float amount = Mathf.Clamp(view.localPosition.y + trackingHeight - .78f, 0, .74f);
            crouchOffset = Mathf.MoveTowards(crouchOffset, crouch ? amount : 0, 4f * Time.deltaTime);
            trackingSpace.localPosition = Vector3.up * (trackingHeight - crouchOffset);
            Vector3 localHead = transform.InverseTransformPoint(view.position);
            body.height = Mathf.Max(.55f, localHead.y + .08f);
            body.center = new Vector3(localHead.x, body.height * .5f + .012f, localHead.z);

            run.SetWatchVisible(watch);
            if (watch && !Desktop)
            {
                var display = run.watchDisplay.transform;
                display.position = watchMount.position;
                display.rotation = Quaternion.LookRotation(display.position - view.position, Vector3.up);
            }
        }

        void Walk(Vector3 direction, float speed, bool loud)
        {
            if (speed <= 0f || direction.sqrMagnitude < .01f) return;
            Vector3 previous = transform.position;
            body.Move(direction * (speed * Time.deltaTime));
            stepDistance += Vector3.Distance(previous, transform.position);
            if (stepDistance < .46f) return;
            stepDistance = 0;
            stepAudio.volume = loud ? .76f : .18f;
            stepAudio.pitch = Random.Range(.93f, 1.08f);
            stepAudio.Play();
            if (loud) run.ReportNoise(transform.position);
        }

        public void StopMovement()
        {
            if (Desktop && rightHand.hasSelection) rightHand.EndManualInteraction();
        }
    }
}
