using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

public class HDRPSkyboxHelper : MonoBehaviour
{
    [Header("HDRP Skybox Settings")]
    public bool useHDRI = true;
    public bool useGradient = false;
    public float exposure = 1f;
    public float multiplier = 1f;
    public Color topColor = new Color(0.3f, 0.7f, 1f, 1f);
    public Color middleColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    public Color bottomColor = new Color(1f, 1f, 1f, 1f);
    
    [Header("HDRI Settings")]
    public Texture2D hdriTexture;
    public Cubemap hdriCubemap;
    public float hdriRotation = 0f;
    public bool enableDistortion = false;
    public Texture2D flowmap;
    
    public Material CreateHDRPSkyboxMaterial()
    {
        Material skyboxMat = null;
        
        if (useHDRI)
        {
            // Try HDRP HDRI skybox shader
            Shader hdriShader = Shader.Find("Hidden/HDRP/Skybox/Cubemap");
            if (hdriShader != null)
            {
                skyboxMat = new Material(hdriShader);
                SetupHDRIProperties(skyboxMat);
            }
            else
            {
                //Debug.LogWarning("HDRP HDRI skybox shader not found");
                return null;
            }
        }
        else if (useGradient)
        {
            // Try HDRP gradient skybox shader
            Shader gradientShader = Shader.Find("Hidden/HDRP/Skybox/Gradient");
            if (gradientShader != null)
            {
                skyboxMat = new Material(gradientShader);
                SetupGradientProperties(skyboxMat);
            }
            else
            {
                //gWarning("HDRP gradient skybox shader not found");
                return null;
            }
        }
        
        return skyboxMat;
    }
    
    void SetupHDRIProperties(Material material)
    {
        // Prefer cubemap over 2D texture for HDRP
        if (hdriCubemap != null)
        {
            material.SetTexture("_Tex", hdriCubemap);
        }
        else if (hdriTexture != null)
        {
            // Convert 2D texture to cubemap
            Cubemap cubemap = ConvertToCubemap(hdriTexture);
            if (cubemap != null)
            {
                material.SetTexture("_Tex", cubemap);
            }
            else
            {
                material.SetTexture("_Tex", hdriTexture);
            }
        }
        
        material.SetFloat("_Rotation", hdriRotation);
        material.SetFloat("_Exposure", exposure);
        material.SetFloat("_Multiplier", multiplier);
        
        if (enableDistortion && flowmap != null)
        {
            material.SetTexture("_Flowmap", flowmap);
            material.SetFloat("_DistortionMode", 1f);
        }
        else
        {
            material.SetFloat("_DistortionMode", 0f);
        }
    }
    
    void SetupGradientProperties(Material material)
    {
        material.SetColor("_Top", topColor);
        material.SetColor("_Middle", middleColor);
        material.SetColor("_Bottom", bottomColor);
        material.SetFloat("_Exposure", exposure);
        material.SetFloat("_Multiplier", multiplier);
        material.SetFloat("_GradientDiffusion", 1f);
    }
    
    Cubemap ConvertToCubemap(Texture2D sourceTexture)
    {
        // Create a simple cubemap from the 2D texture
        // This is a basic conversion - for better results, use proper cubemap textures
        int size = Mathf.Max(sourceTexture.width, sourceTexture.height);
        Cubemap cubemap = new Cubemap(size, TextureFormat.RGB24, false);
        
        // Fill each face with the source texture
        for (int face = 0; face < 6; face++)
        {
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;
                    Color color = sourceTexture.GetPixelBilinear(u, v);
                    cubemap.SetPixel((CubemapFace)face, x, y, color);
                }
            }
        }
        
        cubemap.Apply();
        return cubemap;
    }
    
    public bool IsHDRPActive()
    {
        return GraphicsSettings.defaultRenderPipeline != null && 
               GraphicsSettings.defaultRenderPipeline.GetType().Name.Contains("HDRenderPipelineAsset");
    }
    
    public void ApplySkyboxToRenderSettings(Material skyboxMaterial)
    {
        if (skyboxMaterial != null)
        {
            RenderSettings.skybox = skyboxMaterial;
            //Debug.Log("Applied HDRP skybox to render settings");
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(HDRPSkyboxHelper))]
public class HDRPSkyboxHelperEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        HDRPSkyboxHelper helper = (HDRPSkyboxHelper)target;
        
        EditorGUILayout.Space();
        
        // Show HDRP status
        bool isHDRP = helper.IsHDRPActive();
        EditorGUILayout.LabelField("HDRP Active:", isHDRP ? "Yes" : "No");
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Create HDRP Skybox Material"))
        {
            Material skyboxMat = helper.CreateHDRPSkyboxMaterial();
            if (skyboxMat != null)
            {
                helper.ApplySkyboxToRenderSettings(skyboxMat);
                
                // Save the material
                AssetDatabase.CreateAsset(skyboxMat, "Assets/HDRPSkyboxMaterial.mat");
                AssetDatabase.Refresh();
                
                //Debug.Log("Created and applied HDRP skybox material");
            }
        }
        
        EditorGUILayout.Space();
        
        EditorGUILayout.HelpBox(
            "This helper creates HDRP-compatible skybox materials. " +
            "For HDRI skyboxes, assign an EXR texture. " +
            "For gradient skyboxes, adjust the color settings.",
            MessageType.Info
        );
    }
}
#endif 