using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Arena
{
    /// <summary>
    /// Turns an ArenaConfig plus a car length into actual GameObjects.
    ///
    /// Sizes here are absolute metres. The one exception is the placeholder
    /// calibration grid, which is car-relative on purpose -- it exists to let you
    /// count distance and drift in car lengths while play-testing.
    /// </summary>
    public static class ArenaBuilder
    {
        /// <summary>Cells inside one placeholder grid repeat: a heavier line every 5 cars.</summary>
        public const int GridCellsPerTile = 5;

        // Placeholder palette. Only used when ArenaConfig leaves a material slot
        // empty -- assign real materials and none of this runs.
        static readonly Color GroundBase = new Color(0.18f, 0.19f, 0.22f);
        static readonly Color GroundMinor = new Color(0.30f, 0.32f, 0.36f);
        static readonly Color GroundMajor = new Color(0.55f, 0.58f, 0.62f);
        static readonly Color WallDark = new Color(0.30f, 0.26f, 0.24f);
        static readonly Color WallLight = new Color(0.42f, 0.37f, 0.34f);

        public static float RadiusFor(ArenaConfig config)
        {
            return Mathf.Max(0.01f, config.radiusMetres);
        }

        /// <summary>
        /// World size of one floor texture repeat. A real material tiles at its
        /// own physical scale; the placeholder tiles at whole car lengths so its
        /// cells stay countable.
        /// </summary>
        public static float GroundTileSize(ArenaConfig config, float carLength)
        {
            return config.groundMaterial != null
                ? Mathf.Max(0.01f, config.groundTileMetres)
                : PlaceholderTileSize(config, carLength);
        }

        public static float WallTileSize(ArenaConfig config, float carLength)
        {
            return config.wallMaterial != null
                ? Mathf.Max(0.01f, config.wallTileMetres)
                : PlaceholderTileSize(config, carLength);
        }

        /// <summary>
        /// One repeat holds GridCellsPerTile cells, each one car long. Both
        /// placeholders share it so the wall's stripes line up with floor cells.
        /// </summary>
        static float PlaceholderTileSize(ArenaConfig config, float carLength)
        {
            return Mathf.Max(0.01f, config.gridCellInCarLengths * carLength * GridCellsPerTile);
        }

        /// <summary>
        /// Builds ground and wall under a new "Arena" GameObject and returns it.
        /// carLength is needed only to scale the placeholder grid.
        /// </summary>
        public static GameObject Build(ArenaConfig config, float carLength, Transform parent = null)
        {
            float radius = RadiusFor(config);

            var root = new GameObject("Arena");
            if (parent != null) root.transform.SetParent(parent, false);

            CreatePiece(
                "Ground",
                ArenaMeshBuilder.BuildDisc(radius, config.segments, GroundTileSize(config, carLength)),
                config.groundMaterial != null ? config.groundMaterial : PlaceholderGround(),
                root.transform);

            CreatePiece(
                "Wall",
                ArenaMeshBuilder.BuildRing(radius, config.wallHeight, config.segments, WallTileSize(config, carLength)),
                config.wallMaterial != null ? config.wallMaterial : PlaceholderWall(),
                root.transform);

            return root;
        }

        static Material PlaceholderGround()
        {
            Texture2D grid = ArenaTextureBuilder.BuildGrid(
                512, GridCellsPerTile, 2, 6, GroundBase, GroundMinor, GroundMajor);
            return CreateMaterial(grid, "ArenaGroundMaterial");
        }

        static Material PlaceholderWall()
        {
            // Two stripes per cell ties the wall's rotation reference to the same
            // unit as the floor grid: one stripe is half a car length.
            Texture2D stripes = ArenaTextureBuilder.BuildStripes(
                256, 32, GridCellsPerTile * 2, WallDark, WallLight);
            return CreateMaterial(stripes, "ArenaWallMaterial");
        }

        static Material CreateMaterial(Texture2D texture, string name)
        {
            // URP's lit shader. Falls back to the built-in standard shader if the
            // project is ever moved off URP.
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = name };

            // URP Lit ignores _MainTex outright -- a texture assigned there is
            // silently dropped, which looks exactly like a broken generator.
            // Built-in Standard is the mirror image, so set whichever exists.
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);

            // White tint: the texture already carries the colour.
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);

            // Arena surfaces are matt; a default-glossy floor mirrors the single
            // directional light into a distracting hotspot.
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.05f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.05f);

            return material;
        }

        static void CreatePiece(string name, Mesh mesh, Material material, Transform parent)
        {
            var piece = new GameObject(name);
            piece.transform.SetParent(parent, false);

            int arenaLayer = PhysicsLayers.Arena;
            if (arenaLayer >= 0) piece.layer = arenaLayer;

            piece.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = piece.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            // Non-convex mesh colliders are legal on static geometry, which this is.
            var collider = piece.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = false;
        }
    }
}
