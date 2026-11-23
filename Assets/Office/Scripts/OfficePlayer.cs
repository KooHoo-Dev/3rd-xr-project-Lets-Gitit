using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace OfficeEscape
{
    public sealed class OfficePlayer : MonoBehaviour
    {
        const float EyeHeight = 1.63f;
        const float SlowMoveSpeed = .6f;

        public OfficeRun run;
        public CharacterController body;
        public Transform trackingSpace;
        public Transform view;
        public TrackedPoseDriver headTracking;
        public TrackedPoseDriver leftTracking;
        public TrackedPoseDriver rightTracking;
        public XRDirectInteractor leftHand;
        public XRDirectInteractor rightHand;
        public LayerMask desktopAimLayers;
        public GameObject desktopReticle;
        public GameObject desktopStart;
        public AudioSource stepAudio;
        [Min(0f)] public float turnSpeed = 60f;

        readonly List<XRInputSubsystem> inputSubsystems = new();
        readonly Dictionary<Mouse, bool> rightButtonStates = new();
        readonly Queue<(Mouse mouse, bool pressed)> remoteButtonChanges = new();
        bool ready;
        bool retryPressed;
        bool retryHeld;
        bool interactHeld;
        bool desktopControlsActive;
        Mouse remoteGripMouse;
        float crouchOffset;
        float stepDistance;
        float yaw;
        float pitch;
        float drawerPull;
        int drawerPullFrame;
        Vector3 previewDrawerOrigin;
        Transform previewDrawerAxis;

        public Transform View => view;
        public bool Desktop { get; private set; }
        public bool Ready => ready;
        public bool RetryPressed => retryPressed;
        public string DesktopRemoteReleaseReason { get; private set; }

        void Awake()
        {
            Desktop = Application.platform != RuntimePlatform.Android;
            OfficeHud.ConfigureFor(run, Desktop);
            desktopReticle.SetActive(Desktop);
            desktopStart.SetActive(Desktop);
            if (!Desktop) return;

            headTracking.enabled = false;
            leftTracking.enabled = false;
            rightTracking.enabled = false;
            leftHand.gameObject.SetActive(false);
            view.localPosition = new Vector3(0, EyeHeight, 0);
            yaw = transform.eulerAngles.y;
        }

        void OnEnable()
        {
            InputSystem.onEvent += ReadDesktopRightButton;
            Application.onBeforeRender += UpdateHeadsetHeight;
        }

        void OnDisable()
        {
            InputSystem.onEvent -= ReadDesktopRightButton;
            Application.onBeforeRender -= UpdateHeadsetHeight;
            remoteGripMouse = null;
            remoteButtonChanges.Clear();
            rightButtonStates.Clear();
        }

        void ReadDesktopRightButton(InputEventPtr inputEvent, UnityEngine.InputSystem.InputDevice device)
        {
            if (!Desktop || device is not Mouse mouse ||
                (!inputEvent.IsA<StateEvent>() && !inputEvent.IsA<DeltaStateEvent>())) return;

            // Remember event states ourselves so a device reset cannot fabricate a button transition.
            if (!mouse.rightButton.ReadValueFromEvent(inputEvent, out float right)) return;
            if (!rightButtonStates.TryGetValue(mouse, out bool previous)) previous = mouse.rightButton.isPressed;
            bool pressed = right >= .5f;
            rightButtonStates[mouse] = pressed;
            if (pressed == previous) return;

            Debug.Log($"[RemoteGrip] 우클릭 {(pressed ? "누름" : "뗌")} (frame={Time.frameCount}, mouse={mouse.deviceId})", this);
            if (desktopControlsActive) remoteButtonChanges.Enqueue((mouse, pressed));
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
            bool crouch = Button(right, CommonUsages.primaryButton);
            bool interact = Button(right, CommonUsages.secondaryButton);
            retryPressed = crouch && !retryHeld;
            bool interactPressed = interact && !interactHeld;
            retryHeld = crouch;
            interactHeld = interact;

            if (!ready && Button(hmd, CommonUsages.isTracked) && left.isValid && right.isValid)
            {
                SubsystemManager.GetSubsystems(inputSubsystems);
                foreach (var subsystem in inputSubsystems)
                {
                    if (subsystem.running)
                        subsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
                }
                ready = true;
            }

            if (!run.IsPlaying || !Button(hmd, CommonUsages.isTracked)) return;
            bool watch = Button(left, CommonUsages.primaryButton);
            if (right.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 turnStick))
                TurnHeadset(turnStick);
            ApplyStance(crouch, watch);

            var remote = run.printer.remote;
            Transform aim = rightHand.IsSelecting(remote) ? leftHand.transform : rightHand.transform;
            if (Physics.Raycast(aim.position, aim.forward, out RaycastHit hit, 1.25f, desktopAimLayers))
                UpdateInteraction(hit.collider, interactPressed);
            else
                UpdateInteraction(null, interactPressed);

            if (leftHand.IsSelecting(run.drawer.handle) || rightHand.IsSelecting(run.drawer.handle)) return;

            if (!left.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick)) return;

            Vector3 forward = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = transform.forward;
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            Vector3 movement = (side * stick.x + forward * stick.y).normalized;
            float amount = Mathf.InverseLerp(.17f, .96f, stick.magnitude);
            bool slow = crouch || watch;
            Walk(movement, amount * (slow ? SlowMoveSpeed : 1.9f), !slow && stick.magnitude >= .72f);
        }

        static bool Button(UnityEngine.XR.InputDevice device, InputFeatureUsage<bool> usage) =>
            device.TryGetFeatureValue(usage, out bool value) && value;

        void TurnHeadset(Vector2 stick)
        {
            float amount = Mathf.InverseLerp(.17f, .96f, Mathf.Abs(stick.x)) * Mathf.Sign(stick.x);
            if (amount == 0f) return;
            transform.RotateAround(view.position, Vector3.up, amount * turnSpeed * Time.deltaTime);
        }

        void UpdateDesktop()
        {
            if (!rightHand.IsSelecting(run.printer.remote))
                remoteGripMouse = null;
            var mouse = remoteGripMouse ?? Mouse.current;
            var keyboard = Keyboard.current;
            if (mouse == null || keyboard == null)
            {
                remoteButtonChanges.Clear();
                return;
            }
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
            if (!run.IsPlaying || !desktopControlsActive)
            {
                remoteButtonChanges.Clear();
                return;
            }

            Vector2 look = mouse.delta.ReadValue() * .12f;
            yaw += look.x;
            pitch = Mathf.Clamp(pitch - look.y, -82f, 82f);
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            view.localRotation = Quaternion.Euler(pitch, 0, 0);
            bool crouch = keyboard.cKey.isPressed;
            bool watch = keyboard.tabKey.isPressed;
            ApplyStance(crouch, watch);
            UpdateDesktopHand(keyboard);
            remoteButtonChanges.Clear();
            if (rightHand.IsSelecting(run.drawer.handle)) return;

            Vector2 direction = new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
            bool slow = crouch || watch;
            bool loud = !slow && keyboard.shiftKey.isPressed;
            Vector3 worldDirection = transform.TransformDirection(new Vector3(direction.x, 0, direction.y).normalized);
            Walk(worldDirection, slow ? SlowMoveSpeed : loud ? 1.9f : .98f, loud);
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused)
            {
                desktopControlsActive = false;
                remoteButtonChanges.Clear();
            }
        }

        void UpdateDesktopHand(Keyboard keyboard)
        {
            bool hasTarget = Physics.Raycast(view.position, view.forward, out RaycastHit target,
                2f, desktopAimLayers, QueryTriggerInteraction.Collide);
            var handle = run.drawer.handle;
            var badge = run.drawer.badge;

            if (rightHand.IsSelecting(handle))
            {
                run.SetInteractionHint(null);
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

            Transform hand = rightHand.transform;
            hand.SetPositionAndRotation(view.TransformPoint(new Vector3(.23f, -.19f, .43f)), view.rotation);
            var remote = run.printer.remote;
            bool pressed = keyboard.eKey.wasPressedThisFrame;
            if (!rightHand.hasSelection && hasTarget)
            {
                if (pressed && target.collider.GetComponentInParent<XRSimpleInteractable>() == handle)
                {
                    previewDrawerOrigin = target.point;
                    previewDrawerAxis = run.drawer.transform.parent;
                    drawerPull = 0;
                    drawerPullFrame = Time.frameCount;
                    hand.position = previewDrawerOrigin;
                    rightHand.StartManualInteraction((IXRSelectInteractable)handle);
                    return;
                }

                if (pressed && badge.gameObject.activeInHierarchy && target.collider.GetComponentInParent<XRGrabInteractable>() == badge)
                {
                    hand.position = badge.transform.position;
                    rightHand.StartManualInteraction((IXRSelectInteractable)badge);
                    rightHand.EndManualInteraction();
                    return;
                }
            }

            // Consume each down/up edge once, including a complete click within one frame.
            while (remoteButtonChanges.Count > 0)
            {
                var change = remoteButtonChanges.Dequeue();
                if (change.pressed)
                {
                    if (!rightHand.hasSelection && hasTarget && !remote.WasDropped &&
                        target.collider.GetComponentInParent<OfficePrinterRemote>() == remote)
                    {
                        rightHand.StartManualInteraction((IXRSelectInteractable)remote);
                        if (rightHand.IsSelecting(remote)) remoteGripMouse = change.mouse;
                    }
                }
                else if (change.mouse == remoteGripMouse && rightHand.IsSelecting(remote))
                    ReleaseDesktopRemote("우클릭 해제");
            }

            if (!rightHand.hasSelection && hasTarget && !remote.WasDropped &&
                target.collider.GetComponentInParent<OfficePrinterRemote>() == remote)
            {
                run.SetInteractionHint("우클릭 유지  리모컨 잡기");
                return;
            }

            UpdateInteraction(hasTarget && target.distance <= 1.55f ? target.collider : null, pressed);
        }

        void ReleaseDesktopRemote(string reason)
        {
            DesktopRemoteReleaseReason = reason;
            rightHand.EndManualInteraction();
            DesktopRemoteReleaseReason = null;
            remoteGripMouse = null;
        }

        void UpdateInteraction(Collider target, bool pressed)
        {
            string key = Desktop ? "E" : "B";
            var printer = run.printer;
            if (target && run.reader.IsTarget(target.transform) && !run.reader.IsAuthorized)
            {
                run.SetInteractionHint(key + (run.HasBadge ? "  사원증 찍기" : "  문 확인"));
                if (pressed) run.reader.Scan();
                return;
            }

            if (printer.remote.isSelected && printer.CanSchedule)
            {
                run.SetInteractionHint(key + "  예약 출력");
                if (pressed) printer.Schedule();
                return;
            }

            if (target == printer.printButton && printer.CanSchedule)
            {
                run.SetInteractionHint(key + "  출력");
                if (pressed) printer.PrintImmediately();
                return;
            }

            run.SetInteractionHint(!Desktop && target && target.GetComponentInParent<OfficePrinterRemote>() == printer.remote
                && !printer.remote.isSelected && !printer.remote.WasDropped ? "그립 유지  리모컨 잡기" : null);
        }

        void ApplyStance(bool crouch, bool watch)
        {
            float amount = Mathf.Clamp(EyeHeight - .78f, 0, .74f);
            crouchOffset = Mathf.MoveTowards(crouchOffset, crouch ? amount : 0, 4f * Time.deltaTime);
            if (Desktop) trackingSpace.localPosition = Vector3.down * crouchOffset;
            else UpdateHeadsetHeight();
            Vector3 localHead = transform.InverseTransformPoint(view.position);
            body.height = Mathf.Max(.55f, localHead.y + .08f);
            body.center = new Vector3(localHead.x, body.height * .5f + .012f, localHead.z);

            run.SetWatchVisible(watch);
        }

        void LateUpdate() => UpdateHeadsetHeight();

        [BeforeRenderOrder(50)]
        void UpdateHeadsetHeight()
        {
            if (Desktop) return;
            // Offset the whole tracking space so head rotation and hand positions stay relative to the headset.
            trackingSpace.localPosition = Vector3.up * (EyeHeight - view.localPosition.y - crouchOffset);
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
            if (loud) run.ReportNoise(transform.position, isFootstep: true);
        }

        public void StopMovement()
        {
            remoteButtonChanges.Clear();
            if (Desktop && rightHand.IsSelecting(run.printer.remote)) ReleaseDesktopRemote("게임 종료");
            else if (Desktop && rightHand.hasSelection) rightHand.EndManualInteraction();
            remoteGripMouse = null;
        }
    }
}
