// BuildKTANEScene.cs
// Editor utility that builds the KTANEGame scene from scratch.
//
// HOW TO USE:
//   In Unity's top menu click:  Tools ▶ KTANE ▶ Build KTANEGame Scene
//   The scene is saved as Assets/Scenes/KTANEGame.unity.
//   Open it from the Project window and press Play to test.
//
// WHAT IS CREATED:
//   • Directional Light + ambient settings (matches NetSandbox)
//   • Floor (Plane), 4 walls (invisible boundary cubes)
//   • Wooden table in the centre of the room
//   • Bomb placeholder on the table (empty GO – drag your KTANE BOMB.fbx here)
//   • Ubiq Network Scene (Demo) prefab  ← networking hub (NetworkScene + RoomClient)
//   • XR Origin (XR Rig) prefab         ← player camera + controllers
//   • AutoRoomJoiner                     ← auto-joins room "ktane-vr-game" on start
//   • KTANEGameManager                   ← game-state machine
//   • Module GameObjects (Timer, Wires, Button, Keypad, Simon) – scripts attached
//   • ExpertUI World-Space Canvas        ← read-only panel for the Expert player
//   • Module reference wiring between ExpertUIManager and all module scripts

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;
using KTANE;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Feedback;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class BuildKTANEScene
{
    // ------------------------------------------------------------------ paths
    private const string ScenePath = "Assets/Scenes/KTANEGame.unity";

    private const string UbiqNetworkScenePrefabPath =
        "Assets/Samples/Ubiq/1.0.0-pre.16/Demo (XRI)/Assets/Prefabs/Ubiq Network Scene (Demo).prefab";

    private const string XrRigPrefabPath =
        "Assets/Samples/XR Interaction Toolkit/3.0.7/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";

    // Prefer the Unity-native bomb prefab; fall back to the FBX if it exists.
    private const string KTANEBombPrefabPath = "Assets/Prefabs/KTANEBomb.prefab";
    private const string BombFbxPath         = "Assets/KTANE BOMB.fbx";

    // ------------------------------------------------------------------ entry
    [MenuItem("Tools/KTANE/Build KTANEGame Scene")]
    public static void BuildScene()
    {
        // ── 0. Ensure Scenes folder exists ──────────────────────────────────
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        // ── 1. Create fresh empty scene ─────────────────────────────────────
        var scene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── 2. Render / ambient settings (matches NetSandbox) ───────────────
        ApplyRenderSettings();

        // ── 3. Directional Light ─────────────────────────────────────────────
        CreateDirectionalLight();

        // ── 4. Environment (floor + walls) ───────────────────────────────────
        CreateEnvironment();

        // ── 5. Table + Bomb placeholder ──────────────────────────────────────
        var bombMount = CreateTable();

        // ── 6. Ubiq Network Scene prefab ─────────────────────────────────────
        var ubiqRoot = InstantiateUbiqNetworkScene();

        // ── 7. XR Origin (XR Rig) prefab ─────────────────────────────────────
        //    Position at Defuser's side of the table, facing the bomb.
        InstantiateXrRig(ubiqRoot);

        // ── 7b. EventSystem with XR UI Input Module ───────────────────────────
        //    Required for Quest controllers to interact with World-Space UI.
        CreateXrEventSystem();

        // ── 8. AutoRoomJoiner ─────────────────────────────────────────────────
        CreateAutoRoomJoiner();

        // ── 9. KTANEGameManager ───────────────────────────────────────────────
        var gmGO = CreateGameManager();

        // ── 9b. KTANESoundManager ─────────────────────────────────────────────
        CreateSoundManager();

        // ── 10. Module GameObjects (children of BombMount) ────────────────────
        var timerMod   = CreateModuleGO<TimerModule>  (bombMount, "TimerModule");
        var wiresMod   = CreateModuleGO<WiresModule>  (bombMount, "WiresModule");
        var buttonMod  = CreateModuleGO<ButtonModule> (bombMount, "ButtonModule");
        var keypadMod  = CreateModuleGO<KeypadModule> (bombMount, "KeypadModule");
        var simonMod   = CreateModuleGO<SimonModule>  (bombMount, "SimonModule");

        // ── 11. ExpertUIManager (self-building canvas — no wiring needed) ────
        CreateExpertUI();

        // ── 12. Place bomb ────────────────────────────────────────────────────
        TryPlaceBombFbx(bombMount);

        // ── 13. Auto-wire all module Inspector slots ───────────────────────────
        AutoWireModuleSlots(bombMount, timerMod, wiresMod, buttonMod, keypadMod, simonMod);

        // ── 14. KTANELobbyManager ─────────────────────────────────────────────
        CreateLobbyManager(bombMount, timerMod, wiresMod, buttonMod, keypadMod, simonMod);

        // ── 15. Manual Tablet ─────────────────────────────────────────────────
        CreateManualTablet();

        // ── 16. Save ──────────────────────────────────────────────────────────
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        Debug.Log($"[BuildKTANEScene] Scene saved to {ScenePath}");
        EditorUtility.DisplayDialog(
            "KTANEGame Scene Built",
            $"Scene saved to:\n{ScenePath}\n\n" +
            "Everything is wired up automatically — colliders, XR interactables,\n" +
            "module Inspector slots, lobby manager, and manual tablet.\n\n" +
            "Run  Tools ▶ KTANE ▶ Create Level Assets  before pressing Play.\n" +
            "Open the scene and press Play with two Editor instances to test.",
            "OK");
    }

    // ==========================================================================
    // Step implementations
    // ==========================================================================

    // ── 2 ──────────────────────────────────────────────────────────────────────
    private static void ApplyRenderSettings()
    {
        // Trilight ambient (sky/equator/ground) mirroring NetSandbox values
        RenderSettings.ambientMode         = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor     = new Color(0.423f, 0.455f, 0.518f);
        RenderSettings.ambientEquatorColor = new Color(0.227f, 0.251f, 0.267f);
        RenderSettings.ambientGroundColor  = new Color(0.141f, 0.118f, 0.067f);
        RenderSettings.ambientIntensity    = 1f;
        RenderSettings.fog                 = false;

        // Default skybox – use Unity's built-in procedural skybox
        var skyboxMat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
        if (skyboxMat != null) RenderSettings.skybox = skyboxMat;
    }

    // ── 3 ──────────────────────────────────────────────────────────────────────
    private static void CreateDirectionalLight()
    {
        var go    = new GameObject("Directional Light");
        var light = go.AddComponent<Light>();
        light.type      = LightType.Directional;
        light.intensity = 1.1f;
        light.color     = new Color(1f, 0.96f, 0.87f);
        light.shadows   = LightShadows.Soft;
        go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    // ── 4 ──────────────────────────────────────────────────────────────────────
    private static void CreateEnvironment()
    {
        // Floor
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.localScale = new Vector3(3f, 1f, 3f); // 30 m × 30 m
        SetMaterial(floor, new Color(0.35f, 0.28f, 0.22f)); // warm brown

        // Ceiling (optional visual reference in VR)
        var ceiling = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ceiling.name = "Ceiling";
        ceiling.transform.position   = new Vector3(0, 3f, 0);
        ceiling.transform.rotation   = Quaternion.Euler(180f, 0f, 0f);
        ceiling.transform.localScale = new Vector3(3f, 1f, 3f);
        SetMaterial(ceiling, new Color(0.85f, 0.83f, 0.78f));

        // Four walls (thin boxes, 6 m wide × 3 m tall)
        CreateWall("Wall_North", new Vector3(0f, 1.5f,  15f), new Vector3(30f, 3f, 0.3f));
        CreateWall("Wall_South", new Vector3(0f, 1.5f, -15f), new Vector3(30f, 3f, 0.3f));
        CreateWall("Wall_East",  new Vector3( 15f, 1.5f, 0f), new Vector3(0.3f, 3f, 30f));
        CreateWall("Wall_West",  new Vector3(-15f, 1.5f, 0f), new Vector3(0.3f, 3f, 30f));
    }

    private static void CreateWall(string name, Vector3 pos, Vector3 scale)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.position   = pos;
        wall.transform.localScale = scale;
        SetMaterial(wall, new Color(0.78f, 0.76f, 0.72f));
    }

    // ── 5 ──────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Creates a table in the centre of the room and returns the BombMount
    /// transform (an empty child of the table top where the bomb sits).
    /// </summary>
    private static Transform CreateTable()
    {
        var tableRoot = new GameObject("Table");

        // ── Tabletop ──────────────────────────────────────────────────────────
        // Standard table: 1.2 m wide, 0.7 m deep, 0.05 m thick, sitting at 0.75 m
        var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
        top.name = "TableTop";
        top.transform.SetParent(tableRoot.transform, false);
        top.transform.localPosition = new Vector3(0f, 0.775f, 0f);
        top.transform.localScale    = new Vector3(1.2f, 0.05f, 0.7f);
        SetMaterial(top, new Color(0.55f, 0.34f, 0.13f)); // wood colour

        // ── Four legs ─────────────────────────────────────────────────────────
        float lx = 0.55f; float lz = 0.30f;
        CreateTableLeg(tableRoot.transform, "Leg_FL", new Vector3(-lx,  0.375f, -lz));
        CreateTableLeg(tableRoot.transform, "Leg_FR", new Vector3( lx,  0.375f, -lz));
        CreateTableLeg(tableRoot.transform, "Leg_BL", new Vector3(-lx,  0.375f,  lz));
        CreateTableLeg(tableRoot.transform, "Leg_BR", new Vector3( lx,  0.375f,  lz));

        // ── BombMount: empty transform on top of the table ────────────────────
        // The bomb FBX (or placeholder) will be a child of this.
        var mountGO = new GameObject("BombMount");
        mountGO.transform.SetParent(tableRoot.transform, false);
        // Table top surface is at y = 0.8 (TableTop localPos 0.775 + half-thickness 0.025).
        // KTANEBomb body is 0.14 m tall, centered at origin → bottom at -0.07 m bomb-local.
        // So mount must be at y = 0.8 + 0.07 = 0.87 to place the bomb bottom on the table.
        mountGO.transform.localPosition = new Vector3(0f, 0.87f, 0f);

        return mountGO.transform;
    }

    private static void CreateTableLeg(Transform parent, string name, Vector3 localPos)
    {
        var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leg.name = name;
        leg.transform.SetParent(parent, false);
        leg.transform.localPosition = localPos;
        leg.transform.localScale    = new Vector3(0.06f, 0.75f, 0.06f);
        SetMaterial(leg, new Color(0.45f, 0.28f, 0.10f));
    }

    // ── 6 ──────────────────────────────────────────────────────────────────────
    private static GameObject InstantiateUbiqNetworkScene()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UbiqNetworkScenePrefabPath);
        if (prefab == null)
        {
            Debug.LogError(
                $"[BuildKTANEScene] Could not load Ubiq prefab at:\n{UbiqNetworkScenePrefabPath}\n" +
                "Make sure Ubiq 1.0.0-pre.16 (Demo XRI) sample is imported.");
            // Fallback: create a bare named GO so the scene still saves cleanly
            return new GameObject("Ubiq Network Scene (Demo) [MISSING PREFAB]");
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = "Ubiq Network Scene (Demo)";
        go.transform.position = Vector3.zero;
        Debug.Log("[BuildKTANEScene] Ubiq Network Scene prefab placed.");
        return go;
    }

    // ── 7 ──────────────────────────────────────────────────────────────────────
    private static void InstantiateXrRig(GameObject ubiqRoot)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(XrRigPrefabPath);
        if (prefab == null)
        {
            Debug.LogError(
                $"[BuildKTANEScene] Could not load XR Rig prefab at:\n{XrRigPrefabPath}\n" +
                "Make sure XR Interaction Toolkit 3.0.7 Starter Assets sample is imported.");
            return;
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = "XR Origin (XR Rig)";

        // Place the Defuser on the south side of the table, facing north (the bomb).
        // y = 0 because XR Rig manages camera height via tracking.
        go.transform.position = new Vector3(0f, 0f, -1.2f);
        go.transform.rotation = Quaternion.identity; // faces +Z (toward table)

        // In the NetSandbox the XR Rig is a child of the Ubiq Network Scene so
        // the Ubiq avatar system can find it. Replicate that relationship.
        if (ubiqRoot != null)
            go.transform.SetParent(ubiqRoot.transform, true);

        // Disable hover haptics on all SimpleHapticFeedback components so pointing
        // at canvases doesn't cause continuous vibration. Select haptics are kept.
        foreach (var hf in go.GetComponentsInChildren<SimpleHapticFeedback>(true))
        {
            hf.playHoverEntered  = false;
            hf.playHoverExited   = false;
            hf.playHoverCanceled = false;
        }

        Debug.Log("[BuildKTANEScene] XR Origin (XR Rig) placed at Defuser position.");
    }

    // ── 7b ─────────────────────────────────────────────────────────────────────
    private static void CreateXrEventSystem()
    {
        // XR controllers need XRUIInputModule (not the default StandaloneInputModule)
        // to ray-cast against World-Space canvases and fire button clicks + haptics.
        // Check first so rebuilding the scene doesn't create duplicates.
        if (Object.FindFirstObjectByType<EventSystem>() != null)
        {
            Debug.Log("[BuildKTANEScene] EventSystem already present – skipping.");
            return;
        }

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<XRUIInputModule>();
        Debug.Log("[BuildKTANEScene] EventSystem + XRUIInputModule created.");
    }

    // ── 8 ──────────────────────────────────────────────────────────────────────
    private static void CreateAutoRoomJoiner()
    {
        var go     = new GameObject("AutoRoomJoiner");
        var joiner = go.AddComponent<AutoRoomJoiner>();

        // Set the private [SerializeField] roomName via SerializedObject so it
        // persists when the scene is saved.
        var so = new SerializedObject(joiner);
        so.FindProperty("roomName").stringValue = "ktane-vr-game";
        so.FindProperty("publish").boolValue    = false;
        so.FindProperty("autoJoinDelaySeconds").floatValue = 0.5f;
        so.FindProperty("requireConnection").boolValue     = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("[BuildKTANEScene] AutoRoomJoiner created (room: ktane-vr-game).");
    }

    // ── 9 ──────────────────────────────────────────────────────────────────────
    private static GameObject CreateGameManager()
    {
        var go = new GameObject("KTANEGameManager");
        var gm = go.AddComponent<KTANEGameManager>();

        var so = new SerializedObject(gm);
        so.FindProperty("modulesToSolve").intValue = 5;
        so.ApplyModifiedPropertiesWithoutUndo();

        return go;
    }

    // ── 9b ─────────────────────────────────────────────────────────────────────
    private static void CreateSoundManager()
    {
        var go = new GameObject("KTANESoundManager");
        go.AddComponent<KTANESoundManager>();
        Debug.Log("[BuildKTANEScene] KTANESoundManager created.");
    }

    // ── 10 helper ─────────────────────────────────────────────────────────────
    private static T CreateModuleGO<T>(Transform parent, string goName)
        where T : MonoBehaviour
    {
        var go = new GameObject(goName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        return go.AddComponent<T>();
    }

    // ── 11 ────────────────────────────────────────────────────────────────────
    private static void CreateExpertUI()
    {
        // ExpertUIManager builds its own World-Space Canvas at runtime (Awake).
        // No Inspector wiring required — it auto-discovers modules via
        // FindFirstObjectByType and positions itself at the lobby canvas location.
        var go = new GameObject("ExpertUI");
        go.AddComponent<ExpertUIManager>();
        Debug.Log("[BuildKTANEScene] ExpertUI created (self-building canvas).");
    }

    // ── 12 ────────────────────────────────────────────────────────────────────
    private static void TryPlaceBombFbx(Transform bombMount)
    {
        // 1. Prefer the Unity-native bomb prefab (created by CreateKTANEBombModel).
        var nativePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KTANEBombPrefabPath);
        if (nativePrefab != null)
        {
            var bomb = (GameObject)PrefabUtility.InstantiatePrefab(nativePrefab);
            bomb.name = "KTANEBomb";
            bomb.transform.SetParent(bombMount, false);
            bomb.transform.localPosition = Vector3.zero;
            bomb.transform.localScale    = Vector3.one;
            Debug.Log("[BuildKTANEScene] Unity-native KTANEBomb prefab placed on the table.");
            return;
        }

        // 2. Fall back to the original FBX if it has been imported.
        var bombFbx = AssetDatabase.LoadAssetAtPath<GameObject>(BombFbxPath);
        if (bombFbx != null)
        {
            var bomb = (GameObject)PrefabUtility.InstantiatePrefab(bombFbx);
            bomb.name = "KTANE_Bomb";
            bomb.transform.SetParent(bombMount, false);
            bomb.transform.localPosition = Vector3.zero;
            bomb.transform.localScale    = Vector3.one;
            Debug.Log("[BuildKTANEScene] KTANE BOMB.fbx placed on the table.");
            return;
        }

        // 3. Neither found – place a clearly labelled placeholder cube.
        var placeholder = GameObject.CreatePrimitive(PrimitiveType.Cube);
        placeholder.name = "BombPlaceholder [run Tools > KTANE > Create KTANE Bomb Model first]";
        placeholder.transform.SetParent(bombMount, false);
        placeholder.transform.localPosition = Vector3.zero;
        placeholder.transform.localScale    = new Vector3(0.60f, 0.12f, 0.40f);
        SetMaterial(placeholder, new Color(0.15f, 0.15f, 0.15f));
        Debug.LogWarning(
            "[BuildKTANEScene] No bomb found.\n" +
            "Run Tools > KTANE > Create KTANE Bomb Model, then re-run Build KTANEGame Scene.");
    }

    // ==========================================================================
    // Auto-wiring
    // ==========================================================================

    /// <summary>
    /// Finds every named bomb child under bombMount and assigns it into the
    /// correct Inspector slot on each module script via SerializedObject.
    /// This runs after both the module GOs and the bomb have been created.
    /// </summary>
    private static void AutoWireModuleSlots(
        Transform    bombMount,
        TimerModule  timer,
        WiresModule  wires,
        ButtonModule button,
        KeypadModule keypad,
        SimonModule  simon)
    {
        // Locate the bomb root — first child of bombMount that has the
        // expected named children.
        Transform bombRoot = null;
        foreach (Transform child in bombMount)
        {
            if (child.Find("Wire_0") != null ||
                child.Find("Timer_Display") != null)
            {
                bombRoot = child;
                break;
            }
        }

        if (bombRoot == null)
        {
            Debug.LogWarning("[BuildKTANEScene] AutoWire: bomb root not found under BombMount. " +
                             "Run 'Create KTANE Bomb Model' first, then rebuild the scene.");
            return;
        }

        // ── TimerModule ──────────────────────────────────────────────────────
        var timerDisplay = bombRoot.Find("Timer_Display");
        if (timerDisplay != null && timer != null)
        {
            var so = new SerializedObject(timer);
            so.FindProperty("timerDisplay").objectReferenceValue =
                timerDisplay.GetComponent<Renderer>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── WiresModule ──────────────────────────────────────────────────────
        if (wires != null)
        {
            var so       = new SerializedObject(wires);
            var wiresProp = so.FindProperty("wireObjects");
            wiresProp.arraySize = 6;
            for (int i = 0; i < 6; i++)
            {
                var wireT = bombRoot.Find($"Wire_{i}");
                wiresProp.GetArrayElementAtIndex(i).objectReferenceValue =
                    wireT != null ? wireT.gameObject : null;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── ButtonModule ─────────────────────────────────────────────────────
        if (button != null)
        {
            var btnT  = bombRoot.Find("Button_Main");
            var ledT  = bombRoot.Find("Button_LED");
            var so    = new SerializedObject(button);
            so.FindProperty("buttonInteractable").objectReferenceValue =
                btnT != null ? btnT.GetComponent<XRSimpleInteractable>() : null;
            so.FindProperty("buttonLED").objectReferenceValue =
                ledT != null ? ledT.GetComponent<Renderer>() : null;
            // Wire buttonRenderer so Configure() can tint the face colour
            so.FindProperty("buttonRenderer").objectReferenceValue =
                btnT != null ? btnT.GetComponent<Renderer>() : null;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── KeypadModule ─────────────────────────────────────────────────────
        if (keypad != null)
        {
            var so       = new SerializedObject(keypad);
            var keysProp  = so.FindProperty("keyObjects");
            keysProp.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                var keyT = bombRoot.Find($"Keypad_Key_{i}");
                keysProp.GetArrayElementAtIndex(i).objectReferenceValue =
                    keyT != null ? keyT.gameObject : null;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── SimonModule ──────────────────────────────────────────────────────
        if (simon != null)
        {
            string[] padNames = { "Simon_Red", "Simon_Blue", "Simon_Green", "Simon_Yellow" };
            var so        = new SerializedObject(simon);
            var rendProp  = so.FindProperty("padRenderers");
            var intProp   = so.FindProperty("padInteractables");
            rendProp.arraySize = 4;
            intProp.arraySize  = 4;
            for (int i = 0; i < 4; i++)
            {
                var padT = bombRoot.Find(padNames[i]);
                rendProp.GetArrayElementAtIndex(i).objectReferenceValue =
                    padT != null ? padT.GetComponent<Renderer>() : null;
                intProp.GetArrayElementAtIndex(i).objectReferenceValue =
                    padT != null ? padT.GetComponent<XRSimpleInteractable>() : null;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── Solved indicator lights ───────────────────────────────────────────
        void WireSolved(string childName, UnityEngine.Object target, string field)
        {
            var t = bombRoot.Find(childName);
            if (t == null || target == null) return;
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = t.GetComponent<Renderer>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        WireSolved("Wire_Solved",   wires,  "solvedLight");
        WireSolved("Button_Solved", button, "solvedLight");
        WireSolved("Keypad_Solved", keypad, "solvedLight");
        WireSolved("Simon_Solved",  simon,  "solvedLight");

        Debug.Log("[BuildKTANEScene] All module Inspector slots wired automatically.");
    }

    // ── 14 ────────────────────────────────────────────────────────────────────
    private static void CreateLobbyManager(
        Transform    bombMount,
        TimerModule  timer,
        WiresModule  wires,
        ButtonModule button,
        KeypadModule keypad,
        SimonModule  simon)
    {
        var go    = new GameObject("KTANELobbyManager");
        var lobby = go.AddComponent<KTANELobbyManager>();

        var so = new SerializedObject(lobby);
        so.FindProperty("timerModule").objectReferenceValue  = timer;
        so.FindProperty("wiresModule").objectReferenceValue  = wires;
        so.FindProperty("buttonModule").objectReferenceValue = button;
        so.FindProperty("keypadModule").objectReferenceValue = keypad;
        so.FindProperty("simonModule").objectReferenceValue  = simon;
        so.FindProperty("bombMount").objectReferenceValue    = bombMount;
        so.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("[BuildKTANEScene] KTANELobbyManager created and wired.");
    }

    // ── 15 ────────────────────────────────────────────────────────────────────
    private static void CreateManualTablet()
    {
        var go = new GameObject("ManualTablet");
        go.AddComponent<ManualTablet>();

        // Place the tablet on the Defuser's side of the table, within easy reach.
        // y = 0.9 m (table top + a bit) so it sits naturally near the bomb.
        go.transform.position = new Vector3(-0.55f, 0.9f, -0.3f);
        // Tilt face-up slightly so it's readable when resting on the table
        go.transform.rotation = Quaternion.Euler(70f, 0f, 0f);

        Debug.Log("[BuildKTANEScene] ManualTablet placed next to the bomb.");
    }

    // ==========================================================================
    // Shared helpers
    // ==========================================================================

    /// <summary>
    /// Assigns a simple URP/Lit (or Standard fallback) material with the given
    /// albedo colour to the renderer on <paramref name="go"/>.
    /// A new material instance is created so objects don't share the same asset.
    /// </summary>
    private static void SetMaterial(GameObject go, Color colour)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer == null) return;

        var shader = Shader.Find("Universal Render Pipeline/Lit")
                  ?? Shader.Find("Standard");
        if (shader == null) return;

        var mat = new Material(shader);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", colour);
        else if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", colour);

        renderer.sharedMaterial = mat;
    }
}
