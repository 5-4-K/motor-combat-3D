using UnityEngine;

namespace MotorCombat.Arena
{
    /// <summary>
    /// Turns an ArenaConfig plus a car length into actual GameObjects.
    /// </summary>
    public static class ArenaBuilder
    {
        public static float RadiusFor(ArenaConfig config, float carLength)
        {
            return config.radiusInCarLengths * carLength;
        }

        /// <summary>
        /// Builds ground and wall under a new "Arena" GameObject and returns it.
        /// </summary>
        public static GameObject Build(ArenaConfig config, float carLength, Transform parent = null)
        {
            float radius = RadiusFor(config, carLength);

            var root = new GameObject("Arena");
            if (parent != null) root.transform.SetParent(parent, false);

            CreatePiece(
                "Ground",
                ArenaMeshBuilder.BuildDisc(radius, config.segments),
                new Color(0.22f, 0.24f, 0.27f),
                root.transform);

            CreatePiece(
                "Wall",
                ArenaMeshBuilder.BuildRing(radius, config.wallHeight, config.segments),
                new Color(0.35f, 0.30f, 0.28f),
                root.transform);

            return root;
        }

        static void CreatePiece(string name, Mesh mesh, Color colour, Transform parent)
        {
            var piece = new GameObject(name);
            piece.transform.SetParent(parent, false);

            piece.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = piece.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateMaterial(colour);

            // Non-convex mesh colliders are legal on static geometry, which this is.
            var collider = piece.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = false;
        }

        static Material CreateMaterial(Color colour)
        {
            // URP's lit shader. Falls back to the built-in standard shader if the
            // project is ever moved off URP.
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = "ArenaMaterial" };
            material.color = colour;
            return material;
        }
    }
}
