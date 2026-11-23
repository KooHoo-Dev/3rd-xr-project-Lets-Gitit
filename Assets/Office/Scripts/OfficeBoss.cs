using UnityEngine;

namespace OfficeEscape
{
    public sealed class OfficeBoss : MonoBehaviour
    {
        public OfficeRun run;
        public Transform chair;
        public Transform torso;
        public Transform head;
        public Transform leftArm;
        public Transform rightArm;
        public Transform[] upperLegs;
        public Transform[] lowerLegs;
        public AudioSource chairAudio;
        public LayerMask coverLayers;
        [Min(.1f)] public float warningSeconds = 1.2f;
        [Min(.1f)] public float exposureSeconds = .5f;

        enum Phase { Seated, StandingUp, Searching, SittingDown, TurningToPrinter, WatchingPrinter }

        Phase phase;
        float phaseAge;
        float nextStand;
        float exposure;
        int loudFootsteps;
        float lookYaw;
        float soundYaw;
        float searchLength;
        float standingAmount;
        float chairAmount;
        float bodyYaw;
        float startStandingAmount;
        float startChairAmount;
        float printerYaw;
        float watchEndsAt;
        bool heard;
        Vector3 chairRest;
        Quaternion torsoRest;
        Quaternion armRest;

        public bool IsWatchingPrinter => phase == Phase.WatchingPrinter && Time.time < watchEndsAt;

        void Awake()
        {
            chairRest = chair.localPosition;
            torsoRest = torso.localRotation;
            armRest = leftArm.localRotation;
            ApplyPose(0, 0, 0);
        }

        public void Begin() => ScheduleStand();

        public void Hear(Vector3 position, bool isFootstep = false)
        {
            if (phase == Phase.TurningToPrinter || phase == Phase.WatchingPrinter) return;
            if (isFootstep && (phase == Phase.Seated || phase == Phase.SittingDown))
            {
                loudFootsteps++;
                if (loudFootsteps < 2) return;
            }
            soundYaw = LocalYaw(position);
            heard = true;
            if (phase == Phase.Seated || phase == Phase.SittingDown) StartStanding();
        }

        public void InvestigatePrinter(Vector3 position)
        {
            loudFootsteps = 0;
            printerYaw = LocalYaw(position);
            startStandingAmount = standingAmount;
            startChairAmount = chairAmount;
            exposure = 0;
            phase = Phase.TurningToPrinter;
            phaseAge = 0;
            chairAudio.Play();
        }

        void Update()
        {
            if (!run.IsPlaying) return;
            phaseAge += Time.deltaTime;
            switch (phase)
            {
                case Phase.Seated:
                    if (phaseAge >= nextStand) StartStanding();
                    break;
                case Phase.StandingUp:
                    float rise = Mathf.InverseLerp(.24f, warningSeconds, phaseAge);
                    float brace = Mathf.Sin(Mathf.InverseLerp(.13f, .94f, phaseAge) * Mathf.PI);
                    ApplyPose(rise, Mathf.Clamp01(phaseAge / .48f), brace);
                    if (phaseAge >= warningSeconds)
                    {
                        phase = Phase.Searching;
                        phaseAge = 0;
                        searchLength = Random.Range(2.4f, 3.4f);
                    }
                    break;
                case Phase.Searching:
                    Search();
                    if (phaseAge >= searchLength)
                    {
                        phase = Phase.SittingDown;
                        phaseAge = 0;
                    }
                    break;
                case Phase.SittingDown:
                    float sit = 1f - Mathf.Clamp01(phaseAge / .63f);
                    ApplyPose(sit, sit, 0, Mathf.MoveTowardsAngle(bodyYaw, 0, 150f * Time.deltaTime));
                    TurnToward(0);
                    if (sit <= 0) ScheduleStand();
                    break;
                case Phase.TurningToPrinter:
                    float turn = Mathf.SmoothStep(0, 1, Mathf.Clamp01(phaseAge / .55f));
                    ApplyPose(Mathf.Lerp(startStandingAmount, 1, turn), Mathf.Lerp(startChairAmount, 1, turn), 0,
                        Mathf.MoveTowardsAngle(bodyYaw, printerYaw, 180f * Time.deltaTime));
                    TurnToward(printerYaw);
                    if (turn >= 1 && Mathf.Abs(Mathf.DeltaAngle(lookYaw, printerYaw)) <= 1f)
                    {
                        phase = Phase.WatchingPrinter;
                        watchEndsAt = Time.time + 5f;
                        run.ShowBossLine("거기 누구 있나?");
                    }
                    break;
                case Phase.WatchingPrinter:
                    if (Time.time >= watchEndsAt)
                    {
                        phase = Phase.SittingDown;
                        phaseAge = 0;
                    }
                    break;
            }
        }

        void Search()
        {
            float sweep = Mathf.Sin(phaseAge / searchLength * Mathf.PI * 2f) * (heard ? 36f : 78f);
            TurnToward(Mathf.Clamp((heard ? soundYaw : -8f) + sweep, -104f, 104f));

            Vector3 playerHead = run.player.View.position;
            Vector3 toPlayer = playerHead - head.position;
            bool withinView = toPlayer.sqrMagnitude < 16f * 16f &&
                Mathf.Abs(Mathf.DeltaAngle(lookYaw, LocalYaw(playerHead))) < 49f;
            bool visible = withinView && !Physics.Raycast(head.position, toPlayer.normalized,
                toPlayer.magnitude, coverLayers, QueryTriggerInteraction.Ignore);

            exposure = visible ? exposure + Time.deltaTime : 0f;
            if (exposure >= exposureSeconds) run.Caught();
        }

        void StartStanding()
        {
            loudFootsteps = 0;
            phase = Phase.StandingUp;
            phaseAge = 0;
            chairAudio.Play();
        }

        void ScheduleStand()
        {
            phase = Phase.Seated;
            phaseAge = 0;
            exposure = 0;
            heard = false;
            loudFootsteps = 0;
            nextStand = Random.Range(3.9f, 7.3f);
        }

        public void FacePlayer()
        {
            ApplyPose(1, 1, 0);
            lookYaw = LocalYaw(run.player.View.position);
            head.localRotation = Quaternion.Euler(0, lookYaw, 0);
        }

        void ApplyPose(float standing, float chairSlide, float brace, float yaw = 0)
        {
            standingAmount = standing;
            chairAmount = chairSlide;
            bodyYaw = yaw;
            torso.localPosition = new Vector3(0, Mathf.Lerp(.87f, 1.41f, standing), .1f * brace);
            torso.localRotation = Quaternion.Euler(0, bodyYaw, 0) * torsoRest * Quaternion.Euler(22f * brace, 0, 0);
            chair.localPosition = chairRest + Vector3.back * (.35f * chairSlide);
            PoseArm(leftArm, -.24f, brace);
            PoseArm(rightArm, .24f, brace);
            for (int i = 0; i < upperLegs.Length; i++)
            {
                float x = i == 0 ? -.105f : .105f;
                var hip = new Vector3(x, torso.localPosition.y - .31f, 0);
                var knee = new Vector3(x, Mathf.Lerp(.47f, .57f, standing), Mathf.Lerp(.38f, .03f, standing));
                var ankle = new Vector3(x, .075f, Mathf.Lerp(.32f, 0, standing));
                PoseLeg(upperLegs[i], hip, knee);
                PoseLeg(lowerLegs[i], knee, ankle);
            }
        }

        void PoseArm(Transform arm, float x, float brace)
        {
            var deskContact = transform.TransformPoint(new Vector3(x, .876f, .48f));
            var direction = torso.InverseTransformDirection(deskContact - arm.position);
            var rest = armRest * Quaternion.Euler(-15f, 0, 0);
            var reaching = Quaternion.FromToRotation(Vector3.down, direction.normalized);
            arm.localRotation = Quaternion.Slerp(rest, reaching, brace);
        }

        static void PoseLeg(Transform segment, Vector3 from, Vector3 to)
        {
            var direction = to - from;
            segment.localPosition = (from + to) * .5f;
            segment.localRotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
            segment.localScale = new Vector3(.158f, direction.magnitude, .17f);
        }

        void TurnToward(float target)
        {
            lookYaw = Mathf.MoveTowardsAngle(lookYaw, target, 175f * Time.deltaTime);
            head.localRotation = Quaternion.Euler(0, lookYaw - bodyYaw, 0);
        }

        float LocalYaw(Vector3 position)
        {
            Vector3 local = transform.InverseTransformPoint(position);
            return Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
        }
    }
}
