using UnityEngine;

public class MaterialShaderFix : MonoBehaviour
{
    [SerializeField] private bool forceShader = true;
    [SerializeField] private string[] shaderNames = new[]
    {
        "Universal Render Pipeline/Lit",
        "Standard"
    };

    private void Awake()
    {
        var renderer = GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        var material = renderer.material;
        if (material == null)
        {
            return;
        }

        var needsFix = forceShader || material.shader == null || material.shader.name == "Hidden/InternalErrorShader";
        if (!needsFix)
        {
            return;
        }

        foreach (var name in shaderNames)
        {
            var shader = Shader.Find(name);
            if (shader != null)
            {
                material.shader = shader;
                break;
            }
        }
    }
}
