using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine.Rendering;

public class Synthesize : MonoBehaviour
{
    [Header("Card Model")]
    public GameObject cardModel;
    public Material frontMaterial;
    
    [Header("Lighting Settings")]
    public bool setupLighting = true;
    public float mainLightIntensity = 0.3f;
    public float fillLightIntensity = 0.1f;
    public float emissionIntensity = 0.0f;
    public Color mainLightColor = Color.white;
    public Color fillLightColor = new Color(0.8f, 0.8f, 1.0f); // Slight blue tint for fill
    public float exposure = 0.5f;
    [Range(0.1f, 2.0f)] public float exposureRange = 1.0f;
    
    [Header("Camera Setup")]
    public Camera renderCamera;
    public Transform cameraTransform;
    
    [Header("Asset Folders")]
    public string frontImagesFolder = "Assets/FrontImages/";
    public string chipImagesFolder = "Assets/ChipImages/";
    public string logoImagesFolder = "Assets/LogoImages/";
    public string bankLogoImagesFolder = "Assets/BankLogoImages/";
    public string skyboxImagesFolder = "Assets/SkyboxImages/";
    public string fontsFolder = "Assets/Fonts/";
    
    int imageWidth = 1024;
    int imageHeight = 1024;
    int imagesToGenerate = 10;
    
    [Header("Randomization")]
    [Range(0f, 1f)] public float minColorBlend = 0.2f;
    [Range(0f, 1f)] public float maxColorBlend = 0.8f;
    [Range(0f, 15f)] public float maxRotationAngle = 2.5f;
    
    public string[] cardBrands = { "Visa", "Mastercard", "American Express", "Discover" };
    public string[] cardTypes = { "Credit", "Debit", "Prepaid" };
    
    [Header("Text Generation")]
    public CreditCardTextGenerator textGenerator;
    public bool textOnFront = true;
    public float textOnFrontProbability = 0.7f;
    
    [Header("Logo Placement")]
    float chipOpacity = 1.0f;
    float brandLogoOpacity = 1.0f;
    float bankLogoOpacity = 1.0f;
    
    // EMV chip positioning based on real-world specifications
    // Card: 3.37" x 2.125", Chip: 0.85" x 0.71"
    // Chip position from left edge: 0.34", from top edge: 0.46"
    // Converting to center-based coordinates (0,0 at card center)
    public Vector2 chipPosition = new Vector2(-0.4f, 0.0f); // Calculated from specs
    
    [Header("Logo Padding")]
    float logoPadding = 0.04f; // Padding from chip edges
    
    public Vector2 brandLogoPosition = new Vector2(0.4f, 0.2f);
    public Vector2 bankLogoPosition = new Vector2(0.4f, 0.4f);
    
    float chipSizeRatio = 0.15f; // Percentage of card width
    float brandLogoSizeRatio = 0.2f; // Percentage of card width
    float bankLogoSizeRatio = 0.1f; // Percentage of card width
    
    [Header("Magnetic Stripe")]
    public bool useMagneticStripe = true;
    [Range(0f, 1f)] public float magneticStripeProbability = 0.3f;
    public MagneticStripeGenerator magneticStripeGenerator = null;
    
    [Header("Card Scaling")]
    public bool useStandardCardDimensions = true;
    public float cardWidthInches = 3.375f;
    public float cardHeightInches = 2.125f;
    public float cardThicknessInches = 0.03f;
    
    [Header("Skybox Settings")]
    public bool useRandomSkybox = true;
    [Range(0f, 360f)] public float maxSkyboxRotation = 360f;
    public UnityEngine.Rendering.VolumeProfile defaultVolumeProfile;
    
    private RenderTexture renderTexture;
    private Texture2D captureTexture;
    private List<Texture2D> frontTextures = new List<Texture2D>();
    private List<Texture2D> chipTextures = new List<Texture2D>();
    private List<Texture2D> logoTextures = new List<Texture2D>();
    private List<Texture2D> bankLogoTextures = new List<Texture2D>();
    private List<Texture2D> skyboxTextures = new List<Texture2D>();
    private List<Cubemap> skyboxCubemaps = new List<Cubemap>();
    private List<Font> availableFonts = new List<Font>();
    private List<Color> cardColors = new List<Color>();
    
    // Performance optimization: Texture pooling
    private Queue<Texture2D> texturePool = new Queue<Texture2D>();
    private const int MAX_POOL_SIZE = 10;
    
    private int currentImageIndex = 0;
    private bool isGenerating = false;
    private List<GameObject> addedElements = new List<GameObject>();
    
    // Performance monitoring
    private System.Diagnostics.Stopwatch generationTimer = new System.Diagnostics.Stopwatch();
    
    void Start()
    {
        InitializeSystem();
        LoadAssets();
        SetupCamera();
        SetupMagneticStripe();
       ///SetupLighting();
        SetupRenderTexture();
        DisableGlobalPostProcessing();
        
        // Start generation
        GenerateCreditCards();
    }
    
    void SetupMagneticStripe()
    {
        if (magneticStripeGenerator == null)
        {
            magneticStripeGenerator = gameObject.AddComponent<MagneticStripeGenerator>();
        }
    }
    void DisableGlobalPostProcessing()
    {
        try
        {
            // Disable any global post-processing settings
            QualitySettings.antiAliasing = 0; // Disable antialiasing
            QualitySettings.softParticles = false; // Disable soft particles
            QualitySettings.realtimeReflectionProbes = false; // Disable real-time reflection probes
            
            // Disable any post-processing volumes in the scene
            var allVolumes = FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None);
            foreach (var volume in allVolumes)
            {
                if (volume.isGlobal)
                {
                    volume.enabled = false;
                    Debug.Log($"Disabled global post-processing volume: {volume.name}");
                }
            }
            
            Debug.Log("Global post-processing settings disabled");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Could not disable global post-processing: {e.Message}");
        }
    }
    
    void SetupLighting()
    {
        if (!setupLighting) return;
        
        try
        {
            // Find or create main light
            Light mainLight = FindFirstObjectByType<Light>();
            if (mainLight == null)
            {
                GameObject mainLightGO = new GameObject("Main Light");
                mainLight = mainLightGO.AddComponent<Light>();
                Debug.Log("Added Main Light");
            }
            
            // Configure main light
            mainLight.type = LightType.Directional;
            mainLight.intensity = mainLightIntensity;
            mainLight.color = mainLightColor;
            mainLight.shadows = LightShadows.Soft;
            mainLight.shadowStrength = 0.5f;
            
            // Set emission intensity for HDRP
            var hdLightData = mainLight.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
            if (hdLightData != null)
            {
                //hdLightData.intensity = mainLightIntensity;
                //hdLightData.luxAtDistance = emissionIntensity; // Use emission intensity setting
                hdLightData.affectDiffuse = true;
                hdLightData.affectSpecular = false; // Disable specular to prevent glow
                Debug.Log($"Configured HDRP light emission settings - Intensity: {mainLightIntensity}, Emission: {emissionIntensity}");
            }
            
            // Position main light
            mainLight.transform.rotation = Quaternion.Euler(45f, 45f, 0f);
            
            // // Create fill light
            // GameObject fillLightGO = new GameObject("Fill Light");
            // Light fillLight = fillLightGO.AddComponent<Light>();
            // fillLight.type = LightType.Directional;
            // fillLight.intensity = fillLightIntensity;
            // fillLight.color = fillLightColor;
            // fillLight.shadows = LightShadows.None;
            
            // // Position fill light opposite to main light
            // fillLight.transform.rotation = Quaternion.Euler(-45f, -45f, 0f);
            
            // Set exposure if using HDRP
            SetExposure();
            
            Debug.Log("Lighting setup complete");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error setting up lighting: {e.Message}");
        }
    }
    
    void SetExposure()
    {
        // HDRP exposure control through Volume system
        if (defaultVolumeProfile != null)
        {
            // Try to get exposure component using the correct API
            if (defaultVolumeProfile.TryGet(out UnityEngine.Rendering.HighDefinition.Exposure exposureComponent))
            {
                exposureComponent.fixedExposure.value = exposure;
                Debug.Log($"Set HDRP exposure to {exposure} via Default Volume Profile");
            }
            else
            {
                Debug.LogWarning("Could not find Exposure component in Default Volume Profile");
            }
        }
        else
        {
            Debug.LogWarning("Default Volume Profile not assigned - cannot set HDRP exposure");
        }
        
        Debug.Log($"Set HDRP exposure to {exposure}");
    }
    
    void InitializeSystem()
    {
        try
        {
            // Create card colors
            cardColors.AddRange(new Color[] {
                Color.white,
                Color.black,
                new Color(0.1f, 0.1f, 0.3f), // Dark blue
                new Color(0.3f, 0.1f, 0.1f), // Dark red
                new Color(0.1f, 0.3f, 0.1f), // Dark green
                new Color(0.8f, 0.6f, 0.2f), // Gold
                new Color(0.2f, 0.2f, 0.2f), // Dark gray
                new Color(0.9f, 0.9f, 0.9f)  // Light gray
            });
            
            // Find card model if not assigned
            if (cardModel == null)
            {
                cardModel = GameObject.Find("card");
                if (cardModel == null)
                {
                    Debug.LogWarning("Card model not found. Please assign a card model in the inspector.");
                }
            }
            
            // Scale card to standard dimensions
            // if (useStandardCardDimensions && cardModel != null)
            // {
            //     ScaleCardToStandardDimensions();
            // }
            
            // Find camera if not assigned
            if (renderCamera == null)
            {
                renderCamera = Camera.main;
                if (renderCamera == null)
                {
                    Debug.LogWarning("No camera found. Please assign a camera in the inspector.");
                }
            }
            
            if (cameraTransform == null && renderCamera != null)
            {
                cameraTransform = renderCamera.transform;
            }
            
          
            // Ensure name generator is available
            NameGenerator nameGenerator = GetComponent<NameGenerator>();
            if (nameGenerator == null)
            {
                nameGenerator = gameObject.AddComponent<NameGenerator>();
                Debug.Log("Added NameGenerator component");
            }
            
            // Find or add text generator
            textGenerator = GetComponent<CreditCardTextGenerator>();
            if (textGenerator == null)
            {
                textGenerator = gameObject.AddComponent<CreditCardTextGenerator>();
            }
            
            
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error in InitializeSystem: {e.Message}");
        }
    }
    
    void LoadAssets()
    {
        // Load front images
        LoadTexturesFromFolder(frontImagesFolder, frontTextures);
        
        // Load chip images
        LoadTexturesFromFolder(chipImagesFolder, chipTextures);
        
        // Load logo images
        LoadTexturesFromFolder(logoImagesFolder, logoTextures);
        
        // Load bank logo images
        LoadTexturesFromFolder(bankLogoImagesFolder, bankLogoTextures);
        
        // Load skybox images
        LoadSkyboxTextures();
        
        // Load fonts
        LoadFontsFromFolder(fontsFolder);
        
        //Debug.Log($"Loaded {frontTextures.Count} front textures, {chipTextures.Count} chip textures, {logoTextures.Count} logo textures, {bankLogoTextures.Count} bank logos, {skyboxTextures.Count} skybox textures, {skyboxCubemaps.Count} skybox cubemaps, {availableFonts.Count} fonts");
        
    }
    
    void LoadTexturesFromFolder(string folderPath, List<Texture2D> textureList)
    {
        textureList.Clear();
        
        if (!System.IO.Directory.Exists(folderPath))
        {
            Debug.LogWarning($"Folder not found: {folderPath}");
            return;
        }
        
        string[] imageExtensions = { "*.png", "*.jpg", "*.jpeg" };
        foreach (string extension in imageExtensions)
        {
            string[] files = System.IO.Directory.GetFiles(folderPath, extension);
            foreach (string file in files)
            {
                // Try loading directly from file data first to preserve original dimensions
                Texture2D texture = LoadTextureFromFile(file);
                if (texture == null)
                {
                    // Fallback to AssetDatabase loading
                    Texture2D originalTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(file);
                    if (originalTexture != null)
                    {
                        // Debug the original texture dimensions
                        Debug.Log($"Original texture: {originalTexture.name} - Size: {originalTexture.width}x{originalTexture.height}, Readable: {originalTexture.isReadable}");
                        
                        // Create a readable copy of the texture
                        texture = CreateReadableTextureCopy(originalTexture);
                    }
                }
                
                if (texture != null)
                {
                    // Ensure sharp filtering for all loaded textures
                    texture.filterMode = FilterMode.Bilinear;
                    texture.wrapMode = TextureWrapMode.Clamp;
                    textureList.Add(texture);
                }
            }
        }
        
        Debug.Log($"Loaded {textureList.Count} textures from {folderPath}");
    }
    
    Texture2D LoadTextureFromFile(string filePath)
    {
        try
        {
            // Load the file data
            byte[] fileData = System.IO.File.ReadAllBytes(filePath);
            
            // Create a new texture and load the image data
            Texture2D texture = new Texture2D(2, 2);
            if (texture.LoadImage(fileData))
            {
                Debug.Log($"Loaded texture from file: {System.IO.Path.GetFileName(filePath)} - Size: {texture.width}x{texture.height}");
                return texture;
            }
            else
            {
                Debug.LogWarning($"Failed to load image data from: {filePath}");
                DestroyImmediate(texture);
                return null;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Error loading texture from file {filePath}: {e.Message}");
            return null;
        }
    }
    
    Texture2D CreateReadableTextureCopy(Texture2D sourceTexture)
    {
        if (sourceTexture == null) return null;
        
        try
        {
            // Create a render texture to copy the source texture
            RenderTexture renderTexture = RenderTexture.GetTemporary(sourceTexture.width, sourceTexture.height, 0, RenderTextureFormat.ARGB32);
            renderTexture.filterMode = FilterMode.Bilinear;
            
            // Copy the source texture to the render texture
            Graphics.Blit(sourceTexture, renderTexture);
            
            // Create a new readable texture
            Texture2D readableTexture = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.RGBA32, false);
            readableTexture.filterMode = FilterMode.Bilinear;
            readableTexture.wrapMode = TextureWrapMode.Clamp;
            
            // Read pixels from render texture
            RenderTexture.active = renderTexture;
            readableTexture.ReadPixels(new Rect(0, 0, sourceTexture.width, sourceTexture.height), 0, 0);
            readableTexture.Apply();
            RenderTexture.active = null;
            
            // Release the temporary render texture
            RenderTexture.ReleaseTemporary(renderTexture);
            
            // Log texture info for debugging
            float aspectRatio = (float)readableTexture.width / readableTexture.height;
            Debug.Log($"Created readable copy: {sourceTexture.name} - Size: {readableTexture.width}x{readableTexture.height}, Aspect: {aspectRatio:F2}");
            
            return readableTexture;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Could not create readable copy of texture {sourceTexture.name}: {e.Message}");
            return null;
        }
    }
    
    void LoadSkyboxTextures()
    {
        if (!Directory.Exists(skyboxImagesFolder))
        {
            Debug.LogWarning($"Skybox folder not found: {skyboxImagesFolder}");
            return;
        }
        
        string[] exrFiles = Directory.GetFiles(skyboxImagesFolder, "*.exr");
        
        foreach (string filePath in exrFiles)
        {
            byte[] fileData = File.ReadAllBytes(filePath);
            Texture2D texture = new Texture2D(2, 2);
            if (texture.LoadImage(fileData))
            {
                // For HDRP, we need to convert 2D textures to cubemaps
                bool isHDRP = GraphicsSettings.defaultRenderPipeline != null && 
                              GraphicsSettings.defaultRenderPipeline.GetType().Name.Contains("HDRenderPipelineAsset");
                
                if (isHDRP)
                {
                    // Convert 2D texture to cubemap for HDRP
                    Cubemap cubemap = ConvertToCubemap(texture);
                    if (cubemap != null)
                    {
                        skyboxCubemaps.Add(cubemap);
                    }
                }
                else
                {
                    skyboxTextures.Add(texture);
                }
            }
        }
    }
    
    Cubemap ConvertToCubemap(Texture2D sourceTexture)
    {
        // Create a more realistic cubemap from the 2D texture
        int size = Mathf.Max(sourceTexture.width, sourceTexture.height);
        Cubemap cubemap = new Cubemap(size, TextureFormat.RGB24, false);
        
        Debug.Log($"Converting 2D texture ({sourceTexture.width}x{sourceTexture.height}) to cubemap ({size}x{size})");
        
        // Create a more sophisticated mapping for each face
        for (int face = 0; face < 6; face++)
        {
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    // Convert cubemap coordinates to spherical coordinates
                    Vector3 direction = GetDirectionFromCubemapFace((CubemapFace)face, x, y, size);
                    
                    // Convert spherical coordinates to texture coordinates
                    Vector2 texCoord = GetTextureCoordinateFromDirection(direction, sourceTexture.width, sourceTexture.height);
                    
                    // Sample the texture
                    Color color = sourceTexture.GetPixelBilinear(texCoord.x, texCoord.y);
                    cubemap.SetPixel((CubemapFace)face, x, y, color);
                }
            }
        }
        
        cubemap.Apply();
        Debug.Log($"Created cubemap: {cubemap.name}");
        return cubemap;
    }
    
    Vector3 GetDirectionFromCubemapFace(CubemapFace face, int x, int y, int size)
    {
        // Convert cubemap face coordinates to 3D direction
        float u = (float)x / size * 2f - 1f;
        float v = (float)y / size * 2f - 1f;
        
        switch (face)
        {
            case CubemapFace.PositiveX: return new Vector3(1f, -v, -u);
            case CubemapFace.NegativeX: return new Vector3(-1f, -v, u);
            case CubemapFace.PositiveY: return new Vector3(u, 1f, v);
            case CubemapFace.NegativeY: return new Vector3(u, -1f, -v);
            case CubemapFace.PositiveZ: return new Vector3(u, -v, 1f);
            case CubemapFace.NegativeZ: return new Vector3(-u, -v, -1f);
            default: return Vector3.forward;
        }
    }
    
    Vector2 GetTextureCoordinateFromDirection(Vector3 direction, int textureWidth, int textureHeight)
    {
        // Convert 3D direction to spherical coordinates
        float theta = Mathf.Atan2(direction.z, direction.x);
        float phi = Mathf.Acos(Mathf.Clamp(direction.y, -1f, 1f));
        
        // Convert to texture coordinates
        float u = (theta / (2f * Mathf.PI) + 0.5f);
        float v = phi / Mathf.PI;
        
        // Ensure coordinates are in valid range
        u = Mathf.Clamp01(u);
        v = Mathf.Clamp01(v);
        
        return new Vector2(u, v);
    }
    
    void LoadFontsFromFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            Debug.LogWarning($"Font folder not found: {folderPath}");
            return;
        }
        
        string[] fontFiles = Directory.GetFiles(folderPath, "*.ttf")
            .Concat(Directory.GetFiles(folderPath, "*.otf"))
            .ToArray();
            
        foreach (string filePath in fontFiles)
        {
            Font font = new Font(filePath);
            if (font != null)
            {
                availableFonts.Add(font);
            }
        }
    }
    
    void SetupCamera()
    {
        if (renderCamera == null) return;
        
        // Set camera to render texture
        renderCamera.targetTexture = renderTexture;
        
        // HDRP-specific camera settings
        renderCamera.clearFlags = CameraClearFlags.Skybox;
        renderCamera.farClipPlane = 1000f;
        renderCamera.nearClipPlane = 0.1f;
        renderCamera.fieldOfView = 60f;
        
        // Configure HDRP camera data
        var hdCameraData = renderCamera.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
        if (hdCameraData != null)
        {
            // Disable anti-aliasing to prevent blur
            hdCameraData.antialiasing = UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData.AntialiasingMode.None;
            
            Debug.Log("Configured HDRP camera settings");
        }
        
        // Disable post-processing effects that cause blur
        DisablePostProcessingEffects();
        
        Debug.Log("Camera setup complete - HDRP optimized");
    }
    
    void DisablePostProcessingEffects()
    {
        try
        {
            // HDRP post-processing is handled through Volume system
            DisableHDRPPostProcessing();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Could not disable post-processing effects: {e.Message}");
        }
    }
    
    void DisableHDRPPostProcessing()
    {
        // Use the configured Default Settings Volume Profile for post-processing
        if (defaultVolumeProfile != null)
        {
            // Disable Depth of Field
            if (defaultVolumeProfile.TryGet(out UnityEngine.Rendering.HighDefinition.DepthOfField depthOfField))
            {
                depthOfField.active = false;
                Debug.Log("Disabled HDRP Depth of Field in Default Volume Profile");
            }
            
            // Disable Motion Blur
            if (defaultVolumeProfile.TryGet(out UnityEngine.Rendering.HighDefinition.MotionBlur motionBlur))
            {
                motionBlur.active = false;
                Debug.Log("Disabled HDRP Motion Blur in Default Volume Profile");
            }
            
            // Disable Bloom
            if (defaultVolumeProfile.TryGet(out UnityEngine.Rendering.HighDefinition.Bloom bloom))
            {
                bloom.active = false;
                Debug.Log("Disabled HDRP Bloom in Default Volume Profile");
            }
            
            // Disable Chromatic Aberration
            if (defaultVolumeProfile.TryGet(out UnityEngine.Rendering.HighDefinition.ChromaticAberration chromaticAberration))
            {
                chromaticAberration.active = false;
                Debug.Log("Disabled HDRP Chromatic Aberration in Default Volume Profile");
            }
            
            // Disable other blur effects
            if (defaultVolumeProfile.TryGet(out UnityEngine.Rendering.HighDefinition.Vignette vignette))
            {
                vignette.active = false;
                Debug.Log("Disabled HDRP Vignette in Default Volume Profile");
            }
            
            if (defaultVolumeProfile.TryGet(out UnityEngine.Rendering.HighDefinition.PaniniProjection paniniProjection))
            {
                paniniProjection.active = false;
                Debug.Log("Disabled HDRP Panini Projection in Default Volume Profile");
            }
        }
        else
        {
            Debug.LogWarning("Default Volume Profile not assigned - cannot disable HDRP post-processing");
        }
    }
    
    void SetupRenderTexture()
    {
        // Create high-quality render texture with no filtering
        renderTexture = new RenderTexture(imageWidth, imageHeight, 24);
        renderTexture.antiAliasing = 1; // No anti-aliasing to prevent blur
        renderTexture.filterMode = FilterMode.Bilinear; // Sharp pixel rendering
        renderTexture.wrapMode = TextureWrapMode.Clamp;
        renderTexture.Create();
        
        // Create capture texture with point filtering for sharpness
        captureTexture = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);
        captureTexture.filterMode = FilterMode.Bilinear;
        captureTexture.wrapMode = TextureWrapMode.Clamp;
    }
    
    void GenerateCreditCards()
    {
        if (isGenerating) return;
        
        isGenerating = true;
        currentImageIndex = 0;
        generationTimer.Reset();
        generationTimer.Start();
        
        Debug.Log($"Starting generation of {imagesToGenerate} credit card images...");
        
        // Start the generation process
        GenerateNextCard();
    }
    
    void GenerateNextCard()
    {
        if (currentImageIndex >= imagesToGenerate)
        {
            isGenerating = false;
            generationTimer.Stop();
            
            // Calculate performance metrics
            float totalTime = (float)generationTimer.ElapsedMilliseconds / 1000f;
            float avgTime = totalTime / imagesToGenerate;
            float cardsPerSecond = imagesToGenerate / totalTime;
            
            Debug.Log($"Credit card generation complete!");
            Debug.Log($"Performance: {totalTime:F2}s total, {avgTime:F3}s per card, {cardsPerSecond:F1} cards/second");
            
            return;
        }
        
        // Generate random card
        GenerateRandomCard();
        
        // Capture image
        StartCoroutine(CaptureAndSave());
    }
    
    void GenerateRandomCard()
    {
        if (cardModel == null) return;
        
        
        // Randomize card position and rotation
        RandomizeCardTransform();
        
        // Apply random skybox
        ApplyRandomSkybox();
        
        // Apply random materials with superimposed logos
        ApplyRandomMaterialsWithLogos();
        
        // Ensure card model uses sharp rendering
        EnsureSharpCardRendering();
    }
    
    void EnsureSharpCardRendering()
    {
        if (cardModel == null) return;
        
        // Get all renderers in the card model hierarchy
        Renderer[] renderers = cardModel.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            // Ensure renderer doesn't cast shadows (can cause blur)
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            
            // Ensure materials use sharp filtering
            if (renderer.material != null)
            {
                EnsureSharpMaterialRendering(renderer.material);
            }
        }
        
        // Also check for any mesh renderers and ensure they're optimized
        MeshRenderer[] meshRenderers = cardModel.GetComponentsInChildren<MeshRenderer>();
        foreach (MeshRenderer meshRenderer in meshRenderers)
        {
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }
    }
    
    void RandomizeCardTransform()
    {
        if (cardModel == null || cameraTransform == null) return;
        
        Transform cardTransform = cardModel.transform.parent;
        
        // Random distance from camera (first-person view)
        float distance = 3.0f;
        Vector3 cameraForward = cameraTransform.forward;
        cardTransform.position = cameraTransform.position + cameraForward * distance;
        //Debug.Log($"Card position: {cardTransform.position}, distance: {distance}");
        
        // Random rotation (mostly orthogonal to camera)
        Vector3 randomRotation = new Vector3(
            Random.Range(-maxRotationAngle, maxRotationAngle),
            Random.Range(-maxRotationAngle, maxRotationAngle),
            Random.Range(-maxRotationAngle, maxRotationAngle)
        );
        
        cardTransform.rotation = Quaternion.LookRotation(cameraForward) * Quaternion.Euler(randomRotation);
    }
    
    void ApplyRandomSkybox()
    {
        if (!useRandomSkybox || (skyboxTextures.Count == 0 && skyboxCubemaps.Count == 0)) return;
        
        // Random rotation
        float randomRotation = Random.Range(0f, maxSkyboxRotation);
        
        // Apply HDRP skybox
        ApplyHDRPSkybox(randomRotation);
    }
    
    void ApplyHDRPSkybox(float rotation)
    {
        try
        {
            // Use the configured Default Settings Volume Profile
            if (defaultVolumeProfile == null)
            {
                Debug.LogWarning("Default Settings Volume Profile not configured. Please assign it in the inspector.");
                return;
            }
            
            // Try to get the Visual Environment component
            if (defaultVolumeProfile.TryGet(out UnityEngine.Rendering.HighDefinition.VisualEnvironment visualEnvironment))
            {
                // Set sky type to HDRI
                visualEnvironment.skyType.value = (int)UnityEngine.Rendering.HighDefinition.SkyType.HDRI;
                
                // Try to get the HDRI Sky component
                if (defaultVolumeProfile.TryGet(out UnityEngine.Rendering.HighDefinition.HDRISky hdriSky))
                {
                    if (skyboxCubemaps.Count > 0)
                    {
                        // Set random HDRI cubemap
                        Cubemap randomSkyboxCubemap = skyboxCubemaps[Random.Range(0, skyboxCubemaps.Count)];
                        hdriSky.hdriSky.value = randomSkyboxCubemap;
                        hdriSky.rotation.value = rotation;
                        hdriSky.exposure.value = 1f;
                        hdriSky.multiplier.value = 1f;
                        
                        Debug.Log($"Applied HDRP HDRI skybox: {randomSkyboxCubemap.name} with rotation: {rotation}°");
                    }
                    else
                    {
                        Debug.LogWarning("No HDRI cubemaps available for HDRP skybox");
                    }
                }
                else
                {
                    Debug.LogWarning("Could not find HDRISky component in Default Settings Volume Profile");
                }
            }
            else
            {
                Debug.LogWarning("Could not find Visual Environment component in Default Settings Volume Profile");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Error applying HDRP skybox: {e.Message}");
        }
    }
    
   
    
    void ApplyRandomMaterialsWithLogos()
    {
        if (frontMaterial == null) return;
        
        // Create one final texture for the front face only
        if (frontTextures.Count > 0)
        {
            // Get random front texture
            Texture2D randomFrontTexture = frontTextures[Random.Range(0, frontTextures.Count)];
            
            // Blend with random color
            Color randomColor = cardColors[Random.Range(0, cardColors.Count)];
            float blendAmount = Random.Range(minColorBlend, maxColorBlend);
            Texture2D blendedTexture = BlendTextureWithColor(randomFrontTexture, randomColor, blendAmount);
            
            // Add chip, brand logo, and bank logo to the texture
            Texture2D finalTexture = AddLogosToTexture(blendedTexture);
            
            // Apply the final texture to the front material
            frontMaterial.mainTexture = finalTexture;
            
            // Ensure sharp rendering
            //EnsureSharpMaterialRendering(frontMaterial);
            //AdjustMaterialForExposure(frontMaterial);
        }
    }
    
    void EnsureSharpMaterialRendering(Material material)
    {
        if (material == null || material.mainTexture == null) return;
        
        // Set texture filtering to point for sharp rendering
        material.mainTexture.filterMode = FilterMode.Bilinear;
        material.mainTexture.wrapMode = TextureWrapMode.Clamp;
        
        // Disable any material properties that might cause blur
        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.0f); // No smoothness to prevent blur
        }
        
        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", 0.0f); // No metallic to prevent reflections
        }
        
        // Ensure no emission that might cause bloom
        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", Color.black);
        }
    }
    
    void AdjustMaterialForExposure(Material material)
    {
        if (material == null) return;
        
        try
        {
            // HDRP material properties
            if (material.HasProperty("_BaseColor"))
            {
                Color baseColor = material.GetColor("_BaseColor");
                // Reduce brightness to prevent overexposure
                baseColor *= Mathf.Clamp(exposure, 0.1f, 1.0f);
                material.SetColor("_BaseColor", baseColor);
            }
            
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0.0f); // No metallic to prevent reflections
            }
            
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.0f); // No smoothness to prevent blur
            }
            
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", Color.black); // No emission
            }
            
            // Additional HDRP properties to prevent overexposure
            if (material.HasProperty("_EmissiveColor"))
            {
                material.SetColor("_EmissiveColor", Color.black);
            }
            
            if (material.HasProperty("_EmissiveIntensity"))
            {
                material.SetFloat("_EmissiveIntensity", 0.0f);
            }
            
            // HDRP-specific material properties
            if (material.HasProperty("_SpecularOcclusionFromAO"))
            {
                material.SetFloat("_SpecularOcclusionFromAO", 0.0f);
            }
            
            if (material.HasProperty("_SpecularAAScreenSpaceVariance"))
            {
                material.SetFloat("_SpecularAAScreenSpaceVariance", 0.0f);
            }
            
            if (material.HasProperty("_SpecularAAThreshold"))
            {
                material.SetFloat("_SpecularAAThreshold", 0.0f);
            }
            
            Debug.Log("Applied HDRP material optimizations");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Could not adjust HDRP material properties: {e.Message}");
        }
    }
    
    Texture2D BlendTextureWithColor(Texture2D sourceTexture, Color blendColor, float blendAmount)
    {
        Texture2D result = GetPooledTexture(sourceTexture.width, sourceTexture.height);
        
        // Use Color32 for faster pixel operations
        Color32[] sourcePixels = sourceTexture.GetPixels32();
        Color32[] resultPixels = new Color32[sourcePixels.Length];
        
        for (int i = 0; i < sourcePixels.Length; i++)
        {
            Color sourceColor = sourcePixels[i];
            Color blendedColor = Color.Lerp(sourceColor, blendColor, blendAmount);
            resultPixels[i] = blendedColor;
        }
        
        result.SetPixels32(resultPixels);
        result.Apply();
        return result;
    }
    
    Texture2D AddLogosToTexture(Texture2D baseTexture)
    {
        bool isCardBack = Random.Range(0f, 1f) < 0.5f;

        Texture2D result = GetPooledTexture(baseTexture.width, baseTexture.height);
        
        // Copy base texture using GetPixels32 for better performance
        Color32[] basePixels = baseTexture.GetPixels32();
        result.SetPixels32(basePixels);
        
        
        // Add chip
        if (chipTextures.Count > 0 && !isCardBack)
        {
            Texture2D chipTexture = chipTextures[Random.Range(0, chipTextures.Count)];
            Vector2 chipCenter = CalculateChipPosition();
            AddLogoToTexture(result, chipTexture, chipCenter, chipSizeRatio, chipOpacity);
            Debug.Log($"Chip: {chipTexture.name} - Center: {chipCenter}, MaxWidth: {chipSizeRatio}");
        }
        
        // Add brand logo (independent of chip)
        if (logoTextures.Count > 0 && !isCardBack)
        {
            Texture2D brandLogoTexture = logoTextures[Random.Range(0, logoTextures.Count)];
            Vector2 brandLogoCenter = new Vector2(1.0f, -1.0f); // Bottom right corner
            AddLogoToTexture(result, brandLogoTexture, brandLogoCenter, brandLogoSizeRatio, brandLogoOpacity);
            Debug.Log($"Brand Logo: {brandLogoTexture.name} - Center: {brandLogoCenter}, MaxWidth: {brandLogoSizeRatio}");
        }
        
        // Add bank logo (independent of chip)
        if (bankLogoTextures.Count > 0 && !isCardBack)
        {
            Texture2D bankLogoTexture = bankLogoTextures[Random.Range(0, bankLogoTextures.Count)];
            Vector2 bankLogoCenter = new Vector2(1.0f, 1.0f); // Upper right corner
            AddLogoToTexture(result, bankLogoTexture, bankLogoCenter, bankLogoSizeRatio, bankLogoOpacity);
            Debug.Log($"Bank Logo: {bankLogoTexture.name} - Center: {bankLogoCenter}, MaxWidth: {bankLogoSizeRatio}");
        }
        
        // Randomly add magnetic stripe
        if (useMagneticStripe && isCardBack)
        {
            AddMagneticStripeToTexture(result);
        }
        
        result.Apply();
        return result;
    }
    
    void AddLogoToTexture(Texture2D targetTexture, Texture2D logoTexture, Vector2 center, float maxWidth, float opacity)
    {
        // Convert center from card space (-1 to 1) to texture space (0 to 1)
        Vector2 textureCenter = new Vector2(
            (center.x + 1f) * 0.5f,
            (center.y + 1f) * 0.5f
        );

        Debug.Log($"targetTexture.width: {targetTexture.width}, targetTexture.height: {targetTexture.height}");
        Debug.Log($"logoTexture.width: {logoTexture.width}, logoTexture.height: {logoTexture.height}");
        
        // Calculate logo dimensions in pixels
        // maxWidth is a percentage of the card width (0.0 to 1.0)
        int logoWidth = (int)(maxWidth * targetTexture.width);
        int logoHeight = (int)(logoWidth * ((float)logoTexture.height / logoTexture.width)); // Preserve aspect ratio
        
        Debug.Log($"Calculated logo size: {logoWidth}x{logoHeight} pixels");
        
        // Calculate start position in texture coordinates (center-based)
        int startX = (int)(textureCenter.x * targetTexture.width - logoWidth * 0.5f);
        int startY = (int)(textureCenter.y * targetTexture.height - logoHeight * 0.5f);
        
        // Ensure we don't go out of bounds
        // Maintain padding on edges
        int paddingX = Mathf.RoundToInt(logoPadding * targetTexture.width);
        int paddingY = Mathf.RoundToInt(logoPadding * targetTexture.height);
        startX = Mathf.Clamp(startX, paddingX, targetTexture.width - logoWidth - paddingX);
        startY = Mathf.Clamp(startY, paddingY, targetTexture.height - logoHeight - paddingY);
        
        Debug.Log($"Logo position: ({startX}, {startY})");
        
        // Fast GPU-accelerated blending using RenderTexture
        BlendLogoFast(targetTexture, logoTexture, startX, startY, logoWidth, logoHeight, opacity);
    }
    
    void BlendLogoFast(Texture2D targetTexture, Texture2D logoTexture, int startX, int startY, int logoWidth, int logoHeight, float opacity)
    {
        // Get pixels for fast array operations
        Color32[] logoPixels = logoTexture.GetPixels32();
        Color32[] targetPixels = targetTexture.GetPixels32();
        
        // Calculate scaling factors for logo sampling
        float scaleX = (float)logoTexture.width / logoWidth;
        float scaleY = (float)logoTexture.height / logoHeight;
        
        // Fast pixel blending using arrays
        for (int y = 0; y < logoHeight; y++)
        {
            for (int x = 0; x < logoWidth; x++)
            {
                // Sample from logo texture
                int logoX = (int)(x * scaleX);
                int logoY = (int)(y * scaleY);
                logoX = Mathf.Clamp(logoX, 0, logoTexture.width - 1);
                logoY = Mathf.Clamp(logoY, 0, logoTexture.height - 1);
                
                int logoIndex = logoY * logoTexture.width + logoX;
                int targetIndex = (startY + y) * targetTexture.width + (startX + x);
                
                Color32 logoColor = logoPixels[logoIndex];
                Color32 targetColor = targetPixels[targetIndex];
                
                // Fast alpha blending with opacity
                if (logoColor.a > 0)
                {
                    float alpha = (logoColor.a / 255f) * opacity;
                    targetPixels[targetIndex] = Color32.Lerp(targetColor, logoColor, alpha);
                }
            }
        }
        
        // Apply the blended pixels back to the target texture
        targetTexture.SetPixels32(targetPixels);
        targetTexture.Apply();
    }
    
    void AddMagneticStripeToTexture(Texture2D targetTexture)
    {
        if (magneticStripeGenerator == null) return;
        
        try
        {
            // Generate magnetic stripe texture
            Texture2D stripeTexture = magneticStripeGenerator.GenerateMagneticStripe(targetTexture.width, targetTexture.height);
            
            // Blend stripe onto target texture
            for (int x = 0; x < targetTexture.width; x++)
            {
                for (int y = 0; y < targetTexture.height; y++)
                {
                    Color targetColor = targetTexture.GetPixel(x, y);
                    Color stripeColor = stripeTexture.GetPixel(x, y);
                    
                    // Blend based on stripe alpha
                    float alpha = 1.0f;// stripeColor.a * magneticStripeGenerator.stripeOpacity;
                    Color blendedColor = Color.Lerp(targetColor, stripeColor, alpha);
                    
                    targetTexture.SetPixel(x, y, blendedColor);
                }
            }
            
            // Clean up the generated stripe texture
            DestroyImmediate(stripeTexture);
            
            Debug.Log("Added magnetic stripe to texture");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Could not add magnetic stripe: {e.Message}");
        }
    }
    
    
    System.Collections.IEnumerator CaptureAndSave()
    {
        // Wait for rendering to complete
        yield return new WaitForEndOfFrame();
        
        // Set camera target texture
        renderCamera.targetTexture = renderTexture;
        
        // Render the scene
        renderCamera.Render(); 
        
        // Read pixels from render texture
        RenderTexture.active = renderTexture;
        captureTexture.ReadPixels(new Rect(0, 0, imageWidth, imageHeight), 0, 0);
        captureTexture.Apply();
        RenderTexture.active = null;
        
        // Ensure the captured texture is sharp
        captureTexture.filterMode = FilterMode.Bilinear;
        captureTexture.wrapMode = TextureWrapMode.Clamp;
        
        // Save the image with maximum quality
        string filename = $"credit_card_{currentImageIndex:D4}.png";
        string filepath = Path.Combine("SyntheticCreditCardData", filename);
        
        byte[] imageData = captureTexture.EncodeToPNG();
        File.WriteAllBytes(filepath, imageData);
        
        Debug.Log($"Saved sharp image: {filepath}");
        
        currentImageIndex++;
        
        // Generate next card
        GenerateNextCard();
    }
    
    void OnDestroy()
    {
        if (renderTexture != null)
        {
            renderTexture.Release();
        }
        
        if (captureTexture != null)
        {
            DestroyImmediate(captureTexture);
        }
        
        // Clean up readable textures
        CleanupReadableTextures();
        
        // Clean up texture pool
        CleanupTexturePool();
        
        // Clean up added elements
        foreach (GameObject element in addedElements)
        {
            if (element != null)
            {
                DestroyImmediate(element);
            }
        }
        addedElements.Clear();
    }
    
    void CleanupReadableTextures()
    {
        // Clean up all readable textures we created
        var allTextureLists = new List<Texture2D>[] { frontTextures, chipTextures, logoTextures, bankLogoTextures, skyboxTextures };
        
        foreach (var textureList in allTextureLists)
        {
            foreach (Texture2D texture in textureList)
            {
                if (texture != null)
                {
                    DestroyImmediate(texture);
                }
            }
            textureList.Clear();
        }
        
        // Clean up cubemaps
        foreach (Cubemap cubemap in skyboxCubemaps)
        {
            if (cubemap != null)
            {
                DestroyImmediate(cubemap);
            }
        }
        skyboxCubemaps.Clear();
    }
    
    // Performance optimization: Texture pooling methods
    Texture2D GetPooledTexture(int width, int height)
    {
        if (texturePool.Count > 0)
        {
            Texture2D pooledTexture = texturePool.Dequeue();
            if (pooledTexture.width == width && pooledTexture.height == height)
            {
                return pooledTexture;
            }
            else
            {
                DestroyImmediate(pooledTexture);
            }
        }
        
        return new Texture2D(width, height, TextureFormat.RGBA32, false);
    }
    
    void ReturnToPool(Texture2D texture)
    {
        if (texture == null) return;
        
        if (texturePool.Count < MAX_POOL_SIZE)
        {
            texturePool.Enqueue(texture);
        }
        else
        {
            DestroyImmediate(texture);
        }
    }
    
    void CleanupTexturePool()
    {
        while (texturePool.Count > 0)
        {
            Texture2D texture = texturePool.Dequeue();
            if (texture != null)
            {
                DestroyImmediate(texture);
            }
        }
    }
    
    // Public method to trigger generation from UI
    public void StartGeneration()
    {
        GenerateCreditCards();
    }
    
    // Public method to set number of images to generate
    public void SetImageCount(int count)
    {
        imagesToGenerate = count;
    }
    
    // Public method to manually test skybox changes
    public void TestSkyboxChange()
    {
        ApplyRandomSkybox();
    }
    
    // Public method to scale card to standard dimensions
    // public void ScaleCardToStandardDimensions()
    // {
    //     if (cardModel == null) return;
        
        
    //     // Convert inches to Unity units (1 inch = 0.0254 meters)
    //     float widthMeters = cardWidthInches * 0.0254f;
    //     float heightMeters = cardHeightInches * 0.0254f;
    //     float thicknessMeters = cardThicknessInches * 0.0254f;
        
    //     // Get the current bounds of the card model
    //     Renderer cardRenderer = cardModel.GetComponent<Renderer>();
    //     if (cardRenderer != null)
    //     {
    //         Bounds currentBounds = cardRenderer.bounds;
            
    //         // Calculate scale factors
    //         float scaleX = widthMeters / currentBounds.size.x;
    //         float scaleY = heightMeters / currentBounds.size.y;
    //         float scaleZ = thicknessMeters / currentBounds.size.z;
            
    //         // Apply uniform scaling (use the smallest scale to maintain proportions)
    //         float uniformScale = Mathf.Min(scaleX, scaleY, scaleZ);
            
    //         // Apply scaling
    //         //cardModel.transform.localScale = Vector3.one * uniformScale * 500.0f;
            
    //         Debug.Log($"Scaled card to {cardWidthInches}\" × {cardHeightInches}\" × {cardThicknessInches}\" (scale: {uniformScale:F3})");
    //     }
    //     else
    //     {
    //         // Fallback: apply estimated scaling based on typical card proportions
    //         float estimatedScale = widthMeters / 0.0856f; // 3.375 inches = 0.0856 meters
    //         //cardModel.transform.localScale = Vector3.one * estimatedScale;

            
            
    //         Debug.Log($"Applied estimated card scaling (scale: {estimatedScale:F3})");
    //     }
    // }

    // Calculate EMV chip position based on real-world specifications
    Vector2 CalculateChipPosition()
    {
        // Real-world specifications from the technical diagram:
        // Card dimensions: 3.37" x 2.125"
        // Chip dimensions: 0.85" x 0.71"
        // Chip position from left edge: 0.34"
        // Chip position from top edge: 0.46"
        
        float cardWidth = 3.37f;
        float cardHeight = 2.125f;
        float chipWidth = 0.85f;
        float chipHeight = 0.71f;
        float chipLeftEdge = 0.34f;
        float chipTopEdge = 0.46f;
        
        // Calculate chip center position in inches
        float chipCenterX = chipLeftEdge + (chipWidth / 2f);
        float chipCenterY = chipTopEdge + (chipHeight / 2f);
        
        // Convert to center-based coordinates (0,0 at card center)
        float normalizedX = (chipCenterX - (cardWidth / 2f)) / (cardWidth / 2f);
        float normalizedY = ((cardHeight / 2f) - chipCenterY) / (cardHeight / 2f);
        
        return new Vector2(normalizedX, normalizedY);
    }
}
