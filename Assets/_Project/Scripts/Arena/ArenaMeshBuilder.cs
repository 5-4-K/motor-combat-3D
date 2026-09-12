using UnityEngine;

namespace MotorCombat.Arena
{
    /// <summary>
    /// Generates the arena's geometry. A real circular mesh rather than a ring of
    /// box colliders, because a car slides along this wall constantly and box
    /// seams catch.
    ///
    /// UVs are baked in WORLD scale, not normalised 0..1. Normalised UVs stretch
    /// a single texture across the whole 90m arena; worse, they make tiling a
    /// property of the material, so every texture swapped in would need its scale
    /// re-tuned by hand and re-tuned again on every radius change. Baked here,
    /// any material dropped into ArenaConfig tiles correctly with no setup.
    /// </summary>
    public static class ArenaMeshBuilder
    {
        /// <summary>
        /// How many times the texture repeats around the wall. Rounded to a whole
        /// number so the seam at angle 0 is impossible by construction rather
        /// than merely unlikely -- a fractional repeat leaves a visible jump.
        /// </summary>
        public static int WallRepeats(float radius, float tileSize)
        {
            if (tileSize <= 0f) return 1;
            float circumference = 2f * Mathf.PI * radius;
            return Mathf.Max(1, Mathf.RoundToInt(circumference / tileSize));
        }

        /// <summary>
        /// Flat disc on the XZ plane at y = 0, normals up.
        /// <paramref name="tileSize"/> is the world size of one texture repeat.
        /// </summary>
        public static Mesh BuildDisc(float radius, int segments, float tileSize)
        {
            segments = Mathf.Max(3, segments);
            tileSize = Mathf.Max(1e-4f, tileSize);

            var vertices = new Vector3[segments + 1];
            var normals = new Vector3[segments + 1];
            var uvs = new Vector2[segments + 1];
            var triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            normals[0] = Vector3.up;

            // Half-tile offset so the world origin -- where the player spawns --
            // sits inside a cell rather than on a line crossing.
            uvs[0] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                float x = Mathf.Cos(angle);
                float z = Mathf.Sin(angle);

                vertices[i + 1] = new Vector3(x * radius, 0f, z * radius);
                normals[i + 1] = Vector3.up;
                uvs[i + 1] = new Vector2(
                    x * radius / tileSize + 0.5f,
                    z * radius / tileSize + 0.5f);

                int next = (i + 1) % segments;
                triangles[i * 3 + 0] = 0;
                triangles[i * 3 + 1] = next + 1;
                triangles[i * 3 + 2] = i + 1;
            }

            var mesh = new Mesh { name = "ArenaGround" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Open-ended cylinder wall, inward facing, from y = 0 to y = height.
        /// The seam column is duplicated so UVs do not wrap.
        /// </summary>
        public static Mesh BuildRing(float radius, float height, int segments, float tileSize)
        {
            segments = Mathf.Max(3, segments);
            tileSize = Mathf.Max(1e-4f, tileSize);

            int repeats = WallRepeats(radius, tileSize);

            // Vertical UVs keep tiles square rather than stretching one repeat to
            // the wall's height. A short wall therefore shows a horizontal slice
            // of the texture, which is what an undistorted material should do.
            float vTop = height / tileSize;

            int columns = segments + 1;   // duplicate the seam column
            var vertices = new Vector3[columns * 2];
            var normals = new Vector3[columns * 2];
            var uvs = new Vector2[columns * 2];
            var triangles = new int[segments * 6];

            for (int i = 0; i < columns; i++)
            {
                float t = i / (float)segments;
                float angle = t * Mathf.PI * 2f;
                float x = Mathf.Cos(angle);
                float z = Mathf.Sin(angle);

                int bottom = i * 2;
                int top = bottom + 1;

                vertices[bottom] = new Vector3(x * radius, 0f, z * radius);
                vertices[top] = new Vector3(x * radius, height, z * radius);

                // Inward facing: the players are inside the cylinder.
                var inward = new Vector3(-x, 0f, -z);
                normals[bottom] = inward;
                normals[top] = inward;

                uvs[bottom] = new Vector2(t * repeats, 0f);
                uvs[top] = new Vector2(t * repeats, vTop);
            }

            for (int i = 0; i < segments; i++)
            {
                int bottom = i * 2;
                int top = bottom + 1;
                int nextBottom = bottom + 2;
                int nextTop = bottom + 3;

                // Wound so the front face points at the arena centre. Unity
                // front faces are clockwise seen from the front, so reversing
                // these two triples would leave the wall invisible from inside
                // (collision would still work, which makes it easy to miss).
                triangles[i * 6 + 0] = bottom;
                triangles[i * 6 + 1] = nextTop;
                triangles[i * 6 + 2] = top;

                triangles[i * 6 + 3] = bottom;
                triangles[i * 6 + 4] = nextBottom;
                triangles[i * 6 + 5] = nextTop;
            }

            var mesh = new Mesh { name = "ArenaWall" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
