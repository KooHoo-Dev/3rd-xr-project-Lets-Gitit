using UnityEngine;

namespace OfficeEscape
{
    public sealed class BadgeReader : MonoBehaviour
    {
        public OfficeRun run;
        public Transform door;
        public Renderer statusLight;
        public Material authorizedLight;
        public AudioSource beep;
        public Transform threshold;
        public BoxCollider reminderArea;

        Vector3 doorClosed;
        float openProgress;
        float flashUntil;
        bool badgeReminderShown;

        public bool IsAuthorized { get; private set; }

        void Awake()
        {
            doorClosed = door.localPosition;
        }

        public void Scan()
        {
            if (!run.IsPlaying || run.SecondsLeft <= 0f || IsAuthorized) return;
            if (!run.HasBadge)
            {
                flashUntil = Time.time + .55f;
                run.ShowThought("사원증이 필요해! 내 자리 서랍에 넣어뒀는데!");
                return;
            }

            IsAuthorized = true;
            statusLight.enabled = true;
            statusLight.sharedMaterial = authorizedLight;
            beep.Play();
        }

        void Update()
        {
            if (!run.IsPlaying) return;
            if (!run.HasBadge && !badgeReminderShown && reminderArea && reminderArea.bounds.Contains(run.player.View.position))
            {
                badgeReminderShown = true;
                run.ShowThought("이런, 문을 열려면 사원증이 필요해. 서랍에 넣어뒀는데!");
            }
            if (!IsAuthorized)
            {
                statusLight.enabled = flashUntil - Time.time <= .26f;
                return;
            }

            openProgress = Mathf.MoveTowards(openProgress, 1, Time.deltaTime / .45f);
            door.localPosition = doorClosed + Vector3.forward * (1.45f * Mathf.SmoothStep(0, 1, openProgress));
        }

        public bool IsTarget(Transform target) => target.IsChildOf(transform) || target.IsChildOf(door);

        public bool HasCrossed(Vector3 playerPosition)
        {
            Vector3 atExit = threshold.InverseTransformPoint(playerPosition);
            return IsAuthorized && atExit.x > 0f && Mathf.Abs(atExit.z) < .8f;
        }
    }
}
