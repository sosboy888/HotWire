using System.IO;
using UnityEditor;
using UnityEngine;

public static class GenerateBombPrefab
{
    private const string PrefabPath = "Assets/Prefabs/BombRoot.prefab";
    private const string MaterialFolder = "Assets/Materials/Bomb";

    [MenuItem("Tools/Bomb/Create BombRoot Prefab")]
    public static void CreateBombRootPrefab()
    {
        EnsureFolders();

        var bodyMat = CreateOrLoadMaterial(
            Path.Combine(MaterialFolder, "BombBody.mat"),
            new Color(0.18f, 0.18f, 0.18f, 1f),
            emissive: false);

        var panelMat = CreateOrLoadMaterial(
            Path.Combine(MaterialFolder, "BombPanel.mat"),
            new Color(0.28f, 0.28f, 0.28f, 1f),
            emissive: false);

        var lightMat = CreateOrLoadMaterial(
            Path.Combine(MaterialFolder, "BombLight.mat"),
            new Color(0.75f, 0.2f, 0.2f, 1f),
            emissive: true);

        var root = new GameObject("BombRoot");

        // Body
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = new Vector3(0.3f, 0.2f, 0.15f);
        SetMaterial(body, bodyMat);

        // Panels
        CreatePanel(root.transform, "Panel_Front", panelMat, lightMat, Vector3.forward, 0.005f);
        CreatePanel(root.transform, "Panel_Back", panelMat, lightMat, Vector3.back, 0.005f);
        CreatePanel(root.transform, "Panel_Left", panelMat, lightMat, Vector3.left, 0.005f);
        CreatePanel(root.transform, "Panel_Right", panelMat, lightMat, Vector3.right, 0.005f);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        if (prefab != null)
        {
            Debug.Log($"Bomb prefab saved at {PrefabPath}");
        }

        Object.DestroyImmediate(root);
    }

    private static void CreatePanel(Transform parent, string name, Material panelMat, Material lightMat, Vector3 outward, float inset)
    {
        var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = name;
        panel.transform.SetParent(parent, false);

        var bodySize = new Vector3(0.3f, 0.2f, 0.15f);
        var panelSize = new Vector3(0.28f, 0.18f, 0.01f);

        if (outward == Vector3.forward || outward == Vector3.back)
        {
            panelSize = new Vector3(0.28f, 0.18f, 0.01f);
            var z = (bodySize.z * 0.5f) - (panelSize.z * 0.5f) - inset;
            panel.transform.localPosition = new Vector3(0f, 0f, z * outward.z);
            panel.transform.localScale = panelSize;
        }
        else
        {
            panelSize = new Vector3(0.01f, 0.18f, 0.13f);
            var x = (bodySize.x * 0.5f) - (panelSize.x * 0.5f) - inset;
            panel.transform.localPosition = new Vector3(x * outward.x, 0f, 0f);
            panel.transform.localScale = panelSize;
        }

        SetMaterial(panel, panelMat);

        // Module socket
        var socket = new GameObject($"ModuleSocket_{name.Replace("Panel_", "")}");
        socket.transform.SetParent(panel.transform, false);
        socket.transform.localPosition = outward * 0.02f;
        socket.transform.localRotation = Quaternion.LookRotation(outward, Vector3.up);

        // Label
        var label = new GameObject($"Label_{name.Replace("Panel_", "")}");
        label.transform.SetParent(panel.transform, false);
        label.transform.localPosition = outward * 0.03f + Vector3.up * 0.035f;
        label.transform.localRotation = Quaternion.LookRotation(outward, Vector3.up);

        var text = label.AddComponent<TextMesh>();
        text.text = name.Replace("Panel_", "");
        text.fontSize = 80;
        text.characterSize = 0.03f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = Color.white;

        var textRenderer = label.GetComponent<MeshRenderer>();
        if (textRenderer != null && lightMat != null)
        {
            textRenderer.sharedMaterial = lightMat;
        }
    }

    private static void SetMaterial(GameObject go, Material mat)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null && mat != null)
        {
            renderer.sharedMaterial = mat;
        }
    }

    private static Material CreateOrLoadMaterial(string path, Color color, bool emissive)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null)
        {
            return mat;
        }

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null)
        {
            shader = Shader.Find("Diffuse");
        }

        mat = new Material(shader);
        ApplyColor(mat, color);

        if (emissive)
        {
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.SetColor("_EmissionColor", color);
            }
        }

        AssetDatabase.CreateAsset(mat, path);
        AssetDatabase.SaveAssets();
        return mat;
    }

    private static void ApplyColor(Material mat, Color color)
    {
        if (mat == null)
        {
            return;
        }

        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color);
        }
        else if (mat.HasProperty("_Color"))
        {
            mat.SetColor("_Color", color);
        }
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
        }

        if (!AssetDatabase.IsValidFolder(MaterialFolder))
        {
            AssetDatabase.CreateFolder("Assets/Materials", "Bomb");
        }
    }
}
