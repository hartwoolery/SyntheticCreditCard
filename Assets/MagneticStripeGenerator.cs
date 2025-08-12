using UnityEngine;
using System.Collections.Generic;

public class MagneticStripeGenerator : MonoBehaviour
{
    [Header("Magnetic Stripe Settings")]
    [Range(0.8f, 1.2f)] public float stripeWidth = 1.0f; // Full width of card
    [Range(0.15f, 0.25f)] public float stripeHeight = 0.177f; // 0.375 inches / 2.125 inches = 0.177
    [Range(0f, 1f)] public float stripeOpacity = 0.8f;
    public Vector2 stripePosition = new Vector2(0f, 0.65f); // 0.223 inches from top = 0.223/2.125 = 0.105 from center, so 0.5 + 0.105 = 0.605, but we need to account for the stripe height, so 0.605 - 0.177/2 = 0.5165, but since we're positioning from center, it's actually 0.5 - 0.105 = 0.395
    
    [Header("Stripe Appearance")]
    [Range(0.1f, 0.5f)] public float noiseIntensity = 0.2f;
    [Range(1f, 10f)] public float noiseScale = 3f;
    [Range(0.1f, 0.5f)] public float scratchIntensity = 0.15f;
    [Range(0.1f, 0.3f)] public float wearIntensity = 0.2f;
    
    [Header("Color Settings")]
    public Color stripeBaseColor = new Color(0.1f, 0.1f, 0.1f);
    public Color stripeHighlightColor = new Color(0.3f, 0.3f, 0.3f);
    public Color stripeShadowColor = new Color(0.05f, 0.05f, 0.05f);
    
    [Header("Track Data")]
    public bool generateTrackData = true;
    [Range(0.1f, 0.5f)] public float trackDataOpacity = 0.3f;
    public Color trackDataColor = new Color(0.2f, 0.2f, 0.2f);
    
    public Texture2D GenerateMagneticStripe(int width, int height)
    {
        Texture2D stripeTexture = new Texture2D(width, height);
        
        // Calculate stripe dimensions
        int stripePixelWidth = (int)(stripeWidth * width);
        int stripePixelHeight = (int)(stripeHeight * height);
        int stripeStartX = (int)((stripePosition.x + 1f) * 0.5f * width - stripePixelWidth * 0.5f);
        int stripeStartY = (int)((stripePosition.y + 1f) * 0.5f * height - stripePixelHeight * 0.5f);
        
        // Ensure stripe is within bounds
        stripeStartX = Mathf.Clamp(stripeStartX, 0, width - stripePixelWidth);
        stripeStartY = Mathf.Clamp(stripeStartY, 0, height - stripePixelHeight);
        
        // Generate base stripe
        GenerateBaseStripe(stripeTexture, stripeStartX, stripeStartY, stripePixelWidth, stripePixelHeight);
        
        // Add noise and wear
        AddNoiseAndWear(stripeTexture, stripeStartX, stripeStartY, stripePixelWidth, stripePixelHeight);
        
        // Add track data if enabled
        if (generateTrackData)
        {
            AddTrackData(stripeTexture, stripeStartX, stripeStartY, stripePixelWidth, stripePixelHeight);
        }
        
        stripeTexture.Apply();
        return stripeTexture;
    }
    
    void GenerateBaseStripe(Texture2D texture, int startX, int startY, int width, int height)
    {
        for (int x = startX; x < startX + width; x++)
        {
            for (int y = startY; y < startY + height; y++)
            {
                // Base stripe color
                Color baseColor = stripeBaseColor;
                
                // Add subtle gradient
                float gradient = (float)(y - startY) / height;
                baseColor = Color.Lerp(stripeShadowColor, stripeHighlightColor, gradient);
                
                // Add horizontal variation
                float horizontalVar = Mathf.Sin((float)(x - startX) / width * Mathf.PI * 2f) * 0.1f;
                baseColor = Color.Lerp(baseColor, stripeHighlightColor, horizontalVar);
                
                texture.SetPixel(x, y, baseColor);
            }
        }
    }
    
    void AddNoiseAndWear(Texture2D texture, int startX, int startY, int width, int height)
    {
        for (int x = startX; x < startX + width; x++)
        {
            for (int y = startY; y < startY + height; y++)
            {
                Color currentColor = texture.GetPixel(x, y);
                
                // Add noise
                float noise = Mathf.PerlinNoise(
                    (float)x / width * noiseScale,
                    (float)y / height * noiseScale
                );
                currentColor = Color.Lerp(currentColor, stripeShadowColor, noise * noiseIntensity);
                
                // Add horizontal scratches
                if (Random.Range(0f, 1f) < scratchIntensity * 0.01f)
                {
                    // Create horizontal scratch across full width
                    int scratchY = y;
                    int scratchHeight = Random.Range(1, 4); // 1-3 pixels tall
                    Color scratchColor = stripeShadowColor;
                    
                    // Draw horizontal line across the full stripe width
                    for (int sx = startX; sx < startX + width; sx++)
                    {
                        for (int sy = Mathf.Max(startY, scratchY - scratchHeight/2); 
                             sy < Mathf.Min(startY + height, scratchY + scratchHeight/2 + 1); sy++)
                        {
                            if (sy >= startY && sy < startY + height)
                            {
                                texture.SetPixel(sx, sy, scratchColor);
                            }
                        }
                    }
                }
                
                // Add wear
                float wear = Mathf.PerlinNoise(
                    (float)x / width * 5f,
                    (float)y / height * 5f
                );
                currentColor = Color.Lerp(currentColor, stripeShadowColor, wear * wearIntensity);
                
                texture.SetPixel(x, y, currentColor);
            }
        }
    }
    
    void AddTrackData(Texture2D texture, int startX, int startY, int width, int height)
    {
        // Generate random track data
        string trackData = GenerateTrackData();
        
        // Create track data texture
        Texture2D trackTexture = CreateTrackDataTexture(trackData, width, height);
        
        // Blend track data onto stripe
        for (int x = startX; x < startX + width; x++)
        {
            for (int y = startY; y < startY + height; y++)
            {
                Color stripeColor = texture.GetPixel(x, y);
                Color trackColor = trackTexture.GetPixel(x - startX, y - startY);
                
                // Blend track data with stripe
                Color blendedColor = Color.Lerp(stripeColor, trackColor, trackDataOpacity * trackColor.a);
                texture.SetPixel(x, y, blendedColor);
            }
        }
    }
    
    string GenerateTrackData()
    {
        // Generate realistic track data
        string track1 = GenerateTrack1();
        string track2 = GenerateTrack2();
        string track3 = GenerateTrack3();
        
        return track1 + "\n" + track2 + "\n" + track3;
    }
    
    string GenerateTrack1()
    {
        // Track 1 format: %B123456789012345^DOE/JOHN^123456789012345678901234567890?
        string cardNumber = GenerateCardNumber();
        string cardholderName = GenerateCardholderName();
        string expiryDate = GenerateExpiryDate();
        string serviceCode = Random.Range(100, 999).ToString();
        
        return "%B" + cardNumber + "^" + cardholderName + "^" + expiryDate + serviceCode + "?";
    }
    
    string GenerateTrack2()
    {
        // Track 2 format: 1234567890123456=123456789012345678901234567890?
        string cardNumber = GenerateCardNumber();
        string expiryDate = GenerateExpiryDate();
        string serviceCode = Random.Range(100, 999).ToString();
        
        return cardNumber + "=" + expiryDate + serviceCode + "?";
    }
    
    string GenerateTrack3()
    {
        // Track 3 format: 1234567890123456=123456789012345678901234567890?
        string cardNumber = GenerateCardNumber();
        string countryCode = Random.Range(100, 999).ToString();
        string currencyCode = Random.Range(100, 999).ToString();
        string amount = Random.Range(10000, 99999).ToString();
        
        return cardNumber + "=" + countryCode + currencyCode + amount + "?";
    }
    
    string GenerateCardNumber()
    {
        string[] prefixes = { "4", "5", "3" };
        string prefix = prefixes[Random.Range(0, prefixes.Length)];
        
        string cardNumber = prefix;
        for (int i = 1; i < 16; i++)
        {
            cardNumber += Random.Range(0, 10).ToString();
        }
        
        return cardNumber;
    }
    
    string GenerateCardholderName()
    {
        string[] names = { "DOE/JOHN", "SMITH/JANE", "JOHNSON/MICHAEL", "WILLIAMS/SARAH" };
        return names[Random.Range(0, names.Length)];
    }
    
    string GenerateExpiryDate()
    {
        int month = Random.Range(1, 13);
        int year = Random.Range(24, 30);
        return month.ToString("D2") + year.ToString("D2");
    }
    
    Texture2D CreateTrackDataTexture(string trackData, int width, int height)
    {
        Texture2D texture = new Texture2D(width, height);
        
        // Create a simple pattern representing magnetic track data
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Create magnetic stripe pattern
                float pattern = Mathf.Sin((float)x / width * Mathf.PI * 20f) * 
                              Mathf.Cos((float)y / height * Mathf.PI * 5f);
                
                Color trackColor = Color.Lerp(
                    Color.clear, 
                    trackDataColor, 
                    Mathf.Abs(pattern) * 0.5f
                );
                
                texture.SetPixel(x, y, trackColor);
            }
        }
        
        texture.Apply();
        return texture;
    }
} 