using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using MotorCombat.HUD;

namespace MotorCombat.Tests
{
    public class HudRootTests
    {
        /// <summary>The fairness rule: HUD proportions follow screen WIDTH, identical on every monitor.</summary>
        [Test]
        public void Create_ScalesWithScreenWidth()
        {
            HudRoot hud = HudRoot.Create();
            try
            {
                var scaler = hud.GetComponent<CanvasScaler>();
                Assert.AreEqual(RenderMode.ScreenSpaceOverlay, hud.Canvas.renderMode);
                Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
                Assert.AreEqual(new Vector2(1920f, 1080f), scaler.referenceResolution);
                Assert.AreEqual(CanvasScaler.ScreenMatchMode.MatchWidthOrHeight, scaler.screenMatchMode);
                Assert.AreEqual(0f, scaler.matchWidthOrHeight);
            }
            finally
            {
                Object.DestroyImmediate(hud.gameObject);
            }
        }
    }
}
