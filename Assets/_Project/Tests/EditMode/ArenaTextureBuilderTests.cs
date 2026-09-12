using NUnit.Framework;
using UnityEngine;
using MotorCombat.Arena;

namespace MotorCombat.Tests
{
    public class ArenaTextureBuilderTests
    {
        // RGBA32 quantises to bytes, and Color.Equals is exact float comparison --
        // so every test colour must be pure 0 or 1 per channel to survive a round trip.
        static readonly Color Background = Color.black;
        static readonly Color Minor = Color.white;
        static readonly Color Major = Color.red;

        static Texture2D Grid(int size = 64, int cells = 4, int minor = 1, int major = 2)
        {
            return ArenaTextureBuilder.BuildGrid(size, cells, minor, major, Background, Minor, Major);
        }

        // --- Grid: the tiling seam ------------------------------------------

        /// <summary>
        /// The property the whole placeholder rests on. A cell line is drawn on
        /// the LOW edge of each cell only. Draw the high edge as well and every
        /// join between two tiles becomes a double-width line -- subtle enough
        /// to ship and ugly enough to notice later.
        /// </summary>
        [Test]
        public void Grid_DrawsCellLinesOnTheLowEdgeOnly()
        {
            var texture = Grid();
            int last = texture.width - 1;

            // Sample across the line at 33, not 32: 32 is itself a cell boundary,
            // so a pixel there is a line whichever axis you meant to test.
            Assert.AreNotEqual(Background, texture.GetPixel(0, 33), "low edge must carry a line");
            Assert.AreEqual(Background, texture.GetPixel(last, 33), "high edge must stay clear");
            Assert.AreEqual(Background, texture.GetPixel(33, last), "high edge must stay clear vertically too");
        }

        [Test]
        public void Grid_TileEdgeCarriesTheMajorLine()
        {
            var texture = Grid();
            Assert.AreEqual(Major, texture.GetPixel(0, 33));
            Assert.AreEqual(Major, texture.GetPixel(33, 0));
        }

        [Test]
        public void Grid_InternalCellBoundariesCarryTheMinorLine()
        {
            var texture = Grid(64, 4, 1, 2);
            // 4 cells across 64px puts internal boundaries at 16, 32, 48.
            Assert.AreEqual(Minor, texture.GetPixel(16, 33));
            Assert.AreEqual(Minor, texture.GetPixel(32, 33));
            Assert.AreEqual(Minor, texture.GetPixel(48, 33));
        }

        [Test]
        public void Grid_CellInteriorsAreBackground()
        {
            var texture = Grid(64, 4, 1, 2);
            Assert.AreEqual(Background, texture.GetPixel(24, 24));
            Assert.AreEqual(Background, texture.GetPixel(40, 40));
        }

        /// <summary>
        /// Counting the lines catches an off-by-one that pixel spot-checks miss:
        /// N cells must produce exactly N lines across a row, never N+1.
        /// </summary>
        [Test]
        public void Grid_HasExactlyOneLinePerCellAcrossARow()
        {
            var texture = Grid(64, 4, 1, 2);

            int runs = 0;
            bool wasLine = false;
            for (int x = 0; x < texture.width; x++)
            {
                bool isLine = texture.GetPixel(x, 33) != Background;
                if (isLine && !wasLine) runs++;
                wasLine = isLine;
            }

            Assert.AreEqual(4, runs, "four cells means four lines, not five");
        }

        [Test]
        public void Grid_RepeatsAndIsMipmapped()
        {
            var texture = Grid();
            Assert.AreEqual(TextureWrapMode.Repeat, texture.wrapMode,
                "a tiling texture that clamps shows one stretched tile");
            Assert.Greater(texture.mipmapCount, 1,
                "without mipmaps a 1-car grid aliases into shimmer at distance");
        }

        // --- Wall stripes ----------------------------------------------------

        [Test]
        public void Stripes_AlternateAcrossTheWidth()
        {
            var texture = ArenaTextureBuilder.BuildStripes(64, 8, 4, Color.black, Color.white);
            Assert.AreEqual(Color.black, texture.GetPixel(0, 4));
            Assert.AreEqual(Color.white, texture.GetPixel(16, 4));
            Assert.AreEqual(Color.black, texture.GetPixel(32, 4));
            Assert.AreEqual(Color.white, texture.GetPixel(48, 4));
        }

        /// <summary>
        /// An odd stripe count puts the same colour on both sides of the tile
        /// seam, merging two stripes into one double-wide band that reads as a
        /// glitch exactly once per lap. The count is forced even.
        /// </summary>
        [Test]
        public void Stripes_ForceAnEvenCountSoTheSeamNeverDoublesUp()
        {
            var texture = ArenaTextureBuilder.BuildStripes(64, 8, 5, Color.black, Color.white);

            Color first = texture.GetPixel(0, 4);
            Color last = texture.GetPixel(texture.width - 1, 4);
            Assert.AreNotEqual(first, last,
                "first and last columns must differ, or the seam shows a double-wide stripe");
        }

        [Test]
        public void Stripes_RepeatAndAreMipmapped()
        {
            var texture = ArenaTextureBuilder.BuildStripes(64, 8, 4, Color.black, Color.white);
            Assert.AreEqual(TextureWrapMode.Repeat, texture.wrapMode);
            Assert.Greater(texture.mipmapCount, 1);
        }
    }
}
