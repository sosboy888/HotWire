// ManualTablet.cs
// Physical VR-grabbable tablet showing the bomb defusal manual.
//
// PDF MODE (preferred):
//   Place PNG textures named page_000.png … page_NNN.png inside
//   Assets/Resources/ManualPages/ using:
//     Tools → KTANE → Import Manual PDF Pages
//   The tablet will automatically switch to PDF mode and display the images.
//
// TEXT MODE (fallback):
//   If no PDF textures are found the tablet falls back to the built-in
//   plain-text rule pages.
//
// The tablet is 1.5× larger than the original design for better VR readability.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;
using TMPro;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace KTANE
{
    public class ManualTablet : MonoBehaviour
    {
        // ── Text-mode page content ────────────────────────────────────────────
        private static readonly string[] PageTitles =
        {
            "FIELD REFERENCE MANUAL",   // 0
            "MODULE 1 — TIMER (1/2)",   // 1
            "MODULE 1 — TIMER (2/2)",   // 2
            "MODULE 2 — WIRES (1/3)",   // 3
            "MODULE 2 — WIRES (2/3)",   // 4
            "MODULE 2 — WIRES (3/3)",   // 5
            "MODULE 3 — BUTTON (1/2)",  // 6
            "MODULE 3 — BUTTON (2/2)",  // 7
            "MODULE 4 — KEYPAD (1/2)",  // 8
            "MODULE 4 — KEYPAD (2/2)",  // 9
            "MODULE 5 — SIMON (1/3)",   // 10
            "MODULE 5 — SIMON (2/3)",   // 11
            "MODULE 5 — SIMON (3/3)",   // 12
        };

        private static readonly string[] PageBodies =
        {
            // ── 0  COVER ──────────────────────────────────────────────────────
            "BOMB DEFUSAL SQUAD\n" +
            "Revision 7.3 — Authorized Operational Use Only\n\n" +
            "Improper interpretation of procedural\n" +
            "documentation may result in device detonation,\n" +
            "disciplinary review, or both.\n\n" +
            "ROLES:\n" +
            "  DEFUSER — operates the bomb.\n" +
            "  EXPERT  — reads this manual.\n\n" +
            "STRIKES: Each incorrect action issues one strike.\n" +
            "Maximum strikes before detonation varies\n" +
            "by difficulty level.\n\n" +
            "Solve all active modules to neutralise\n" +
            "the device.\n\n" +
            "Operators are advised to communicate\n" +
            "in precise, unambiguous terms at all times.",

            // ── 1  TIMER 1/2 ──────────────────────────────────────────────────
            "1.0 GENERAL DESCRIPTION\n\n" +
            "The Timer displays a continuously decrementing\n" +
            "value in format MM:SS.\n\n" +
            "  MM = remaining minutes\n" +
            "  SS = remaining seconds\n\n" +
            "1.1 LAST DIGIT\n\n" +
            "The LAST DIGIT is defined as the rightmost\n" +
            "numeric character of the display — the units\n" +
            "position of seconds.\n\n" +
            "  Formally:  Last Digit = SS mod 10\n\n" +
            "  Timer 03:47  →  last digit = 7\n" +
            "  Timer 02:00  →  last digit = 0\n\n" +
            "1.2 CONTAINS DIGIT\n\n" +
            "A timer CONTAINS a digit if that numeral\n" +
            "appears in ANY position of the display.\n\n" +
            "  Timer 05:41  contains: 0, 5, 4, 1\n" +
            "  Timer 02:08  contains: 0, 2, 8",

            // ── 2  TIMER 2/2 ──────────────────────────────────────────────────
            "1.3 DIGIT POSITION TABLE\n\n" +
            "  Reading  Last Digit  Contains\n" +
            "  05:41       1        0,1,4,5\n" +
            "  02:08       8        0,2,8\n" +
            "  00:37       7        0,3,7\n" +
            "  11:20       0        0,1,2\n" +
            "  09:14       4        0,1,4,9\n\n" +
            "1.4 CAUTION\n\n" +
            "!! WARNING !!\n\n" +
            "Do NOT confuse the seconds VALUE with\n" +
            "the LAST DIGIT.\n\n" +
            "  Timer: 02:14\n" +
            "  Seconds value  = 14\n" +
            "  Last digit     =  4  ← use this\n\n" +
            "Rules that reference 'last digit' refer to\n" +
            "the single digit 4, not the value 14.\n\n" +
            "Misinterpretation of this distinction is\n" +
            "the leading cause of preventable detonation.",

            // ── 3  WIRES 1/3 ──────────────────────────────────────────────────
            "2.0 WIRE INDEX TABLE\n\n" +
            "  Index  Colour\n" +
            "    0    Red\n" +
            "    1    Blue\n" +
            "    2    Yellow\n" +
            "    3    White\n" +
            "    4    Black\n" +
            "    5    Red\n\n" +
            "2.1 DEFINITIONS\n\n" +
            "LAST WIRE — the wire with the highest index.\n" +
            "  Standard layout: last wire = Index 5.\n\n" +
            "CUT — deliberate severing of the conductive\n" +
            "wire body. Partial compression or bending\n" +
            "does NOT constitute a valid cut.\n\n" +
            "2.2 PROCEDURAL RULE ORDER\n\n" +
            "Evaluate rules in strict sequential order.\n" +
            "Apply only the FIRST rule whose conditions\n" +
            "are satisfied. Do not evaluate further rules.",

            // ── 4  WIRES 2/3 ──────────────────────────────────────────────────
            "RULE A\n\n" +
            "If:\n" +
            "  (1) The last digit of the timer is ODD\n" +
            "  AND\n" +
            "  (2) There are two or more RED wires\n\n" +
            "Then:\n" +
            "  → Cut the LAST red wire.\n" +
            "     (Standard layout: index 5)\n\n" +
            "─────────────────────────────────────────\n\n" +
            "RULE B\n\n" +
            "If Rule A does not apply:\n\n" +
            "If:\n" +
            "  (1) The last wire is BLACK\n" +
            "  AND\n" +
            "  (2) The last digit is EVEN\n\n" +
            "Then:\n" +
            "  → Cut wire index 5.\n\n" +
            "─────────────────────────────────────────\n\n" +
            "RULE C\n\n" +
            "If neither Rule A nor Rule B apply:\n\n" +
            "If there is EXACTLY ONE blue wire:\n" +
            "  → Cut wire index 1.\n\n" +
            "RULE D\n\n" +
            "If none of the above conditions are TRUE:\n" +
            "  → Cut wire index 2.",

            // ── 5  WIRES 3/3 ──────────────────────────────────────────────────
            "2.5 ILLUMINATION NOTE\n\n" +
            "Under certain lighting environments —\n" +
            "particularly fluorescent or sodium-vapor\n" +
            "sources — the apparent colour of polymer\n" +
            "insulation may shift perceptually toward\n" +
            "adjacent wavelengths.\n\n" +
            "Examples:\n" +
            "  Red insulation may appear dark orange.\n" +
            "  Black insulation may appear deep blue.\n\n" +
            "!! THIS DOES NOT ALTER THE WIRE COLOUR !!\n\n" +
            "The manufacturing specification determines\n" +
            "colour identity irrespective of ambient\n" +
            "lighting conditions.\n\n" +
            "Operators are instructed not to reclassify\n" +
            "wires based on perceived hue.\n\n" +
            "Reclassification under these circumstances\n" +
            "is an operator error.",

            // ── 6  BUTTON 1/2 ─────────────────────────────────────────────────
            "3.0 OVERVIEW\n\n" +
            "The module presents a single coloured button.\n" +
            "Possible colours: Red, Blue, Yellow, White.\n\n" +
            "The operator must determine whether to\n" +
            "execute a TAP or a HOLD.\n\n" +
            "3.1 HOLD vs TAP DETERMINATION\n\n" +
            "VALID HOLD — button depressed and maintained\n" +
            "continuously for MORE than 0.5 seconds.\n\n" +
            "VALID TAP — button pressed and released\n" +
            "in UNDER 0.5 seconds.\n\n" +
            "3.2 PRIMARY CONDITIONAL PROCEDURE\n\n" +
            "BLUE button:\n" +
            "  (a) Initiate a HOLD.\n" +
            "  (b) Continue holding until ANY digit\n" +
            "      of the timer equals 4.\n" +
            "  (c) Release immediately.\n\n" +
            "RED button:\n" +
            "  (a) Initiate a HOLD.\n" +
            "  (b) Continue holding until ANY digit\n" +
            "      of the timer equals 1.\n" +
            "  (c) Release immediately.",

            // ── 7  BUTTON 2/2 ─────────────────────────────────────────────────
            "3.2 CONTINUED\n\n" +
            "YELLOW or WHITE button:\n" +
            "  → Execute a TAP.\n\n" +
            "3.3 DIGIT MATCH CRITERION\n\n" +
            "The phrase 'any digit of the timer equals X'\n" +
            "indicates the numeral may appear in ANY of\n" +
            "the four displayed positions.\n\n" +
            "Example:\n" +
            "  Timer 03:41  →  digits: 0, 3, 4, 1\n" +
            "  Release conditions for 4 AND 1 are both\n" +
            "  satisfied simultaneously.\n\n" +
            "3.4 LED STRIP NOTE\n\n" +
            "The LED colour confirms the button colour.\n" +
            "It provides no additional operational data\n" +
            "and should not be used as the primary\n" +
            "colour identification source.",

            // ── 8  KEYPAD 1/2 ─────────────────────────────────────────────────
            // Symbol table now uses the actual Unicode characters from KeypadSymbols.
            "4.0 OVERVIEW\n\n" +
            "Four keys, each labelled with a unique symbol.\n" +
            "Identify symbols by SHAPE. Find the column\n" +
            "containing all four of your symbols, then\n" +
            "press keys in that column's top-to-bottom order.\n\n" +
            "4.1 SYMBOL TABLE — COLUMNS A, B, C\n\n" +
            "  Row  Col A  Col B  Col C\n" +
            "   1    ☆      Ж      ◈\n" +
            "   2    ψ      Ħ      ♛\n" +
            "   3    Θ      ⊕      ∂\n" +
            "   4    Ω      ↩      ⊙\n\n" +
            "4.2 SYMBOL TABLE — COLUMNS D, E, F\n\n" +
            "  Row  Col D  Col E  Col F\n" +
            "   1    Ʌ      Þ      Ξ\n" +
            "   2    Ŋ      Ð      Φ\n" +
            "   3    Ɋ      ꝏ      ☊\n" +
            "   4    Ʃ      Δ      ℵ",

            // ── 9  KEYPAD 2/2 ─────────────────────────────────────────────────
            "4.3 ORDER DETERMINATION PROCEDURE\n\n" +
            "Step 1:\n" +
            "  Identify the four symbols on the keypad.\n\n" +
            "Step 2:\n" +
            "  Scan each column until one column contains\n" +
            "  ALL four symbols present.\n\n" +
            "Step 3:\n" +
            "  Press keys in the order the symbols appear\n" +
            "  TOP to BOTTOM within that column.\n\n" +
            "The spatial arrangement of the physical\n" +
            "keypad is irrelevant to the correct order.\n\n" +
            "4.4 INCORRECT INPUT\n\n" +
            "If a symbol is pressed out of sequence:\n\n" +
            "  (1) The module issues a STRIKE.\n" +
            "  (2) The entered sequence RESETS.\n" +
            "  (3) Operator must begin again from the\n" +
            "      first symbol in the column.\n\n" +
            "The correct column does NOT change after\n" +
            "an error.",

            // ── 10  SIMON 1/3 ─────────────────────────────────────────────────
            "5.0 CHROMATIC RESPONSE SEQUENCING\n\n" +
            "The module consists of four coloured pads:\n" +
            "  Red, Blue, Green, Yellow.\n\n" +
            "The device emits a sequence of flashing\n" +
            "colours which must be interpreted via\n" +
            "chromatic response mapping.\n\n" +
            "In operational terms: translate the flashed\n" +
            "colour into a response colour, then press\n" +
            "the corresponding pad.\n\n" +
            "5.1 SEQUENCE EXPANSION\n\n" +
            "The module displays:\n" +
            "  Round 1: one colour\n" +
            "  Round 2: two colours\n" +
            "  Round 3: three colours\n" +
            "  (and so on)\n\n" +
            "Each round extends the sequence by one.\n\n" +
            "!! WARNING !!\n" +
            "Failure to reproduce the sequence correctly\n" +
            "RESETS only the current round's input.\n" +
            "The sequence itself does not change.",

            // ── 11  SIMON 2/3 ─────────────────────────────────────────────────
            "5.2 STRIKE-DEPENDENT COLOUR CIPHER\n\n" +
            "The response colour depends on the current\n" +
            "strike count. Consult the cipher table below.\n\n" +
            "0 STRIKES\n" +
            "  Flash     Press\n" +
            "  Red    →  Red\n" +
            "  Blue   →  Blue\n" +
            "  Green  →  Green\n" +
            "  Yellow →  Yellow\n\n" +
            "1 STRIKE\n" +
            "  Flash     Press\n" +
            "  Red    →  Blue\n" +
            "  Blue   →  Yellow\n" +
            "  Green  →  Green\n" +
            "  Yellow →  Red",

            // ── 12  SIMON 3/3 ─────────────────────────────────────────────────
            "5.2 CONTINUED\n\n" +
            "2 STRIKES\n" +
            "  Flash     Press\n" +
            "  Red    →  Yellow\n" +
            "  Blue   →  Green\n" +
            "  Green  →  Red\n" +
            "  Yellow →  Blue\n\n" +
            "5.3 RESPONSE PROTOCOL\n\n" +
            "For each colour in the flashed sequence:\n\n" +
            "  (1) Identify the flashed colour.\n" +
            "  (2) Determine current strike count.\n" +
            "  (3) Translate using the cipher table.\n" +
            "  (4) Press the corresponding pad.\n\n" +
            "Repeat for every element in the sequence.\n\n" +
            "─────────────────────────────────────────\n" +
            "END OF MODULE DOCUMENTATION\n\n" +
            "Failure to adhere strictly to procedural\n" +
            "language may result in device detonation\n" +
            "or administrative reprimand.",
        };

        // ── Runtime ──────────────────────────────────────────────────────────
        private int              currentPage = 0;
        private TextMeshProUGUI  titleText;
        private TextMeshProUGUI  bodyText;
        private TextMeshProUGUI  pageIndicator;
        private GameObject       dividerGO;

        // PDF mode
        private bool         _pdfMode      = false;
        private Texture2D[]  _pageTextures;
        private RawImage     _pdfImage;

        // ================================================================
        // Unity lifecycle
        // ================================================================

        private void Awake()
        {
            // Attempt to load PDF page textures (placed in Assets/Resources/ManualPages/).
            // We load sequentially by name so order is always correct.
            var texList = new List<Texture2D>();
            for (int i = 0; i < 200; i++)
            {
                var t = Resources.Load<Texture2D>($"ManualPages/page_{i:D3}");
                if (t == null) break;
                texList.Add(t);
            }
            if (texList.Count > 0)
            {
                _pageTextures = texList.ToArray();
                _pdfMode      = true;
            }

            BuildTabletBody();
            BuildTabletCanvas();
            ShowPage(0);
        }

        // ================================================================
        // Build physical tablet  (1.5× larger than original design)
        // ================================================================

        private void BuildTabletBody()
        {
            // Main slab — 40.5 × 55.5 cm  (was 27 × 37 cm, × 1.5)
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "TabletBody";
            body.transform.SetParent(transform, false);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale    = new Vector3(0.405f, 0.555f, 0.021f);
            ApplyColour(body, new Color(0.12f, 0.12f, 0.15f));
            Destroy(body.GetComponent<Collider>());

            // Screen area
            var screen = GameObject.CreatePrimitive(PrimitiveType.Cube);
            screen.name = "TabletScreen";
            screen.transform.SetParent(transform, false);
            screen.transform.localPosition = new Vector3(0f, 0f, -0.009f);
            screen.transform.localScale    = new Vector3(0.378f, 0.528f, 0.0015f);
            ApplyColour(screen, new Color(0.04f, 0.04f, 0.07f));
            Destroy(screen.GetComponent<Collider>());

            var bc  = gameObject.AddComponent<BoxCollider>();
            bc.size = new Vector3(0.405f, 0.555f, 0.045f);

            var grab = gameObject.AddComponent<XRGrabInteractable>();
            var rb   = grab.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.useGravity    = false;
                rb.isKinematic   = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.mass          = 0.3f;
            }
        }

        // ================================================================
        // Build canvas with page text + PDF image area
        // ================================================================

        private void BuildTabletCanvas()
        {
            var canvasGO = new GameObject("TabletCanvas");
            canvasGO.transform.SetParent(transform, false);
            // 1.5× z offset (body front face now at ~-0.0105, push clear)
            canvasGO.transform.localPosition = new Vector3(0f, 0f, -0.0225f);
            canvasGO.transform.localRotation = Quaternion.identity;

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.WorldSpace;
            canvas.sortingOrder = 5;

            // Canvas size unchanged in canvas units (380 × 560), but scale ×1.5
            // → world size ≈ 37 cm × 54 cm
            var rt = canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(380f, 560f);
            canvasGO.transform.localScale = Vector3.one * 0.0009750f; // was 0.00065 × 1.5

            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<TrackedDeviceGraphicRaycaster>();

            // Dark background
            AddImage(canvasGO.transform, "BG", new Color(0.03f, 0.03f, 0.07f, 0.97f), true);

            // ── Title (text mode only) ────────────────────────────────────────
            titleText = AddTMP(canvasGO.transform, "Title",
                "", 26, FontStyles.Bold,
                new Color(1f, 0.85f, 0.2f), TextAlignmentOptions.Center,
                new Vector2(0f, 248f), new Vector2(360f, 40f));

            // Thin divider (text mode only)
            dividerGO = new GameObject("Divider");
            dividerGO.transform.SetParent(canvasGO.transform, false);
            var divImg = dividerGO.AddComponent<Image>();
            divImg.color = new Color(0.35f, 0.35f, 0.35f);
            var divRT  = dividerGO.GetComponent<RectTransform>();
            divRT.anchoredPosition = new Vector2(0f, 224f);
            divRT.sizeDelta        = new Vector2(360f, 1.5f);

            // ── Body text (text mode only) ────────────────────────────────────
            bodyText = AddTMP(canvasGO.transform, "Body",
                "", 16f, FontStyles.Normal,
                new Color(0.90f, 0.90f, 0.90f), TextAlignmentOptions.TopLeft,
                new Vector2(4f, 14f), new Vector2(358f, 430f));
            bodyText.enableWordWrapping = true;
            bodyText.enableAutoSizing   = true;
            bodyText.fontSizeMin        = 9f;
            bodyText.fontSizeMax        = 16f;
            bodyText.overflowMode       = TextOverflowModes.Truncate;

            // ── PDF image area (PDF mode only, starts hidden) ─────────────────
            var imgGO = new GameObject("PDFPage");
            imgGO.transform.SetParent(canvasGO.transform, false);
            _pdfImage = imgGO.AddComponent<RawImage>();
            _pdfImage.color = Color.white;
            var imgRT = imgGO.GetComponent<RectTransform>();
            // Fill almost the entire canvas leaving the bottom nav bar.
            // y=10, h=500 → top=260, bottom=-240 (nav buttons are at y=-252)
            imgRT.anchoredPosition = new Vector2(0f, 10f);
            imgRT.sizeDelta        = new Vector2(370f, 500f);
            // Preserve PDF page aspect ratio (fit inside rect, no stretch)
            var arf = imgGO.AddComponent<AspectRatioFitter>();
            arf.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            arf.aspectRatio = 0.707f; // A4 portrait default; overridden per-page below

            imgGO.SetActive(false); // hidden until PDF mode enabled

            // In PDF mode: hide title + divider + bodyText; show pdfImage.
            if (_pdfMode)
            {
                titleText.gameObject.SetActive(false);
                dividerGO.SetActive(false);
                bodyText.gameObject.SetActive(false);
                imgGO.SetActive(true);
            }

            // ── Page indicator ────────────────────────────────────────────────
            pageIndicator = AddTMP(canvasGO.transform, "PageNum",
                "", 14f, FontStyles.Normal,
                new Color(0.45f, 0.45f, 0.45f), TextAlignmentOptions.Center,
                new Vector2(0f, -252f), new Vector2(180f, 26f));

            // ── Prev / Next buttons ───────────────────────────────────────────
            AddNavButton(canvasGO.transform, "BtnPrev", "◄ PREV",
                new Vector2(-120f, -252f), new Vector2(100f, 30f), OnPrevPage);
            AddNavButton(canvasGO.transform, "BtnNext", "NEXT ►",
                new Vector2(120f, -252f), new Vector2(100f, 30f), OnNextPage);
        }

        // ================================================================
        // Page navigation
        // ================================================================

        private void ShowPage(int index)
        {
            int pageCount = _pdfMode ? _pageTextures.Length : PageTitles.Length;
            currentPage = Mathf.Clamp(index, 0, pageCount - 1);

            if (_pdfMode)
            {
                if (_pdfImage != null)
                {
                    _pdfImage.texture = _pageTextures[currentPage];
                    // Update aspect ratio fitter to match actual page dimensions
                    var arf = _pdfImage.GetComponent<AspectRatioFitter>();
                    if (arf != null && _pageTextures[currentPage] != null)
                    {
                        var tex = _pageTextures[currentPage];
                        if (tex.height > 0)
                            arf.aspectRatio = (float)tex.width / tex.height;
                    }
                }
            }
            else
            {
                if (titleText     != null) titleText.text = PageTitles[currentPage];
                if (bodyText      != null) bodyText.text  = PageBodies[currentPage];
            }

            if (pageIndicator != null)
                pageIndicator.text = $"{currentPage + 1} / {pageCount}";
        }

        private void OnPrevPage()
        {
            KTANESoundManager.Instance?.PlayPageTurn();
            ShowPage(currentPage - 1);
        }

        private void OnNextPage()
        {
            KTANESoundManager.Instance?.PlayPageTurn();
            ShowPage(currentPage + 1);
        }

        // ================================================================
        // UI factory helpers
        // ================================================================

        private static TextMeshProUGUI AddTMP(
            Transform parent, string name, string text,
            float size, FontStyles style, Color colour,
            TextAlignmentOptions align,
            Vector2 pos, Vector2 sizeDelta)
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

        private static Image AddImage(Transform parent, string name, Color colour,
            bool stretch,
            Vector2 pos = default, Vector2 sizeDelta = default)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = colour;
            var rt  = go.GetComponent<RectTransform>();
            if (stretch)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
            }
            else
            {
                rt.anchoredPosition = pos;
                rt.sizeDelta        = sizeDelta;
            }
            return img;
        }

        private static void AddNavButton(Transform parent, string name, string label,
            Vector2 pos, Vector2 size, UnityAction onClick)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.15f, 0.22f, 0.48f);
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);
            var rt  = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta        = size;

            var lblGO = new GameObject("Label");
            lblGO.transform.SetParent(go.transform, false);
            var tmp       = lblGO.AddComponent<TextMeshProUGUI>();
            tmp.text      = label;
            tmp.fontSize  = 12f;
            tmp.color     = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            var lrt = lblGO.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        }

        private static void ApplyColour(GameObject go, Color colour)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return;
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colour);
            else                               mat.SetColor("_Color",     colour);
            r.sharedMaterial = mat;
        }
    }
}
