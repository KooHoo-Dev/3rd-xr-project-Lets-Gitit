using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OfficeEscape
{
    public sealed class OfficeRun : MonoBehaviour
    {
        public const float Duration = 60f;
        const string CaughtLine = "자네! 거기 있었구만! 이리 좀 와봐!";
        const string LateLine = "어? 퇴근 목록에 한 사람이 없네?";

        public OfficePlayer player;
        public OfficeBoss boss;
        public OfficeDrawer drawer;
        public BadgeReader reader;
        public Text chat;
        public Text questHint;
        public Text wristTime;
        public GameObject watchDisplay;
        public Image blackout;
        public Text result;
        public Text dialogue;
        public Text retryHint;

        enum Outcome { Playing, Escaped, Caught, Late }

        Outcome outcome;
        float startedAt;
        float finishedAt;
        bool started;

        public bool IsPlaying => started && outcome == Outcome.Playing;
        public float SecondsLeft => started ? Mathf.Max(0, Duration - (Time.time - startedAt)) : Duration;

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

                float elapsed = Duration - SecondsLeft;
                questHint.enabled = !player.Desktop && elapsed < 6f;
                chat.text = elapsed < 3.5f ? "부장님  오늘 일이 좀 많네… 지금 한가한 사람 있나?" :
                    elapsed < 8f ? "부장님  음, 이미 다 퇴근한건가?" : string.Empty;
                int seconds = Mathf.CeilToInt(SecondsLeft);
                wristTime.text = $"{seconds / 60:00}:{seconds % 60:00}";
                wristTime.color = SecondsLeft <= 10f ? new Color(1f, .54f, .38f) : new Color(.68f, .97f, .87f);
                return;
            }

            float sinceEnd = Time.time - finishedAt;
            bool escaped = outcome == Outcome.Escaped;
            float fadeStart = escaped ? 0f : outcome == Outcome.Late ? 2.75f : 1.05f;
            float fade = Mathf.SmoothStep(0, 1, Mathf.Clamp01((sinceEnd - fadeStart) / .65f));
            blackout.color = new Color(0, 0, 0, fade);
            dialogue.text = escaped ? string.Empty : outcome == Outcome.Late && sinceEnd < 1.45f
                ? LateLine : CaughtLine;
            dialogue.enabled = !escaped && fade < .96f;
            result.enabled = fade > .9f;
            retryHint.enabled = result.enabled;
            if (result.enabled && player.RetryPressed)
                SceneManager.LoadScene(gameObject.scene.buildIndex);
        }

        public void SetWatchVisible(bool visible) => watchDisplay.SetActive(visible && IsPlaying);

        public void ReportNoise(Vector3 position)
        {
            if (IsPlaying) boss.Hear(position);
        }

        public void Caught()
        {
            if (IsPlaying && SecondsLeft > 0f) Finish(Outcome.Caught);
        }

        public void CrossExit()
        {
            if (IsPlaying && SecondsLeft > 0f && reader.IsAuthorized) Finish(Outcome.Escaped);
        }

        void Finish(Outcome next)
        {
            outcome = next;
            finishedAt = Time.time;
            chat.text = string.Empty;
            questHint.enabled = false;
            watchDisplay.SetActive(false);
            dialogue.text = next == Outcome.Late ? LateLine : next == Outcome.Caught ? CaughtLine : string.Empty;
            dialogue.enabled = next != Outcome.Escaped;
            result.text = next == Outcome.Escaped ? "정시 퇴근 성공" : "야근 확정";
            result.color = next == Outcome.Escaped ? new Color(.66f, .95f, .84f) : new Color(1f, .72f, .62f);
            retryHint.text = player.Desktop ? "R  다시 시작" : "오른손 A  다시 시작";
            player.StopMovement();
            if (next != Outcome.Escaped) boss.FacePlayer();
        }
    }
}
