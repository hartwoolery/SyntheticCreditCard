# Synthetic Credit Card Image Generator

A Unity-based system for generating synthetic credit card images with randomized elements including textures, text, chips, logos, skybox backgrounds, and procedural magnetic stripes.

## Features

- **Randomized Card Generation**: Creates credit cards with varying textures, colors, and layouts
- **First-Person Camera View**: Simulates holding a card in front of the camera
- **Text Generation**: Adds realistic card numbers, names, expiry dates, and CVV codes
- **Texture-Based Logo Placement**: Superimposes chips, brand logos, and bank logos directly onto card textures
- **Procedural Magnetic Stripes**: Generates realistic magnetic stripes on the back of cards
- **Random Skybox Backgrounds**: Uses EXR files to create varied environmental backgrounds
- **Material Blending**: Mixes solid colors with background textures at varying levels
- **Batch Processing**: Generates multiple images in sequence
- **Customizable Parameters**: Adjust image count, resolution, rotation, distance, and more

## Setup Instructions

### 1. Project Structure

Create the following folder structure in your Unity project:

```
Assets/
├── FrontImages/          # Random card front textures
├── ChipImages/           # EMV chip PNG images
├── LogoImages/           # Card brand logos (Visa, Mastercard, etc.)
├── BankLogoImages/       # Bank logos (Chase, Bank of America, etc.)
├── SkyboxImages/         # EXR skybox background files
├── Fonts/                # TTF/OTF font files for text rendering
└── Prefabs/             # Text element prefabs (optional)
```

### 2. Required Assets

#### Card Model
- Import your 3D credit card model (FBX format)
- Ensure it has separate materials for front and back
- The model should be named "card" or assigned in the inspector

#### Textures and Images
- **Front Images**: Place random card background images in `Assets/FrontImages/`
- **Chip Images**: Place EMV chip PNG images in `Assets/ChipImages/`
- **Brand Logo Images**: Place card brand logos in `Assets/LogoImages/`
- **Bank Logo Images**: Place bank logos in `Assets/BankLogoImages/`
- **Skybox Images**: Place EXR files in `Assets/SkyboxImages/`
- **Fonts**: Place TTF/OTF font files in `Assets/Fonts/`

### 3. Scene Setup

1. **Add the Synthesize Component**:
   - Create an empty GameObject in your scene
   - Add the `Synthesize.cs` script to it
   - Configure the inspector settings (see Configuration section)

2. **Camera Setup**:
   - Ensure you have a Main Camera in the scene
   - The camera will be automatically configured for first-person view with skybox

3. **Card Model Assignment**:
   - Drag your card model to the `Card Model` field in the Synthesize component
   - Assign the front and back materials

4. **Skybox Setup**:
   - Add the `SkyboxMaterialSetup` component to create a skybox material
   - Or manually create a skybox material using the "Skybox/6 Sided" shader
   - Assign the skybox material to the Synthesize component

### 4. Text Generation Setup (Optional)

For text generation, you'll need to create prefabs with TextMeshPro components:

1. **Create Text Prefabs**:
   - Create empty GameObjects for each text element
   - Add TextMeshPro components
   - Configure default text properties
   - Save as prefabs

2. **Assign Prefabs**:
   - Drag the text prefabs to the `CreditCardTextGenerator` component
   - Available prefab fields:
     - `Card Number Prefab`
     - `Cardholder Name Prefab`
     - `Expiry Date Prefab`
     - `CVV Prefab`
     - `Card Brand Prefab`

## Configuration

### Synthesize Component Settings

#### Card Model
- **Card Model**: Reference to your 3D card model
- **Front Material**: Material for the card front
- **Back Material**: Material for the card back

#### Card Scaling
- **Use Standard Card Dimensions**: Enable/disable automatic card scaling to standard dimensions
- **Card Width Inches**: Width of the card in inches (default: 3.375")
- **Card Height Inches**: Height of the card in inches (default: 2.125")
- **Card Thickness Inches**: Thickness of the card in inches (default: 0.03")

The system automatically scales the card model to standard credit card dimensions (3.375" × 2.125" × 0.03") when enabled. This ensures realistic proportions for all generated images.

#### Camera Setup
- **Render Camera**: Camera used for rendering (auto-assigned to Main Camera)
- **Camera Transform**: Camera transform (auto-assigned)

#### Asset Folders
- **Front Images Folder**: Path to front texture images
- **Chip Images Folder**: Path to chip texture images
- **Logo Images Folder**: Path to brand logo images
- **Bank Logo Images Folder**: Path to bank logo images
- **Skybox Images Folder**: Path to EXR skybox files
- **Fonts Folder**: Path to font files

#### Generation Settings
- **Images To Generate**: Number of images to create (1-1000)
- **Image Width**: Output image width in pixels (512-2048)
- **Image Height**: Output image height in pixels (384-1536)
- **Generate Front And Back**: Whether to generate both sides

#### Randomization
- **Min Color Blend**: Minimum color blending amount (0-1)
- **Max Color Blend**: Maximum color blending amount (0-1)
- **Max Rotation Angle**: Maximum rotation in degrees (0-30)
- **Min Card Distance**: Minimum distance from camera (0.5-2)
- **Max Card Distance**: Maximum distance from camera (0.5-3)

#### Text Generation
- **Text Generator**: Reference to CreditCardTextGenerator component
- **Text On Front**: Whether text appears on front by default
- **Text On Front Probability**: Probability of text appearing on front (0-1)

#### Logo Placement
- **Chip Opacity**: Opacity of chip overlay (0-1)
- **Brand Logo Opacity**: Opacity of brand logo overlay (0-1)
- **Bank Logo Opacity**: Opacity of bank logo overlay (0-1)
- **Chip Position**: Position of chip on card (-1 to 1)
- **Brand Logo Position**: Position of brand logo on card (-1 to 1)
- **Bank Logo Position**: Position of bank logo on card (-1 to 1)
- **Chip Size**: Size of chip overlay (0-1)
- **Brand Logo Size**: Size of brand logo overlay (0-1)
- **Bank Logo Size**: Size of bank logo overlay (0-1)

#### Magnetic Stripe
- **Generate Magnetic Stripe**: Enable/disable magnetic stripe generation
- **Stripe Opacity**: Opacity of magnetic stripe overlay (0-1)
- **Stripe Width**: Width of magnetic stripe (0.1-0.3)
- **Stripe Height**: Height of magnetic stripe (0.8-0.95)
- **Stripe Position**: Position of magnetic stripe on card (-1 to 1)
- **Noise Intensity**: Amount of noise in stripe texture (0.1-0.5)
- **Scratch Intensity**: Amount of scratches on stripe (0.1-0.5)
- **Wear Intensity**: Amount of wear on stripe (0.1-0.3)
- **Generate Track Data**: Enable/disable magnetic track data generation
- **Track Data Opacity**: Opacity of track data overlay (0.1-0.5)

#### Skybox Settings
- **Use Random Skybox**: Enable/disable random skybox selection
- **Max Skybox Rotation**: Maximum rotation angle for skybox (0-360°)
- **Skybox Material**: Reference to the skybox material

## Usage

### Automatic Generation
The system will automatically start generating images when the scene starts. Images are saved to the `SyntheticCreditCardData/` folder.

### Manual Generation
1. Add the `CreditCardGeneratorUI` component to a UI GameObject
2. Connect UI elements (buttons, sliders, etc.) to the component
3. Use the UI to control generation parameters
4. Click "Generate" to start manual generation

### Output
Generated images are saved as PNG files in the `SyntheticCreditCardData/` folder with the naming convention:
- `credit_card_0000.png`
- `credit_card_0001.png`
- etc.

## Scripts Overview

### Synthesize.cs
Main controller script that handles:
- Asset loading and management
- Card generation and randomization
- Image capture and saving
- Material blending and texturing
- Logo superimposition on textures
- Random skybox selection and rotation
- Procedural magnetic stripe generation
- Automatic card scaling to standard dimensions

**Public Methods:**
- `StartGeneration()`: Triggers the generation process
- `SetImageCount(int count)`: Sets the number of images to generate
- `ScaleCardToStandardDimensions()`: Manually scales the card to standard credit card dimensions

### CreditCardTextGenerator.cs
Handles text generation for credit cards:
- Card number generation
- Cardholder name selection
- Expiry date generation
- CVV generation
- Text positioning and styling

### MagneticStripeGenerator.cs
Generates realistic magnetic stripes:
- Procedural stripe texture generation
- Noise and wear effects
- Magnetic track data generation
- Realistic stripe positioning and sizing

### CreditCardGeneratorUI.cs
Provides UI controls for:
- Generation parameters
- Real-time parameter adjustment
- Progress monitoring
- Manual generation triggering

### SkyboxMaterialSetup.cs
Helps set up skybox materials:
- Creates skybox materials with proper shaders
- Configures default skybox properties
- Assigns skybox to render settings

## Customization

### Adding New Card Colors
Edit the `cardColors` list in `InitializeSystem()` method of `Synthesize.cs`:

```csharp
cardColors.AddRange(new Color[] {
    Color.white,
    Color.black,
    new Color(0.1f, 0.1f, 0.3f), // Dark blue
    // Add your custom colors here
});
```

### Adding New Cardholder Names
Edit the `cardholderNames` array in `CreditCardTextGenerator.cs`:

```csharp
public string[] cardholderNames = {
    "JOHN DOE", "JANE SMITH",
    // Add your custom names here
};
```

### Customizing Logo Placement
Modify the logo position and size parameters in the Synthesize component inspector:
- **Chip Position**: Controls where the EMV chip appears
- **Brand Logo Position**: Controls where the card brand logo appears
- **Bank Logo Position**: Controls where the bank logo appears (upper right by default)

### Adding New Card Brands
Edit the `cardBrands` arrays in both `Synthesize.cs` and `CreditCardTextGenerator.cs`.

## Logo Placement System

The system now uses texture-based logo placement instead of 3D objects:

### Logo Types
1. **Chip**: EMV chip overlay (typically bottom left)
2. **Brand Logo**: Card brand logo (Visa, Mastercard, etc.)
3. **Bank Logo**: Bank logo (typically upper right)

### Logo Properties
- **Opacity**: Controls how transparent the logo appears
- **Position**: 2D coordinates on the card (-1 to 1)
- **Size**: Relative size of the logo (0 to 1)

### Logo Blending
Logos are blended onto the card texture using alpha blending, ensuring realistic appearance while maintaining the underlying card texture.

## Magnetic Stripe System

The system generates realistic magnetic stripes on the back of credit cards:

### Stripe Features
- **Procedural Generation**: Creates realistic stripe textures programmatically
- **Noise and Wear**: Adds realistic wear patterns and scratches
- **Track Data**: Generates realistic magnetic track data patterns
- **Customizable Appearance**: Adjust width, height, position, and opacity

### Stripe Properties
- **Width**: Controls the width of the magnetic stripe (0.8-1.2, default: 1.0 for full card width)
- **Height**: Controls the height of the stripe (0.15-0.25, default: 0.177 for 0.375 inches)
- **Position**: 2D coordinates for stripe placement (-1 to 1, default: centered horizontally, 0.223 inches from top)
- **Opacity**: Controls stripe transparency (0-1)

### Real-World Dimensions
The magnetic stripe is positioned according to standard credit card specifications:
- **Card Size**: 3.375" × 2.125" (85.6mm × 54mm)
- **Stripe Width**: Full card width (3.375")
- **Stripe Height**: 0.375" (9.5mm)
- **Stripe Position**: 0.223" from top edge (5.7mm from top)

### Track Data Generation
- **Track 1**: Format: %B123456789012345^DOE/JOHN^123456789012345678901234567890?
- **Track 2**: Format: 1234567890123456=123456789012345678901234567890?
- **Track 3**: Format: 1234567890123456=123456789012345678901234567890?

### Stripe Effects
- **Noise**: Perlin noise for realistic texture variation
- **Scratches**: Random scratch patterns for wear simulation
- **Wear**: Gradual wear patterns using noise
- **Gradients**: Subtle color gradients for depth

## Skybox System

The system supports random skybox backgrounds for more realistic card images:

### Skybox Features
- **EXR File Support**: Loads high-quality EXR skybox files
- **Random Selection**: Chooses random skybox for each image
- **Random Rotation**: Rotates skybox randomly (0-360°)
- **Automatic Setup**: Creates skybox materials automatically

### Skybox Setup
1. **Place EXR files** in `Assets/SkyboxImages/`
2. **Create skybox material** using `SkyboxMaterialSetup` component
3. **Assign material** to the Synthesize component
4. **Enable random skybox** in the inspector

#### HDRP Skybox Setup
For HDRP projects, additional setup is required:

1. **Use HDRP Skybox Helper**:
   - Add the `HDRPSkyboxHelper` component to a GameObject
   - Configure HDRI or gradient settings
   - Click "Create HDRP Skybox Material" in the inspector
   - Assign the created material to the Synthesize component

2. **HDRP Shader Compatibility**:
   - The system automatically detects HDRP and uses appropriate shaders
   - HDRI skyboxes use `Hidden/HDRP/Skybox/Cubemap` shader
   - Gradient skyboxes use `Hidden/HDRP/Skybox/Gradient` shader
   - Falls back to built-in shaders if HDRP shaders are not available

3. **HDRP Properties**:
   - `_Exposure`: Controls skybox brightness
   - `_Multiplier`: Controls overall intensity
   - `_Rotation`: Controls skybox rotation
   - `_Tex`: HDRI texture (for cubemap skyboxes)
   - `_Top`, `_Middle`, `_Bottom`: Gradient colors (for gradient skyboxes)

### Skybox Properties
- **Use Random Skybox**: Enable/disable skybox randomization
- **Max Skybox Rotation**: Maximum rotation angle
- **Skybox Material**: Reference to the skybox material

## Troubleshooting

### Common Issues

1. **No images generated**: Check that the card model is assigned and visible to the camera
2. **Missing textures**: Ensure image folders exist and contain valid image files
3. **Text not appearing**: Verify TextMeshPro prefabs are assigned and properly configured
4. **Poor image quality**: Adjust camera settings and render texture resolution
5. **Performance issues**: Reduce image count or resolution for faster generation
6. **Logos not appearing**: Check that logo images have proper alpha channels and are in the correct folders
7. **Skybox not working**: Ensure EXR files are in the SkyboxImages folder and skybox material is assigned

### Debug Information
The system outputs debug logs showing:
- Number of loaded assets
- Generation progress
- File save locations
- Error messages for missing assets
- Skybox rotation information

## Requirements

- Unity 2021.3 or later
- TextMeshPro package (for text generation)
- 3D credit card model with materials
- Image assets for textures, chips, and logos
- EXR files for skybox backgrounds
- Font files for text rendering (optional)

## License

This project is provided as-is for educational and development purposes. 