// CreateKTANEBombModel.cs
// Procedurally builds the full KTANE bomb model from Unity primitives.
//
// HOW TO USE:
//   Tools ▶ KTANE ▶ Create KTANE Bomb Model
//
// WHAT IT CREATES:
//   A prefab saved to Assets/Prefabs/KTANEBomb.prefab.
//   The bomb lies flat on the table (top-facing).  All interactive children
//   (Wire_0-5, Button_Main, Button_LED, Timer_Display, Keypad_Key_0-3,
//   Simon_Red/Blue/Green/Yellow) are correctly named and precisely aligned.
//
// LAYOUT (bird's-eye view of the top face):
//
//   ╔══════════════════════════════════════╗   ← z = -0.200  (Defuser side)
//   ║         TIMER  DISPLAY              ║
//   ╠══════════════════╦═══════════════════╣   divider z = -0.070
//   ║   WIRES MODULE   ║   BUTTON MODULE  ║
//   ║  W0 W1 W2 W3 W4 W5  [BTN]  [LED]   ║
//   ╠══════════════════╬═══════════════════╣   divider z = +0.085
//   ║   KEYPAD MODULE  ║   SIMON MODULE   ║
//   ║ [K0][K1][K2][K3] ║ [R][B] [G][Y]   ║
//   ╚══════════════════╩═══════════════════╝   ← z = +0.200  (Expert side)
//  x=-0.300          x=0            x=+0.300

using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class CreateKTANEBombModel
{
    // ── prefab / material output folders ──────────────────────────────────
    private const string PrefabPath  = "Assets/Prefabs/KTANEBomb.prefab";
    private const string MatFolder   = "Assets/Materials/KTANEBomb";

    // ── bomb body dimensions ───────────────────────────────────────────────
    // Whole bomb case (cube):
    private const float CW = 0.60f;   // case width  (X)
    private const float CH = 0.12f;   // case height (Y)
    private const float CD = 0.40f;   // case depth  (Z)

    // Top surface Y in local space (above centre of the case):
    private const float TopY = CH / 2f;          // = 0.060
    private const float PanelY = TopY + 0.004f;  // top panel surface = 0.064
    private const float ModY   = PanelY + 0.005f;// module sitting height = 0.069

    // ── wire layout constants ──────────────────────────────────────────────
    private const int   WireCount  = 6;
    private const float WirePegTopZ = -0.030f;   // front peg row (toward defuser)
    private const float WirePegBotZ = +0.070f;   // back  peg row
    private const float WireLen     = WirePegBotZ - WirePegTopZ; // 0.100 m
    private const float WireCentreZ = (WirePegTopZ + WirePegBotZ) / 2f; // 0.020

    // X positions for Wire_0 … Wire_5 across the wires module area:
    private static readonly float[] WireX =
        { -0.255f, -0.207f, -0.159f, -0.111f, -0.063f, -0.015f };

    // Wire colours (matching WiresModule.cs colour order: R B Y W Blk R)
    private static readonly Color[] WireColours = new Color[]
    {
        new Color(0.85f, 0.10f, 0.10f),  // Wire_0  Red
        new Color(0.10f, 0.20f, 0.85f),  // Wire_1  Blue
        new Color(0.90f, 0.85f, 0.10f),  // Wire_2  Yellow
        new Color(0.88f, 0.88f, 0.88f),  // Wire_3  White
        new Color(0.06f, 0.06f, 0.06f),  // Wire_4  Black
        new Color(0.85f, 0.10f, 0.10f),  // Wire_5  Red (second)
    };

    // ── entry point ────────────────────────────────────────────────────────
    [MenuItem("Tools/KTANE/Create KTANE Bomb Model")]
    public static void CreateModel()
    {
        EnsureFolders();

        // Root empty (this gets module scripts attached later)
        var root = new GameObject("KTANEBomb");

        // ── structural shell ──────────────────────────────────────────────
        BuildCase(root.transform);

        // ── modules ───────────────────────────────────────────────────────
        BuildTimerSection(root.transform);
        BuildWiresSection(root.transform);
        BuildButtonSection(root.transform);
        BuildKeypadSection(root.transform);
        BuildSimonSection(root.transform);

        // ── cosmetic ──────────────────────────────────────────────────────
        BuildCornerLights(root.transform);
        BuildSidePanels(root.transform);

        // ── save as prefab ────────────────────────────────────────────────
        EnsureFolders();
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        if (prefab != null)
        {
            AssetDatabase.Refresh();
            Selection.activeObject = prefab;
            Debug.Log($"[CreateKTANEBombModel] Bomb prefab saved to {PrefabPath}");
            EditorUtility.DisplayDialog(
                "Bomb Model Created",
                $"Prefab saved to:\n{PrefabPath}\n\n" +
                "Place it under BombMount in the KTANEGame scene.\n" +
                "Wire children are already named correctly — drag them into each\n" +
                "module script's Inspector slots.",
                "OK");
        }
        else
        {
            Debug.LogError("[CreateKTANEBombModel] Failed to save prefab.");
        }
    }

    // ======================================================================
    // Case & top panel
    // ======================================================================

    private static void BuildCase(Transform root)
    {
        // Main body
        var body = MakeCube(root, "BombCase",
            pos:   Vector3.zero,
            scale: new Vector3(CW, CH, CD),
            col:   new Color(0.11f, 0.11f, 0.11f)); // dark charcoal

        // Top panel (slightly lighter; sits on the very top surface)
        MakeCube(root, "BombTopPanel",
            pos:   new Vector3(0f, TopY + 0.0035f, 0f),
            scale: new Vector3(CW - 0.025f, 0.007f, CD - 0.015f),
            col:   new Color(0.17f, 0.17f, 0.17f));

        // Module dividers (decorative raised strips)
        // Horizontal — between timer row and wires/button row
        MakeCube(root, "Divider_H1",
            pos:   new Vector3(0f, ModY, -0.070f),
            scale: new Vector3(CW - 0.025f, 0.008f, 0.005f),
            col:   new Color(0.07f, 0.07f, 0.07f));

        // Horizontal — between wires/button row and keypad/simon row
        MakeCube(root, "Divider_H2",
            pos:   new Vector3(0f, ModY, 0.085f),
            scale: new Vector3(CW - 0.025f, 0.008f, 0.005f),
            col:   new Color(0.07f, 0.07f, 0.07f));

        // Vertical — centre split (left modules vs right modules)
        MakeCube(root, "Divider_V",
            pos:   new Vector3(0.010f, ModY, 0.030f),
            scale: new Vector3(0.005f, 0.008f, 0.245f),
            col:   new Color(0.07f, 0.07f, 0.07f));
    }

    // ======================================================================
    // Timer module
    // ======================================================================

    private static void BuildTimerSection(Transform root)
    {
        // Background bezel
        MakeCube(root, "TimerBezel",
            pos:   new Vector3(0f, ModY - 0.001f, -0.135f),
            scale: new Vector3(0.42f, 0.006f, 0.080f),
            col:   new Color(0.05f, 0.05f, 0.05f));

        // Timer_Display — the script drives _EmissionColor on this material
        // at runtime to shift from green → red as time runs low.
        MakeCube(root, "Timer_Display",
            pos:   new Vector3(0f, ModY + 0.004f, -0.135f),
            scale: new Vector3(0.38f, 0.005f, 0.058f),
            col:   new Color(0.02f, 0.09f, 0.02f),
            emissive: true,
            emissiveCol: new Color(0f, 0.55f, 0f) * 1.5f);
    }

    // ======================================================================
    // Wires module
    // ======================================================================

    private static void BuildWiresSection(Transform root)
    {
        // Panel background
        MakeCube(root, "WiresPanel",
            pos:   new Vector3(-0.148f, ModY - 0.001f, WireCentreZ),
            scale: new Vector3(0.275f, 0.005f, 0.155f),
            col:   new Color(0.14f, 0.14f, 0.14f));

        var pegMat  = GetOrCreateMat("WirePeg",  new Color(0.42f, 0.42f, 0.42f));
        var capMat  = pegMat;

        for (int i = 0; i < WireCount; i++)
        {
            float wx = WireX[i];

            // ── top peg socket (front row) ────────────────────────────────
            CreatePeg(root, $"WirePeg_Top_{i}", wx, WirePegTopZ, pegMat);

            // ── bottom peg socket (back row) ──────────────────────────────
            CreatePeg(root, $"WirePeg_Bot_{i}", wx, WirePegBotZ, pegMat);

            // ── Wire_N — a cylinder lying along Z (interactable) ──────────
            // The XRGrabInteractable is added in the scene after import.
            var wireMat = GetOrCreateMat($"Wire_{i}", WireColours[i]);
            var wireGO  = MakePrimitive(root, $"Wire_{i}",
                PrimitiveType.Cylinder,
                pos:   new Vector3(wx, ModY + 0.012f, WireCentreZ),
                // Rotate 90° around X so the long axis lies along world +Z
                rot:   Quaternion.Euler(90f, 0f, 0f),
                // scale.y = half-length = WireLen/2;  scale.xz = wire radius
                scale: new Vector3(0.013f, WireLen / 2f, 0.013f),
                mat:   wireMat);

            // End-caps (spheres) that sit in the peg sockets — children of Wire_N
            var capMesh = MakePrimitive(wireGO.transform, $"WireCap_Top",
                PrimitiveType.Sphere,
                pos:   new Vector3(0f, 0f, -WireLen / 2f),   // local; cylinder +Z = world Z end
                rot:   Quaternion.identity,
                scale: new Vector3(0.018f, 0.018f, 0.018f),
                mat:   capMat);
            // Note: cap local Z is in the cylinder's local frame AFTER rotation.
            // After Euler(90,0,0), local +Y maps to world +Z.
            // So cap should be at local pos (0, WireLen/2, 0) for the +Z world end
            // and (0, -WireLen/2, 0) for the -Z world end.
            // Re-parent the capspheres using the cylinder's LOCAL axes:
            capMesh.transform.localPosition = new Vector3(0f, WireLen / 2f, 0f);

            var capMesh2 = MakePrimitive(wireGO.transform, "WireCap_Bot",
                PrimitiveType.Sphere,
                pos:   new Vector3(0f, -WireLen / 2f, 0f),
                rot:   Quaternion.identity,
                scale: new Vector3(0.018f, 0.018f, 0.018f),
                mat:   capMat);
            capMesh2.transform.localPosition = new Vector3(0f, -WireLen / 2f, 0f);

            // ── Collider + XR interaction ─────────────────────────────────
            // CapsuleCollider defaults (direction=Y, height=2, radius=0.5) are
            // correct for a Unity cylinder — they scale with the GO's localScale.
            wireGO.AddComponent<CapsuleCollider>();
            var grab = wireGO.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Kinematic;
        }
    }

    // ── peg socket ────────────────────────────────────────────────────────
    private static void CreatePeg(Transform root, string name,
        float wx, float wz, Material mat)
    {
        var go = MakePrimitive(root, name, PrimitiveType.Cylinder,
            pos:   new Vector3(wx, ModY + 0.005f, wz),
            rot:   Quaternion.identity,         // upright cylinder = peg hole
            scale: new Vector3(0.020f, 0.007f, 0.020f),
            mat:   mat);
    }

    // ======================================================================
    // Button module
    // ======================================================================

    private static void BuildButtonSection(Transform root)
    {
        // Panel background
        MakeCube(root, "ButtonPanel",
            pos:   new Vector3(0.152f, ModY - 0.001f, 0.020f),
            scale: new Vector3(0.265f, 0.005f, 0.155f),
            col:   new Color(0.14f, 0.14f, 0.14f));

        // Circular housing ring
        MakePrimitive(root, "ButtonHousing", PrimitiveType.Cylinder,
            pos:   new Vector3(0.152f, ModY + 0.004f, 0.000f),
            rot:   Quaternion.identity,
            scale: new Vector3(0.105f, 0.007f, 0.105f),
            mat:   GetOrCreateMat("ButtonHousing", new Color(0.18f, 0.18f, 0.18f)));

        // Button_Main — Defuser presses this.
        // Colour is randomised at runtime by ButtonModule.cs.
        // The placeholder colour is white.
        MakePrimitive(root, "Button_Main", PrimitiveType.Cylinder,
            pos:   new Vector3(0.152f, ModY + 0.013f, 0.000f),
            rot:   Quaternion.identity,
            scale: new Vector3(0.082f, 0.009f, 0.082f),
            mat:   GetOrCreateMat("Button_Main", new Color(0.88f, 0.88f, 0.88f)));

        // Button_LED — small indicator sphere.
        // Emission colour driven by ButtonModule.cs at runtime.
        MakePrimitive(root, "Button_LED", PrimitiveType.Sphere,
            pos:   new Vector3(0.152f, ModY + 0.008f, 0.063f),
            rot:   Quaternion.identity,
            scale: new Vector3(0.030f, 0.030f, 0.030f),
            mat:   GetOrCreateMat("Button_LED",
                       new Color(0.06f, 0.06f, 0.06f),
                       emissive: true,
                       emissiveCol: Color.black));

        // Button_Main — collider + interactable
        var btnGO = root.Find("Button_Main")?.gameObject;
        if (btnGO != null)
        {
            // CapsuleCollider defaults fit the cylinder shape correctly.
            btnGO.AddComponent<CapsuleCollider>();
            btnGO.AddComponent<XRSimpleInteractable>();
        }
    }

    // ======================================================================
    // Keypad module
    // ======================================================================

    private static void BuildKeypadSection(Transform root)
    {
        // Panel background
        MakeCube(root, "KeypadPanel",
            pos:   new Vector3(-0.148f, ModY - 0.001f, 0.140f),
            scale: new Vector3(0.275f, 0.005f, 0.120f),
            col:   new Color(0.14f, 0.14f, 0.14f));

        // Four keys evenly spaced along X
        float[] kx = { -0.255f, -0.178f, -0.101f, -0.024f };
        var keyMat = GetOrCreateMat("KeypadKey", new Color(0.22f, 0.22f, 0.22f));

        for (int i = 0; i < 4; i++)
        {
            var keyGO = MakeCube(root, $"Keypad_Key_{i}",
                pos:   new Vector3(kx[i], ModY + 0.010f, 0.140f),
                scale: new Vector3(0.057f, 0.012f, 0.062f),
                col:   Color.clear,
                mat:   keyMat);
            // BoxCollider default size = Vector3.one which scales with localScale → perfect fit.
            keyGO.AddComponent<BoxCollider>();
            keyGO.AddComponent<XRSimpleInteractable>();
        }
    }

    // ======================================================================
    // Simon Says module
    // ======================================================================

    private static void BuildSimonSection(Transform root)
    {
        // Panel background
        MakeCube(root, "SimonPanel",
            pos:   new Vector3(0.152f, ModY - 0.001f, 0.140f),
            scale: new Vector3(0.265f, 0.005f, 0.120f),
            col:   new Color(0.14f, 0.14f, 0.14f));

        // 2 × 2 grid of coloured pads
        // SimonModule.cs drives their _EmissionColor at runtime.
        const float padW = 0.100f;
        const float padD = 0.048f;

        // Left column x = 0.079, Right column x = 0.225
        // Front row  z = 0.103, Back row  z = 0.178

        MakeSimonPad(root, "Simon_Red",
            new Vector3(0.079f, ModY + 0.010f, 0.103f), padW, padD,
            new Color(0.85f, 0.08f, 0.08f), new Color(1f, 0f, 0f));

        MakeSimonPad(root, "Simon_Blue",
            new Vector3(0.225f, ModY + 0.010f, 0.103f), padW, padD,
            new Color(0.08f, 0.15f, 0.85f), new Color(0f, 0.3f, 1f));

        MakeSimonPad(root, "Simon_Green",
            new Vector3(0.079f, ModY + 0.010f, 0.178f), padW, padD,
            new Color(0.08f, 0.75f, 0.12f), new Color(0f, 1f, 0.1f));

        MakeSimonPad(root, "Simon_Yellow",
            new Vector3(0.225f, ModY + 0.010f, 0.178f), padW, padD,
            new Color(0.85f, 0.80f, 0.05f), new Color(1f, 0.9f, 0f));
    }

    // ── Simon pad helper ──────────────────────────────────────────────────
    private static void MakeSimonPad(Transform root, string name,
        Vector3 pos, float w, float d,
        Color baseCol, Color emissiveCol)
    {
        var go = MakeCube(root, name,
            pos:         pos,
            scale:       new Vector3(w, 0.012f, d),
            col:         baseCol,
            emissive:    true,
            emissiveCol: emissiveCol * 0.4f); // dim until activated at runtime
        // BoxCollider default size = Vector3.one → scales correctly with localScale.
        go.AddComponent<BoxCollider>();
        go.AddComponent<XRSimpleInteractable>();
    }

    // ======================================================================
    // Cosmetic extras
    // ======================================================================

    private static void BuildCornerLights(Transform root)
    {
        // Four red indicator lights at the top corners of the case
        var lightMat = GetOrCreateMat("CornerLight",
            new Color(0.6f, 0.05f, 0.05f),
            emissive: true, emissiveCol: new Color(1f, 0f, 0f) * 2f);

        float cx = CW / 2f - 0.030f;
        float cz = CD / 2f - 0.025f;

        foreach (var corner in new[]
        {
            new Vector3(-cx, TopY + 0.008f, -cz),
            new Vector3( cx, TopY + 0.008f, -cz),
            new Vector3(-cx, TopY + 0.008f,  cz),
            new Vector3( cx, TopY + 0.008f,  cz),
        })
        {
            MakePrimitive(root, "CornerLight", PrimitiveType.Sphere,
                pos:   corner,
                rot:   Quaternion.identity,
                scale: new Vector3(0.018f, 0.018f, 0.018f),
                mat:   lightMat);
        }
    }

    private static void BuildSidePanels(Transform root)
    {
        // Thin raised strips on the front and back faces to add visual depth
        var edgeMat = GetOrCreateMat("EdgeStrip", new Color(0.20f, 0.20f, 0.20f));

        // Front edge strip (Defuser side)
        MakePrimitive(root, "EdgeStrip_Front", PrimitiveType.Cube,
            pos:   new Vector3(0f, 0f, -(CD / 2f) - 0.003f),
            rot:   Quaternion.identity,
            scale: new Vector3(CW + 0.006f, CH + 0.006f, 0.006f),
            mat:   edgeMat);

        // Back edge strip (Expert side)
        MakePrimitive(root, "EdgeStrip_Back", PrimitiveType.Cube,
            pos:   new Vector3(0f, 0f, (CD / 2f) + 0.003f),
            rot:   Quaternion.identity,
            scale: new Vector3(CW + 0.006f, CH + 0.006f, 0.006f),
            mat:   edgeMat);

        // Left / right edge strips
        MakePrimitive(root, "EdgeStrip_Left", PrimitiveType.Cube,
            pos:   new Vector3(-(CW / 2f) - 0.003f, 0f, 0f),
            rot:   Quaternion.identity,
            scale: new Vector3(0.006f, CH + 0.006f, CD),
            mat:   edgeMat);

        MakePrimitive(root, "EdgeStrip_Right", PrimitiveType.Cube,
            pos:   new Vector3((CW / 2f) + 0.003f, 0f, 0f),
            rot:   Quaternion.identity,
            scale: new Vector3(0.006f, CH + 0.006f, CD),
            mat:   edgeMat);

        // Vent slots on left face (decorative)
        var ventMat = GetOrCreateMat("Vent", new Color(0.06f, 0.06f, 0.06f));
        for (int v = 0; v < 5; v++)
        {
            float vz = -0.12f + v * 0.06f;
            MakePrimitive(root, $"Vent_L_{v}", PrimitiveType.Cube,
                pos:   new Vector3(-(CW / 2f) + 0.005f, 0f, vz),
                rot:   Quaternion.identity,
                scale: new Vector3(0.010f, 0.060f, 0.012f),
                mat:   ventMat);
        }
    }

    // ======================================================================
    // Primitive factory helpers
    // ======================================================================

    /// <summary>Creates a Cube primitive and assigns material + transform.</summary>
    private static GameObject MakeCube(Transform parent, string name,
        Vector3 pos, Vector3 scale, Color col,
        bool emissive = false, Color emissiveCol = default,
        Material mat = null)
    {
        return MakePrimitive(parent, name, PrimitiveType.Cube,
            pos, Quaternion.identity, scale,
            mat ?? GetOrCreateMat(name, col, emissive, emissiveCol));
    }

    /// <summary>Creates any primitive type and assigns material + transform.</summary>
    private static GameObject MakePrimitive(Transform parent, string name,
        PrimitiveType type, Vector3 pos, Quaternion rot, Vector3 scale,
        Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale    = scale;

        var r = go.GetComponent<Renderer>();
        if (r != null && mat != null)
            r.sharedMaterial = mat;

        // Remove the default collider – the interactable scripts will get
        // their own purpose-fit colliders added via the Inspector.
        var col = go.GetComponent<Collider>();
        if (col != null)
            Object.DestroyImmediate(col);

        return go;
    }

    // ======================================================================
    // Material helpers
    // ======================================================================

    private static Material GetOrCreateMat(string name, Color col,
        bool emissive = false, Color emissiveCol = default)
    {
        string path = $"{MatFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        var shader = Shader.Find("Universal Render Pipeline/Lit")
                  ?? Shader.Find("Standard");
        mat = new Material(shader) { name = name };

        ApplyColor(mat, col);

        if (emissive)
        {
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", emissiveCol);
        }

        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static void ApplyColor(Material mat, Color col)
    {
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", col);
        else if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", col);
    }

    // ======================================================================
    // Folder setup
    // ======================================================================

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");

        if (!AssetDatabase.IsValidFolder(MatFolder))
            AssetDatabase.CreateFolder("Assets/Materials", "KTANEBomb");
    }
}
