using UnityEngine;

/// <summary>One card in the level-select carousel.</summary>
public class LevelInfo
{
    public int number;
    public string title;
    public string world;
    public string character;
    public string role;
    public string spatialFocus;
    public bool unlocked;

    public Color planetSurface;
    public Color planetBands;
    public Color planetAtmosphere;
    public bool planetRing;

    // Resources VideoClips only play on desktop and in the editor. WebGL needs the
    // StreamingAssets file, because the VideoPlayer there only accepts a URL.
    public string introClipResource;
    public string introVideoFile;
    public string takeoffClipResource;
    public string takeoffVideoFile;

    public string HighScoreKey => "AstroBunny.Level" + number + ".HighScore";

    public int HighScore => PlayerPrefs.GetInt(HighScoreKey, 0);
}

/// <summary>
/// Level list from section 3 of the v9 spec. Only Level 1 has gameplay; the other
/// three cards show as locked.
/// </summary>
public static class LevelCatalog
{
    public static readonly LevelInfo[] Levels =
    {
        new LevelInfo
        {
            number = 1,
            title = "Station Evacuation",
            world = "Level 1: Station Evacuation",
            character = "Astro Bunny",
            role = "Spatial Cartography & Navigation",
            spatialFocus = "Containment, support and path motion",
            unlocked = true,
            planetSurface = new Color(0.88f, 0.60f, 0.32f),
            planetBands = new Color(0.55f, 0.29f, 0.15f),
            planetAtmosphere = new Color(1f, 0.72f, 0.42f),
            planetRing = true,
            introClipResource = "Videos/Level1_Intro",
            introVideoFile = "Videos/Level1_Intro.mp4",
            takeoffClipResource = "Videos/Level1_Takeoff",
            takeoffVideoFile = "Videos/Level1_Takeoff.mp4"
        },
        new LevelInfo
        {
            number = 2,
            title = "The Lost Jungle",
            world = "Level 2: The Lost Jungle",
            character = "Prof. Kalu Monkey",
            role = "Biomechanics & Canopy Robotics",
            spatialFocus = "Relative heights and vertical support",
            unlocked = false,
            planetSurface = new Color(0.27f, 0.66f, 0.44f),
            planetBands = new Color(0.10f, 0.33f, 0.31f),
            planetAtmosphere = new Color(0.55f, 1f, 0.72f),
            planetRing = false
        },
        new LevelInfo
        {
            number = 3,
            title = "The Ice Dunes",
            world = "Level 3: Ice Dunes",
            character = "Cmdr. Barnaby Dog",
            role = "Structural Geology & Surface Operations",
            spatialFocus = "Projective axes: left / right and front / back",
            unlocked = false,
            planetSurface = new Color(0.93f, 0.79f, 0.53f),
            planetBands = new Color(0.72f, 0.50f, 0.30f),
            planetAtmosphere = new Color(1f, 0.86f, 0.6f),
            planetRing = false
        },
        new LevelInfo
        {
            number = 4,
            title = "The Deep Ocean",
            world = "Level 4: Deep Ocean",
            character = "Dr. Shelby Turtle",
            role = "Oceanographic & Subsurface Dynamics",
            spatialFocus = "Distance relations: near and far",
            unlocked = false,
            planetSurface = new Color(0.22f, 0.48f, 0.86f),
            planetBands = new Color(0.08f, 0.18f, 0.43f),
            planetAtmosphere = new Color(0.5f, 0.8f, 1f),
            planetRing = true
        }
    };
}
