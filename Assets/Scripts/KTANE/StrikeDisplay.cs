// StrikeDisplay.cs
// A small self-building world-space panel placed on the table that shows
// the current strike count.  Subscribes to KTANEGameManager events.
//
// SCENE SETUP:
//   BuildKTANEScene creates and positions this automatically.

using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KTANE
{
    public class StrikeDisplay : MonoBehaviour
    {
        private TextMeshProUGUI _indicators;

        // ================================================================
        // Unity lifecycle
        // ================================================================

        private void Awake()
        {
            BuildUI();
        }

        private void Start()
        {
            var gm = KTANEGameManager.Instance;
            if (gm == null) return;

            gm.OnStrikeAdded.AddListener(_ => Refresh());
            gm.OnGameStarted.AddListener(Refresh);
            gm.OnBombDefused.AddListener(Refresh);
            gm.OnBombExploded.AddListener(Refresh);

            Refresh();
        }

        // ================================================================
        // Display update
        // ================================================================

        private void Refresh()
        {
            if (_indicators == null) return;
            var gm = KTANEGameManager.Instance;
            if (gm == null) return;

            int strikes = gm.Strikes;
            int max     = gm.MaxStrikes;

            var sb = new StringBuilder();
            for (int i = 0; i < max; i++)
            {
                // ■ filled red = strike taken   □ outline dark = remaining
                if (i < strikes)
                    sb.Append("<color=#FF2222>■</color>");
                else
                    sb.Append("<color=#2A2A2A>■</color>");

                if (i < max - 1) sb.Append("  ");
            }
            _indicators.text = sb.ToString();
        }

        // ================================================================
        // Build UI
        // ================================================================

        private void BuildUI()
        {
            // World-space canvas — 28 cm × 14 cm (280 × 140 at scale 0.001)
            var canvasGO = new GameObject("Canvas");
            canvasGO.transform.SetParent(transform, false);

            var canvas       = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGO.AddComponent<CanvasScaler>();

            var rt       = canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(280f, 140f);
            canvasGO.transform.localScale = Vector3.one * 0.001f;

            // Dark background panel
            var bg    = new GameObject("BG");
            bg.transform.SetParent(canvasGO.transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.06f, 0.06f, 0.08f, 0.97f);
            var bgRT  = bg.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;

            // Thin top border stripe
            var stripe    = new GameObject("Stripe");
            stripe.transform.SetParent(canvasGO.transform, false);
            var stripeImg = stripe.AddComponent<Image>();
            stripeImg.color = new Color(0.8f, 0.15f, 0.15f);
            var stripeRT  = stripe.GetComponent<RectTransform>();
            stripeRT.anchorMin        = new Vector2(0f, 1f);
            stripeRT.anchorMax        = new Vector2(1f, 1f);
            stripeRT.pivot            = new Vector2(0.5f, 1f);
            stripeRT.anchoredPosition = Vector2.zero;
            stripeRT.sizeDelta        = new Vector2(0f, 6f);

            // "STRIKES" header label
            AddTMP(canvasGO.transform, "Header", "STRIKES",
                   20f, FontStyles.Bold,
                   new Color(0.65f, 0.65f, 0.65f), TextAlignmentOptions.Center,
                   new Vector2(0f, 38f), new Vector2(260f, 34f));

            // Strike indicators — large bold squares, coloured at runtime
            var indGO = new GameObject("Indicators");
            indGO.transform.SetParent(canvasGO.transform, false);
            _indicators = indGO.AddComponent<TextMeshProUGUI>();
            _indicators.text      = "■  ■  ■";
            _indicators.fontSize  = 52f;
            _indicators.fontStyle = FontStyles.Bold;
            _indicators.color     = new Color(0.2f, 0.2f, 0.2f);
            _indicators.alignment = TextAlignmentOptions.Center;
            _indicators.richText  = true;
            var indRT = indGO.GetComponent<RectTransform>();
            indRT.anchoredPosition = new Vector2(0f, -20f);
            indRT.sizeDelta        = new Vector2(260f, 70f);
        }

        private static TextMeshProUGUI AddTMP(
            Transform parent, string name, string text,
            float size, FontStyles style, Color colour,
            TextAlignmentOptions align, Vector2 pos, Vector2 sizeDelta)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = size;
            tmp.fontStyle = style;
            tmp.color     = colour;
            tmp.alignment = align;
            var r = go.GetComponent<RectTransform>();
            r.anchoredPosition = pos;
            r.sizeDelta        = sizeDelta;
            return tmp;
        }
    }
}
