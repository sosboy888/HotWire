// GenerateBombPrefab.cs
// Menu: Tools → KTANE → Create KTANE Bomb Prefab
//
// Creates Assets/Prefabs/KTANEBomb.prefab – the fully-interactive bomb model
// expected by BuildKTANEScene.cs (path constant KTANEBombPrefabPath).
//
// Front face layout (player at z=-1.2 facing +z, so front face = local −Z):
//
//   LEFT   : Wire_0 … Wire_5   – coloured horizontal bars, XRGrabInteractable
//   TOP-CTR: Timer_Display     – emissive panel (text auto-created by TimerModule)
//   BTM-CTR: Simon_Red/Blue/Green/Yellow – 2×2 pads, XRSimpleInteractable
//   RGT-TOP: Keypad_Key_0 … 3 – 2×2 grid,   XRSimpleInteractable
//   RGT-BOT: Button_Main       – square button, XRSimpleInteractable
//            Button_LED         – small emissive indicator, Renderer only
//
// After running this, also run  Tools ▶ KTANE ▶ Build KTANEGame Scene
// to place the bomb and wire all module scripts.

using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class GenerateBombPrefab
{
    private const string PrefabPath = "Assets/Prefabs/KTANEBomb.prefab";
    private const string MatFolder  = "Assets/Materials/Bomb";

    [MenuItem("Tools/KTANE/Create KTANE Bomb Prefab")]
    public static void CreateKTANEBombPrefab()
    {
        EnsureFolders();

        // ── Materials ─────────────────────────────────────────────────────────
        var bodyMat   = GetMat("BombBody.mat",       Rgb(0.15f, 0.15f, 0.15f));
        var panelMat  = GetMat("BombPanel.mat",      Rgb(0.22f, 0.22f, 0.22f));
        var timerMat  = GetEmMat("TimerDisp.mat",    Rgb(0.04f, 0.04f, 0.04f), Color.black);
        var buttonMat = GetMat("ButtonFace.mat",     Rgb(0.80f, 0.12f, 0.12f));
        var ledMat    = GetEmMat("ButtonLED.mat",    Rgb(0.04f, 0.04f, 0.04f), Color.black);
        var keyMat    = GetMat("KeyFace.mat",        Rgb(0.28f, 0.28f, 0.32f));

        Material[] wireMats =
        {
            GetMat("Wire0_Red.mat",    Rgb(0.80f, 0.10f, 0.10f)),
            GetMat("Wire1_Blue.mat",   Rgb(0.10f, 0.20f, 0.80f)),
            GetMat("Wire2_Yellow.mat", Rgb(0.85f, 0.80f, 0.10f)),
            GetMat("Wire3_White.mat",  Rgb(0.80f, 0.80f, 0.80f)),
            GetMat("Wire4_Black.mat",  Rgb(0.10f, 0.10f, 0.10f)),
            GetMat("Wire5_Red.mat",    Rgb(0.80f, 0.10f, 0.10f)),
        };

        // Simon pad materials: dark base colour, nearly-black emission (lit up at runtime)
        Color dimEm = Rgb(0.05f, 0.05f, 0.05f);
        Material[] simonMats =
        {
            GetEmMat("Simon_Red.mat",    Rgb(0.55f, 0.06f, 0.06f), dimEm),
            GetEmMat("Simon_Blue.mat",   Rgb(0.06f, 0.12f, 0.55f), dimEm),
            GetEmMat("Simon_Green.mat",  Rgb(0.06f, 0.55f, 0.06f), dimEm),
            GetEmMat("Simon_Yellow.mat", Rgb(0.55f, 0.55f, 0.06f), dimEm),
        };

        AssetDatabase.SaveAssets(); // flush all new material assets before creating prefab

        // ── Hierarchy ─────────────────────────────────────────────────────────
        var root = new GameObject("BombRoot");

        // Bomb body: 36 cm wide × 14 cm tall × 24 cm deep
        Cube(root.transform, "Body",       Pt(0, 0, 0),       Sc(0.36f, 0.14f, 0.24f), bodyMat);
        Cube(root.transform, "FrontPanel", Pt(0, 0, -0.121f), Sc(0.34f, 0.12f, 0.004f), panelMat);

        // ── Timer_Display ─────────────────────────────────────────────────────
        // TimerModule.AutoWireModuleSlots wires this Renderer into timerModule.timerDisplay.
        // The MM:SS text is auto-created as a child TextMeshPro by TimerModule.Start().
        Cube(root.transform, "Timer_Display",
             Pt(0f, 0.048f, -0.124f), Sc(0.10f, 0.020f, 0.005f), timerMat);

        // ── Wire_0 … Wire_5 ───────────────────────────────────────────────────
        // Horizontal coloured bars in a vertical column on the left third of the face.
        // WiresModule measures grab-to-current-position distance; >8 cm → cut.
        float[] wy = { 0.042f, 0.024f, 0.006f, -0.012f, -0.030f, -0.048f };
        for (int i = 0; i < 6; i++)
        {
            var wire = Cube(root.transform, $"Wire_{i}",
                            Pt(-0.115f, wy[i], -0.124f),
                            Sc(0.090f, 0.009f, 0.009f), wireMats[i]);
            // Kinematic Rigidbody prevents gravity from pulling the wires off the bomb.
            // XRGrabInteractable will use this existing Rigidbody instead of adding its own.
            var rb = wire.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity  = false;
            wire.AddComponent<XRGrabInteractable>();
            // The wire mesh is only 9 mm tall/deep — far too small to grab in VR.
            // Expand the BoxCollider to ~4 cm in Y and Z so it is reliably grabbable.
            // bc.size is in local space; divide target world size by the wire's local scale.
            var bc = wire.GetComponent<BoxCollider>();
            bc.size = new Vector3(1f, 5f, 5f); // world: 9 cm × 4.5 cm × 4.5 cm
        }

        // ── Button_Main + Button_LED ──────────────────────────────────────────
        var btn = Cube(root.transform, "Button_Main",
                       Pt(0.115f, -0.038f, -0.124f),
                       Sc(0.046f, 0.046f, 0.018f), buttonMat);
        btn.AddComponent<XRSimpleInteractable>();

        // LED indicator (small glowing square, Renderer only – no interaction)
        Cube(root.transform, "Button_LED",
             Pt(0.160f, 0.042f, -0.124f),
             Sc(0.012f, 0.016f, 0.006f), ledMat);

        // ── Keypad_Key_0 … Keypad_Key_3 ──────────────────────────────────────
        // 2×2 grid in the upper-right of the face.
        Vector3[] kp =
        {
            Pt(0.092f, 0.042f, -0.124f),   // Key_0  top-left
            Pt(0.138f, 0.042f, -0.124f),   // Key_1  top-right
            Pt(0.092f, 0.007f, -0.124f),   // Key_2  bottom-left
            Pt(0.138f, 0.007f, -0.124f),   // Key_3  bottom-right
        };
        for (int i = 0; i < 4; i++)
        {
            var key = Cube(root.transform, $"Keypad_Key_{i}",
                           kp[i], Sc(0.038f, 0.028f, 0.014f), keyMat);
            key.AddComponent<XRSimpleInteractable>();
        }

        // ── Simon pads ────────────────────────────────────────────────────────
        // 2×2 grid in the centre-bottom of the face.
        // Layout:   [Green ][Yellow]
        //           [Red   ][Blue  ]
        string[]   sn = { "Simon_Red", "Simon_Blue", "Simon_Green", "Simon_Yellow" };
        Vector3[]  sp =
        {
            Pt(-0.030f, -0.040f, -0.124f),  // Red    (index 0)
            Pt( 0.020f, -0.040f, -0.124f),  // Blue   (index 1)
            Pt(-0.030f, -0.005f, -0.124f),  // Green  (index 2)
            Pt( 0.020f, -0.005f, -0.124f),  // Yellow (index 3)
        };
        for (int i = 0; i < 4; i++)
        {
            var pad = Cube(root.transform, sn[i],
                           sp[i], Sc(0.040f, 0.030f, 0.010f), simonMats[i]);
            pad.AddComponent<XRSimpleInteractable>();
        }

        // ── Solved indicator LEDs ─────────────────────────────────────────────
        // Small lights in the top strip – start black, glow green when the
        // matching module is solved (driven by each module's SetSolvedLight).
        var solvedMat = GetEmMat("Solved.mat", Rgb(0.02f, 0.08f, 0.02f), Color.black);
        Cube(root.transform, "Wire_Solved",   Pt(-0.140f, 0.064f, -0.124f), Sc(0.018f, 0.010f, 0.005f), solvedMat);
        Cube(root.transform, "Simon_Solved",  Pt(-0.058f, 0.064f, -0.124f), Sc(0.018f, 0.010f, 0.005f), solvedMat);
        Cube(root.transform, "Keypad_Solved", Pt( 0.058f, 0.064f, -0.124f), Sc(0.018f, 0.010f, 0.005f), solvedMat);
        Cube(root.transform, "Button_Solved", Pt( 0.145f, 0.064f, -0.124f), Sc(0.018f, 0.010f, 0.005f), solvedMat);

        // ── Save prefab ───────────────────────────────────────────────────────
        AssetDatabase.SaveAssets();
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        if (prefab != null)
        {
            Debug.Log($"[GenerateBombPrefab] KTANEBomb saved → {PrefabPath}\n" +
                      "Run  Tools ▶ KTANE ▶ Build KTANEGame Scene  to wire everything up.");
            EditorUtility.DisplayDialog(
                "KTANEBomb Prefab Created",
                $"Prefab saved to:\n{PrefabPath}\n\n" +
                "Next step:\nTools ▶ KTANE ▶ Build KTANEGame Scene\n" +
                "(this places the bomb and auto-wires all module scripts)",
                "OK");
        }
        else
        {
            Debug.LogError("[GenerateBombPrefab] Failed to save prefab to " + PrefabPath);
        }

        AssetDatabase.Refresh();
    }

    // ── Factory helpers ───────────────────────────────────────────────────────

    /// <summary>Create a unit cube with the given parent, name, position, scale and material.</summary>
    private static GameObject Cube(Transform parent, string name,
                                   Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale    = scale;
        if (mat != null)
            go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    private static Vector3 Pt(float x, float y, float z) => new Vector3(x, y, z);
    private static Vector3 Sc(float x, float y, float z) => new Vector3(x, y, z);
    private static Color   Rgb(float r, float g, float b) => new Color(r, g, b);

    // ── Material helpers ──────────────────────────────────────────────────────

    private static Material GetMat(string file, Color albedo)
    {
        string path = $"{MatFolder}/{file}";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        mat = new Material(shader);
        SetAlbedo(mat, albedo);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static Material GetEmMat(string file, Color albedo, Color emission)
    {
        string path = $"{MatFolder}/{file}";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        mat = new Material(shader);
        SetAlbedo(mat, albedo);
        mat.EnableKeyword("_EMISSION");
        if (mat.HasProperty("_EmissionColor"))
            mat.SetColor("_EmissionColor", emission);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static void SetAlbedo(Material mat, Color c)
    {
        if      (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        else if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     c);
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(MatFolder))
            AssetDatabase.CreateFolder("Assets/Materials", "Bomb");
    }
}
