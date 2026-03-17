// ExpertUIManager.cs
// Unified in-game HUD and game-over overlay for KTANE.
//
// Self-builds its World-Space Canvas in Awake() — no Inspector wiring needed.
// Canvas is positioned at the same world location as the Lobby canvas (0, 1.85, 0)
// so it seamlessly replaces the lobby UI once the game starts.
//
// Panel states:
//   Waiting   → canvas hidden (lobby is showing)
//   Active    → GamePanel visible (strike counter, timer, module hints)
//   Defused / Exploded → GameOverPanel visible (outcome + stats + buttons)
//
// Module references are auto-discovered via FindFirstObjectByType in Start()
// including inactive objects (bomb is hidden in lobby, modules are children of it).

using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using TMPro;

namespace KTANE
{
    public class ExpertUIManager : MonoBehaviour
    {
        // ── Module refs (auto-discovered in Start) ────────────────────────────
        private TimerModule   _timer;
        private WiresModule   _wires;
        private ButtonModule  _button;
        private KeypadModule  _keypad;
        private SimonModule   _simon;

        // ── Canvas root and panels ────────────────────────────────────────────
        private GameObject       _canvas;
        private GameObject       _gamePanel;
        private GameObject       _gameOverPanel;

        // ── Game-panel labels ─────────────────────────────────────────────────
        private TextMeshProUGUI  _lblStrikes;
        private TextMeshProUGUI  _lblTimer;
        private TextMeshProUGUI  _lblModules;

        // ── Game-over panel labels ────────────────────────────────────────────
        private TextMeshProUGUI  _lblOutcome;
        private TextMeshProUGUI  _lblStats;

        // =====================================================================
        // Unity lifecycle
        // =====================================================================

        private void Awake()
        {
            BuildCanvas();
        }

        private void Start()
        {
            // Auto-discover modules (bomb GO is initially inactive, so we must
            // include inactive objects in the search).
            _timer  = FindFirstObjectByType<TimerModule> (FindObjectsInactive.Include);
            _wires  = FindFirstObjectByType<WiresModule> (FindObjectsInactive.Include);
            _button = FindFirstObjectByType<ButtonModule>(FindObjectsInactive.Include);
            _keypad = FindFirstObjectByType<KeypadModule>(FindObjectsInactive.Include);
            _simon  = FindFirstObjectByType<SimonModule> (FindObjectsInactive.Include);

            var gm = KTANEGameManager.Instance;
            if (gm != null)
            {
                gm.OnGameStarted.AddListener(OnGameStarted);
                gm.OnBombDefused.AddListener(() => ShowGameOver(true));
                gm.OnBombExploded.AddListener(() => ShowGameOver(false));
            }

            // Hidden at start — lobby canvas is shown during Waiting state.
            _canvas.SetActive(false);
        }

        private void Update()
        {
            var gm = KTANEGameManager.Instance;
            if (gm == null) return;

            // Auto-hide when lobby resets the game back to Waiting.
            if (gm.CurrentState == GameState.Waiting && _canvas.activeSelf)
            {
                _canvas.SetActive(false);
                return;
            }

            // Live refresh only while game panel is showing.
            if (gm.CurrentState == GameState.Active
                && _canvas.activeSelf
                && _gamePanel.activeSelf)
            {
                RefreshGamePanel(gm);
            }
        }

        // =====================================================================
        // Event handlers
        // =====================================================================

        private void OnGameStarted()
        {
            _canvas.SetActive(true);
            _gamePanel.SetActive(true);
            _gameOverPanel.SetActive(false);
        }

        private void ShowGameOver(bool defused)
        {
            _canvas.SetActive(true);
            _gamePanel.SetActive(false);
            _gameOverPanel.SetActive(true);

            _lblOutcome.text = defused
                ? "<color=#44FF88>BOMB DEFUSED!</color>"
                : "<color=#FF4444>BOMB EXPLODED!</color>";

            var gm = KTANEGameManager.Instance;
            if (gm != null)
            {
                int secs = _timer != null ? _timer.SecondsRemaining : 0;
                _lblStats.text =
                    $"Strikes: {gm.Strikes} / {gm.MaxStrikes}   " +
                    $"Time left: {secs / 60:D2}:{secs % 60:D2}";
            }
        }

        // =====================================================================
        // Game-panel refresh (called every frame while Active)
        // =====================================================================

        private void RefreshGamePanel(KTANEGameManager gm)
        {
            // ── Strike indicators ─────────────────────────────────────────────
            if (_lblStrikes != null)
            {
                var sb = new StringBuilder();
                for (int i = 0; i < gm.MaxStrikes; i++)
                {
                    sb.Append(i < gm.Strikes
                        ? "<color=#FF2222>■</color>"
                        : "<color=#333333>■</color>");
                    if (i < gm.MaxStrikes - 1) sb.Append("  ");
                }
                _lblStrikes.text = sb.ToString();
            }

            // ── Timer ─────────────────────────────────────────────────────────
            if (_lblTimer != null && _timer != null)
            {
                int s = _timer.SecondsRemaining;
                _lblTimer.text = $"{s / 60:D2}:{s % 60:D2}";
            }

            // ── Module hints ──────────────────────────────────────────────────
            if (_lblModules != null)
            {
                var sb = new StringBuilder();
                if (_wires  != null) { sb.AppendLine(BuildWiresText(_wires));          }
                if (_button != null) { sb.AppendLine(BuildButtonText(_button, gm));    }
                if (_keypad != null) { sb.AppendLine(BuildKeypadText(_keypad));        }
                if (_simon  != null) { sb.Append    (BuildSimonText(_simon, gm));      }
                _lblModules.text = sb.ToString();
            }
        }

        // =====================================================================
        // Module text builders
        // =====================================================================

        private static string BuildWiresText(WiresModule w)
        {
            string[] cols = { "Red", "Blue", "Yel", "Wht", "Blk", "Red" };
            var sb = new StringBuilder("─── WIRES ───\n");
            for (int i = 0; i < 6; i++)
            {
                bool cut = w.WiresCut != null && i < w.WiresCut.Length && w.WiresCut[i];
                string col = i < cols.Length ? cols[i] : "?";
                sb.AppendLine($"  {i}: {col}  {(cut ? "<color=#FF4444>CUT</color>" : "intact")}");
            }
            sb.Append(w.IsSolved
                ? "  <color=#44FF88>[SOLVED]</color>"
                : $"  Cut wire #{w.CorrectWire}");
            return sb.ToString();
        }

        private static string BuildButtonText(ButtonModule btn, KTANEGameManager gm)
        {
            var sb = new StringBuilder("─── BUTTON ───\n");
            sb.AppendLine($"  Colour: {btn.Colour}   Held: {btn.IsHeld}");
            switch (btn.Colour)
            {
                case ButtonColour.Blue:
                    sb.AppendLine("  Hold → release when timer has 4"); break;
                case ButtonColour.Red:
                    sb.AppendLine("  Hold → release when timer has 1"); break;
                default:
                    sb.AppendLine("  Tap immediately (quick press)"); break;
            }
            sb.Append(btn.IsSolved
                ? "  <color=#44FF88>[SOLVED]</color>"
                : "  [pending]");
            return sb.ToString();
        }

        private static string BuildKeypadText(KeypadModule kp)
        {
            var sb = new StringBuilder("─── KEYPAD ───\n");
            if (kp.Symbols != null)
                for (int i = 0; i < kp.Symbols.Length; i++)
                    sb.AppendLine($"  Key {i}: {kp.Symbols[i]}");
            if (kp.CorrectOrder != null)
                sb.AppendLine($"  Press order: {string.Join(" → ", kp.CorrectOrder)}");
            sb.Append(kp.IsSolved
                ? "  <color=#44FF88>[SOLVED]</color>"
                : "  [pending]");
            return sb.ToString();
        }

        private static string BuildSimonText(SimonModule simon, KTANEGameManager gm)
        {
            var sb = new StringBuilder("─── SIMON ───\n");
            sb.AppendLine($"  Round: {simon.CurrentRound}");
            if (simon.Sequence != null)
            {
                string[] names = { "Red", "Blue", "Green", "Yellow" };
                sb.Append("  Press: ");
                for (int i = 0; i < simon.CurrentRound && i < simon.Sequence.Length; i++)
                {
                    if (i > 0) sb.Append(" → ");
                    sb.Append(names[simon.Sequence[i]]);
                }
                sb.AppendLine();
                sb.AppendLine($"  (Strikes={gm.Strikes}; colour remapped)");
            }
            sb.Append(simon.IsSolved
                ? "  <color=#44FF88>[SOLVED]</color>"
                : "  [pending]");
            return sb.ToString();
        }

        // =====================================================================
        // Button callbacks
        // =====================================================================

        private void OnPlayAgainClicked()
        {
            FindFirstObjectByType<KTANELobbyManager>()?.ReturnToLobby();
        }

        private void OnChooseLevelClicked()
        {
            FindFirstObjectByType<KTANELobbyManager>()?.ReturnToLobby();
        }

        // =====================================================================
        // Canvas builder (runs in Awake — no scene objects needed yet)
        // =====================================================================

        private void BuildCanvas()
        {
            // Root canvas — 70 cm × 90 cm world-space panel, same position as
            // the lobby canvas so the two swap seamlessly.
            _canvas = new GameObject("ExpertCanvas");
            _canvas.transform.SetParent(transform, false);
            _canvas.transform.localPosition = new Vector3(0f, 1.85f, 0f);
            _canvas.transform.localRotation = Quaternion.identity;
            _canvas.transform.localScale    = Vector3.one * 0.001f;

            var canvasComp = _canvas.AddComponent<Canvas>();
            canvasComp.renderMode = RenderMode.WorldSpace;
            _canvas.AddComponent<CanvasScaler>();
            _canvas.AddComponent<TrackedDeviceGraphicRaycaster>();
            _canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(700f, 900f);

            _gamePanel     = BuildGamePanel();
            _gameOverPanel = BuildGameOverPanel();
            _gameOverPanel.SetActive(false);
        }

        // ── Game panel ────────────────────────────────────────────────────────
        private GameObject BuildGamePanel()
        {
            var panel = MakeStretchPanel(_canvas.transform, "GamePanel",
                            new Color(0.05f, 0.05f, 0.08f, 0.95f));
            AddTopStripe(panel.transform, new Color(0.8f, 0.15f, 0.15f));

            AddTMP(panel.transform, "Title", "EXPERT CONSOLE",
                   24, FontStyles.Bold, new Color(1f, 0.85f, 0.2f),
                   TextAlignmentOptions.Center, new Vector2(0f, 415f), new Vector2(680f, 38f));

            _lblStrikes = AddTMP(panel.transform, "Strikes", "■  ■  ■",
                52, FontStyles.Bold, Color.white,
                TextAlignmentOptions.Center, new Vector2(0f, 350f), new Vector2(680f, 68f));
            _lblStrikes.richText = true;

            _lblTimer = AddTMP(panel.transform, "Timer", "05:00",
                38, FontStyles.Bold, new Color(0.4f, 1f, 0.4f),
                TextAlignmentOptions.Center, new Vector2(0f, 280f), new Vector2(680f, 52f));

            AddDivider(panel.transform, new Vector2(0f, 250f));

            _lblModules = AddTMP(panel.transform, "Modules", "",
                17, FontStyles.Normal, new Color(0.85f, 0.85f, 0.85f),
                TextAlignmentOptions.TopLeft, new Vector2(0f, -100f), new Vector2(660f, 680f));
            _lblModules.richText = true;

            return panel;
        }

        // ── Game-over panel ───────────────────────────────────────────────────
        private GameObject BuildGameOverPanel()
        {
            var panel = MakeStretchPanel(_canvas.transform, "GameOverPanel",
                            new Color(0.04f, 0.04f, 0.06f, 0.98f));

            _lblOutcome = AddTMP(panel.transform, "Outcome", "",
                56, FontStyles.Bold, Color.white,
                TextAlignmentOptions.Center, new Vector2(0f, 220f), new Vector2(660f, 130f));
            _lblOutcome.richText = true;

            _lblStats = AddTMP(panel.transform, "Stats", "",
                22, FontStyles.Normal, new Color(0.7f, 0.7f, 0.7f),
                TextAlignmentOptions.Center, new Vector2(0f, 110f), new Vector2(660f, 50f));

            AddDivider(panel.transform, new Vector2(0f, 60f));

            var btnPlay = MakeButton(panel.transform, "BtnPlayAgain",
                "PLAY AGAIN", new Vector2(0f, -60f), new Vector2(360f, 90f),
                new Color(0.15f, 0.45f, 0.15f));
            btnPlay.onClick.AddListener(OnPlayAgainClicked);

            var btnLevel = MakeButton(panel.transform, "BtnChooseLevel",
                "CHOOSE LEVEL", new Vector2(0f, -190f), new Vector2(360f, 80f),
                new Color(0.15f, 0.25f, 0.55f));
            btnLevel.onClick.AddListener(OnChooseLevelClicked);

            return panel;
        }

        // =====================================================================
        // Factory helpers
        // =====================================================================

        private static GameObject MakeStretchPanel(Transform parent, string name, Color colour)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = colour;
            var rt  = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return go;
        }

        private static void AddTopStripe(Transform parent, Color colour)
        {
            var go  = new GameObject("Stripe");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = colour;
            var rt  = go.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0f, 1f);
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.pivot            = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = new Vector2(0f, 6f);
        }

        private static void AddDivider(Transform parent, Vector2 pos)
        {
            var go  = new GameObject("Divider");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.3f, 0.3f, 0.3f);
            var rt  = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta        = new Vector2(660f, 2f);
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
            tmp.richText  = true;
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta        = sizeDelta;
            return tmp;
        }

        private static Button MakeButton(Transform parent, string name, string label,
            Vector2 pos, Vector2 size, Color colour)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = colour;
            var btn = go.AddComponent<Button>();
            var rt  = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta        = size;

            var lblGO = new GameObject("Label");
            lblGO.transform.SetParent(go.transform, false);
            var tmp       = lblGO.AddComponent<TextMeshProUGUI>();
            tmp.text      = label;
            tmp.fontSize  = 28;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color     = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            var lrt       = lblGO.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;

            return btn;
        }
    }
}
