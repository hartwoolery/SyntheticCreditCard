using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class CreditCardTextGenerator : MonoBehaviour
{
    [Header("Text Elements")]
    public GameObject cardNumberPrefab;
    public GameObject cardholderNamePrefab;
    public GameObject expiryDatePrefab;
    public GameObject cvvPrefab;
    public GameObject cardBrandPrefab;
    
    [Header("Name Generator")]
    public NameGenerator nameGenerator;
    
    [Header("Field Placement Probabilities")]
    [Range(0f, 1f)] public float cardNumberProbability = 0.9f;
    [Range(0f, 1f)] public float cardholderNameProbability = 0.8f;
    [Range(0f, 1f)] public float expiryDateProbability = 0.9f;
    [Range(0f, 1f)] public float cvvProbability = 0.8f;
    [Range(0f, 1f)] public float cardBrandProbability = 0.7f;
    
    [Header("Side Placement")]
    [Range(0f, 1f)] public float frontSideProbability = 0.7f;
    
    [Header("Text Settings")]
    public string[] cardBrands = { "VISA", "MASTERCARD", "AMEX", "DISCOVER" };
    
    private List<GameObject> textElements = new List<GameObject>();
    
    void Start()
    {
        // Find or create name generator
        if (nameGenerator == null)
        {
            nameGenerator = GetComponent<NameGenerator>();
            if (nameGenerator == null)
            {
                nameGenerator = gameObject.AddComponent<NameGenerator>();
            }
        }
    }
    
    public void GenerateCardText(GameObject cardModel, bool forceTextOnFront = false)
    {
        if (cardModel == null) return;
        
        // Clear any existing text
        ClearExistingText();
        
        // Determine which side to place text on
        bool textOnFront = forceTextOnFront || Random.Range(0f, 1f) < frontSideProbability;
        string side = textOnFront ? "Front" : "Back";
        
        // Randomly decide which fields to include
        bool includeCardNumber = Random.Range(0f, 1f) < cardNumberProbability;
        bool includeCardholderName = Random.Range(0f, 1f) < cardholderNameProbability;
        bool includeExpiryDate = Random.Range(0f, 1f) < expiryDateProbability;
        bool includeCVV = Random.Range(0f, 1f) < cvvProbability;
        bool includeCardBrand = Random.Range(0f, 1f) < cardBrandProbability;
        
        int elementsCreated = 0;
        
        // Create text elements based on probabilities
        if (includeCardNumber)
        {
            string cardNumber = GenerateCardNumber();
            if (CreateTextElement(cardNumberPrefab, cardNumber, cardModel, side))
                elementsCreated++;
        }
        
        if (includeCardholderName)
        {
            string cardholderName = GenerateCardholderName();
            if (CreateTextElement(cardholderNamePrefab, cardholderName, cardModel, side))
                elementsCreated++;
        }
        
        if (includeExpiryDate)
        {
            string expiryDate = GenerateExpiryDate();
            if (CreateTextElement(expiryDatePrefab, expiryDate, cardModel, side))
                elementsCreated++;
        }
        
        if (includeCVV)
        {
            string cvv = GenerateCVV();
            if (CreateTextElement(cvvPrefab, cvv, cardModel, side))
                elementsCreated++;
        }
        
        if (includeCardBrand)
        {
            string cardBrand = Random.Range(0f, 1f) < 0.5f ? "VISA" : "MASTERCARD";
            if (CreateTextElement(cardBrandPrefab, cardBrand, cardModel, side))
                elementsCreated++;
        }
        
        // Verify text elements were created properly
        VerifyTextElements();
    }
    
    string GenerateCardholderName()
    {
        if (nameGenerator != null)
        {
            return nameGenerator.GenerateName();
        }
        else
        {
            // Fallback to hardcoded names
            string[] fallbackNames = {
                "JOHN DOE", "JANE SMITH", "MICHAEL JOHNSON", "SARAH WILLIAMS", "DAVID BROWN",
                "LISA DAVIS", "ROBERT MILLER", "JENNIFER WILSON", "WILLIAM MOORE", "AMANDA TAYLOR"
            };
            return fallbackNames[Random.Range(0, fallbackNames.Length)];
        }
    }
    
    bool CreateTextElement(GameObject prefab, string text, GameObject cardModel, string side)
    {
        if (cardModel == null) return false;
        
        // Find or create a parent for text elements
        Transform textParent = FindOrCreateTextParent(cardModel.transform.parent.gameObject, side);
        if (textParent == null)
        {
            return false;
        }
        
        GameObject textElement = null;
        
        // Use prefab if available, otherwise create dynamic text element
        if (prefab != null)
        {
            textElement = Instantiate(prefab, textParent);
        }
        else
        {
            textElement = CreateDynamicTextElement(text, textParent);
        }
        
        if (textElement == null)
        {
            return false;
        }
        
        // Set the text content
        TextMeshPro tmpText = textElement.GetComponent<TextMeshPro>();
        if (tmpText != null)
        {
            tmpText.text = text;
            SetAppropriateFontSize(tmpText, text);
            RandomizeTextProperties(tmpText);
        }
        else
        {
            TextMesh textMesh = textElement.GetComponent<TextMesh>();
            if (textMesh != null)
            {
                textMesh.text = text;
                SetAppropriateFontSize(textMesh, text);
                RandomizeTextProperties(textMesh);
            }
        }
        
        // Position randomly on the card
        RandomizeTextPosition(textElement, textParent);
        
        return true;
    }
    
    Transform FindOrCreateTextParent(GameObject cardModel, string side)
    {
        // Look for existing text parent
        string parentName = $"TextParent_{side}";
        Transform existingParent = cardModel.transform.Find(parentName);
        if (existingParent != null)
        {
            return existingParent;
        }
        
        // Create new text parent
        GameObject textParent = new GameObject(parentName);
        textParent.transform.SetParent(cardModel.transform);
        
        // Position the text parent at the card's position
        textParent.transform.localPosition = Vector3.zero;
        textParent.transform.localRotation = Quaternion.identity;
        textParent.transform.localScale = Vector3.one; // Keep scale at 1 to avoid scaling issues
        
        return textParent.transform;
    }
    
    void SetAppropriateFontSize(TextMeshPro textMesh, string text)
    {
        // Set font size based on text content - appropriate sizes for credit cards
        if (text.Contains("VISA") || text.Contains("MASTERCARD") || text.Contains("AMEX") || text.Contains("DISCOVER"))
        {
            textMesh.fontSize = 0.8f; // Brand logos are smaller
        }
        else if (text.Length > 10) // Card numbers
        {
            textMesh.fontSize = 1.0f; // Card numbers are medium
        }
        else if (text.Length > 5) // Names
        {
            textMesh.fontSize = 1.2f; // Names are slightly larger
        }
        else // CVV, expiry dates
        {
            textMesh.fontSize = 1.0f; // Short text is medium
        }

        textMesh.fontSize *= 0.2f;
    }
    
    void SetAppropriateFontSize(TextMesh textMesh, string text)
    {
        // Set font size based on text content - using appropriate sizes for TextMesh
        if (text.Contains("VISA") || text.Contains("MASTERCARD") || text.Contains("AMEX") || text.Contains("DISCOVER"))
        {
            textMesh.fontSize = 1; // Brand logos are smaller
        }
        else if (text.Length > 10) // Card numbers
        {
            textMesh.fontSize = 1; // Card numbers are medium
        }
        else if (text.Length > 5) // Names
        {
            textMesh.fontSize = 2; // Names are slightly larger
        }
        else // CVV, expiry dates
        {
            textMesh.fontSize = 1; // Short text is medium
        }

    }
    
    GameObject CreateDynamicTextElement(string text, Transform parent)
    {
        // Create a new GameObject for the text
        GameObject textObject = new GameObject($"Text_{text.Replace(" ", "_")}");
        textObject.transform.SetParent(parent);
        
        // Try to add TextMeshPro component first
        TextMeshPro textMesh = textObject.AddComponent<TextMeshPro>();
        if (textMesh == null)
        {
            // Fallback to regular TextMesh
            TextMesh regularTextMesh = textObject.AddComponent<TextMesh>();
            if (regularTextMesh != null)
            {
                regularTextMesh.text = text;
                regularTextMesh.fontSize = 1;
                regularTextMesh.color = new Color(0.2f, 0.2f, 0.2f);
                regularTextMesh.alignment = TextAlignment.Center;
                
                // Ensure the TextMesh has a renderer
                MeshRenderer regularRenderer = textObject.GetComponent<MeshRenderer>();
                if (regularRenderer != null)
                {
                    // Create material that ensures text is always visible
                    Material textMaterial = new Material(Shader.Find("Standard"));
                    if (textMaterial != null)
                    {
                        // textMaterial.SetFloat("_Mode", 3); // Transparent mode
                        // textMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        // textMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        // textMaterial.SetInt("_ZWrite", 1000); // Don't write to depth buffer
                        // textMaterial.DisableKeyword("_ALPHATEST_ON");
                        // textMaterial.EnableKeyword("_ALPHABLEND_ON");
                        // textMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        // textMaterial.renderQueue = 3000; // Render after opaque objects
                        
                        regularRenderer.material = textMaterial;
                    }
                }
            }
        }
        else
        {
            // Configure TextMeshPro
            textMesh.text = text;
            textMesh.fontSize = 1.0f;
            textMesh.color = new Color(0.2f, 0.2f, 0.2f);
            textMesh.alignment = TextAlignmentOptions.Center;
            //textMesh.gameObject.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
            
            // Ensure the TextMeshPro has a renderer
            MeshRenderer tmpRenderer = textObject.GetComponent<MeshRenderer>();
            if (tmpRenderer != null)
            {
                // Create material that ensures text is always visible
                Material textMaterial = new Material(Shader.Find("TextMeshPro/Mobile/Distance Field"));
                if (textMaterial != null)
                {
                    // textMaterial.SetFloat("_Mode", 3); // Transparent mode
                    // textMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    // textMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    // textMaterial.SetInt("_ZWrite", 1000); // Don't write to depth buffer
                    // textMaterial.DisableKeyword("_ALPHATEST_ON");
                    // textMaterial.EnableKeyword("_ALPHABLEND_ON");
                    // textMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    // textMaterial.renderQueue = 3000; // Render after opaque objects
                    
                    tmpRenderer.material = textMaterial;
                }
                else
                {
                    // Fallback to standard shader
                    textMaterial = new Material(Shader.Find("Standard"));
                    if (textMaterial != null)
                    {
                        // textMaterial.SetFloat("_Mode", 3); // Transparent mode
                        // textMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        // textMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        // textMaterial.SetInt("_ZWrite", 1000); // Don't write to depth buffer
                        // textMaterial.DisableKeyword("_ALPHATEST_ON");
                        // textMaterial.EnableKeyword("_ALPHABLEND_ON");
                        // textMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        // textMaterial.renderQueue = 3000; // Render after opaque objects
                        
                        tmpRenderer.material = textMaterial;
                    }
                }
            }
            
            // Force mesh update
            textMesh.ForceMeshUpdate();
        }
        
        return textObject;
    }
    
    // Method to verify text elements are visible
    public void VerifyTextElements()
    {
        // This method can be used for debugging if needed, but currently empty
    }
    
    Transform FindCardSide(GameObject cardModel, string side)
    {
        // Look for child objects that might represent the front or back
        foreach (Transform child in cardModel.transform)
        {
            if (child.name.ToLower().Contains(side.ToLower()))
            {
                return child;
            }
        }
        
        // If no specific side found, return the card model itself
        return cardModel.transform;
    }
    
    void RandomizeTextProperties(TextMeshPro textMesh)
    {
        // Random font size - appropriate range for credit cards
        textMesh.fontSize = Random.Range(0.8f, 1.5f);
        
        // Random color
        Color[] textColors = {
            Color.white, Color.black, Color.gray, new Color(0.2f, 0.2f, 0.2f) // Dark gray
        };
        textMesh.color = textColors[Random.Range(0, textColors.Length)];
        
        // Random alignment
        TextAlignmentOptions[] alignments = {
            TextAlignmentOptions.Left, TextAlignmentOptions.Center, TextAlignmentOptions.Right
        };
        textMesh.alignment = alignments[Random.Range(0, alignments.Length)];
    }
    
    void RandomizeTextProperties(TextMesh textMesh)
    {
        // Random font size - appropriate range for credit cards
        textMesh.fontSize = Random.Range(1, 3);
        
        // Random color
        Color[] textColors = {
            Color.white, Color.black, Color.gray, new Color(0.2f, 0.2f, 0.2f) // Dark gray
        };
        textMesh.color = textColors[Random.Range(0, textColors.Length)];
        
        // Random alignment
        TextAlignment[] alignments = {
            TextAlignment.Left, TextAlignment.Center, TextAlignment.Right
        };
        textMesh.alignment = alignments[Random.Range(0, alignments.Length)];
    }
    
    void RandomizeTextPosition(GameObject textElement, Transform textParent)
    {
        // Get the card's bounds to position text properly
        Renderer cardRenderer = textParent.parent.GetComponent<Renderer>();
        Vector3 position = Vector3.zero;
        
        if (cardRenderer != null)
        {
            Bounds cardBounds = cardRenderer.bounds;
            // Convert world bounds to local bounds relative to text parent
            Vector3 localSize = textParent.InverseTransformVector(cardBounds.size);
            
            // Position text within the card bounds, slightly above the surface
            position = new Vector3(
                Random.Range(-localSize.x * 0.4f, localSize.x * 0.4f),
                Random.Range(-localSize.y * 0.3f, localSize.y * 0.3f),
                localSize.z * 0.5f // Slight offset from surface
            );
        }
        else
        {
            // Fallback positioning if no renderer found - use card-appropriate positioning
            position = new Vector3(
                Random.Range(-0.3f, 0.3f),  // Reduced range for better visibility
                Random.Range(-0.2f, 0.2f),  // Reduced range for better visibility
                0.5f // Slight offset from surface
            );
        }
        
        textElement.transform.localPosition = position;
        
        // Set text to follow the card's surface orientation (no random rotation)
        // This makes the text rotate with the card instead of always facing the camera
        textElement.transform.localRotation = Quaternion.identity;
        
        // Optionally add very slight random rotation for realism (but keep it minimal)
        Vector3 slightRotation = new Vector3(
            Random.Range(-1f, 1f),  // Very slight X rotation
            Random.Range(-1f, 1f),  // Very slight Y rotation
            Random.Range(-2f, 2f)   // Slight Z rotation (text tilt)
        );
        
        //textElement.transform.localRotation = Quaternion.Euler(slightRotation);
        
        // Ensure the text element is active and visible
        textElement.SetActive(true);
        
        // Check if the text element has a renderer and make sure it's visible
        MeshRenderer textRenderer = textElement.GetComponent<MeshRenderer>();
        if (textRenderer != null)
        {
            textRenderer.enabled = true;
        }
    }
    
    string GenerateCardNumber()
    {
        string[] prefixes = { "4", "5", "3" }; // Visa, Mastercard, Amex
        string prefix = prefixes[Random.Range(0, prefixes.Length)];
        
        string cardNumber = prefix;
        
        // Generate remaining digits
        for (int i = 1; i < 16; i++)
        {
            cardNumber += Random.Range(0, 10).ToString();
        }
        
        // Format with spaces
        return FormatCardNumber(cardNumber);
    }
    
    string FormatCardNumber(string cardNumber)
    {
        string formatted = "";
        for (int i = 0; i < cardNumber.Length; i++)
        {
            if (i > 0 && i % 4 == 0)
            {
                formatted += " ";
            }
            formatted += cardNumber[i];
        }
        return formatted;
    }
    
    string GenerateExpiryDate()
    {
        int month = Random.Range(1, 13);
        int year = Random.Range(24, 30); // 2024-2029
        
        return $"{month:D2}/{year:D2}";
    }
    
    string GenerateCVV()
    {
        return Random.Range(100, 1000).ToString();
    }
    
    void ClearExistingText()
    {
        // Clear any existing text elements
        foreach (GameObject element in textElements)
        {
            if (element != null)
            {
                DestroyImmediate(element);
            }
        }
        textElements.Clear();
    }
    
    void OnDestroy()
    {
        // Clean up any remaining text elements
        ClearExistingText();
    }
    
    void EnsureTextVisibility()
    {
        // Ensure all text elements are visible
        foreach (GameObject element in textElements)
        {
            if (element != null)
            {
                element.SetActive(true);
                MeshRenderer renderer = element.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }
        }
    }
} 