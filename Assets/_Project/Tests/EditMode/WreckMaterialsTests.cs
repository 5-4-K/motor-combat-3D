using NUnit.Framework;
using UnityEngine;
using MotorCombat.Combat;

namespace MotorCombat.Tests
{
    public class WreckMaterialsTests
    {
        [Test]
        public void MakeTransparent_SwitchesUrpLitToTransparent()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) Assert.Ignore("URP Lit shader not available in this run");

            var material = new Material(lit);
            WreckMaterials.MakeTransparent(material);

            Assert.AreEqual(1f, material.GetFloat("_Surface"));
            Assert.AreEqual(0f, material.GetFloat("_ZWrite"));
            Assert.AreEqual(3000, material.renderQueue);
            Assert.IsTrue(material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"));

            Object.DestroyImmediate(material);
        }
    }
}
