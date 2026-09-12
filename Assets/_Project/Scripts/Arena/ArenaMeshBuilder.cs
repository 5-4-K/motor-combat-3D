using UnityEngine;

namespace MotorCombat.Arena
{
    /// <summary>
    /// Generates the arena's geometry. A real circular mesh rather than a ring of
    /// box colliders, because a car slides along this wall constantly and box
    /// seams catch.
    /// </summary>
    public static class ArenaMeshBuilder
    {
        /// <summary>Flat disc on the XZ plane at y = 0, normals up.</summary>
        public static Mesh BuildDisc(float radius, int segments)
        {
            segments = Mathf.Max(3, segments);

            var vertices = new Vector3[segments + 1];
            var normals = new Vector3[segments + 1];
            var uvs = new Vector2[segments + 1];
            var triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            normals[0] = Vector3.up;
            uvs[0] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                float x = Mathf.Cos(angle);
                float z = Mathf.Sin(angle);

                vertices[i + 1] = new Vector3(x * radius, 0f, z * radius);
                normals[i + 1] = Vector3.up;
                uvs[i + 1] = new Vector2((x + 1f) * 0.5f, (z + 1f) * 0.5f);

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
        public static Mesh BuildRing(float radius, float height, int segments)
        {
            segments = Mathf.Max(3, segments);

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

                uvs[bottom] = new Vector2(t, 0f);
                uvs[top] = new Vector2(t, 1f);
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
