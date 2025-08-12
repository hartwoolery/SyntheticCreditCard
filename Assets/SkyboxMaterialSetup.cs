using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

public class SkyboxMaterialSetup : MonoBehaviour
{
    [Header("Skybox Setup")]
    public bool createSkyboxMaterial = true;
    public string skyboxMaterialName = "CreditCardSkybox";
    
    [Header("HDRP Settings")]
    public bool useHDRP = true;
    public bool useHDRI = true;
    
    void Start()
    {
        if (createSkyboxMaterial)
        {
            CreateSkyboxMaterial();
        }
    }
    
    void CreateSkyboxMaterial()
    {
        // Detect if we're using HDRP
        bool isHDRP = GraphicsSettings.defaultRenderPipeline != null && 
                      GraphicsSettings.defaultRenderPipeline.GetType().Name.Contains("HDRenderPipelineAsset");
        
        Material skyboxMat = null;
        
        if (isHDRP)
        {
            // Create HDRP skybox material
            if (useHDRI)
            {
                // Use HDRP HDRI skybox shader
                Shader hdriShader = Shader.Find("Hidden/HDRP/Skybox/Cubemap");
                if (hdriShader != null)
                {
                    skyboxMat = new Material(hdriShader);
                }
                else
                {
                    // Fallback to built-in skybox shader
                    skyboxMat = new Material(Shader.Find("Skybox/6 Sided"));
                    Debug.LogWarning("HDRP HDRI skybox shader not found, using built-in shader");
                }
            }
            else
            {
                // Use HDRP gradient skybox shader
                Shader gradientShader = Shader.Find("Hidden/HDRP/Skybox/Gradient");
                if (gradientShader != null)
                {
                    skyboxMat = new Material(gradientShader);
                }
                else
                {
                    // Fallback to built-in skybox shader
                    skyboxMat = new Material(Shader.Find("Skybox/6 Sided"));
                    Debug.LogWarning("HDRP gradient skybox shader not found, using built-in shader");
                }
            }
        }
        else
        {
            // Use built-in render pipeline skybox shader
            skyboxMat = new Material(Shader.Find("Skybox/6 Sided"));
        }
        
        if (skyboxMat == null)
        {
            Debug.LogError("Failed to create skybox material - no compatible shader found");
            return;
        }
        
        skyboxMat.name = skyboxMaterialName;
        
        // Set default properties
        if (isHDRP && useHDRI)
        {
            // HDRP HDRI skybox properties
            skyboxMat.SetFloat("_Rotation", 0f);
            skyboxMat.SetFloat("_Exposure", 1f);
            skyboxMat.SetFloat("_Multiplier", 1f);
        }
        else if (isHDRP && !useHDRI)
        {
            // HDRP gradient skybox properties
            skyboxMat.SetFloat("_Rotation", 0f);
            skyboxMat.SetFloat("_Exposure", 0f);
            skyboxMat.SetFloat("_Multiplier", 1f);
            skyboxMat.SetColor("_Top", new Color(0.3f, 0.7f, 1f, 1f));
            skyboxMat.SetColor("_Middle", new Color(0.5f, 0.5f, 0.5f, 1f));
            skyboxMat.SetColor("_Bottom", new Color(1f, 1f, 1f, 1f));
        }
        else
        {
            // Built-in render pipeline properties
            skyboxMat.SetFloat("_Rotation", 0f);
            skyboxMat.SetFloat("_Exposure", 1f);
        }
        
        // Save the material
        #if UNITY_EDITOR
        AssetDatabase.CreateAsset(skyboxMat, "Assets/" + skyboxMaterialName + ".mat");
        AssetDatabase.Refresh();
        #endif
        
        // Set as current skybox
        RenderSettings.skybox = skyboxMat;
        
        Debug.Log($"Created skybox material: {skyboxMaterialName} (HDRP: {isHDRP}, HDRI: {useHDRI})");
    }
    
    // Editor helper method
    [ContextMenu("Create Skybox Material")]
    public void CreateSkyboxMaterialInEditor()
    {
        CreateSkyboxMaterial();
        
        #if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        #endif
    }
    
    // Method to detect render pipeline
    public bool IsUsingHDRP()
    {
        return GraphicsSettings.defaultRenderPipeline != null && 
               GraphicsSettings.defaultRenderPipeline.GetType().Name.Contains("HDRenderPipelineAsset");
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(SkyboxMaterialSetup))]
public class SkyboxMaterialSetupEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        SkyboxMaterialSetup setup = (SkyboxMaterialSetup)target;
        
        EditorGUILayout.Space();
        
        // Show render pipeline info
        bool isHDRP = setup.IsUsingHDRP();
        EditorGUILayout.LabelField("Render Pipeline:", isHDRP ? "HDRP" : "Built-in");
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Create Skybox Material"))
        {
            setup.CreateSkyboxMaterialInEditor();
        }
        
        EditorGUILayout.Space();
        
        string helpText = isHDRP 
            ? "This will create an HDRP-compatible skybox material for the credit card generator. " +
              "Place EXR files in the SkyboxImages folder to use as backgrounds."
            : "This will create a built-in skybox material for the credit card generator. " +
              "Place EXR files in the SkyboxImages folder to use as backgrounds.";
        
        EditorGUILayout.HelpBox(helpText, MessageType.Info);
    }
}
#endif 