using UnityEngine;
using UnityEditor;

public class CreditCardSetup : MonoBehaviour
{
    [Header("Setup Options")]
    public bool createDefaultFolders = true;
    public bool setupDefaultMaterials = true;
    public bool createSampleTextures = true;
    
    [Header("Card Model")]
    public GameObject cardModel;
    public Material frontMaterial;
    public Material backMaterial;
    
    void Start()
    {
        if (createDefaultFolders)
        {
            CreateDefaultFolders();
        }
        
        if (setupDefaultMaterials)
        {
            SetupDefaultMaterials();
        }
        
        if (createSampleTextures)
        {
            CreateSampleTextures();
        }
        
        Debug.Log("Credit Card Setup Complete!");
    }
    
    void CreateDefaultFolders()
    {
        string[] folders = {
            "Assets/FrontImages",
            "Assets/ChipImages", 
            "Assets/LogoImages",
            "Assets/BankLogoImages",
            "Assets/SkyboxImages",
            "Assets/Fonts",
            "Assets/Prefabs"
        };
        
        foreach (string folder in folders)
        {
            if (!System.IO.Directory.Exists(folder))
            {
                System.IO.Directory.CreateDirectory(folder);
                Debug.Log($"Created folder: {folder}");
            }
        }
    }
    
    void SetupDefaultMaterials()
    {
        // Create default front material
        if (frontMaterial == null)
        {
            frontMaterial = new Material(Shader.Find("Standard"));
            frontMaterial.name = "CardFront";
            frontMaterial.color = Color.white;
            
            // Save the material
            #if UNITY_EDITOR
            AssetDatabase.CreateAsset(frontMaterial, "Assets/card_front.mat");
            #endif
        }
        
        // Create default back material
        if (backMaterial == null)
        {
            backMaterial = new Material(Shader.Find("Standard"));
            backMaterial.name = "CardBack";
            backMaterial.color = Color.gray;
            
            // Save the material
            #if UNITY_EDITOR
            AssetDatabase.CreateAsset(backMaterial, "Assets/card_back.mat");
            #endif
        }
    }
    
    void CreateSampleTextures()
    {
        // Create a simple sample texture for testing
        Texture2D sampleTexture = new Texture2D(256, 256);
        
        // Create a gradient pattern
        for (int x = 0; x < 256; x++)
        {
            for (int y = 0; y < 256; y++)
            {
                float r = (float)x / 256f;
                float g = (float)y / 256f;
                float b = 0.5f;
                
                sampleTexture.SetPixel(x, y, new Color(r, g, b));
            }
        }
        
        sampleTexture.Apply();
        
        // Save as PNG
        byte[] pngData = sampleTexture.EncodeToPNG();
        string filepath = "Assets/FrontImages/sample_texture.png";
        System.IO.File.WriteAllBytes(filepath, pngData);
        
        Debug.Log($"Created sample texture: {filepath}");
    }
    
    // Editor helper method
    [ContextMenu("Setup Credit Card Generator")]
    public void SetupInEditor()
    {
        CreateDefaultFolders();
        SetupDefaultMaterials();
        CreateSampleTextures();
        
        #if UNITY_EDITOR
        AssetDatabase.Refresh();
        #endif
        
        Debug.Log("Credit Card Generator setup complete in editor!");
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(CreditCardSetup))]
public class CreditCardSetupEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        CreditCardSetup setup = (CreditCardSetup)target;
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Setup Credit Card Generator"))
        {
            setup.SetupInEditor();
        }
        
        EditorGUILayout.Space();
        
        EditorGUILayout.HelpBox(
            "This will create the necessary folders and default assets for the credit card generator. " +
            "Make sure to add your own textures, fonts, and card model after setup.",
            MessageType.Info
        );
    }
}
#endif 