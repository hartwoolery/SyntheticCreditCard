using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using System.Linq; // Added for Concat

using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


public class Synthesize : MonoBehaviour
{
    [Header("Card Model")]
    public GameObject cardModel;
    public Material frontMaterial;
    
    [Header("Camera")]
    public Camera mainCamera;
    private Camera screenCamera; // Second camera for screen rendering
    
    [Header("Asset Folders")]
    string frontImagesFolder = "Assets/FrontImages/";
    string chipImagesFolder = "Assets/ChipImages/";
    string logoImagesFolder = "Assets/LogoImages/";
    string bankLogoImagesFolder = "Assets/BankLogoImages/";
    string skyboxImagesFolder = "Assets/SkyboxImages/";
    string cardFontsFolder = "Assets/Card Fonts/";
    
    [Header("Generation Settings")]
    int numberOfImages = 10;
    bool useRandomSkybox = true;
    
    [Header("Randomization")]
    float minCardRotation = -5f;
    float maxCardRotation = 5f;
    float minColorBlend = 0.7f;
    float maxColorBlend = 1.0f;
    float chipOpacity = 1.0f;
    float brandLogoOpacity = 1.0f;
    float bankLogoOpacity = 1.0f;
    float generationDelay = 0.1f;
    
    bool pauseOnFocusLoss = false;
    bool continuousGeneration = true;
    float memoryCleanupInterval = 10f;
    
    [Header("Card Dimensions")]
   
    [Header("Logo Positioning")]
    Vector2 chipPosition = new Vector2(-0.44f, -0.18f); // Based on real EMV specs: 3/8" from left, 11/16" from top
    Vector2 brandLogoPosition = new Vector2(0.5f, -0.5f);
    Vector2 bankLogoPosition = new Vector2(0.5f, 0.5f);
    float chipSize = 0.14f; // Based on real EMV specs: 0.4591" x 0.3692" (using width as reference)
    float brandLogoSize = 0.2f;
    float bankLogoSize = 0.15f;
    float logoPadding = 0.05f;
    
    [Header("Text Setup")]
    public GameObject textSetup;
    
    [Header("Card Colors")]
   
    
    [Header("Text Colors")]
    List<Color> textColorOptions = new List<Color>
    {
        Color.white,
        Color.black
    };
    
    // Private variables
    private List<Texture2D> frontTextures = new List<Texture2D>();
    private List<Texture2D> chipTextures = new List<Texture2D>();
    private List<Texture2D> logoTextures = new List<Texture2D>();
    private List<Texture2D> bankLogoTextures = new List<Texture2D>();
    private List<Cubemap> skyboxCubemaps = new List<Cubemap>();
    private List<TMPro.TMP_FontAsset> availableFonts = new List<TMPro.TMP_FontAsset>();
    private List<TMPro.TMP_FontAsset> cardFonts = new List<TMPro.TMP_FontAsset>();
    private RenderTexture renderTexture;
    private Texture2D captureTexture;
    private Texture2D signatureTexture;
    private Texture2D rfidTexture;
    private bool isGenerating = false;
    private int currentImageCount = 0;
    private float lastMemoryCleanup = 0f;
    private MagneticStripeGenerator magneticStripeGenerator;
    private Queue<Texture2D> texturePool = new Queue<Texture2D>();
    private const int MAX_POOL_SIZE = 10;
    
    // Add a field to store the current generation's blend mode
    // private string currentGenerationBlendMode = "Alpha";
    
    // Track if current card is back or front
    private bool isCardBack = false;
    
    [Header("YOLO Data Settings")]
    public bool saveYoloData = true;
    public float trainSplit = 0.7f;
    public float validSplit = 0.2f;
    public float testSplit = 0.1f;
    private int totalImagesGenerated = 0;
    
    // YOLO class definitions
    private readonly Dictionary<string, int> yoloClasses = new Dictionary<string, int>
    {
        {"Name", 0},
        {"Number", 1}, 
        {"Expires", 2},
        {"CVC", 3},
        {"Front", 4},
        {"Back", 5}
    };
    
    void Start()
    {
        // Clear existing generated data before starting
        ClearGeneratedFolders();
        
        // Initialize the system
        InitializeSystem();
        
        // Load all assets
        LoadAssets();
        
        // Setup magnetic stripe generator
        SetupMagneticStripe();
        
        // Setup render texture and camera
        SetupRenderTexture();
        SetupCamera();
        
        // Load signature texture
        LoadAdditionalTextures();
        
        // Start generating credit cards
        GenerateCreditCards();
        SaveYoloClassesFile();
    }
    
    void ClearGeneratedFolders()
    {
        string baseDir = Path.Combine(Application.dataPath, "..", "SyntheticCreditCardData");
        
        // Clear main directory
        if (Directory.Exists(baseDir))
        {
            try
            {
                Directory.Delete(baseDir, true);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Could not clear folder: {e.Message}");
            }
        }
        
        // Recreate base directory
        Directory.CreateDirectory(baseDir);
        
        // Create YOLO directory structure
        if (saveYoloData)
        {
            string yoloDir = Path.Combine(baseDir, "yolo");
            Directory.CreateDirectory(yoloDir);
            
            // Create split directories
            string[] splits = { "train", "valid", "test" };
            foreach (string split in splits)
            {
                string splitDir = Path.Combine(yoloDir, split);
                Directory.CreateDirectory(splitDir);
                Directory.CreateDirectory(Path.Combine(splitDir, "images"));
                Directory.CreateDirectory(Path.Combine(splitDir, "labels"));
            }
        }
    }
    
    void Update()
    {
        // Handle continuous generation
        if (continuousGeneration && !isGenerating && currentImageCount >= numberOfImages)
        {
            RestartGeneration();
        }
        
        // Periodic memory cleanup for long-running operations
        if (Time.time - lastMemoryCleanup > memoryCleanupInterval)
        {
            CleanupMemory();
            lastMemoryCleanup = Time.time;
        }
    }
    
    void RestartGeneration()
    {
        currentImageCount = 0;
        isGenerating = true;
        GenerateCreditCards();
    }
    
    void CleanupMemory()
    {
        // Force garbage collection
        System.GC.Collect();
        
        // Clean up texture pool
        CleanupTexturePool();
        
        // Clean up any temporary objects
        Resources.UnloadUnusedAssets();
    }
    
    void InitializeSystem()
    {
        // Find card model if not assigned
        if (cardModel == null)
        {
            cardModel = GameObject.Find("card");
            if (cardModel == null)
            {
                Debug.LogWarning("Card model not found. Please assign a card model in the inspector.");
            }
        }
        
        // Find camera if not assigned
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogWarning("No camera found. Please assign a camera in the inspector.");
            }
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
        
        // Load skybox cubemaps
        LoadSkyboxCubemaps();
        
        // Load fonts
        LoadCardFontsFromFolder(cardFontsFolder);
        
        // Load signature
        LoadAdditionalTextures();
    }
    
    void LoadAdditionalTextures()
    {
        // Load signature texture
        string signaturePath = "Assets/signature.png";
        signatureTexture = LoadTextureFromFile(signaturePath);
        
        if (signatureTexture != null)
        {
            signatureTexture.filterMode = FilterMode.Bilinear;
            signatureTexture.wrapMode = TextureWrapMode.Clamp;
        }
        
        // Load RFID texture
        string rfidPath = "Assets/rfid.png";
        rfidTexture = LoadTextureFromFile(rfidPath);
        
        if (rfidTexture != null)
        {
            rfidTexture.filterMode = FilterMode.Bilinear;
            rfidTexture.wrapMode = TextureWrapMode.Clamp;
        }
    }
    
    void LoadTexturesFromFolder(string folderPath, List<Texture2D> textureList)
    {
        textureList.Clear();
        
        if (!Directory.Exists(folderPath))
        {
            Debug.LogWarning($"Folder not found: {folderPath}");
            return;
        }
        
        string[] imageExtensions = { "*.png", "*.jpg", "*.jpeg" };
        foreach (string extension in imageExtensions)
        {
            string[] files = Directory.GetFiles(folderPath, extension);
            foreach (string file in files)
            {
                Texture2D texture = LoadTextureFromFile(file);
                if (texture == null)
                {
                    Texture2D originalTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(file);
                    if (originalTexture != null)
                    {
                        texture = CreateReadableTextureCopy(originalTexture);
                    }
                }
                
                if (texture != null)
                {
                    texture.filterMode = FilterMode.Bilinear;
                    texture.wrapMode = TextureWrapMode.Clamp;
                    textureList.Add(texture);
                }
            }
        }
    }
    
    Texture2D LoadTextureFromFile(string filePath)
    {
        try
        {
            byte[] fileData = File.ReadAllBytes(filePath);
            
            Texture2D texture = new Texture2D(2, 2);
            if (texture.LoadImage(fileData))
            {
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
            Debug.LogError($"Error loading texture from file: {e.Message}");
            return null;
        }
    }
    
    Texture2D CreateReadableTextureCopy(Texture2D sourceTexture)
    {
        if (sourceTexture == null) return null;
        
        try
        {
            RenderTexture renderTexture = RenderTexture.GetTemporary(sourceTexture.width, sourceTexture.height, 0, RenderTextureFormat.ARGB32);
            renderTexture.filterMode = FilterMode.Bilinear;
            
            Graphics.Blit(sourceTexture, renderTexture);
            
            Texture2D readableTexture = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.RGBA32, false);
            readableTexture.filterMode = FilterMode.Bilinear;
            readableTexture.wrapMode = TextureWrapMode.Clamp;
            
            RenderTexture.active = renderTexture;
            readableTexture.ReadPixels(new Rect(0, 0, sourceTexture.width, sourceTexture.height), 0, 0);
            readableTexture.Apply();
            RenderTexture.active = null;
            
            RenderTexture.ReleaseTemporary(renderTexture);
            
            return readableTexture;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Could not create readable copy of texture {sourceTexture.name}: {e.Message}");
            return null;
        }
    }
    
    void LoadCardFontsFromFolder(string folderPath)
    {
        cardFonts.Clear();
        
        if (!Directory.Exists(folderPath))
        {
            Debug.LogWarning($"Card Font folder not found: {folderPath}");
            return;
        }

        CreateFontAssetsFromRawFiles(folderPath);

        string[] fontExtensions = { "*.asset" };
        foreach (string extension in fontExtensions)
        {
            string[] files = Directory.GetFiles(folderPath, extension);
            foreach (string file in files)
            {
                TMPro.TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(file);
                if (fontAsset != null && fontAsset.atlasTexture != null)
                {
                    cardFonts.Add(fontAsset);
                }
                else if (fontAsset != null)
                {
                    Debug.LogWarning($"Skipping corrupted font asset: {fontAsset.name} (missing atlas texture)");
                }
            }
        }
    }
    
    void CreateFontAssetsFromRawFiles(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            Debug.LogWarning($"Font folder does not exist: {folderPath}");
            return;
        }

        string[] ttfFiles = Directory.GetFiles(folderPath, "*.ttf");
        string[] otfFiles = Directory.GetFiles(folderPath, "*.otf");
        string[] allFontFiles = ttfFiles.Concat(otfFiles).ToArray();

        foreach (string fontFile in allFontFiles)
        {
            string fileName = Path.GetFileNameWithoutExtension(fontFile);
            string assetPath = Path.Combine(folderPath, fileName + ".asset");

            // Always regenerate the font asset to ensure atlas texture is created
            if (File.Exists(assetPath))
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            try
            {
                // Load the font file
                Font font = new Font(fontFile);
                if (font == null)
                {
                    Debug.LogError($"Failed to load font: {fontFile}");
                    continue;
                }

                // Create TMP Font Asset with proper settings
                TMPro.TMP_FontAsset tmpFontAsset = TMPro.TMP_FontAsset.CreateFontAsset(
                    font, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, TMPro.AtlasPopulationMode.Dynamic);

                if (tmpFontAsset != null)
                {
                    // Set the correct URP Lit shader for the font asset's material
                    Shader urpLitShader = Shader.Find("TextMeshPro/SRP/TMP_SDF-URP Lit");
                    if (urpLitShader != null)
                    {
                        tmpFontAsset.material.shader = urpLitShader;
                    }
                    else
                    {
                        Debug.LogWarning("Could not find shader: TextMeshPro/SRP/TMP_SDF-URP Lit. Font asset will use default shader.");
                    }

                    // Validate that the atlas texture was created
                    if (tmpFontAsset.atlasTexture == null)
                    {
                        Debug.LogError($"Font asset created but atlas texture is null for: {fileName}");
                        continue;
                    }

                    // Save the font asset
                    AssetDatabase.CreateAsset(tmpFontAsset, assetPath);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
                else
                {
                    Debug.LogError($"Failed to create TMP Font Asset for: {fontFile}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error creating font asset for {fontFile}: {e.Message}");
            }
        }

        // Force refresh to ensure all assets are properly loaded
        AssetDatabase.Refresh();
    }
    
    void SetupMagneticStripe()
    {
        magneticStripeGenerator = new MagneticStripeGenerator();
    }
    
    void SetupCamera()
    {
        if (mainCamera == null) return;
        
        // Set camera to render texture
        mainCamera.targetTexture = renderTexture;
        
        // Built-in Render Pipeline camera settings
        mainCamera.clearFlags = CameraClearFlags.Skybox;
        mainCamera.farClipPlane = 1000f;
        mainCamera.nearClipPlane = 0.1f;
        mainCamera.fieldOfView = 60f;
        
        // Enable anti-aliasing for smoother edges
        mainCamera.allowMSAA = true;
        
        // Create a second camera for screen rendering
        CreateScreenCamera();
    }
    
    void CreateScreenCamera()
    {
        // Create a second camera for screen rendering
        GameObject screenCameraObj = new GameObject("ScreenCamera");
        Camera screenCamera = screenCameraObj.AddComponent<Camera>();

        var urpCam = screenCamera.GetComponent<UniversalAdditionalCameraData>();
        if (urpCam == null) urpCam = screenCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        urpCam.renderPostProcessing = true;
        // Enable a post-process AA (matches the "Anti-aliasing" dropdown on the Camera)
        urpCam.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; // or FastApproximateAntialiasing
        urpCam.antialiasingQuality = AntialiasingQuality.High;    

        
        // Copy settings from main camera
        screenCamera.clearFlags = mainCamera.clearFlags;
        screenCamera.farClipPlane = mainCamera.farClipPlane;
        screenCamera.nearClipPlane = mainCamera.nearClipPlane;
        screenCamera.fieldOfView = mainCamera.fieldOfView;
        screenCamera.allowMSAA = true;
        
        
        
        
        // Position it at the same location as main camera
        screenCamera.transform.position = mainCamera.transform.position;
        screenCamera.transform.rotation = mainCamera.transform.rotation;
        
        // Set it to render to screen (no target texture)
        screenCamera.targetTexture = null;
        
        // Set render order - main camera renders first, screen camera renders second
        screenCamera.depth = mainCamera.depth + 1;
        
        // Make it a child of the main camera so it follows
        screenCameraObj.transform.SetParent(mainCamera.transform);
        
        // Store reference to screen camera
        this.screenCamera = screenCamera;
    }
    
    void SetupRenderTexture()
    {
        // Create high-quality render texture with anti-aliasing for soft edges
        renderTexture = new RenderTexture(1024, 1024, 24);
        renderTexture.antiAliasing = 4; // Enable 4x MSAA for smooth edges
        renderTexture.filterMode = FilterMode.Trilinear; // Softer filtering for smoother edges
        renderTexture.wrapMode = TextureWrapMode.Clamp;
        renderTexture.Create();
        
        // Create capture texture with trilinear filtering for smoothness
        captureTexture = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
        captureTexture.filterMode = FilterMode.Trilinear; // Softer filtering
        captureTexture.wrapMode = TextureWrapMode.Clamp;
    }
    
    void GenerateCreditCards()
    {
        if (isGenerating) return;
        
        isGenerating = true;
        currentImageCount = 0;
        GenerateNextCard();
    }
    
    void GenerateNextCard()
    {
        // Check if we should pause when Unity loses focus
        if (pauseOnFocusLoss && !EditorApplication.isPlaying)
        {
            return;
        }
        
        if (currentImageCount < numberOfImages)
        {
            
            // Add delay between generations for background operation
            if (generationDelay > 0)
            {
                StartCoroutine(GenerateCardWithDelay());
            }
            else
            {
                GenerateRandomCard();
                // Capture and save the image
                StartCoroutine(CaptureAndSave());
            }
        }
        else
        {
            // Generation complete
            isGenerating = false;
            
        }
    }
    
    System.Collections.IEnumerator GenerateCardWithDelay()
    {
        yield return new WaitForSeconds(generationDelay);
        GenerateRandomCard();
        // Capture and save the image
        StartCoroutine(CaptureAndSave());
    }
    
    void GenerateRandomCard()
    {
        if (cardModel == null) return;
        
        isCardBack = Random.Range(0f, 1f) < 0.5f;
        
        // Remove blend mode selection
        // SelectRandomBlendModeForGeneration();
        
        // Randomize card position and rotation
        RandomizeCardTransform();
        
        Color randomColor = new Color(Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f));
        
        // Randomize text setup position and content
        RandomizeTextSetup(isCardBack, randomColor);
        
        // Apply random skybox with delay
        StartCoroutine(ApplySkyboxWithDelay());
        
        // Apply random materials with superimposed logos
        ApplyRandomMaterialsWithLogos(isCardBack, randomColor);
        
    }
    
    // Remove the SelectRandomBlendModeForGeneration method
    // void SelectRandomBlendModeForGeneration()
    // {
    //     // Define available blend modes
    //     string[] blendModes = {
    //         "Alpha",           // Normal alpha blending
    //         "Additive",        // Additive blending
    //         "Multiply",        // Multiply blending
    //         "Screen",          // Screen blending
    //         "Overlay",         // Overlay blending
    //         "SoftLight",       // Soft light blending
    //         "HardLight",       // Hard light blending
    //         "ColorDodge",      // Color dodge blending
    //         "ColorBurn",       // Color burn blending
    //         "Darken",          // Darken blending
    //         "Lighten"          // Lighten blending
    //     };
        
    //     // Select random blend mode for this generation
    //     currentGenerationBlendMode = blendModes[Random.Range(0, blendModes.Length)];
    //     Debug.Log($"Selected blend mode for this generation: {currentGenerationBlendMode}");
    // }
    
    System.Collections.IEnumerator ApplySkyboxWithDelay()
    {
        // Apply random skybox
        ApplyRandomSkybox();
        
        // Wait a frame to let Unity update the environment map
        yield return null;
        
        // Additional small delay to ensure environment map is properly set
        yield return new WaitForSeconds(0.1f);
    }
    
    void RandomizeTextSetup(bool isCardBack, Color cardBlendColor)
    {
        if (textSetup == null) return;
        
        // Randomize the position of the TextSetup GameObject
        Vector3 currentPosition = textSetup.transform.localPosition;
        Vector3 randomOffset = new Vector3(
            Random.Range(0.0f, 0.05f), // Small random X offset
            Random.Range(-0.03f, 0.01f), // Small random Y offset
            0.0f
        );
        
        // Different positioning for back design simulation
        if (isCardBack) {
            randomOffset = new Vector3(
                Random.Range(0.0f, 0.2f), // Different X offset for back design
                Random.Range(-0.03f, 0.03f), // Different Y offset for back design
                0.0f
            );
        }
        textSetup.transform.localPosition = randomOffset;
        
        // Get all children and randomize their Y positions
        List<float> children = new List<float>();
        for (int i = 0; i < textSetup.transform.childCount; i++)
        {
            children.Add(textSetup.transform.GetChild(i).localPosition.y);
        }

        // Shuffle the children list
        for (int i = 0; i < children.Count; i++)
        {
            int rnd = Random.Range(i, children.Count);
            float temp = children[i];
            children[i] = children[rnd];
            children[rnd] = temp;
        }

        // Loop through the textSetup children and reassign the y values
        for (int i = 0; i < textSetup.transform.childCount; i++)
        {
            Transform child = textSetup.transform.GetChild(i);
            Vector3 pos = child.localPosition;
            pos.y = children[i];
            child.localPosition = pos;
        }
        
        // Choose a random font for this rendering
        TMPro.TMP_FontAsset randomFont = null;
        if (cardFonts.Count > 0)
        {
            randomFont = cardFonts[Random.Range(0, cardFonts.Count)];
        }

        
        // Generate a color that contrasts well with cardBlendColor
        // Color.RGBToHSV(cardBlendColor, out float h, out float s, out float v);
        // // Flip the hue by 180 degrees (0.5 in HSV), invert value for strong contrast
        // float contrastHue = (h + 0.1f) % 1.0f;
        // float contrastS = 1.0f;
        // float contrastV = v > 0.8f ? 0.1f : 1.0f; // invert brightness for contrast
        Color randomColor = Color.HSVToRGB(Random.Range(0f,1f), Random.Range(0.8f,1f), Random.Range(0.8f,1f));

        if (Random.Range(0f, 1f) < 0.15f) {
            randomColor = Color.white;
        }
        


       
        
        // Assign random text content and font to credit card fields
        void ApplyRandomTextAndFontRecursive(Transform parent, TMPro.TMP_FontAsset font, Color textColor)
        {
            foreach (Transform child in parent)
            {
                string childName = child.name;
                TMPro.TextMeshPro textMesh = child.GetComponent<TMPro.TextMeshPro>();

                if (textMesh != null)
                {
                    // Apply random font if available
                    if (font != null)
                    {
                        textMesh.font = font;
                    }

                    // Apply text color and chrome effects
                    ApplyTextEffects(textMesh, new Color(textColor.r, textColor.g, textColor.b, 1.0f));

                    // Always assign text content regardless of front/back design
                    switch (childName)
                    {
                        case "Name":
                            textMesh.text = GenerateRandomName();
                            break;
                        case "Number":
                            textMesh.text = GenerateRandomCardNumber();
                            break;
                        case "Expires":
                            textMesh.text = GenerateRandomExpiryDate();
                            break;
                        case "CVC":
                            textMesh.text = GenerateRandomCVC();
                            break;
                        case "Expires Label":
                            string[] options = { "Expires", "Expires On", "Expires On", "Exp", "Exp Date" };
                            textMesh.text = options[Random.Range(0, options.Length)];
                            textMesh.text = string.Join("\n",textMesh.text.Split(" "));
                            break;
                        case "CVC Label":
                            string[] options2 = { "CVC", "CVV", "Sec Code", "CVC2" };
                            textMesh.text = options2[Random.Range(0, options2.Length)];
                            textMesh.text = string.Join("\n",textMesh.text.Split(" "));
                            break;
                    }
                }

                // Recursively process children
                if (child.childCount > 0)
                {
                    ApplyRandomTextAndFontRecursive(child, font, textColor);
                }
            }
        }

        // Call the recursive function for each top-level child
        ApplyRandomTextAndFontRecursive(textSetup.transform, randomFont, randomColor);
        
    }
    
    string GenerateRandomName()
    {
        // Simple inline name generation to avoid compilation issues
        string[] firstNames = {
            "JAMES", "MARY", "JOHN", "PATRICIA", "ROBERT", "JENNIFER", "MICHAEL", "LINDA", "WILLIAM", "ELIZABETH",
            "DAVID", "BARBARA", "RICHARD", "SUSAN", "JOSEPH", "JESSICA", "THOMAS", "SARAH", "CHRISTOPHER", "KAREN",
            "CHARLES", "NANCY", "DANIEL", "LISA", "MATTHEW", "BETTY", "ANTHONY", "HELEN", "MARK", "SANDRA",
            "DONALD", "DONNA", "STEVEN", "CAROL", "PAUL", "RUTH", "ANDREW", "SHARON", "JOSHUA", "MICHELLE",
            "KENNETH", "LAURA", "KEVIN", "EMILY", "BRIAN", "KIMBERLY", "GEORGE", "DEBORAH", "EDWARD", "DOROTHY",
            "RONALD", "LISA", "TIMOTHY", "NANCY", "JASON", "KAREN", "JEFFREY", "BETTY", "RYAN", "HELEN",
            "JACOB", "SANDRA", "GARY", "DONNA", "NICHOLAS", "CAROL", "ERIC", "RUTH", "JONATHAN", "SHARON",
            "STEPHEN", "MICHELLE", "LARRY", "LAURA", "JUSTIN", "EMILY", "SCOTT", "KIMBERLY", "BRANDON", "DEBORAH",
            "BENJAMIN", "DOROTHY", "SAMUEL", "LISA", "FRANK", "NANCY", "GREGORY", "KAREN", "RAYMOND", "BETTY",
            "ALEXANDER", "HELEN", "PATRICK", "SANDRA", "JACK", "DONNA", "DENNIS", "CAROL", "JERRY", "RUTH"
        };
        
        string[] lastNames = {
            "SMITH", "JOHNSON", "WILLIAMS", "BROWN", "JONES", "GARCIA", "MILLER", "DAVIS", "RODRIGUEZ", "MARTINEZ",
            "HERNANDEZ", "LOPEZ", "GONZALEZ", "WILSON", "ANDERSON", "THOMAS", "TAYLOR", "MOORE", "JACKSON", "MARTIN",
            "LEE", "PEREZ", "THOMPSON", "WHITE", "HARRIS", "SANCHEZ", "CLARK", "RAMIREZ", "LEWIS", "ROBINSON",
            "WALKER", "YOUNG", "ALLEN", "KING", "WRIGHT", "SCOTT", "TORRES", "NGUYEN", "HILL", "FLORES",
            "GREEN", "ADAMS", "NELSON", "BAKER", "HALL", "RIVERA", "CAMPBELL", "MITCHELL", "CARTER", "ROBERTS",
            "GOMEZ", "PHILLIPS", "EVANS", "TURNER", "DIAZ", "PARKER", "CRUZ", "EDWARDS", "COLLINS", "REYES",
            "STEWART", "MORRIS", "MORALES", "MURPHY", "COOK", "ROGERS", "GUTIERREZ", "ORTIZ", "MORGAN", "COOPER",
            "PETERSON", "BAILEY", "REED", "KELLY", "HOWARD", "RAMOS", "KIM", "COX", "WARD", "RICHARDSON",
            "WATSON", "BROOKS", "CHAVEZ", "WOOD", "JAMES", "BENNETT", "GRAY", "MENDOZA", "RUIZ", "HUGHES",
            "PRICE", "ALVAREZ", "CASTILLO", "SANDERS", "PATEL", "MYERS", "LONG", "ROSS", "FOSTER", "JIMENEZ"
        };
        
        string[] middleInitials = {
            "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
            "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z"
        };
        string firstName = firstNames[Random.Range(0, firstNames.Length)];
        string lastName = lastNames[Random.Range(0, lastNames.Length)];
        if (Random.Range(0, 100) < 50) {
            return $"{firstName} {lastName}";
        } 
        string middleInitial = middleInitials[Random.Range(0, middleInitials.Length)];
        return $"{firstName} {middleInitial} {lastName}";
    }
    
    string GenerateRandomCardNumber()
    {
        // Generate a random 16-digit card number with spaces
        string number = "";
        for (int i = 0; i < 16; i++)
        {
            number += Random.Range(0, 10).ToString();
            if (i == 3 || i == 7 || i == 11) // Add spaces after 4th, 8th, and 12th digits
            {
                number += " ";
            }
        }
        return number;
    }
    
    string GenerateRandomExpiryDate()
    {
        // Generate a random expiry date (MM/YY format)
        int month = Random.Range(1, 13);
        int year = Random.Range(24, 30); // 2024-2029
        return $"{month:D2}/{year:D2}";
    }
    
    string GenerateRandomCVC()
    {
        // Generate a random 3-digit CVC
        return Random.Range(100, 1000).ToString();
    }
    
    void RandomizeCardTransform()
    {
        if (cardModel == null || mainCamera == null) return;
        
        Transform cardTransform = cardModel.transform.parent;
        
        // Random distance from camera (first-person view) - increased distance
        float distance = 2.0f; // Increased from 0.5-1.0 to 2.0-4.0
        Vector3 cameraForward = mainCamera.transform.forward;
        cardTransform.position = mainCamera.transform.position + cameraForward * distance;
        
        // Start with card facing camera
        //cardTransform.rotation = Quaternion.LookRotation(-cameraForward); // Face camera
        
        // Add small random rotation for natural variation
        Vector3 randomRotation = new Vector3(
            Random.Range(minCardRotation, maxCardRotation),
            Random.Range(minCardRotation, maxCardRotation),
            Random.Range(minCardRotation, maxCardRotation)
        );
        
        // Apply random rotation on top of camera-facing rotation
        cardTransform.rotation = Quaternion.Euler(randomRotation);
    }
    
    void ApplyRandomSkybox()
    {
        if (!useRandomSkybox || skyboxCubemaps.Count == 0) return;
        
        float rotation = Random.Range(0f, 360f);
        
        try
        {
            Cubemap randomSkyboxCubemap = skyboxCubemaps[Random.Range(0, skyboxCubemaps.Count)];
            
            Material skyboxMaterial = new Material(Shader.Find("Skybox/Cubemap"));
            skyboxMaterial.SetTexture("_Tex", randomSkyboxCubemap);
            skyboxMaterial.SetFloat("_Rotation", rotation);
            skyboxMaterial.SetFloat("_Exposure", 1.0f);
            
            RenderSettings.skybox = skyboxMaterial;
            
            // Enhanced environment settings for better reflections
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.0f;
            
            // For URP, we need to set the environment map differently
            // Try both approaches to ensure compatibility
            try
            {
                // Built-in Render Pipeline approach
                RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = randomSkyboxCubemap;
                RenderSettings.reflectionIntensity = Random.Range(0.5f, 2.0f);
            }
            catch (System.Exception reflectionError) 
            {
                Debug.LogWarning($"Built-in reflection setup failed: {reflectionError.Message}");
            }
            
            // URP-specific environment setup (simplified)
            try
            {
                // Set the environment map for URP materials
                Shader.SetGlobalTexture("unity_SpecCube0", randomSkyboxCubemap);
                
                // Alternative URP environment map setup
                Shader.SetGlobalTexture("_EnvironmentMap", randomSkyboxCubemap);
                
                // Force shader to update environment map
                Shader.SetGlobalFloat("_EnvironmentMapHDR", 1.0f);
            }
            catch (System.Exception urpError)
            {
                Debug.LogWarning($"URP reflection setup failed: {urpError.Message}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error applying skybox: {e.Message}");
        }
    }
    
    void ApplyRandomMaterialsWithLogos(bool isCardBack, Color randomColor)
    {
        if (frontMaterial == null) 
        {
            Debug.LogError("Front material is null!");
            return;
        }
        
        if (frontTextures.Count > 0)
        {
            Texture2D randomFrontTexture = frontTextures[Random.Range(0, frontTextures.Count)];
            
            
            float blendAmount = Random.Range(minColorBlend, maxColorBlend);
            if (isCardBack) {
                blendAmount = 1.0f;
            }
            Texture2D blendedTexture = BlendTextureWithColor(randomFrontTexture, randomColor, blendAmount);
            
            Texture2D finalTexture = AddLogosToTexture(blendedTexture, isCardBack);
            
            frontMaterial.mainTexture = finalTexture;
            
            // Set the background texture as the normal map for realistic surface detail
            // if (frontMaterial.HasProperty("_BumpMap"))
            // {
            //     Texture2D normalMap = ConvertToNormalMap(randomFrontTexture);
            //     frontMaterial.SetTexture("_BumpMap", normalMap);
            // }
            // else if (frontMaterial.HasProperty("_NormalMap"))
            // {
            //     // Convert the texture to a normal map before assigning
            //     Texture2D normalMap = ConvertToNormalMap(randomFrontTexture);
            //     frontMaterial.SetTexture("_NormalMap", normalMap);
            // }
            
            // Make the card background shiny
            MakeCardShiny(frontMaterial);
            
            // Refresh environment map on card material after skybox change
            RefreshCardEnvironmentMap(frontMaterial);
        }
        else
        {
            Debug.LogWarning("No front textures available!");
        }
    }
    
    Texture2D ConvertToNormalMap(Texture2D sourceTexture)
    {
        // Create a readable copy if needed
        if (!sourceTexture.isReadable)
        {
            sourceTexture = CreateReadableTextureCopy(sourceTexture);
        }
        
        // Create normal map texture
        Texture2D normalMap = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.RGBA32, false);
        normalMap.filterMode = FilterMode.Bilinear;
        normalMap.wrapMode = TextureWrapMode.Clamp;
        
        // Get source pixels
        Color[] sourcePixels = sourceTexture.GetPixels();
        Color[] normalPixels = new Color[sourcePixels.Length];
        
        // Convert height map to normal map
        for (int y = 0; y < sourceTexture.height; y++)
        {
            for (int x = 0; x < sourceTexture.width; x++)
            {
                // Get current pixel and neighbors
                float current = GetHeight(sourcePixels, x, y, sourceTexture.width, sourceTexture.height);
                float right = GetHeight(sourcePixels, x + 1, y, sourceTexture.width, sourceTexture.height);
                float left = GetHeight(sourcePixels, x - 1, y, sourceTexture.width, sourceTexture.height);
                float up = GetHeight(sourcePixels, x, y + 1, sourceTexture.width, sourceTexture.height);
                float down = GetHeight(sourcePixels, x, y - 1, sourceTexture.width, sourceTexture.height);
                
                // Calculate normal using Sobel filter
                Vector3 normal = CalculateNormal(right, left, up, down, current);
                
                // Convert to normal map format (0.5 + normal * 0.5)
                Color normalColor = new Color(
                    0.5f + normal.x * 0.5f,
                    0.5f + normal.y * 0.5f,
                    0.5f + normal.z * 0.5f,
                    1.0f
                );
                
                normalPixels[y * sourceTexture.width + x] = normalColor;
            }
        }
        
        normalMap.SetPixels(normalPixels);
        normalMap.Apply();
        
        return normalMap;
    }
    
    float GetHeight(Color[] pixels, int x, int y, int width, int height)
    {
        // Handle edge cases
        if (x < 0 || x >= width || y < 0 || y >= height)
            return 0.5f;
        
        // Get grayscale height from RGB
        Color pixel = pixels[y * width + x];
        return (pixel.r + pixel.g + pixel.b) / 3.0f;
    }
    
    Vector3 CalculateNormal(float right, float left, float up, float down, float center)
    {
        // Calculate gradients using Sobel filter
        float dx = (right - left) * 2.0f;
        float dy = (up - down) * 2.0f;
        
        // Create normal vector
        Vector3 normal = new Vector3(-dx, -dy, 1.0f);
        return normal.normalized;
    }
    
    void RefreshCardEnvironmentMap(Material cardMaterial)
    {
        if (cardMaterial == null) return;
        
        // Get the current skybox cubemap
        if (RenderSettings.skybox != null && RenderSettings.skybox.HasProperty("_Tex"))
        {
            Cubemap skyboxCubemap = RenderSettings.skybox.GetTexture("_Tex") as Cubemap;
            if (skyboxCubemap != null)
            {
                // Set environment map directly on card material
                if (cardMaterial.HasProperty("_EnvironmentMap"))
                {
                    cardMaterial.SetTexture("_EnvironmentMap", skyboxCubemap);
                }
                
                // Alternative environment map property
                if (cardMaterial.HasProperty("unity_SpecCube0"))
                {
                    cardMaterial.SetTexture("unity_SpecCube0", skyboxCubemap);
                }
                
                // Force material to update
                cardMaterial.SetPass(0);
            }
        }
    }
    
    void MakeCardShiny(Material material)
    {
        if (material == null) return;
        
        // URP Lit specific properties for shiny, metallic, plasticky appearance
        
        // Very high smoothness for maximum reflectivity
        if (material.HasProperty("_Smoothness"))
        {
            float smoothness = Random.Range(0.95f, 1.0f);
            material.SetFloat("_Smoothness", smoothness);
        }
        else
        {
            Debug.LogWarning("Material does not have _Smoothness property");
        }
        
        // Higher metallic for stronger reflections
        if (material.HasProperty("_Metallic"))
        {
            float metallic = Random.Range(0.0f, 1.0f);
            material.SetFloat("_Metallic", metallic);
        }
        else
        {
            Debug.LogWarning("Material does not have _Metallic property");
        }
        
        // Normal map strength for subtle surface detail
        if (material.HasProperty("_BumpScale"))
        {
            float bumpScale = Random.Range(0.5f, 0.9f);
            material.SetFloat("_BumpScale", bumpScale);
        }
        else
        {
            Debug.LogWarning("Material does not have _BumpScale property");
        }
        
        // Ambient occlusion strength for better depth
        if (material.HasProperty("_OcclusionStrength"))
        {
            // float occlusionStrength = Random.Range(0.6f, 0.8f);
            // material.SetFloat("_OcclusionStrength", occlusionStrength);
        }
        else
        {
            Debug.LogWarning("Material does not have _OcclusionStrength property");
        }
        
        // URP Lit specific - Specular highlights (crucial for reflections)
        if (material.HasProperty("_SpecularHighlights"))
        {
            material.EnableKeyword("_SPECULARHIGHLIGHTS_ON");
        }
        else
        {
            Debug.LogWarning("Material does not have _SpecularHighlights property");
        }
        
        // URP Lit specific - Environment reflections (this is key for skybox reflection)
        if (material.HasProperty("_EnvironmentReflections"))
        {
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_ON");
        }
        else
        {
            Debug.LogWarning("Material does not have _EnvironmentReflections property");
        }
        
        // Additional URP reflection properties
        if (material.HasProperty("_ReflectionProbeUsage"))
        {
            material.SetFloat("_ReflectionProbeUsage", 1.0f);
        }
        
        // Ensure environment map is properly set
        if (material.HasProperty("_EnvironmentMap"))
        {
            // This will be set by the skybox setup
        }
        
        // Force material to refresh environment reflections
        material.SetFloat("_EnvironmentReflections", 1.0f);
        
        // Try to set environment map directly on card material
        if (material.HasProperty("_EnvironmentMap"))
        {
            // Get the current skybox cubemap
            if (RenderSettings.skybox != null && RenderSettings.skybox.HasProperty("_Tex"))
            {
                Cubemap skyboxCubemap = RenderSettings.skybox.GetTexture("_Tex") as Cubemap;
                if (skyboxCubemap != null)
                {
                    material.SetTexture("_EnvironmentMap", skyboxCubemap);
                }
            }
        }
        
        // Alternative environment map property names for card material
        if (material.HasProperty("unity_SpecCube0"))
        {
            if (RenderSettings.skybox != null && RenderSettings.skybox.HasProperty("_Tex"))
            {
                Cubemap skyboxCubemap = RenderSettings.skybox.GetTexture("_Tex") as Cubemap;
                if (skyboxCubemap != null)
                {
                    material.SetTexture("unity_SpecCube0", skyboxCubemap);
                }
            }
        }
        
        // Set render queue for proper rendering
        material.renderQueue = 2000;
        
        // Ensure proper shader keywords for URP Lit
        // material.EnableKeyword("_RECEIVE_SHADOWS_ON");
        // material.EnableKeyword("_MAIN_LIGHT_SHADOWS");
        // material.EnableKeyword("_MAIN_LIGHT_SHADOWS_CASCADE");
        
        // // Additional URP keywords for better reflections
        // material.EnableKeyword("_ADDITIONAL_LIGHTS");
        // material.EnableKeyword("_ADDITIONAL_LIGHT_SHADOWS");
        
        // Force material to update
        material.SetPass(0);
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
    
    Texture2D AddLogosToTexture(Texture2D baseTexture, bool isCardBack)
    {
        Texture2D result = GetPooledTexture(baseTexture.width, baseTexture.height);
        
        // Copy base texture using GetPixels32 for better performance
        Color32[] basePixels = baseTexture.GetPixels32();
        result.SetPixels32(basePixels);
        
        
        // Add chip
        if (chipTextures.Count > 0 && !isCardBack)
        {
            Texture2D chipTexture = chipTextures[Random.Range(0, chipTextures.Count)];
            Vector2 chipCenter = CalculateChipPosition();
            AddLogoToTexture(result, chipTexture, chipCenter, chipSize, chipOpacity);
        }
        
        // Add brand logo (independent of chip)
        if (logoTextures.Count > 0)
        {
            Texture2D brandLogoTexture = logoTextures[Random.Range(0, logoTextures.Count)];
            Vector2 brandLogoCenter = new Vector2(1.0f, -1.0f); // Bottom right corner
            AddLogoToTexture(result, brandLogoTexture, brandLogoCenter, brandLogoSize, brandLogoOpacity);
        }
        
        // Add bank logo (independent of chip)
        if (bankLogoTextures.Count > 0)
        {
            Texture2D bankLogoTexture = bankLogoTextures[Random.Range(0, bankLogoTextures.Count)];
            Vector2 bankLogoCenter = new Vector2(1.0f, 1.0f); // Upper right corner
            if (isCardBack) {
                bankLogoCenter = new Vector2(-1.0f, -1.0f); // Bottom left corner
            }
            AddLogoToTexture(result, bankLogoTexture, bankLogoCenter, bankLogoSize, bankLogoOpacity);
        }
        
        // Add magnetic stripe to back of card
        if (isCardBack) // Only add magnetic stripe to back of card
        {
            AddMagneticStripeToTexture(result);
            
            // Add signature below the magnetic stripe
            if (signatureTexture != null)
            {
                AddSignatureToTexture(result);
            }
     
            // Add RFID if it's the back of the card
            if (rfidTexture != null)
            {
                AddLogoToTexture(result, rfidTexture, new Vector2(0.7f, 0.0f), 0.15f, 1.0f);
            } else {
                Debug.LogWarning("rfid texture not found at: ");
            }
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

        int logoWidth = 0;
        int logoHeight = 0;

        // Calculate logo dimensions in pixels
        // maxWidth is a percentage of the card width (0.0 to 1.0)
        
        logoWidth = (int)(maxWidth * targetTexture.width);
        logoHeight = (int)(logoWidth * ((float)logoTexture.height / logoTexture.width)); // Preserve aspect ratio
   
        
        // Calculate start position in texture coordinates (center-based)
        int startX = (int)(textureCenter.x * targetTexture.width - logoWidth * 0.5f);
        int startY = (int)(textureCenter.y * targetTexture.height - logoHeight * 0.5f);
        
        // Ensure we don't go out of bounds
        // Maintain padding on edges
        int paddingX = Mathf.RoundToInt(logoPadding * targetTexture.width);
        int paddingY = Mathf.RoundToInt(logoPadding * targetTexture.height);
        startX = Mathf.Clamp(startX, paddingX, targetTexture.width - logoWidth - paddingX);
        startY = Mathf.Clamp(startY, paddingY, targetTexture.height - logoHeight - paddingY);
        
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
        if (magneticStripeGenerator == null) 
        {
            Debug.LogWarning("Magnetic stripe generator is null!");
            return;
        }
        
        try
        {
            Texture2D stripeTexture = magneticStripeGenerator.GenerateMagneticStripe(targetTexture.width, targetTexture.height);
            
            if (stripeTexture == null)
            {
                Debug.LogError("Failed to generate magnetic stripe texture!");
                return;
            }
            
            for (int x = 0; x < targetTexture.width; x++)
            {
                for (int y = 0; y < targetTexture.height; y++)
                {
                    Color targetColor = targetTexture.GetPixel(x, y);
                    Color stripeColor = stripeTexture.GetPixel(x, y);
                    
                    float alpha = stripeColor.a;
                    Color blendedColor = Color.Lerp(targetColor, stripeColor, alpha);
                    
                    targetTexture.SetPixel(x, y, blendedColor);
                }
            }
            
            DestroyImmediate(stripeTexture);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Could not add magnetic stripe: {e.Message}");
        }
    }
    
    void AddSignatureToTexture(Texture2D targetTexture)
    {
        if (signatureTexture == null)
        {
            Debug.LogWarning("Signature texture is null!");
            return;
        }

        // Calculate signature position
        Vector2 signatureCenter = new Vector2(0.0f, 0.25f); // Position it at the bottom center of the card
        Vector2 textureCenter = new Vector2(
            (signatureCenter.x + 1f) * 0.5f,
            (signatureCenter.y + 1f) * 0.5f
        );

        textureCenter += new Vector2(Random.Range(-0.02f, 0.00f), Random.Range(-0.01f, 0.01f));

        int signatureWidth = 0;
        int signatureHeight = 0;

        // Calculate signature dimensions in pixels - increased size
        if (signatureTexture.isReadable)
        {
            // Use a larger percentage of card width for signature
            float signatureSizeRatio = 0.5f; // 50% of card width instead of 10%
            signatureWidth = (int)(signatureSizeRatio * targetTexture.width);
            signatureHeight = (int)(signatureWidth * ((float)signatureTexture.height / signatureTexture.width)); // Preserve aspect ratio
        }
        else
        {
            Debug.LogWarning("Signature texture is not readable. Cannot add signature.");
            return;
        }
        
        // Calculate start position in texture coordinates (center-based)
        int startX = (int)(textureCenter.x * targetTexture.width - signatureWidth * 0.5f);
        int startY = (int)(textureCenter.y * targetTexture.height - signatureHeight * 0.5f);
        
        // Ensure we don't go out of bounds
        int paddingX = Mathf.RoundToInt(logoPadding * targetTexture.width);
        int paddingY = Mathf.RoundToInt(logoPadding * targetTexture.height);
        startX = Mathf.Clamp(startX, paddingX, targetTexture.width - signatureWidth - paddingX);
        startY = Mathf.Clamp(startY, paddingY, targetTexture.height - signatureHeight - paddingY);

        // Fast GPU-accelerated blending using RenderTexture
        BlendLogoFast(targetTexture, signatureTexture, startX, startY, signatureWidth, signatureHeight, 1.0f);
    }
    
    System.Collections.IEnumerator CaptureAndSave()
    {
        yield return new WaitForEndOfFrame();
        
        // Capture the render texture
        RenderTexture.active = renderTexture;
        captureTexture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        captureTexture.Apply();
        RenderTexture.active = null;
        
        // Generate filename
        string filename = $"credit_card_{currentImageCount:D6}.jpg";

        // If YOLO data saving is enabled, determine split and write image directly to YOLO images directory
        if (saveYoloData)
        {
            string split = DetermineDataSplit();
            string yoloDir = Path.Combine(Application.dataPath, "..", "SyntheticCreditCardData", "yolo", split);
            string imagesDir = Path.Combine(yoloDir, "images");
            Directory.CreateDirectory(imagesDir);
            string destImagePath = Path.Combine(imagesDir, filename);

            // Convert to JPG and save directly to YOLO images directory
            byte[] imageData = captureTexture.EncodeToJPG(80);
            File.WriteAllBytes(destImagePath, imageData);

            // Save YOLO annotation
            SaveYoloAnnotation(filename, currentImageCount, split, yoloDir, imagesDir);
        }
        else
        {
            // If not saving YOLO data, save to the parent folder as before
            string filepath = Path.Combine(Application.dataPath, "..", "SyntheticCreditCardData", filename);
            Directory.CreateDirectory(Path.GetDirectoryName(filepath));
            byte[] imageData = captureTexture.EncodeToJPG(80);
            File.WriteAllBytes(filepath, imageData);
        }
        
        currentImageCount++;
        totalImagesGenerated++;
        
        // Generate next card
        GenerateNextCard();
    }
    
    void SaveYoloAnnotation(string imageFilename, int imageIndex, string split = null, string yoloDir = null, string imagesDir = null)
    {
        // If split/yoloDir/imagesDir are not provided, determine them (for backward compatibility)
        if (string.IsNullOrEmpty(split))
            split = DetermineDataSplit();
        if (string.IsNullOrEmpty(yoloDir))
            yoloDir = Path.Combine(Application.dataPath, "..", "SyntheticCreditCardData", "yolo", split);
        if (string.IsNullOrEmpty(imagesDir))
            imagesDir = Path.Combine(yoloDir, "images");

        // Create YOLO annotation filename
        string annotationFilename = Path.ChangeExtension(imageFilename, ".txt");
        string labelsDir = Path.Combine(yoloDir, "labels");
        string annotationPath = Path.Combine(labelsDir, annotationFilename);
        
        // Ensure directories exist
        Directory.CreateDirectory(imagesDir);
        Directory.CreateDirectory(labelsDir);
        
        // Generate YOLO annotations
        List<string> yoloAnnotations = GenerateYoloAnnotations();
        
        // Write annotations to file
        File.WriteAllLines(annotationPath, yoloAnnotations);

        // No need to copy image, as it is already written directly to the YOLO images directory
    }
    
    string DetermineDataSplit()
    {
        float randomValue = Random.Range(0f, 1f);
        
        if (randomValue < trainSplit)
            return "train";
        else if (randomValue < trainSplit + validSplit)
            return "valid";
        else
            return "test";
    }

    public Bounds GetTextBounds(TextMeshPro tmp)
    {
        tmp.ForceMeshUpdate(); // make sure the mesh is up to date
        TMP_TextInfo textInfo = tmp.textInfo;

        if (textInfo.characterCount == 0)
            return new Bounds(Vector3.zero, Vector3.zero);

        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible)
                continue;

            Vector3 bl = charInfo.bottomLeft;
            Vector3 tr = charInfo.topRight;

            min = Vector3.Min(min, bl);
            max = Vector3.Max(max, tr);
        }

        Bounds bounds = new Bounds();
        bounds.SetMinMax(min, max);
        return bounds;
    }
    
    List<string> GenerateYoloAnnotations()
    {
        List<string> annotations = new List<string>();
        
        // Get card dimensions in pixels
        int imageWidth = renderTexture.width;
        int imageHeight = renderTexture.height;
        
        // Add card bounding box (Front or Back)
        string cardClass = isCardBack ? "Back" : "Front";
        Vector4 cardBbox = CalculateCardBoundingBox(imageWidth, imageHeight);
        annotations.Add($"{yoloClasses[cardClass]} {cardBbox.x:F6} {cardBbox.y:F6} {cardBbox.z:F6} {cardBbox.w:F6}");
        
        // Add text element bounding boxes - search recursively
        if (textSetup != null)
        {
            FindAndAnnotateTextElements(textSetup.transform, annotations, imageWidth, imageHeight);
        }
        
        return annotations;
    }
    
    void FindAndAnnotateTextElements(Transform parent, List<string> annotations, int imageWidth, int imageHeight)
    {
        // Check all children recursively
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            
            // Check if this child has a TextMeshPro component
            TMPro.TextMeshPro textMesh = child.GetComponent<TMPro.TextMeshPro>();
            if (textMesh != null)
            {
                string childName = child.name;
                if (yoloClasses.ContainsKey(childName))
                {
                    Vector4 textBbox = CalculateTextBoundingBoxTight(textMesh, imageWidth, imageHeight);
                    
                    if (textBbox.z > 0 && textBbox.w > 0) // Only add if bounding box has size
                    {
                        annotations.Add($"{yoloClasses[childName]} {textBbox.x:F6} {textBbox.y:F6} {textBbox.z:F6} {textBbox.w:F6}");
                    }
                }
            }
            
            // Recursively search children of this child
            FindAndAnnotateTextElements(child, annotations, imageWidth, imageHeight);
        }
    }
    
    Vector4 CalculateTextBoundingBoxTight(TMPro.TextMeshPro textMesh, int imageWidth, int imageHeight)
    {
        if (textMesh == null || string.IsNullOrEmpty(textMesh.text)) 
            return Vector4.zero;
        
        // Get tight bounds using the GetTextBounds function
        Bounds textBounds = GetTextBounds(textMesh);
        
        if (textBounds.size == Vector3.zero)
            return Vector4.zero;
        
        // Convert world bounds to screen coordinates (2D card, use 4 corners with z values)
        Vector3[] corners = new Vector3[4];
        corners[0] = new Vector3(textBounds.min.x, textBounds.min.y, textBounds.min.z); // Bottom-left
        corners[1] = new Vector3(textBounds.max.x, textBounds.min.y, textBounds.min.z); // Bottom-right
        corners[2] = new Vector3(textBounds.min.x, textBounds.max.y, textBounds.max.z); // Top-left
        corners[3] = new Vector3(textBounds.max.x, textBounds.max.y, textBounds.max.z); // Top-right
        
        // Convert all corners to screen space
        Vector2 minScreen = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 maxScreen = new Vector2(float.MinValue, float.MinValue);
        
        for (int i = 0; i < 4; i++)
        {
            // Convert from local space to world space using text mesh transform
            Vector3 worldCorner = textMesh.transform.TransformPoint(corners[i]);
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldCorner);
            minScreen.x = Mathf.Min(minScreen.x, screenPos.x);
            minScreen.y = Mathf.Min(minScreen.y, screenPos.y);
            maxScreen.x = Mathf.Max(maxScreen.x, screenPos.x);
            maxScreen.y = Mathf.Max(maxScreen.y, screenPos.y);
        }

        
        // Convert to normalized coordinates (0-1)
        float centerX = (minScreen.x + maxScreen.x) / 2f / imageWidth;
        float centerY = 1.0f - ((minScreen.y + maxScreen.y) / 2f / imageHeight); // YOLO uses top-left origin
        float width = (maxScreen.x - minScreen.x) / imageWidth;
        float height = (maxScreen.y - minScreen.y) / imageHeight;
        
        // Ensure bounding box is within image bounds
        centerX = Mathf.Clamp(centerX, width/2, 1.0f - width/2);
        centerY = Mathf.Clamp(centerY, height/2, 1.0f - height/2);
        
        return new Vector4(centerX, centerY, width, height);
    }
    
    Vector4 CalculateCardBoundingBox(int imageWidth, int imageHeight)
    {
        // Use specific local coordinates for card corners
        Vector3[] cardCorners = new Vector3[4];
        cardCorners[0] = new Vector3(-0.0115f, -0.0076f, 0f); // Bottom-left
        cardCorners[1] = new Vector3(0.0125f, -0.0076f, 0f);  // Bottom-right
        cardCorners[2] = new Vector3(-0.0115f, 0.0076f, 0f);  // Top-left
        cardCorners[3] = new Vector3(0.0125f, 0.0076f, 0f);   // Top-right
        
        // Transform corners to world space using card model transform
        Vector2 minScreen = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 maxScreen = new Vector2(float.MinValue, float.MinValue);
        
        for (int i = 0; i < 4; i++)
        {
            // Transform from local to world space
            Vector3 worldCorner = cardModel.transform.TransformPoint(cardCorners[i]);
            
            // Convert to screen space
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldCorner);
            if (screenPos.z > 0) // Only consider points in front of camera
            {
                minScreen.x = Mathf.Min(minScreen.x, screenPos.x);
                minScreen.y = Mathf.Min(minScreen.y, screenPos.y);
                maxScreen.x = Mathf.Max(maxScreen.x, screenPos.x);
                maxScreen.y = Mathf.Max(maxScreen.y, screenPos.y);
            }
        }
        
        // Convert to normalized coordinates (0-1)
        float centerX = (minScreen.x + maxScreen.x) / 2f / imageWidth;
        float centerY = 1.0f - ((minScreen.y + maxScreen.y) / 2f / imageHeight); // YOLO uses top-left origin
        float width = (maxScreen.x - minScreen.x) / imageWidth;
        float height = (maxScreen.y - minScreen.y) / imageHeight;
        
        // Ensure bounding box is within image bounds
        centerX = Mathf.Clamp(centerX, width/2, 1.0f - width/2);
        centerY = Mathf.Clamp(centerY, height/2, 1.0f - height/2);
        
        return new Vector4(centerX, centerY, width, height);
    }
    
    void SaveYoloClassesFile()
    {
        string yoloDir = Path.Combine(Application.dataPath, "..", "SyntheticCreditCardData", "yolo");
        Directory.CreateDirectory(yoloDir);
        
        string classesPath = Path.Combine(yoloDir, "classes.txt");
        List<string> classNames = new List<string>();
        
        foreach (var kvp in yoloClasses.OrderBy(x => x.Value))
        {
            classNames.Add(kvp.Key);
        }
        
        File.WriteAllLines(classesPath, classNames);
        
        // Also create a data.yaml file for YOLO training
        CreateYoloDataYaml();
    }
    
    void CreateYoloDataYaml()
    {
        string yoloDir = Path.Combine(Application.dataPath, "..", "SyntheticCreditCardData", "yolo");
        string dataYamlPath = Path.Combine(yoloDir, "data.yaml");
        
        List<string> yamlContent = new List<string>();
        yamlContent.Add("# YOLO dataset configuration");
        yamlContent.Add($"path: {yoloDir}");
        yamlContent.Add("train: train/images");
        yamlContent.Add("val: valid/images");
        yamlContent.Add("test: test/images");
        yamlContent.Add("");
        yamlContent.Add("# Classes");
        yamlContent.Add($"nc: {yoloClasses.Count}");
        yamlContent.Add("names:");
        
        foreach (var kvp in yoloClasses.OrderBy(x => x.Value))
        {
            yamlContent.Add($"  {kvp.Value}: {kvp.Key}");
        }
        
        File.WriteAllLines(dataYamlPath, yamlContent);
    }
    
    void OnDestroy()
    {
        
    }
    
    void CleanupReadableTextures()
    {
        
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
    
    void CleanupCorruptedFontAssets()
    {
        if (!Directory.Exists(cardFontsFolder))
            return;
            
        string[] assetFiles = Directory.GetFiles(cardFontsFolder, "*.asset");
        foreach (string assetFile in assetFiles)
        {
            TMPro.TMP_FontAsset fontAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(assetFile);
            if (fontAsset != null && fontAsset.atlasTexture == null)
            {
                // Debug.LogWarning($"Removing corrupted font asset: {fontAsset.name}");
                UnityEditor.AssetDatabase.DeleteAsset(assetFile);
            }
        }
        UnityEditor.AssetDatabase.Refresh();
    }
    
    // Public method to trigger generation from UI
    public void StartGeneration()
    {
        GenerateCreditCards();
    }
    
    // Public method to set number of images to generate
    public void SetImageCount(int count)
    {
        numberOfImages = count;
    }
    
    // Public method to manually test skybox changes
    public void TestSkyboxChange()
    {
        ApplyRandomSkybox();
    }
    
    // Public method to scale card to standard dimensions

    // Calculate EMV chip position based on real-world specifications
    Vector2 CalculateChipPosition()
    {
        // Real-world specifications from the technical diagram:
        // Card dimensions: 3.37" x 2.125"
        // Chip dimensions: 0.4591" x 0.3692"
        // Chip position from left edge: 3/8" (0.375")
        // Chip position from top edge: 11/16" (0.6875")
        
        float cardWidth = 3.37f;
        float cardHeight = 2.125f;
        float chipLeftEdge = 0.375f;
        float chipTopEdge = 0.6875f;
        float chipWidth = 0.4591f;
        float chipHeight = 0.3692f;
        
        // Calculate chip center position in inches
        float chipCenterX = chipLeftEdge + (chipWidth / 2f);
        float chipCenterY = chipTopEdge + (chipHeight / 2f);
        
        // Convert to center-based coordinates (0,0 at card center)
        float normalizedX = (chipCenterX - (cardWidth / 2f)) / (cardWidth / 2f);
        float normalizedY = ((cardHeight / 2f) - chipCenterY) / (cardHeight / 2f);
        
        return new Vector2(normalizedX, normalizedY);
    }

    
    
    
    void ApplyTextEffects(TMPro.TextMeshPro textMesh, Color textColor)
    {
        if (textMesh == null) return;

        // Enhance text color for better contrast
        
        
        // Apply gradient chrome effect
        ApplyGradientChromeEffect(textMesh, textColor);
        
        // Add TextMeshPro lighting bevel effects
        ApplyTextMeshProLightingEffects(textMesh);
        
        // Add strong outline for better contrast
        if (textMesh.fontSharedMaterial.HasProperty("_OutlineColor"))
        {
            Color outlineColor = Color.black;
            textMesh.fontSharedMaterial.SetColor("_OutlineColor", outlineColor);
            textMesh.fontSharedMaterial.SetFloat("_OutlineWidth", Random.Range(0.02f, 0.04f));
        }
        
        // Add shadow for depth
        if (textMesh.fontSharedMaterial.HasProperty("_UnderlayColor"))
        {
            Color shadowColor = new Color(0f, 0f, 0f, 0.5f);
            textMesh.fontSharedMaterial.SetColor("_UnderlayColor", shadowColor);
            textMesh.fontSharedMaterial.SetFloat("_UnderlayOffsetX", Random.Range(0.001f, 0.003f));
            textMesh.fontSharedMaterial.SetFloat("_UnderlayOffsetY", Random.Range(-0.003f, -0.001f));
            textMesh.fontSharedMaterial.SetFloat("_UnderlayDilate", Random.Range(0.5f, 1.0f));
        }
        
        // Force text mesh to update without triggering material property errors
        textMesh.ForceMeshUpdate();
    }
    
    void ApplyTextMeshProLightingEffects(TMPro.TextMeshPro textMesh)
    {
        if (textMesh.fontSharedMaterial == null) return;
        
        Material textMaterial = textMesh.fontSharedMaterial;
        
        // Apply lighting bevel effects with safety checks
        if (textMaterial.HasProperty("_LightingBevel"))
        {
            textMaterial.SetFloat("_LightingBevel", 1.0f);
        }
        
        if (textMaterial.HasProperty("_LightAngle"))
        {
            float lightAngle = Random.Range(0f, 6.28f);
            textMaterial.SetFloat("_LightAngle", lightAngle);
        }
        
        if (textMaterial.HasProperty("_Reflectivity"))
        {
            float reflectivityPower = Random.Range(5.0f, 15.0f);
            textMaterial.SetFloat("_Reflectivity", reflectivityPower);
        }
        
        // Set bevel amount
        if (textMaterial.HasProperty("_BevelAmount"))
        {
            float bevelAmount = Random.Range(0.0f, 0.3f);
            textMaterial.SetFloat("_BevelAmount", bevelAmount);
        }

        if (textMaterial.HasProperty("_SpecularPower")) {
            float specularPower = Random.Range(1.0f, 3.0f);
            textMaterial.SetFloat("_SpecularPower", specularPower);
        }
        
        // Set bevel width
        if (textMaterial.HasProperty("_BevelWidth"))
        {
            float bevelWidth = Random.Range(0.1f, 0.2f);
            textMaterial.SetFloat("_BevelWidth", bevelWidth);
        }
        
        // Set bevel offset
        if (textMaterial.HasProperty("_BevelOffset"))
        {
            float bevelOffset = Random.Range(0.0f, 0.1f);
            textMaterial.SetFloat("_BevelOffset", bevelOffset);
        }
        
        // Set bevel roundness
        if (textMaterial.HasProperty("_BevelRoundness"))
        {
            float bevelRoundness = Random.Range(0.1f, 0.5f);
            textMaterial.SetFloat("_BevelRoundness", bevelRoundness);
        }
        
        // Set local lighting intensity
        if (textMaterial.HasProperty("_LocalLightingIntensity"))
        {
            float localLightingIntensity = Random.Range(0.5f, 1.5f);
            textMaterial.SetFloat("_LocalLightingIntensity", localLightingIntensity);
        }
        
        // Set local lighting color
        if (textMaterial.HasProperty("_LocalLightingColor"))
        {
            Color localLightingColor = new Color(
                Random.Range(0.8f, 1.2f),
                Random.Range(0.8f, 1.2f),
                Random.Range(0.8f, 1.2f),
                1.0f
            );
            textMaterial.SetColor("_LocalLightingColor", localLightingColor);
        }
        
        // Set diffuse
        if (textMaterial.HasProperty("_Diffuse"))
        {
            textMaterial.SetFloat("_Diffuse", Random.Range(0.0f, 0.1f));
        }
        
        // Set underlay softness
        // if (textMaterial.HasProperty("_UnderlaySoftness"))
        // {
        //     float underlaySoftness = Random.Range(0.1f, 0.3f);
        //     textMaterial.SetFloat("_UnderlaySoftness", underlaySoftness);
        // }
    }
    
    
    
   
    
    void ApplyGradientChromeEffect(TMPro.TextMeshPro textMesh, Color baseColor)
    {
        if (textMesh.fontSharedMaterial == null) return;
        
        // Generate stronger color variations for better contrast
        float variation = 0.1f;
        Color gradientColor1 = baseColor * 1.5f; // Brighter main color
        
        Color gradientColor2 = new Color(
            Mathf.Clamp01(baseColor.r - variation ),
            Mathf.Clamp01(baseColor.g - variation ),
            Mathf.Clamp01(baseColor.b - variation ),
            baseColor.a
        );

        // Apply gradient effect through material properties - URP compatible
        if (textMesh.fontSharedMaterial.HasProperty("_FaceColor"))
        {
            textMesh.fontSharedMaterial.SetColor("_FaceColor", gradientColor1);
        }
        if (textMesh.fontSharedMaterial.HasProperty("_OutlineColor1"))
        {
            //textMesh.fontSharedMaterial.SetColor("_OutlineColor1", gradientColor2);
        }
        
        if (textMesh.fontSharedMaterial.HasProperty("_OutlineColor2"))
        {
            textMesh.fontSharedMaterial.SetColor("_OutlineColor2", Color.black);
        }

        if (textMesh.fontSharedMaterial.HasProperty("_IsoPerimeter")) {
            //float isoPerimeter = Random.Range(-1.0f, 0.0f);
            textMesh.fontSharedMaterial.SetVector("_IsoPerimeter", new Vector4(0, -1f, -1f, -1f));
        }

        if (textMesh.fontSharedMaterial.HasProperty("_Softness")) {
            textMesh.fontSharedMaterial.SetVector("_Softness", new Vector4(1.0f,0,0,0));
        }

        if (textMesh.fontSharedMaterial.HasProperty("_UnderlayDilate")) {
            textMesh.fontSharedMaterial.SetFloat("_UnderlayDilate", -1);
        }
    }


    void LoadSkyboxCubemaps()
    {
        if (!Directory.Exists(skyboxImagesFolder))
        {
            Debug.LogWarning($"Skybox folder not found: {skyboxImagesFolder}");
            return;
        }
        
        // Load cubemap assets from the folder
        string[] cubemapExtensions = { "*.cubemap" };
        foreach (string extension in cubemapExtensions)
        {
            string[] files = Directory.GetFiles(skyboxImagesFolder, extension);
            foreach (string file in files)
            {
                Cubemap cubemap = UnityEditor.AssetDatabase.LoadAssetAtPath<Cubemap>(file);
                if (cubemap != null)
                {
                    skyboxCubemaps.Add(cubemap);
                }
            }
        }
        
        // Also try to load any texture files that might be cubemaps
        string[] textureExtensions = { "*.png", "*.jpg", "*.jpeg", "*.exr", "*.hdr" };
        foreach (string extension in textureExtensions)
        {
            string[] files = Directory.GetFiles(skyboxImagesFolder, extension);
            foreach (string file in files)
            {
                // Try loading as cubemap first
                Cubemap cubemap = UnityEditor.AssetDatabase.LoadAssetAtPath<Cubemap>(file);
                if (cubemap != null)
                {
                    skyboxCubemaps.Add(cubemap);
                }
                else
                {
                    // If not a cubemap, try as texture and check if it's a cubemap texture
                    Texture2D texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(file);
                    if (texture != null && texture.dimension == UnityEngine.Rendering.TextureDimension.Cube)
                    {
                        // For cubemap textures, we need to create a cubemap from the texture
                        // This is more complex and might require manual cubemap creation
                        //Debug.Log($"Found cubemap texture: {texture.name}, but manual conversion needed");
                    }
                }
            }
        }
        
    }


}
