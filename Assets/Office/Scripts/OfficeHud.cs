using UnityEngine;
using UnityEngine.UI;

namespace OfficeEscape
{
    [RequireComponent(typeof(Canvas), typeof(CanvasScaler))]
    public sealed class OfficeHud : MonoBehaviour
    {
        [SerializeField] RectTransform timer;
        [SerializeField] Material headsetOverlay;

        internal static void ConfigureFor(OfficeRun run, bool desktop)
        {
            var canvas = run.dialogue.GetComponentInParent<Canvas>(true);
            if (!canvas) return;

            if (!canvas.TryGetComponent(out OfficeHud hud))
            {
                hud = canvas.gameObject.AddComponent<OfficeHud>();
                var scaler = hud.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(900f, 560f);
                scaler.matchWidthOrHeight = 1f;

                var blackout = run.blackout.rectTransform;
                blackout.anchorMin = Vector2.zero;
                blackout.anchorMax = Vector2.one;
                blackout.offsetMin = blackout.offsetMax = Vector2.zero;
            }

            if (!hud.timer) hud.timer = (RectTransform)run.watchDisplay.transform;
            if (hud.timer.parent != canvas.transform)
            {
                if (hud.timer.TryGetComponent(out Canvas watchCanvas))
                {
                    watchCanvas.enabled = false;
                    Destroy(watchCanvas);
                }
                hud.timer.SetParent(canvas.transform, false);
                hud.timer.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                hud.timer.localScale = new Vector3(.52f, .52f, 1f);
                hud.timer.SetSiblingIndex(run.blackout.transform.GetSiblingIndex());
            }

            hud.Configure(desktop);
        }

        internal void Configure(bool desktop)
        {
            if (desktop)
            {
                var canvas = GetComponent<Canvas>();
                transform.SetParent(null, false);
                transform.localScale = Vector3.one;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                timer.anchorMin = timer.anchorMax = timer.pivot = Vector2.zero;
                timer.anchoredPosition = new Vector2(24f, 22f);
                return;
            }

            timer.anchorMin = timer.anchorMax = timer.pivot = new Vector2(.5f, .5f);
            timer.anchoredPosition = new Vector2(-245f, -215f);
            if (!headsetOverlay) headsetOverlay = Resources.Load<Material>("Headset HUD");
            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
                graphic.material = headsetOverlay;
        }
    }
}
