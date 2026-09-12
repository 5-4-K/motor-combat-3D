using UnityEngine;

namespace MotorCombat.Arena
{
    /// <summary>
    /// Generates the arena's placeholder textures in code, so the project needs
    /// no binary art to look right. These are stand-ins: assign real materials to
    /// ArenaConfig and nothing here runs.
    ///
    /// Every texture produced here is designed to TILE. That constrains the
    /// drawing more than it looks -- see the low-edge rule on the grid and the
    /// even-count rule on the stripes.
    /// </summary>
    public static class ArenaTextureBuilder
    {
        /// <summary>
        /// A calibration grid: one texture repeat holds <paramref name="cells"/>
        /// cells each way, with a heavier line on the repeat's own edge.
        /// Sized in world space so one cell is one car length, the major lines
        /// then land every <paramref name="cells"/> cars.
        /// </summary>
        public static Texture2D BuildGrid(
            int size,
            int cells,
            int minorWidth,
            int majorWidth,
            Color background,
            Color minorLine,
            Color majorLine)
        {
            size = Mathf.Max(4, size);
            cells = Mathf.Clamp(cells, 1, size / 2);
            minorWidth = Mathf.Max(1, minorWidth);
            majorWidth = Mathf.Max(minorWidth, majorWidth);

            // Lines go on the LOW edge of each cell and nowhere else. Drawing the
            // high edge too would leave every seam between two tiles carrying two
            // adjacent lines, i.e. one double-width line, once per repeat.
            bool[] minor = new bool[size];
            for (int c = 0; c < cells; c++)
            {
                int start = Mathf.RoundToInt(c * size / (float)cells);
                for (int w = 0; w < minorWidth; w++)
                {
                    int x = start + w;
                    if (x < size) minor[x] = true;
                }
            }

            bool[] major = new bool[size];
            for (int w = 0; w < majorWidth && w < size; w++) major[w] = true;

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color pixel = background;
                    if (minor[x] || minor[y]) pixel = minorLine;
                    if (major[x] || major[y]) pixel = majorLine;   // major wins the corner
                    pixels[y * size + x] = pixel;
                }
            }

            return Finish(pixels, size, size, "ArenaGridTexture");
        }

        /// <summary>
        /// Vertical stripes for the wall, giving the eye a rotation reference.
        /// The stripe count is forced even: an odd count puts the same colour on
        /// both sides of the seam, merging two stripes into one double-wide band
        /// that reads as a glitch exactly once per lap.
        /// </summary>
        public static Texture2D BuildStripes(int width, int height, int stripes, Color a, Color b)
        {
            width = Mathf.Max(2, width);
            height = Mathf.Max(1, height);

            stripes = Mathf.Clamp(stripes, 2, width);
            if (stripes % 2 != 0) stripes--;
            stripes = Mathf.Max(2, stripes);

            var pixels = new Color[width * height];
            for (int x = 0; x < width; x++)
            {
                Color column = (x * stripes / width) % 2 == 0 ? a : b;
                for (int y = 0; y < height; y++)
                {
                    pixels[y * width + x] = column;
                }
            }

            return Finish(pixels, width, height, "ArenaStripeTexture");
        }

        static Texture2D Finish(Color[] pixels, int width, int height, string name)
        {
            // mipChain is not optional here. A one-car grid spread across a 90m
            // floor without mipmaps aliases into crawling shimmer at distance --
            // worst exactly where the player is looking while driving fast.
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: true)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 9   // the floor is viewed at a grazing angle almost always
            };

            texture.SetPixels(pixels);
            texture.Apply(updateMipmaps: true);
            return texture;
        }
    }
}
