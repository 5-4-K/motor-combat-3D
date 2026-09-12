using NUnit.Framework;
using UnityEngine;
using MotorCombat.Arena;

namespace MotorCombat.Tests
{
    public class ArenaMeshBuilderTests
    {
        // One texture repeat spans 5 car lengths at the default 4.5m car.
        const float Tile = 22.5f;

        [Test]
        public void Disc_HasCentreVertexPlusOnePerSegment()
        {
            var mesh = ArenaMeshBuilder.BuildDisc(45f, 32, Tile);
            Assert.AreEqual(33, mesh.vertexCount, "one centre vertex plus one per segment");
            Assert.AreEqual(32 * 3, mesh.triangles.Length, "one triangle per segment");
        }

        [Test]
        public void Disc_RimVerticesSitOnTheRadius()
        {
            var mesh = ArenaMeshBuilder.BuildDisc(45f, 32, Tile);
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
            var mesh = ArenaMeshBuilder.BuildDisc(45f, 16, Tile);
            foreach (var v in mesh.vertices)
            {
                Assert.AreEqual(0f, v.y, 1e-5f);
            }
        }

        [Test]
        public void Ring_HasABottomAndTopVertexPerColumn()
        {
            var mesh = ArenaMeshBuilder.BuildRing(45f, 2.5f, 32, Tile);
            Assert.AreEqual((32 + 1) * 2, mesh.vertexCount, "a bottom and top vertex per column, seam column duplicated");
            Assert.AreEqual(32 * 6, mesh.triangles.Length, "two triangles per segment");
        }

        [Test]
        public void Ring_SpansFromGroundToWallHeight()
        {
            var mesh = ArenaMeshBuilder.BuildRing(45f, 2.5f, 32, Tile);
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
            var mesh = ArenaMeshBuilder.BuildRing(45f, 2.5f, 24, Tile);
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
            var mesh = ArenaMeshBuilder.BuildRing(45f, 2.5f, 24, Tile);
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
            var mesh = ArenaMeshBuilder.BuildDisc(45f, 24, Tile);
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

        // --- UVs --------------------------------------------------------------

        /// <summary>
        /// UVs are world-scaled, not normalised. A rim vertex 45m out with a
        /// 22.5m repeat must land two repeats from the centre, not at uv 1.
        /// </summary>
        [Test]
        public void Disc_UvsAreWorldScaledNotNormalised()
        {
            var mesh = ArenaMeshBuilder.BuildDisc(45f, 32, Tile);
            var uvs = mesh.uv;

            Assert.AreEqual(0.5f, uvs[0].x, 1e-4f, "centre carries the half-tile offset");
            Assert.AreEqual(0.5f, uvs[0].y, 1e-4f, "centre carries the half-tile offset");

            // Vertex 1 sits at angle 0, i.e. (+45, 0, 0).
            Assert.AreEqual(45f / Tile + 0.5f, uvs[1].x, 1e-4f);
            Assert.AreEqual(0.5f, uvs[1].y, 1e-4f);
        }

        [Test]
        public void Disc_UvSpanIsOneRepeatPerTileOfDiameter()
        {
            var mesh = ArenaMeshBuilder.BuildDisc(45f, 64, Tile);

            float min = float.MaxValue, max = float.MinValue;
            foreach (var uv in mesh.uv)
            {
                min = Mathf.Min(min, uv.x);
                max = Mathf.Max(max, uv.x);
            }

            // 90m of diameter over a 22.5m repeat is four repeats across.
            Assert.AreEqual(4f, max - min, 1e-3f);
        }

        /// <summary>
        /// The property that kills the wall seam: a fractional repeat count
        /// leaves the texture cut mid-tile at angle 0, visible once per lap.
        /// </summary>
        [Test]
        public void Ring_RepeatsAWholeNumberOfTimesAroundTheCircle()
        {
            var mesh = ArenaMeshBuilder.BuildRing(45f, 2.5f, 96, Tile);
            var uvs = mesh.uv;

            // Last bottom vertex minus first bottom vertex: the full lap.
            float span = uvs[uvs.Length - 2].x - uvs[0].x;

            Assert.Greater(span, 0f);
            Assert.AreEqual(Mathf.Round(span), span, 1e-4f, "u must close on a whole repeat");
        }

        [Test]
        public void Ring_RepeatCountIsTheRoundedCircumference()
        {
            // 2*pi*45 = 282.7m over a 22.5m repeat rounds to 13.
            Assert.AreEqual(13, ArenaMeshBuilder.WallRepeats(45f, Tile));
            Assert.AreEqual(1, ArenaMeshBuilder.WallRepeats(45f, 100000f), "never drops below one repeat");
        }

        /// <summary>
        /// Vertical UVs must keep tiles square. Stretching one repeat to the
        /// wall height would squash any real texture by the height-to-tile ratio.
        /// </summary>
        [Test]
        public void Ring_VerticalUvsKeepTilesSquare()
        {
            var mesh = ArenaMeshBuilder.BuildRing(45f, 2.5f, 32, Tile);
            var uvs = mesh.uv;

            Assert.AreEqual(0f, uvs[0].y, 1e-4f, "bottom row sits at v = 0");
            Assert.AreEqual(2.5f / Tile, uvs[1].y, 1e-4f, "top row is height/tileSize, not 1");
        }

        // --- Tangents ---------------------------------------------------------

        /// <summary>
        /// Normal-mapped and parallax-mapped materials shade in TANGENT space.
        /// A mesh with no tangents leaves the shader reading zeros, the basis
        /// degenerates, and the perturbed normal flips per triangle: the wall
        /// shades in visible chunks and the floor's highlights smear. Nothing
        /// about the geometry looks wrong, which is what makes it hard to spot.
        /// </summary>
        [Test]
        public void Disc_HasUsableTangentsForNormalMappedMaterials()
        {
            AssertUsableTangents(ArenaMeshBuilder.BuildDisc(45f, 32, Tile));
        }

        [Test]
        public void Ring_HasUsableTangentsForNormalMappedMaterials()
        {
            AssertUsableTangents(ArenaMeshBuilder.BuildRing(45f, 2.5f, 32, Tile));
        }

        static void AssertUsableTangents(Mesh mesh)
        {
            var tangents = mesh.tangents;
            var normals = mesh.normals;

            Assert.AreEqual(mesh.vertexCount, tangents.Length, "one tangent per vertex");

            for (int i = 0; i < tangents.Length; i++)
            {
                var tangent = new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);

                Assert.Greater(tangent.sqrMagnitude, 1e-6f, $"tangent {i} is degenerate");
                Assert.AreEqual(0f, Vector3.Dot(tangent.normalized, normals[i]), 1e-3f,
                    $"tangent {i} must lie in the surface, perpendicular to the normal");
                Assert.AreEqual(1f, Mathf.Abs(tangents[i].w), 1e-3f,
                    $"tangent {i} carries no handedness sign");
            }
        }
    }
}
