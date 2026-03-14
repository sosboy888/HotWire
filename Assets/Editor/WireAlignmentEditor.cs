// WireAlignmentEditor.cs
// Editor window for fixing wire-to-peg alignment on the KTANE bomb model.
//
// HOW TO USE:
//   Tools ▶ KTANE ▶ Wire Alignment Editor
//
// WORKFLOW:
//   1. Open the KTANEGame scene and enter Edit mode (do NOT press Play).
//   2. Open this window.
//   3. Click "Find Bomb in Scene" – it locates the bomb root automatically.
//   4. Use "Bomb Scale" to make the whole bomb bigger if the wires look tiny.
//   5. Use "Global Wire Offset" to slide all wires together until they sit
//      over the peg holes.
//   6. Fine-tune individual wires with the per-wire position sliders.
//   7. Click "Save Transforms as Scene Overrides" to bake the values.
//   8. Save the scene (Ctrl+S).
//
// TIP: Keep the Scene view open alongside this window.  Tick "Live Preview"
//      and every slider change immediately moves the wires in the scene.

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class WireAlignmentEditor : EditorWindow
{
    // ── state ────────────────────────────────────────────────────────────────
    private GameObject  bombRoot;              // root of the bomb model
    private Transform[] wires = new Transform[6];
    private bool        wiresFound;

    // Persistent adjustments (relative to the wire's original local position)
    private Vector3   globalOffset  = Vector3.zero;
    private float     bombScale     = 1f;

    private Vector3[] perWireOffset = new Vector3[6];

    // Original positions captured when bomb is first located (so sliders
    // always show a delta rather than absolute world coords)
    private Vector3[] originalLocalPos = new Vector3[6];
    private Vector3   originalBombScale;
    private bool      originsCaptured;

    // UI state
    private bool  livePreview   = true;
    private bool[] wireFoldouts = new bool[6];
    private Vector2 scrollPos;

    private static readonly string[] WireNames =
        { "Wire_0", "Wire_1", "Wire_2", "Wire_3", "Wire_4", "Wire_5" };

    private static readonly Color[] WireColourHints = new Color[]
    {
        new Color(1.0f, 0.25f, 0.25f), // Red
        new Color(0.25f, 0.45f, 1.0f), // Blue
        new Color(1.0f, 1.0f,  0.25f), // Yellow
        new Color(0.9f, 0.9f,  0.9f),  // White
        new Color(0.15f,0.15f, 0.15f), // Black
        new Color(1.0f, 0.25f, 0.25f), // Red (second)
    };

    // ── open window ──────────────────────────────────────────────────────────
    [MenuItem("Tools/KTANE/Wire Alignment Editor")]
    public static void OpenWindow()
    {
        var w = GetWindow<WireAlignmentEditor>("Wire Alignment");
        w.minSize = new Vector2(380, 500);
        w.Show();
    }

    // ── GUI ──────────────────────────────────────────────────────────────────
    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        DrawHeader();
        EditorGUILayout.Space(6);

        DrawBombLocator();
        EditorGUILayout.Space(6);

        if (!wiresFound)
        {
            EditorGUILayout.HelpBox(
                "Locate the bomb first (button above).", MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        DrawScaleSection();
        EditorGUILayout.Space(6);

        DrawGlobalOffsetSection();
        EditorGUILayout.Space(6);

        DrawPerWireSection();
        EditorGUILayout.Space(8);

        DrawActions();

        EditorGUILayout.EndScrollView();
    }

    // ── header ───────────────────────────────────────────────────────────────
    private void DrawHeader()
    {
        EditorGUILayout.LabelField("KTANE Wire Alignment Editor",
            EditorStyles.boldLabel);
        livePreview = EditorGUILayout.Toggle("Live Preview", livePreview);
        EditorGUILayout.HelpBox(
            "Adjustments here are OFFSETS on top of the wire's original local " +
            "position. Click \"Save\" to bake them permanently.",
            MessageType.None);
    }

    // ── bomb locator ─────────────────────────────────────────────────────────
    private void DrawBombLocator()
    {
        EditorGUILayout.LabelField("1. Locate Bomb", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        bombRoot = (GameObject)EditorGUILayout.ObjectField(
            "Bomb Root", bombRoot, typeof(GameObject), allowSceneObjects: true);

        if (GUILayout.Button("Find in Scene", GUILayout.Width(110)))
            AutoFindBomb();
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Locate Wires on Bomb Root"))
            TryLocateWires();

        if (wiresFound)
        {
            EditorGUILayout.HelpBox(
                $"Found {CountWires()}/6 wire children.", MessageType.Info);
        }
    }

    // ── scale section ─────────────────────────────────────────────────────────
    private void DrawScaleSection()
    {
        EditorGUILayout.LabelField("2. Bomb Scale", EditorStyles.boldLabel);

        float newScale = EditorGUILayout.Slider("Uniform Scale", bombScale, 0.1f, 5f);
        if (!Mathf.Approximately(newScale, bombScale))
        {
            bombScale = newScale;
            if (livePreview) ApplyScale();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset Scale (×1)"))
        {
            bombScale = 1f;
            if (livePreview) ApplyScale();
        }
        if (GUILayout.Button("×2 (double size)"))
        {
            bombScale = 2f;
            if (livePreview) ApplyScale();
        }
        if (GUILayout.Button("×1.5"))
        {
            bombScale = 1.5f;
            if (livePreview) ApplyScale();
        }
        EditorGUILayout.EndHorizontal();
    }

    // ── global offset ─────────────────────────────────────────────────────────
    private void DrawGlobalOffsetSection()
    {
        EditorGUILayout.LabelField("3. Global Wire Offset (moves ALL wires)",
            EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        float x = EditorGUILayout.Slider("  X (left/right)",   globalOffset.x, -0.1f, 0.1f);
        float y = EditorGUILayout.Slider("  Y (up/down)",      globalOffset.y, -0.1f, 0.1f);
        float z = EditorGUILayout.Slider("  Z (forward/back)", globalOffset.z, -0.1f, 0.1f);
        if (EditorGUI.EndChangeCheck())
        {
            globalOffset = new Vector3(x, y, z);
            if (livePreview) ApplyAll();
        }

        if (GUILayout.Button("Reset Global Offset"))
        {
            globalOffset = Vector3.zero;
            if (livePreview) ApplyAll();
        }
    }

    // ── per-wire fine-tune ────────────────────────────────────────────────────
    private void DrawPerWireSection()
    {
        EditorGUILayout.LabelField("4. Per-Wire Fine Tuning", EditorStyles.boldLabel);

        for (int i = 0; i < 6; i++)
        {
            if (wires[i] == null) continue;

            // Coloured label strip
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = WireColourHints[i];
            wireFoldouts[i] = EditorGUILayout.BeginFoldoutHeaderGroup(
                wireFoldouts[i], $"  Wire_{i}  ({WireNameHint(i)})");
            GUI.backgroundColor = prevBg;

            if (wireFoldouts[i])
            {
                EditorGUI.indentLevel++;
                EditorGUI.BeginChangeCheck();

                float px = EditorGUILayout.Slider("X offset", perWireOffset[i].x, -0.05f, 0.05f);
                float py = EditorGUILayout.Slider("Y offset", perWireOffset[i].y, -0.05f, 0.05f);
                float pz = EditorGUILayout.Slider("Z offset", perWireOffset[i].z, -0.05f, 0.05f);

                if (EditorGUI.EndChangeCheck())
                {
                    perWireOffset[i] = new Vector3(px, py, pz);
                    if (livePreview) ApplyWire(i);
                }

                // Show resolved local position for debugging
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.Vector3Field(
                    "Resolved local pos",
                    originalLocalPos[i] + globalOffset + perWireOffset[i]);
                EditorGUI.EndDisabledGroup();

                if (GUILayout.Button($"Reset Wire_{i} offset"))
                {
                    perWireOffset[i] = Vector3.zero;
                    if (livePreview) ApplyWire(i);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }
    }

    // ── action buttons ────────────────────────────────────────────────────────
    private void DrawActions()
    {
        EditorGUILayout.LabelField("5. Apply", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Preview Now", GUILayout.Height(30)))
            ApplyAll();

        GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
        if (GUILayout.Button("Save Transforms as Scene Overrides", GUILayout.Height(30)))
        {
            ApplyAll();
            ApplyScale();
            // Mark objects dirty so Unity knows to save them with the scene
            foreach (var w in wires)
                if (w != null) EditorUtility.SetDirty(w);
            if (bombRoot != null) EditorUtility.SetDirty(bombRoot);
            Debug.Log("[WireAlignmentEditor] Transforms saved. Press Ctrl+S to save the scene.");
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        GUI.backgroundColor = new Color(1f, 0.6f, 0.4f);
        if (GUILayout.Button("Reset ALL to original positions"))
        {
            globalOffset = Vector3.zero;
            for (int i = 0; i < 6; i++) perWireOffset[i] = Vector3.zero;
            bombScale = 1f;
            ApplyAll();
            ApplyScale();
        }
        GUI.backgroundColor = Color.white;
    }

    // ── logic: find bomb ─────────────────────────────────────────────────────
    private void AutoFindBomb()
    {
        // Look for common names used in the scene builder
        string[] candidates = { "KTANE_Bomb", "BombMount", "BombPlaceholder [replace with KTANE BOMB.fbx]" };
        foreach (var name in candidates)
        {
            var found = GameObject.Find(name);
            if (found != null)
            {
                // If we found BombMount, take its first child (the actual model)
                bombRoot = (name == "BombMount" && found.transform.childCount > 0)
                    ? found.transform.GetChild(0).gameObject
                    : found;
                break;
            }
        }

        // Fallback: search all GameObjects for one that has a Wire_0 child
        if (bombRoot == null)
        {
            foreach (var go in FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (go.transform.Find("Wire_0") != null)
                {
                    bombRoot = go;
                    break;
                }
            }
        }

        if (bombRoot != null)
        {
            TryLocateWires();
            Debug.Log($"[WireAlignmentEditor] Bomb root found: {bombRoot.name}");
        }
        else
        {
            EditorUtility.DisplayDialog("Not Found",
                "Could not find the bomb in the scene.\n\n" +
                "Drag the bomb root GameObject into the 'Bomb Root' field manually.",
                "OK");
        }
    }

    private void TryLocateWires()
    {
        if (bombRoot == null) return;
        wiresFound      = false;
        originsCaptured = false;

        for (int i = 0; i < 6; i++)
        {
            var t = bombRoot.transform.Find(WireNames[i]);
            // Also search recursively (model may have an intermediate parent)
            if (t == null) t = FindDeep(bombRoot.transform, WireNames[i]);
            wires[i] = t;
        }

        // Capture originals
        originalBombScale = bombRoot.transform.localScale;
        for (int i = 0; i < 6; i++)
        {
            originalLocalPos[i] = wires[i] != null
                ? wires[i].localPosition
                : Vector3.zero;
            perWireOffset[i] = Vector3.zero;
        }

        globalOffset    = Vector3.zero;
        bombScale       = originalBombScale.x; // assume uniform scale
        originsCaptured = true;
        wiresFound      = true;
    }

    // ── logic: apply transforms ───────────────────────────────────────────────
    private void ApplyAll()
    {
        for (int i = 0; i < 6; i++) ApplyWire(i);
    }

    private void ApplyWire(int i)
    {
        if (!originsCaptured || wires[i] == null) return;
        Undo.RecordObject(wires[i], $"Adjust Wire_{i} position");
        wires[i].localPosition = originalLocalPos[i] + globalOffset + perWireOffset[i];
    }

    private void ApplyScale()
    {
        if (bombRoot == null) return;
        Undo.RecordObject(bombRoot.transform, "Scale Bomb");
        bombRoot.transform.localScale = Vector3.one * bombScale;
    }

    // ── helpers ───────────────────────────────────────────────────────────────
    private int CountWires()
    {
        int c = 0;
        foreach (var w in wires) if (w != null) c++;
        return c;
    }

    private static string WireNameHint(int i) => i switch
    {
        0 => "Red",
        1 => "Blue",
        2 => "Yellow",
        3 => "White",
        4 => "Black",
        5 => "Red (2nd)",
        _ => "?"
    };

    private static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    // Repaint every frame so the Scene view update is reflected
    private void OnInspectorUpdate() => Repaint();
}
