// 3DTo2DRenderer.cs
using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class MainRenderer : MonoBehaviour
{
    public Shader renderShader;
    [Range(1, 8)] public int colorLevels = 4;
    [Range(0, 1)] public float edgeThreshold = 0.2f;
    public Color edgeColor = Color.black;
    [Range(0.5f, 2.0f)] public float brightness = 1.0f;

    private Material _renderMaterial;

    private void OnEnable()
    {
        GetComponent<Camera>().depthTextureMode |= DepthTextureMode.DepthNormals;
        _renderMaterial = new Material(renderShader);
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (_renderMaterial == null) return;

        // 确保边缘颜色的alpha值不为0
        Color safeEdgeColor = edgeColor;
        if (safeEdgeColor.a <= 0)
        {
            safeEdgeColor.a = 0.001f; // 使用一个极小的非零值
        }

        _renderMaterial.SetInt("_ColorLevels", colorLevels);
        _renderMaterial.SetFloat("_EdgeThreshold", edgeThreshold);
        _renderMaterial.SetColor("_EdgeColor", safeEdgeColor);
        _renderMaterial.SetFloat("_Brightness", brightness);

        Graphics.Blit(source, destination, _renderMaterial);
    }
}