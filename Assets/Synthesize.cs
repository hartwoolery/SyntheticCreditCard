using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using System.Linq; // Added for Concat

public class Synthesize : MonoBehaviour
{
    [Header("Card Model")]
    public GameObject cardModel;
    public Material frontMaterial;
    
    [Header("Camera")]
    Camera mainCamera;
    
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
    bool useChromeReflection = true;
    
    [Header("Randomization")]
    float minCardRotation = -5f;
    float maxCardRotation = 5f;
    float minColorBlend = 0.0f;
    float maxColorBlend = 0.4f;
    float chipOpacity = 1.0f;
    float brandLogoOpacity = 1.0f;
    float bankLogoOpacity = 1.0f;
    float generationDelay = 0.1f;
    
    bool pauseOnFocusLoss = false;
    bool continuousGeneration = true;
    float memoryCleanupInterval = 10f;
    float chromeReflectionIntensity = 0.5f;
    
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
    List<Color> cardColors = new List<Color>
    {
        Color.white,
        Color.black,
        Color.gray,
        Color.blue,
        Color.red,
        Color.green,
        Color.yellow,
        Color.cyan,
        Color.magenta
    };
    
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
    private bool isGenerating = false;
    private int currentImageCount = 0;
    private float lastMemoryCleanup = 0f;
    private MagneticStripeGenerator magneticStripeGenerator;
    private Queue<Texture2D> texturePool = new Queue<Texture2D>();
    private const int MAX_POOL_SIZE = 10;
    
    // Remove the blend mode field
    // private string currentGenerationBlendMode = "Alpha";
    
    void Start()
    {
        InitializeSystem();
        LoadAssets();
        SetupCamera();
        SetupMagneticStripe();
        SetupRenderTexture();
        
        GenerateCreditCards();
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
        LoadSignature();
    }
    
    void LoadSignature()
    {
        string signaturePath = "Assets/signature.png";
        signatureTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(signaturePath);
        if (signatureTexture != null)
        {
            // Create a readable copy if needed
            if (!signatureTexture.isReadable)
            {
                signatureTexture = CreateReadableTextureCopy(signatureTexture);
            }
            
            // Set texture properties
            signatureTexture.filterMode = FilterMode.Bilinear;
            signatureTexture.wrapMode = TextureWrapMode.Clamp;
        }
        else
        {
            Debug.LogWarning("Signature texture not found at: " + signaturePath);
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
        string[] ttfFiles = Directory.GetFiles(folderPath, "*.ttf");
        string[] otfFiles = Directory.GetFiles(folderPath, "*.otf");
        string[] allFontFiles = ttfFiles.Concat(otfFiles).ToArray();
        
        foreach (string fontFile in allFontFiles)
        {
            string fileName = Path.GetFileNameWithoutExtension(fontFile);
            string assetPath = Path.Combine(folderPath, fileName + ".asset");
            
            if (File.Exists(assetPath))
            {
                TMPro.TMP_FontAsset existingAsset = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(assetPath);
                if (existingAsset != null && existingAsset.atlasTexture != null)
                {
                    continue;
                }
                else
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }
            }
            
            try
            {
                Font font = AssetDatabase.LoadAssetAtPath<Font>(fontFile);
                if (font == null)
                {
                    Debug.LogWarning($"Could not load font file: {fontFile}");
                    continue;
                }
                
                TMPro.TMP_FontAsset tmpFontAsset = TMPro.TMP_FontAsset.CreateFontAsset(font, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, TMPro.AtlasPopulationMode.Dynamic);
                if (tmpFontAsset != null)
                {
                    if (tmpFontAsset.atlasTexture == null)
                    {
                        Debug.LogWarning($"Atlas texture not generated for: {fileName}, skipping");
                        DestroyImmediate(tmpFontAsset);
                        continue;
                    }
                    
                    AssetDatabase.CreateAsset(tmpFontAsset, assetPath);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
                else
                {
                    Debug.LogWarning($"Failed to create TMP_FontAsset for: {fontFile}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error creating TMP_FontAsset for {fontFile}: {e.Message}");
            }
        }
    }
    
    void SetupMagneticStripe()
    {
        magneticStripeGenerator = new MagneticStripeGenerator();
    }
    
    void SetupCamera()
    {
        if (mainCamera == null) return;
        
        // Set camera to render texture
        // mainCamera.targetTexture = renderTexture;
        
        // Built-in Render Pipeline camera settings
        mainCamera.clearFlags = CameraClearFlags.Skybox;
        mainCamera.farClipPlane = 1000f;
        mainCamera.nearClipPlane = 0.1f;
        mainCamera.fieldOfView = 60f;
        
        // Enable anti-aliasing for smoother edges
        mainCamera.allowMSAA = true;
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
        
        bool isCardBack = Random.Range(0f, 1f) < 0.5f;
        
        // Remove blend mode selection
        // SelectRandomBlendModeForGeneration();
        
        // Randomize card position and rotation
        RandomizeCardTransform();
        
        // Randomize text setup position and content
        RandomizeTextSetup(isCardBack);
        
        // Apply random skybox with delay
        StartCoroutine(ApplySkyboxWithDelay());
        
        // Apply random materials with superimposed logos
        ApplyRandomMaterialsWithLogos(isCardBack);
        
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
        
        Debug.Log("Skybox applied with delay, environment map should be updated");
    }
    
    void RandomizeTextSetup(bool isCardBack)
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

        // Generate random color
        Color randomColor = GenerateRandomTextColor();
        
        randomColor = GenerateChromeReflectionColor(randomColor);


       
        
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
                    ApplyTextEffects(textMesh, textColor);

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
        cardTransform.rotation *= Quaternion.Euler(randomRotation);
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
                RenderSettings.reflectionIntensity = Random.Range(1.0f, 3.0f);
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
            
            Debug.Log($"Applied skybox with rotation: {rotation}, cubemap: {randomSkyboxCubemap.name}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error applying skybox: {e.Message}");
        }
    }
    
    void ApplyRandomMaterialsWithLogos(bool isCardBack)
    {
        if (frontMaterial == null) 
        {
            Debug.LogError("Front material is null!");
            return;
        }
        
        if (frontTextures.Count > 0)
        {
            Texture2D randomFrontTexture = frontTextures[Random.Range(0, frontTextures.Count)];
            
            Color randomColor = cardColors[Random.Range(0, cardColors.Count)];
            float blendAmount = Random.Range(minColorBlend, maxColorBlend);
            if (isCardBack) {
                blendAmount = 1.0f;
                randomColor *= 0.5f;
            }
            Texture2D blendedTexture = BlendTextureWithColor(randomFrontTexture, randomColor, blendAmount);
            
            Texture2D finalTexture = AddLogosToTexture(blendedTexture, isCardBack);
            
            frontMaterial.mainTexture = finalTexture;
            
            // Set the background texture as the normal map for realistic surface detail
            if (frontMaterial.HasProperty("_BumpMap"))
            {
                frontMaterial.SetTexture("_BumpMap", randomFrontTexture);
                Debug.Log($"Set background texture as normal map: {randomFrontTexture.name}");
            }
            else if (frontMaterial.HasProperty("_NormalMap"))
            {
                frontMaterial.SetTexture("_NormalMap", randomFrontTexture);
                Debug.Log($"Set background texture as normal map: {randomFrontTexture.name}");
            }
            
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
                
                Debug.Log($"Refreshed environment map on card material: {skyboxCubemap.name}");
            }
        }
    }
    
    void MakeCardShiny(Material material)
    {
        if (material == null) return;
        
        Debug.Log($"Making card shiny. Material: {material.name}, Shader: {material.shader.name}");
        
        // URP Lit specific properties for shiny, metallic, plasticky appearance
        
        // Very high smoothness for maximum reflectivity
        if (material.HasProperty("_Smoothness"))
        {
            float smoothness = Random.Range(0.95f, 1.0f); // Increased from 0.85-0.95 to 0.95-1.0
            material.SetFloat("_Smoothness", smoothness);
            Debug.Log($"Set _Smoothness to: {smoothness}");
        }
        else
        {
            Debug.LogWarning("Material does not have _Smoothness property");
        }
        
        // Higher metallic for stronger reflections
        if (material.HasProperty("_Metallic"))
        {
            float metallic = Random.Range(0.0f, 1.0f); // Increased from 0.1-0.3 to 0.4-0.8
            material.SetFloat("_Metallic", metallic);
            Debug.Log($"Set _Metallic to: {metallic}");
        }
        else
        {
            Debug.LogWarning("Material does not have _Metallic property");
        }
        
        // Normal map strength for subtle surface detail
        if (material.HasProperty("_BumpScale"))
        {
            float bumpScale = Random.Range(0.5f, 0.9f); // Reduced for smoother reflections
            material.SetFloat("_BumpScale", bumpScale);
            Debug.Log($"Set _BumpScale to: {bumpScale}");
        }
        else
        {
            Debug.LogWarning("Material does not have _BumpScale property");
        }
        
        // Emission for subtle glow (credit cards often have a slight glow)
        // if (material.HasProperty("_EmissionColor"))
        // {
        //     Color emissionColor = Color.white * Random.Range(0.05f, 0.1f);
        //     material.SetColor("_EmissionColor", emissionColor);
        //     material.EnableKeyword("_EMISSION");
        //     Debug.Log($"Set _EmissionColor to: {emissionColor}");
        // }
        // else
        // {
        //     Debug.LogWarning("Material does not have _EmissionColor property");
        // }
        
        // Ambient occlusion strength for better depth
        // if (material.HasProperty("_OcclusionStrength"))
        // {
        //     float occlusionStrength = Random.Range(0.6f, 0.8f); // Back to previous value
        //     material.SetFloat("_OcclusionStrength", occlusionStrength);
        //     Debug.Log($"Set _OcclusionStrength to: {occlusionStrength}");
        // }
        // else
        // {
        //     Debug.LogWarning("Material does not have _OcclusionStrength property");
        // }
        
        // URP Lit specific - Specular highlights (crucial for reflections)
        if (material.HasProperty("_SpecularHighlights"))
        {
            material.EnableKeyword("_SPECULARHIGHLIGHTS_ON");
            Debug.Log("Enabled _SPECULARHIGHLIGHTS_ON keyword");
        }
        else
        {
            Debug.LogWarning("Material does not have _SpecularHighlights property");
        }
        
        // URP Lit specific - Environment reflections (this is key for skybox reflection)
        if (material.HasProperty("_EnvironmentReflections"))
        {
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_ON");
            Debug.Log("Enabled _ENVIRONMENTREFLECTIONS_ON keyword");
        }
        else
        {
            Debug.LogWarning("Material does not have _EnvironmentReflections property");
        }
        
        // Additional URP reflection properties
        if (material.HasProperty("_ReflectionProbeUsage"))
        {
            material.SetFloat("_ReflectionProbeUsage", 1.0f); // Use reflection probes
            Debug.Log("Set _ReflectionProbeUsage to 1.0");
        }
        
        // Ensure environment map is properly set
        if (material.HasProperty("_EnvironmentMap"))
        {
            // This will be set by the skybox setup
            Debug.Log("Material has _EnvironmentMap property");
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
                    Debug.Log($"Set environment map directly on card material: {skyboxCubemap.name}");
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
                    Debug.Log($"Set unity_SpecCube0 on card material: {skyboxCubemap.name}");
                }
            }
        }
        
        // Set render queue for proper rendering
        material.renderQueue = 2000;
        
        // Ensure proper shader keywords for URP Lit
        material.EnableKeyword("_RECEIVE_SHADOWS_ON");
        material.EnableKeyword("_MAIN_LIGHT_SHADOWS");
        material.EnableKeyword("_MAIN_LIGHT_SHADOWS_CASCADE");
        
        // Additional URP keywords for better reflections
        material.EnableKeyword("_ADDITIONAL_LIGHTS");
        material.EnableKeyword("_ADDITIONAL_LIGHT_SHADOWS");
        
        // Force material to update
        material.SetPass(0);
        
        Debug.Log("Finished making card shiny with enhanced contrast and reflection support");
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
        // Wait for rendering to complete
        yield return new WaitForEndOfFrame();
        
        // Set camera target texture
        mainCamera.targetTexture = renderTexture;
        
        // Render the scene
        mainCamera.Render(); 
        
        // Read pixels from render texture
        RenderTexture.active = renderTexture;
        captureTexture.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
        captureTexture.Apply();
        RenderTexture.active = null;
        
        // Ensure the captured texture is sharp
        captureTexture.filterMode = FilterMode.Bilinear;
        captureTexture.wrapMode = TextureWrapMode.Clamp;
        
        // Save the image with maximum quality
        string filename = $"credit_card_{currentImageCount:D4}.png";
        string filepath = Path.Combine("SyntheticCreditCardData", filename);
        
        byte[] imageData = captureTexture.EncodeToPNG();
        File.WriteAllBytes(filepath, imageData);
        
        currentImageCount++;
        
        // Generate next card
        GenerateNextCard();
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

    Color GenerateRandomTextColor()
    {
        return textColorOptions[Random.Range(0, textColorOptions.Count)];
    }
    
    Color GenerateChromeReflectionColor(Color baseColor)
    {
        if (!useChromeReflection)
        {
            return baseColor;
        }
        
        // Create chrome effect with metallic reflection
        float reflection = Random.Range(0.5f, chromeReflectionIntensity);
        float metallic = Random.Range(0.7f, 1.0f);
        float saturation = Random.Range(1.0f, 1.5f);
        
        // Convert to HSV for better color manipulation
        Color.RGBToHSV(baseColor, out float h, out float s, out float v);
        
        // Enhance saturation for metallic look
        s = Mathf.Clamp01(s * saturation);
        
        // Add metallic brightness and reflection
        v = Mathf.Clamp01(v + reflection * metallic);
        
        // Convert back to RGB
        Color chromeColor = Color.HSVToRGB(h, s, v);
        
        // Add metallic tint based on reflection angle
        float tintStrength = Random.Range(0.2f, 0.4f);
        Color metallicTint = new Color(
            Random.Range(0.9f, 1.1f), // Slight red variation
            Random.Range(0.9f, 1.1f), // Slight green variation  
            Random.Range(1.0f, 1.3f), // Enhanced blue for metallic look
            1f
        );
        
        // Apply metallic tint
        chromeColor = new Color(
            Mathf.Clamp01(chromeColor.r * metallicTint.r * (1f - tintStrength) + metallicTint.r * tintStrength),
            Mathf.Clamp01(chromeColor.g * metallicTint.g * (1f - tintStrength) + metallicTint.g * tintStrength),
            Mathf.Clamp01(chromeColor.b * metallicTint.b * (1f - tintStrength) + metallicTint.b * tintStrength),
            chromeColor.a
        );
        
        // Add final brightness boost for chrome effect
        chromeColor = new Color(
            Mathf.Clamp01(chromeColor.r * 1.2f),
            Mathf.Clamp01(chromeColor.g * 1.2f),
            Mathf.Clamp01(chromeColor.b * 1.3f), // Slightly more blue for metallic look
            chromeColor.a
        );
        
        return chromeColor;
    }
    
    void ApplyTextEffects(TMPro.TextMeshPro textMesh, Color textColor)
    {
        if (textMesh == null) return;
        
        // Apply chrome effect to the text color
        Color chromeColor = GenerateChromeReflectionColor(textColor);
        textMesh.color = chromeColor;
        
        // Apply gradient chrome effect
        ApplyGradientChromeEffect(textMesh, chromeColor);
        
        // Add metallic properties if the material supports them
        if (textMesh.fontSharedMaterial.HasProperty("_Metallic"))
        {
            textMesh.fontSharedMaterial.SetFloat("_Metallic", Random.Range(0.7f, 1.0f));
        }
        
        if (textMesh.fontSharedMaterial.HasProperty("_Smoothness"))
        {
            textMesh.fontSharedMaterial.SetFloat("_Smoothness", Random.Range(0.8f, 1.0f));
        }
        
        // Try to enable glow effect if supported
        if (textMesh.fontSharedMaterial.HasProperty("_GlowPower"))
        {
            textMesh.fontSharedMaterial.SetFloat("_GlowPower", Random.Range(0.1f, 0.3f));
        }
        
        if (textMesh.fontSharedMaterial.HasProperty("_GlowColor"))
        {
            textMesh.fontSharedMaterial.SetColor("_GlowColor", chromeColor * 0.5f);
        }
        
        // Add embossing effects to text
        ApplyEmbossedTextEffect(textMesh, textColor);
        
        // Add environment reflection support to text material
        AddEnvironmentReflectionsToText(textMesh);
    }
    
    void ApplyEmbossedTextEffect(TMPro.TextMeshPro textMesh, Color textColor)
    {
        if (textMesh.fontSharedMaterial == null) return;
        
        Material textMaterial = textMesh.fontSharedMaterial;
        
        // Enable outline for embossed effect
        if (textMaterial.HasProperty("_OutlineColor"))
        {
            // Create embossed outline color (darker version of text color)
            Color embossColor = textColor * 0.3f; // Darker outline
            textMaterial.SetColor("_OutlineColor", embossColor);
            
            // Set outline width for embossed appearance
            if (textMaterial.HasProperty("_OutlineWidth"))
            {
                float outlineWidth = Random.Range(0.02f, 0.05f);
                textMaterial.SetFloat("_OutlineWidth", outlineWidth);
            }
            
            // Enable outline
            textMaterial.EnableKeyword("_OUTLINE_ON");
        }
        
        // Add shadow for depth effect
        if (textMaterial.HasProperty("_ShadowColor"))
        {
            // Create shadow color (very dark version of text color)
            Color shadowColor = textColor * 0.1f;
            shadowColor.a = 0.8f; // Semi-transparent shadow
            textMaterial.SetColor("_ShadowColor", shadowColor);
            
            // Set shadow offset for embossed depth
            if (textMaterial.HasProperty("_ShadowOffset"))
            {
                Vector2 shadowOffset = new Vector2(
                    Random.Range(0.01f, 0.03f),
                    Random.Range(-0.03f, -0.01f) // Negative Y for shadow below
                );
                textMaterial.SetVector("_ShadowOffset", shadowOffset);
            }
            
            // Enable shadow
            textMaterial.EnableKeyword("_SHADOW_ON");
        }
        
        // Add depth effect through face color variation
        if (textMaterial.HasProperty("_FaceColor"))
        {
            // Slightly lighter face color for embossed effect
            Color faceColor = textColor * 1.2f;
            faceColor.a = textColor.a;
            textMaterial.SetColor("_FaceColor", faceColor);
        }
        else if (textMaterial.HasProperty("_BaseColor"))
        {
            // URP TextMeshPro uses _BaseColor
            Color baseColor = textColor * 1.2f;
            baseColor.a = textColor.a;
            textMaterial.SetColor("_BaseColor", baseColor);
        }
        
        // Add subtle glow for embossed highlight
        if (textMaterial.HasProperty("_GlowPower") && !textMaterial.HasProperty("_GlowColor"))
        {
            textMaterial.SetFloat("_GlowPower", Random.Range(0.05f, 0.15f));
        }
        
        if (textMaterial.HasProperty("_GlowColor") && !textMaterial.HasProperty("_GlowPower"))
        {
            // Subtle glow color for embossed highlight
            Color glowColor = textColor * 0.3f;
            textMaterial.SetColor("_GlowColor", glowColor);
        }
        
        Debug.Log($"Applied embossed text effect to: {textMesh.text}");
    }
    
    void AddEnvironmentReflectionsToText(TMPro.TextMeshPro textMesh)
    {
        if (textMesh.fontSharedMaterial == null) return;
        
        Material textMaterial = textMesh.fontSharedMaterial;
        
        // Enable environment reflections for text
        if (textMaterial.HasProperty("_EnvironmentReflections"))
        {
            textMaterial.EnableKeyword("_ENVIRONMENTREFLECTIONS_ON");
            textMaterial.SetFloat("_EnvironmentReflections", 1.0f);
        }
        
        // Enable specular highlights for text
        if (textMaterial.HasProperty("_SpecularHighlights"))
        {
            textMaterial.EnableKeyword("_SPECULARHIGHLIGHTS_ON");
        }
        
        // Set metallic properties for text to enable reflections
        if (textMaterial.HasProperty("_Metallic"))
        {
            // Only set if not already set by chrome effects
            if (!useChromeReflection)
            {
                textMaterial.SetFloat("_Metallic", Random.Range(0.3f, 0.6f));
            }
        }
        
        // Set smoothness for text to enable reflections
        if (textMaterial.HasProperty("_Smoothness"))
        {
            // Only set if not already set by chrome effects
            if (!useChromeReflection)
            {
                textMaterial.SetFloat("_Smoothness", Random.Range(0.6f, 0.9f));
            }
        }
        
        // Try to set environment map directly on text material
        if (textMaterial.HasProperty("_EnvironmentMap"))
        {
            // Get the current skybox cubemap
            if (RenderSettings.skybox != null && RenderSettings.skybox.HasProperty("_Tex"))
            {
                Cubemap skyboxCubemap = RenderSettings.skybox.GetTexture("_Tex") as Cubemap;
                if (skyboxCubemap != null)
                {
                    textMaterial.SetTexture("_EnvironmentMap", skyboxCubemap);
                }
            }
        }
        
        // Alternative environment map property names
        if (textMaterial.HasProperty("unity_SpecCube0"))
        {
            if (RenderSettings.skybox != null && RenderSettings.skybox.HasProperty("_Tex"))
            {
                Cubemap skyboxCubemap = RenderSettings.skybox.GetTexture("_Tex") as Cubemap;
                if (skyboxCubemap != null)
                {
                    textMaterial.SetTexture("unity_SpecCube0", skyboxCubemap);
                }
            }
        }
        
        // Set reflection probe usage
        if (textMaterial.HasProperty("_ReflectionProbeUsage"))
        {
            textMaterial.SetFloat("_ReflectionProbeUsage", 1.0f);
        }
        
        // Enable additional URP keywords for text
        textMaterial.EnableKeyword("_RECEIVE_SHADOWS_ON");
        textMaterial.EnableKeyword("_MAIN_LIGHT_SHADOWS");
        textMaterial.EnableKeyword("_MAIN_LIGHT_SHADOWS_CASCADE");
        
        // Additional URP keywords for better reflections
        textMaterial.EnableKeyword("_ADDITIONAL_LIGHTS");
        textMaterial.EnableKeyword("_ADDITIONAL_LIGHT_SHADOWS");
        
        // Force material to update
        textMaterial.SetPass(0);
        
        Debug.Log($"Added environment reflections to text material: {textMaterial.name}");
    }
    
    void ApplyGradientChromeEffect(TMPro.TextMeshPro textMesh, Color baseColor)
    {
        // Create a subtle gradient effect by varying the color slightly
        // This simulates the way chrome reflects light differently across its surface
        
        // Generate slight color variations for gradient effect
        float variation = Random.Range(0.05f, 0.5f);
        Color gradientColor1 = new Color(
            Mathf.Clamp01(baseColor.r + variation),
            Mathf.Clamp01(baseColor.g + variation),
            Mathf.Clamp01(baseColor.b + variation),
            baseColor.a
        );
        
        Color gradientColor2 = new Color(
            Mathf.Clamp01(baseColor.r - variation),
            Mathf.Clamp01(baseColor.g - variation),
            Mathf.Clamp01(baseColor.b - variation),
            baseColor.a
        );
        
        // Apply gradient effect through material properties - URP compatible
        if (textMesh.fontSharedMaterial.HasProperty("_FaceColor"))
        {
            textMesh.fontSharedMaterial.SetColor("_FaceColor", gradientColor1);
        }
        else if (textMesh.fontSharedMaterial.HasProperty("_BaseColor"))
        {
            // URP TextMeshPro uses _BaseColor instead of _FaceColor
            textMesh.fontSharedMaterial.SetColor("_BaseColor", gradientColor1);
        }
        
        if (textMesh.fontSharedMaterial.HasProperty("_OutlineColor"))
        {
            textMesh.fontSharedMaterial.SetColor("_OutlineColor", gradientColor2);
            textMesh.fontSharedMaterial.SetFloat("_OutlineWidth", Random.Range(0.01f, 0.03f));
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
                        Debug.Log($"Found cubemap texture: {texture.name}, but manual conversion needed");
                    }
                }
            }
        }
        
    }

    
}
