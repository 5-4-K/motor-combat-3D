using NUnit.Framework;
using UnityEngine;
using MotorCombat.Arena;
using MotorCombat.Core;

namespace MotorCombat.Tests
{
    public class ArenaBuilderTests
    {
        const float CarLength = 4.5f;

        static ArenaConfig Config()
        {
            var config = ScriptableObject.CreateInstance<ArenaConfig>();
            config.radiusMetres = 60f;
            config.groundTileMetres = 4f;
            config.wallTileMetres = 2.5f;
            config.gridCellInCarLengths = 1f;
            return config;
        }

        static Material AnyMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return new Material(shader);
        }

        // --- Radius -----------------------------------------------------------

        /// <summary>
        /// The arena is a place, not a multiple of the car. Retuning car length
        /// must leave its size alone.
        /// </summary>
        [Test]
        public void Radius_IsAbsoluteAndIgnoresCarLength()
        {
            var config = Config();
            Assert.AreEqual(60f, ArenaBuilder.RadiusFor(config), 1e-4f);
        }

        [Test]
        public void Radius_NeverCollapsesToZero()
        {
            var config = Config();
            config.radiusMetres = 0f;
            Assert.Greater(ArenaBuilder.RadiusFor(config), 0f);
        }

        // --- Tile sizes -------------------------------------------------------

        /// <summary>
        /// A texture's scale is a physical property of the material, so a real
        /// material tiles at its configured metres regardless of the car.
        /// </summary>
        [Test]
        public void GroundTile_UsesConfiguredMetresWhenAMaterialIsAssigned()
        {
            var config = Config();
            config.groundMaterial = AnyMaterial();

            Assert.AreEqual(4f, ArenaBuilder.GroundTileSize(config, CarLength), 1e-4f);
            Assert.AreEqual(4f, ArenaBuilder.GroundTileSize(config, CarLength * 3f), 1e-4f,
                "a real material's tiling must not move with car length");
        }

        /// <summary>
        /// The placeholder is the deliberate exception: it is a ruler, so its
        /// cells stay one car long and it DOES scale with the car.
        /// </summary>
        [Test]
        public void GroundTile_FallsBackToWholeCarLengthsForThePlaceholder()
        {
            var config = Config();   // no material assigned

            Assert.AreEqual(CarLength * ArenaBuilder.GridCellsPerTile,
                ArenaBuilder.GroundTileSize(config, CarLength), 1e-4f);
        }

        [Test]
        public void WallTile_IsIndependentOfTheFloorTile()
        {
            var config = Config();
            config.groundMaterial = AnyMaterial();
            config.wallMaterial = AnyMaterial();

            Assert.AreEqual(4f, ArenaBuilder.GroundTileSize(config, CarLength), 1e-4f);
            Assert.AreEqual(2.5f, ArenaBuilder.WallTileSize(config, CarLength), 1e-4f);
        }

        /// <summary>
        /// Both placeholders share a tile so the wall's stripes line up with the
        /// floor's grid cells.
        /// </summary>
        [Test]
        public void Placeholders_ShareATileSoStripesAlignWithGridCells()
        {
            var config = Config();

            Assert.AreEqual(
                ArenaBuilder.GroundTileSize(config, CarLength),
                ArenaBuilder.WallTileSize(config, CarLength), 1e-4f);
        }

        [Test]
        public void Build_PutsGroundAndWallOnTheArenaLayer()
        {
            var config = Config();
            config.groundMaterial = AnyMaterial();
            config.wallMaterial = AnyMaterial();

            GameObject root = ArenaBuilder.Build(config, CarLength);
            try
            {
                Assert.AreEqual(PhysicsLayers.Arena, root.transform.Find("Ground").gameObject.layer);
                Assert.AreEqual(PhysicsLayers.Arena, root.transform.Find("Wall").gameObject.layer);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
