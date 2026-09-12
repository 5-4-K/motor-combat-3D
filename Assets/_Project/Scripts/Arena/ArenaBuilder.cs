using UnityEngine;

namespace MotorCombat.Arena
{
    /// <summary>
    /// Turns an ArenaConfig plus a car length into actual GameObjects.
    /// </summary>
    public static class ArenaBuilder
    {
        // Placeholder palette. Only used when ArenaConfig leaves a material slot
        // empty -- assign real materials and none of this runs.
        static readonly Color GroundBase = new Color(0.18f, 0.19f, 0.22f);
        static readonly Color GroundMinor = new Color(0.30f, 0.32f, 0.36f);
        static readonly Color GroundMajor = new Color(0.55f, 0.58f, 0.62f);
        static readonly Color WallDark = new Color(0.30f, 0.26f, 0.24f);
        static readonly Color WallLight = new Color(0.42f, 0.37f, 0.34f);

        public static float RadiusFor(ArenaConfig config, float carLength)
        {
            return config.radiusInCarLengths * carLength;
        }

        /// <summary>
        /// World size of one texture repeat. Config states it in car lengths so
        /// the arena's look scales with the car rather than drifting out of
        /// proportion whenever car length is tweaked.
        /// </summary>
        public static float TileSizeFor(ArenaConfig config, float carLength)
        {
            return Mathf.Max(0.01f, config.tileSizeInCarLengths * carLength);
        }

        /// <summary>Cells per texture repeat: one per car length.</summary>
        public static int CellsPerTile(ArenaConfig config)
        {
            return Mathf.Clamp(Mathf.RoundToInt(config.tileSizeInCarLengths), 1, 32);
        }

        /// <summary>
        /// Builds ground and wall under a new "Arena" GameObject and returns it.
        /// </summary>
        public static GameObject Build(ArenaConfig config, float carLength, Transform parent = null)
        {
            float radius = RadiusFor(config, carLength);
            float tileSize = TileSizeFor(config, carLength);
            int cells = CellsPerTile(config);

            var root = new GameObject("Arena");
            if (parent != null) root.transform.SetParent(parent, false);

            CreatePiece(
                "Ground",
                ArenaMeshBuilder.BuildDisc(radius, config.segments, tileSize),
                config.groundMaterial != null ? config.groundMaterial : PlaceholderGround(cells),
                root.transform);

            CreatePiece(
                "Wall",
                ArenaMeshBuilder.BuildRing(radius, config.wallHeight, config.segments, tileSize),
                config.wallMaterial != null ? config.wallMaterial : PlaceholderWall(cells),
                root.transform);

            return root;
        }

        static Material PlaceholderGround(int cells)
        {
            // 512px over a repeat of ~22m gives roughly 23 px/m, so a 2px minor
            // line reads as a ~9cm painted stripe and a 6px major as ~26cm.
            Texture2D grid = ArenaTextureBuilder.BuildGrid(
                512, cells, 2, 6, GroundBase, GroundMinor, GroundMajor);
            return CreateMaterial(grid, "ArenaGroundMaterial");
        }

        static Material PlaceholderWall(int cells)
        {
            // Two stripes per cell ties the wall's rotation reference to the same
            // unit as the floor grid: one stripe is half a car length.
            Texture2D stripes = ArenaTextureBuilder.BuildStripes(
                256, 32, cells * 2, WallDark, WallLight);
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
