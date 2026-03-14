// CreateLevelAssets.cs
// Editor utility that creates the five KTANELevelConfig ScriptableObject assets
// required by KTANELobbyManager at runtime.
//
// HOW TO USE:
//   Tools ▶ KTANE ▶ Create Level Assets
//
// OUTPUT:
//   Assets/Resources/KTANE/Levels/
//     Level_1_Training.asset
//     Level_2_Novice.asset
//     Level_3_Intermediate.asset
//     Level_4_Advanced.asset
//     Level_5_Expert.asset
//
// Re-running this tool is safe – existing assets are overwritten.

using UnityEditor;
using UnityEngine;
using KTANE;

public static class CreateLevelAssets
{
    private const string OutputFolder = "Assets/Resources/KTANE/Levels";

    [MenuItem("Tools/KTANE/Create Level Assets")]
    public static void Create()
    {
        EnsureFolders();

        CreateLevel(
            fileName:        "Level_1_Training",
            levelName:       "Training",
            levelNumber:     1,
            description:     "One module, plenty of time. Read the manual first!",
            accentColour:    Color.green,
            timerSeconds:    420f,   // 7 minutes
            maxStrikes:      3,
            wiresActive:     true,
            buttonActive:    false,
            keypadActive:    false,
            simonActive:     false,
            simonRounds:     3,
            simonFlash:      0.5f,
            hardWireRules:   false
        );

        CreateLevel(
            fileName:        "Level_2_Novice",
            levelName:       "Novice",
            levelNumber:     2,
            description:     "Wires and a button. Talk carefully before cutting.",
            accentColour:    new Color(0.4f, 0.8f, 1f),
            timerSeconds:    360f,   // 6 minutes
            maxStrikes:      3,
            wiresActive:     true,
            buttonActive:    true,
            keypadActive:    false,
            simonActive:     false,
            simonRounds:     3,
            simonFlash:      0.5f,
            hardWireRules:   false
        );

        CreateLevel(
            fileName:        "Level_3_Intermediate",
            levelName:       "Intermediate",
            levelNumber:     3,
            description:     "Three modules active. Speed and accuracy required.",
            accentColour:    Color.yellow,
            timerSeconds:    300f,   // 5 minutes
            maxStrikes:      3,
            wiresActive:     true,
            buttonActive:    true,
            keypadActive:    true,
            simonActive:     false,
            simonRounds:     3,
            simonFlash:      0.5f,
            hardWireRules:   false
        );

        CreateLevel(
            fileName:        "Level_4_Advanced",
            levelName:       "Advanced",
            levelNumber:     4,
            description:     "All four modules. Only two mistakes allowed.",
            accentColour:    new Color(1f, 0.5f, 0f),
            timerSeconds:    240f,   // 4 minutes
            maxStrikes:      2,
            wiresActive:     true,
            buttonActive:    true,
            keypadActive:    true,
            simonActive:     true,
            simonRounds:     4,
            simonFlash:      0.4f,
            hardWireRules:   false
        );

        CreateLevel(
            fileName:        "Level_5_Expert",
            levelName:       "Expert",
            levelNumber:     5,
            description:     "Harder wire rules, faster Simon, two mistakes max. Good luck.",
            accentColour:    Color.red,
            timerSeconds:    180f,   // 3 minutes
            maxStrikes:      2,
            wiresActive:     true,
            buttonActive:    true,
            keypadActive:    true,
            simonActive:     true,
            simonRounds:     5,
            simonFlash:      0.25f,
            hardWireRules:   true
        );

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[CreateLevelAssets] 5 level assets created in {OutputFolder}");
        EditorUtility.DisplayDialog(
            "Level Assets Created",
            $"5 level configs saved to:\n{OutputFolder}\n\n" +
            "They are now available to KTANELobbyManager at runtime.",
            "OK");
    }

    // ─────────────────────────────────────────────────────────────────────────

    private static void CreateLevel(
        string fileName,
        string levelName,
        int    levelNumber,
        string description,
        Color  accentColour,
        float  timerSeconds,
        int    maxStrikes,
        bool   wiresActive,
        bool   buttonActive,
        bool   keypadActive,
        bool   simonActive,
        int    simonRounds,
        float  simonFlash,
        bool   hardWireRules)
    {
        string path = $"{OutputFolder}/{fileName}.asset";

        var cfg = AssetDatabase.LoadAssetAtPath<KTANELevelConfig>(path);
        if (cfg == null)
        {
            cfg = ScriptableObject.CreateInstance<KTANELevelConfig>();
            AssetDatabase.CreateAsset(cfg, path);
        }

        // Fill values via SerializedObject so they are saved correctly
        var so = new SerializedObject(cfg);
        so.FindProperty("levelName").stringValue       = levelName;
        so.FindProperty("levelNumber").intValue        = levelNumber;
        so.FindProperty("description").stringValue     = description;
        so.FindProperty("accentColour").colorValue     = accentColour;
        so.FindProperty("timerSeconds").floatValue     = timerSeconds;
        so.FindProperty("maxStrikes").intValue         = maxStrikes;
        so.FindProperty("wiresActive").boolValue       = wiresActive;
        so.FindProperty("buttonActive").boolValue      = buttonActive;
        so.FindProperty("keypadActive").boolValue      = keypadActive;
        so.FindProperty("simonActive").boolValue       = simonActive;
        so.FindProperty("simonRounds").intValue        = simonRounds;
        so.FindProperty("simonFlashSeconds").floatValue = simonFlash;
        so.FindProperty("hardWireRules").boolValue     = hardWireRules;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(cfg);
        Debug.Log($"[CreateLevelAssets] Created {path}");
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/KTANE"))
            AssetDatabase.CreateFolder("Assets/Resources", "KTANE");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/KTANE/Levels"))
            AssetDatabase.CreateFolder("Assets/Resources/KTANE", "Levels");
    }
}
