using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OfficeEscape
{
    public sealed class OfficeRun : MonoBehaviour
    {
        public const float Duration = 60f;
        const float SecondsPerClockSecond = 1.5f;
        const float FailureFollowUpDelay = 3f;
        const float FailureFollowUpDuration = 2.25f;
        const string CaughtLine = "자네! 거기 있었구만! 이리 좀 와봐!";
        const string LateLine = "어? 퇴근 목록에 한 사람이 없네?";
        const string ExposureLine = "부장님 시선을 먼저 돌려야 해! 프린터기를 작동시키면 되지 않을까?";

        public OfficePlayer player;
        public OfficeBoss boss;
        public OfficeDrawer drawer;
        public BadgeReader reader;
        public OfficePrinter printer;
        public BoxCollider exposedApproach;
        public Text chat;
        public Text questHint;
        public Text wristTime;
        public GameObject watchDisplay;
        public Image blackout;
        public Text result;
        public Text dialogue;
        public Text retryHint;
        public Text thought;
        public Text interactionHint;
        public Text printerCountdown;

        enum Outcome { Playing, Escaped, Caught, Late }

        Outcome outcome;
        float startedAt;
        float finishedAt;
        float thoughtUntil;
        float dialogueUntil;
        bool started;
        bool hasFailureThought;
        string failureFollowUp;

        public bool IsPlaying => started && outcome == Outcome.Playing;
        public float SecondsLeft => started ? Mathf.Max(0, Duration - (Time.time - startedAt) / SecondsPerClockSecond) : Duration;
        public bool HasBadge { get; private set; }

        bool InExposedApproach => exposedApproach.bounds.Contains(player.View.position);

        void Start() => questHint.enabled = !player.Desktop;

        void Update()
        {
            if (!started && player.Ready)
            {
                started = true;
                startedAt = Time.time;
                boss.Begin();
            }

            if (!started) return;
            if (IsPlaying)
            {
                if (SecondsLeft <= 0f)
                {
                    Finish(Outcome.Late);
                    return;
                }

                float elapsed = Time.time - startedAt;
                questHint.enabled = !player.Desktop && elapsed < 6f;
                int seconds = Mathf.CeilToInt(SecondsLeft);
                wristTime.text = $"{seconds / 60:00}:{seconds % 60:00}";
                wristTime.color = SecondsLeft <= 10f ? new Color(1f, .54f, .38f) : new Color(.68f, .97f, .87f);
                if (Time.time >= thoughtUntil) thought.text = string.Empty;
                dialogue.enabled = Time.time < dialogueUntil;
                chat.text = dialogue.enabled || thought.text.Length > 0 || elapsed >= 8f ? string.Empty :
                    elapsed < 3.5f ? "부장님  오늘 일이 좀 많네… 지금 한가한 사람 있나?" : "부장님  음, 이미 다 퇴근한건가?";
                int count = printer.Countdown;
                printerCountdown.text = count > 0 ? count.ToString() : string.Empty;
                return;
            }

            float sinceEnd = Time.time - finishedAt;
            bool hasFollowUp = !string.IsNullOrEmpty(failureFollowUp);
            float followUpEnd = FailureFollowUpDelay + FailureFollowUpDuration;
            if (hasFollowUp && sinceEnd < followUpEnd)
            {
                if (sinceEnd >= FailureFollowUpDelay) thought.text = failureFollowUp;
                dialogue.enabled = false;
                return;
            }

            bool escaped = outcome == Outcome.Escaped;
            float fadeStart = hasFollowUp ? followUpEnd : escaped ? 0f : outcome == Outcome.Late ? 2.75f : hasFailureThought ? 1.85f : 1.05f;
            float fade = Mathf.SmoothStep(0, 1, Mathf.Clamp01((sinceEnd - fadeStart) / .65f));
            blackout.color = new Color(0, 0, 0, fade);
            dialogue.text = escaped ? string.Empty : outcome == Outcome.Late && sinceEnd < 1.45f
                ? LateLine : CaughtLine;
            dialogue.enabled = !escaped && !hasFollowUp && fade < .96f && (!hasFailureThought || sinceEnd >= 1.05f);
            if (sinceEnd >= 1.4f) thought.text = string.Empty;
            result.enabled = fade > .9f;
            retryHint.enabled = result.enabled;
            if (result.enabled && player.RetryPressed)
                SceneManager.LoadScene(gameObject.scene.buildIndex);
        }

        void LateUpdate()
        {
            if (!IsPlaying || SecondsLeft <= 0f) return;
            if (InExposedApproach && !boss.IsWatchingPrinter)
            {
                Caught(ExposureLine);
                return;
            }

            if (reader.HasCrossed(player.View.position)) Finish(Outcome.Escaped);
        }

        public void SetWatchVisible(bool visible) => watchDisplay.SetActive(visible && IsPlaying);

        public void ReportNoise(Vector3 position, bool isFootstep = false)
        {
            if (!IsPlaying) return;
            if (InExposedApproach)
                Caught(boss.IsWatchingPrinter ? "발소리가 너무 컸어!" : ExposureLine);
            else
                boss.Hear(position, isFootstep);
        }

        public void CollectBadge()
        {
            if (!IsPlaying || HasBadge) return;
            HasBadge = true;
            ShowThought("사원증을 챙겼다.");
        }

        public void ShowThought(string line)
        {
            chat.text = string.Empty;
            thought.text = line;
            thoughtUntil = Time.time + 2.25f;
        }

        public void ShowBossLine(string line)
        {
            chat.text = string.Empty;
            dialogue.text = line;
            dialogue.enabled = true;
            dialogueUntil = Time.time + 1.8f;
        }

        public void SetInteractionHint(string line) => interactionHint.text = line ?? string.Empty;

        public void Caught(string playerThought = null, string followUpThought = null)
        {
            if (IsPlaying && SecondsLeft > 0f) Finish(Outcome.Caught, playerThought, followUpThought);
        }

        void Finish(Outcome next, string playerThought = null, string followUpThought = null)
        {
            outcome = next;
            finishedAt = Time.time;
            chat.text = string.Empty;
            questHint.enabled = false;
            watchDisplay.SetActive(false);
            hasFailureThought = !string.IsNullOrEmpty(playerThought);
            failureFollowUp = followUpThought;
            thought.text = playerThought ?? string.Empty;
            SetInteractionHint(null);
            printerCountdown.text = string.Empty;
            dialogue.text = next == Outcome.Late ? LateLine : next == Outcome.Caught ? CaughtLine : string.Empty;
            dialogue.enabled = next != Outcome.Escaped && !hasFailureThought;
            result.text = next == Outcome.Escaped ? "정시 퇴근 성공" : "야근 확정";
            result.color = next == Outcome.Escaped ? new Color(.66f, .95f, .84f) : new Color(1f, .72f, .62f);
            retryHint.text = player.Desktop ? "R  다시 시작" : "오른손 A  다시 시작";
            player.StopMovement();
            if (next != Outcome.Escaped) boss.FacePlayer();
        }
    }
}
