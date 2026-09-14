using System;
using UnityEngine;

namespace MotorCombat.HUD
{
    /// <summary>Circle and ring sprites generated in code, like the arena's textures. White, so an Image tints them.</summary>
    public static class HudShapes
    {
        const int Size = 128;

        static Sprite _circle;

        public static Sprite Circle => _circle != null ? _circle : (_circle = Build("HudCircle", d => CircleAlpha(d, Size * 0.5f)));

        /// <summary>A new ring whose band is <paramref name="thicknessFraction"/> of the diameter. The caller owns it.</summary>
        public static Sprite CreateRing(float thicknessFraction)
        {
            float thickness = Mathf.Max(1f, thicknessFraction * Size);
            return Build("HudRing", d => RingAlpha(d, Size * 0.5f, thickness));
        }

        /// <summary>1 inside, 0 outside, a one-pixel soft edge centred on the radius.</summary>
        public static float CircleAlpha(float distanceFromCentre, float radius)
        {
            return Mathf.Clamp01(radius - distanceFromCentre + 0.5f);
        }

        public static float RingAlpha(float distanceFromCentre, float radius, float thickness)
        {
            return Mathf.Min(CircleAlpha(distanceFromCentre, radius), 1f - CircleAlpha(distanceFromCentre, radius - thickness));
        }

        static Sprite Build(string name, Func<float, float> alpha)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[Size * Size];
            float centre = Size * 0.5f;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = x + 0.5f - centre;
                    float dy = y + 0.5f - centre;
                    byte a = (byte)Mathf.RoundToInt(alpha(Mathf.Sqrt(dx * dx + dy * dy)) * 255f);
                    pixels[y * Size + x] = new Color32(255, 255, 255, a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            return sprite;
        }
    }
}
