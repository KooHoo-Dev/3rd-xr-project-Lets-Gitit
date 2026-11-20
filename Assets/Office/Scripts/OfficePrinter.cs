using UnityEngine;

namespace OfficeEscape
{
    public sealed class OfficePrinter : MonoBehaviour
    {
        public OfficeRun run;
        public OfficePrinterRemote remote;
        public Collider printButton;
        public Transform paper;
        public AudioSource motor;

        enum State { Ready, CountingDown, Printing }

        State state;
        float printAt;
        float printStartedAt;
        Vector3 paperAtRest;

        public bool CanSchedule => state == State.Ready;
        public int Countdown => state == State.CountingDown ? Mathf.Max(0, Mathf.CeilToInt(printAt - Time.time)) : 0;

        void Awake()
        {
            paperAtRest = paper.localPosition;
            paper.gameObject.SetActive(false);
        }

        public void Schedule()
        {
            if (!run.IsPlaying || !CanSchedule || !remote.isSelected) return;
            state = State.CountingDown;
            printAt = Time.time + 3f;
        }

        public void PrintImmediately()
        {
            if (!run.IsPlaying || !CanSchedule) return;
            StartPrinting();
            run.Caught("버튼을 누르자마자 부장님이 이쪽을 봤어!");
        }

        void Update()
        {
            if (state == State.CountingDown && run.IsPlaying && remote.isSelected && Time.time >= printAt)
            {
                StartPrinting();
                run.boss.InvestigatePrinter(transform.position);
            }

            if (state == State.Printing)
            {
                float paperTravel = Mathf.Clamp01((Time.time - printStartedAt) / 2.2f) * .24f;
                paper.localPosition = paperAtRest + Vector3.up * paperTravel;
            }
        }

        void StartPrinting()
        {
            state = State.Printing;
            printStartedAt = Time.time;
            motor.Play();
            paper.gameObject.SetActive(true);
        }
    }
}
