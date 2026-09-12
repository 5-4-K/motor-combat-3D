using NUnit.Framework;
using UnityEngine;
using MotorCombat.Arena;

namespace MotorCombat.Tests
{
    public class ArenaMeshBuilderTests
    {
        [Test]
        public void Disc_HasCentreVertexPlusOnePerSegment()
        {
            var mesh = ArenaMeshBuilder.BuildDisc(45f, 32);
            Assert.AreEqual(33, mesh.vertexCount, "one centre vertex plus one per segment");
            Assert.AreEqual(32 * 3, mesh.triangles.Length, "one triangle per segment");
        }

        [Test]
        public void Disc_RimVerticesSitOnTheRadius()
        {
            var mesh = ArenaMeshBuilder.BuildDisc(45f, 32);
            var vertices = mesh.vertices;

            // vertex 0 is the centre; the rest are the rim
            for (int i = 1; i < vertices.Length; i++)
            {
                float distance = new Vector2(vertices[i].x, vertices[i].z).magnitude;
                Assert.AreEqual(45f, distance, 1e-3f, $"vertex {i} off the rim");
            }
        }

        [Test]
        public void Disc_IsFlatAtYZero()
        {
            var mesh = ArenaMeshBuilder.BuildDisc(45f, 16);
            foreach (var v in mesh.vertices)
            {
                Assert.AreEqual(0f, v.y, 1e-5f);
            }
        }

        [Test]
        public void Ring_HasABottomAndTopVertexPerColumn()
        {
            var mesh = ArenaMeshBuilder.BuildRing(45f, 2.5f, 32);
            Assert.AreEqual((32 + 1) * 2, mesh.vertexCount, "a bottom and top vertex per column, seam column duplicated");
            Assert.AreEqual(32 * 6, mesh.triangles.Length, "two triangles per segment");
        }

        [Test]
        public void Ring_SpansFromGroundToWallHeight()
        {
            var mesh = ArenaMeshBuilder.BuildRing(45f, 2.5f, 32);
            float minY = float.MaxValue, maxY = float.MinValue;

            foreach (var v in mesh.vertices)
            {
                minY = Mathf.Min(minY, v.y);
                maxY = Mathf.Max(maxY, v.y);
            }

            Assert.AreEqual(0f, minY, 1e-5f);
            Assert.AreEqual(2.5f, maxY, 1e-5f);
        }

        [Test]
        public void Ring_VerticesSitOnTheRadius()
        {
            var mesh = ArenaMeshBuilder.BuildRing(45f, 2.5f, 24);
            foreach (var v in mesh.vertices)
            {
                float distance = new Vector2(v.x, v.z).magnitude;
                Assert.AreEqual(45f, distance, 1e-3f);
            }
        }

        /// <summary>
        /// Winding, not just geometry. Unity front faces are clockwise seen from
        /// the front, which makes cross(b-a, c-a) point along the face normal. A
        /// wall wound the wrong way still collides correctly, so nothing else in
        /// this suite would catch it — you would only notice the arena looking
        /// hollow at runtime.
        /// </summary>
        [Test]
        public void Ring_TrianglesFaceInward()
        {
            var mesh = ArenaMeshBuilder.BuildRing(45f, 2.5f, 24);
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]];
                Vector3 b = vertices[triangles[i + 1]];
                Vector3 c = vertices[triangles[i + 2]];

                Vector3 faceNormal = Vector3.Cross(b - a, c - a);
                Vector3 outward = new Vector3(a.x, 0f, a.z);

                Assert.Less(Vector3.Dot(faceNormal, outward), 0f,
                    $"triangle at index {i} faces away from the arena centre");
            }
        }

        [Test]
        public void Disc_TrianglesFaceUp()
        {
            var mesh = ArenaMeshBuilder.BuildDisc(45f, 24);
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]];
                Vector3 b = vertices[triangles[i + 1]];
                Vector3 c = vertices[triangles[i + 2]];

                Vector3 faceNormal = Vector3.Cross(b - a, c - a);

                Assert.Greater(Vector3.Dot(faceNormal, Vector3.up), 0f,
                    $"triangle at index {i} faces downward");
            }
        }
    }
}
