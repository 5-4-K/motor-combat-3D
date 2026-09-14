using NUnit.Framework;
using UnityEngine;
using MotorCombat.Ramming;

namespace MotorCombat.Tests
{
    /// <summary>
    /// Every ram rule, without a scene. Footprint used throughout: 2 m wide,
    /// 4 m long, 0.3 m corner band.
    /// </summary>
    public class RamRulesTests
    {
        const float W = 2f;
        const float L = 4f;
        const float Band = 0.3f;
        const float MinSpeed = 3f;
        const float HeadOn = 45f;

        static RamRegion RegionAt(float x, float z) => RamRules.Region(new Vector3(x, 0f, z), W, L, Band);

        static Vector3 Yawed(Vector3 direction, float degrees) => Quaternion.AngleAxis(degrees, Vector3.up) * direction;

        static RamParticipant P(RamRegion region, Vector3 forward, float speed, bool canAttack = true)
        {
            return new RamParticipant { region = region, flatForward = forward, forwardSpeed = speed, canAttack = canAttack };
        }

        // --- Regions ------------------------------------------------------------

        [Test] public void Region_FrontFaceCentre_IsFront() => Assert.AreEqual(RamRegion.Front, RegionAt(0f, 2f));
        [Test] public void Region_RearFaceCentre_IsRear() => Assert.AreEqual(RamRegion.Rear, RegionAt(0f, -2f));
        [Test] public void Region_SideCentre_IsSide() => Assert.AreEqual(RamRegion.Side, RegionAt(1f, 0f));
        [Test] public void Region_FrontLeftCorner_IsFrontCorner() => Assert.AreEqual(RamRegion.FrontCorner, RegionAt(-0.9f, 1.9f));
        [Test] public void Region_RearRightCorner_IsRearCorner() => Assert.AreEqual(RamRegion.RearCorner, RegionAt(0.9f, -1.9f));
        [Test] public void Region_InsideTheBandOnTheFrontFace_IsFrontCorner() => Assert.AreEqual(RamRegion.FrontCorner, RegionAt(0.75f, 2f));
        [Test] public void Region_JustOutsideTheBandOnTheFrontFace_IsFront() => Assert.AreEqual(RamRegion.Front, RegionAt(0.65f, 2f));

        /// <summary>
        /// The T-bone-near-the-nose case: a side panel 0.5 m behind the bumper is
        /// the side, not the front. A volume-based front zone would get this wrong.
        /// </summary>
        [Test] public void Region_SidePanelNearTheNose_IsSide() => Assert.AreEqual(RamRegion.Side, RegionAt(1f, 1.5f));

        [Test] public void Region_PenetratingContactPastTheFrontFace_IsStillFront() => Assert.AreEqual(RamRegion.Front, RegionAt(0f, 2.05f));

        [Test]
        public void IsAttackRegion_OnlyFrontAndFrontCorner()
        {
            Assert.IsTrue(RamRules.IsAttackRegion(RamRegion.Front));
            Assert.IsTrue(RamRules.IsAttackRegion(RamRegion.FrontCorner));
            Assert.IsFalse(RamRules.IsAttackRegion(RamRegion.Side));
            Assert.IsFalse(RamRules.IsAttackRegion(RamRegion.RearCorner));
            Assert.IsFalse(RamRules.IsAttackRegion(RamRegion.Rear));
        }

        // --- Speed and heading -------------------------------------------------

        [Test]
        public void ForwardSpeed_IsTheComponentAlongTheNoseAndNeverNegative()
        {
            Assert.AreEqual(10f, RamRules.ForwardSpeed(new Vector3(0f, 0f, 10f), Vector3.forward), 1e-5f);
            Assert.AreEqual(4f, RamRules.ForwardSpeed(new Vector3(3f, 0f, 4f), Vector3.forward), 1e-5f);
            Assert.AreEqual(0f, RamRules.ForwardSpeed(new Vector3(0f, 0f, -5f), Vector3.forward), 1e-5f);
        }

        [Test]
        public void FlatForward_DropsPitchAndNormalises()
        {
            Vector3 flat = RamRules.FlatForward(new Vector3(0f, 0.5f, 2f));
            Assert.AreEqual(0f, flat.y);
            Assert.AreEqual(1f, flat.magnitude, 1e-5f);
        }

        // --- Classification ----------------------------------------------------

        [Test]
        public void Classify_SideIsAlwaysFlank()
        {
            Assert.AreEqual(RamType.Flank, RamRules.Classify(RamRegion.Side, Vector3.back, Vector3.forward, HeadOn));
            Assert.AreEqual(RamType.Flank, RamRules.Classify(RamRegion.Side, Vector3.forward, Vector3.forward, HeadOn));
        }

        [Test]
        public void Classify_FrontFaceNoseToNose_IsHeadOn()
        {
            Assert.AreEqual(RamType.HeadOn, RamRules.Classify(RamRegion.Front, Vector3.back, Vector3.forward, HeadOn));
        }

        [Test]
        public void Classify_FrontAt44Degrees_IsHeadOn_At46_IsFlank()
        {
            Assert.AreEqual(RamType.HeadOn, RamRules.Classify(RamRegion.FrontCorner, Yawed(Vector3.back, 44f), Vector3.forward, HeadOn));
            Assert.AreEqual(RamType.Flank, RamRules.Classify(RamRegion.FrontCorner, Yawed(Vector3.back, 46f), Vector3.forward, HeadOn));
        }

        [Test]
        public void Classify_GlancingHitOnTheFrontFace_IsFlank()
        {
            Assert.AreEqual(RamType.Flank, RamRules.Classify(RamRegion.Front, Yawed(Vector3.back, 60f), Vector3.forward, HeadOn));
        }

        [Test]
        public void Classify_RearAt44Degrees_IsRear_At46_IsFlank()
        {
            Assert.AreEqual(RamType.Rear, RamRules.Classify(RamRegion.Rear, Yawed(Vector3.forward, -44f), Vector3.forward, HeadOn));
            Assert.AreEqual(RamType.Flank, RamRules.Classify(RamRegion.RearCorner, Yawed(Vector3.forward, -46f), Vector3.forward, HeadOn));
        }

        // --- Resolve -----------------------------------------------------------

        [Test]
        public void Resolve_NoFrontContact_IsNone()
        {
            var outcome = RamRules.Resolve(P(RamRegion.Side, Vector3.forward, 10f), P(RamRegion.Side, Vector3.right, 10f), MinSpeed, HeadOn);
            Assert.AreEqual(RamType.None, outcome.type);
            Assert.AreEqual(-1, outcome.attacker);
        }

        [Test]
        public void Resolve_BelowMinimumSpeed_IsNone()
        {
            var outcome = RamRules.Resolve(P(RamRegion.Front, Vector3.forward, 2.9f), P(RamRegion.Side, Vector3.right, 0f), MinSpeed, HeadOn);
            Assert.AreEqual(RamType.None, outcome.type);
        }

        [Test]
        public void Resolve_AtMinimumSpeed_Rams()
        {
            var outcome = RamRules.Resolve(P(RamRegion.Front, Vector3.forward, 3f), P(RamRegion.Side, Vector3.right, 0f), MinSpeed, HeadOn);
            Assert.AreEqual(RamType.Flank, outcome.type);
        }

        [Test]
        public void Resolve_CarThatCannotAttack_IsNone()
        {
            var outcome = RamRules.Resolve(P(RamRegion.Front, Vector3.forward, 10f, canAttack: false), P(RamRegion.Side, Vector3.right, 0f), MinSpeed, HeadOn);
            Assert.AreEqual(RamType.None, outcome.type);
        }

        [Test]
        public void Resolve_FrontIntoSide_IsFlankByTheFirstCar()
        {
            var outcome = RamRules.Resolve(P(RamRegion.Front, Vector3.forward, 10f), P(RamRegion.Side, Vector3.right, 0f), MinSpeed, HeadOn);
            Assert.AreEqual(RamType.Flank, outcome.type);
            Assert.AreEqual(0, outcome.attacker);
        }

        [Test]
        public void Resolve_SecondCarIntoFirstCarsRear_IsRearBySecondCar()
        {
            var outcome = RamRules.Resolve(P(RamRegion.Rear, Vector3.forward, 0f), P(RamRegion.Front, Vector3.forward, 10f), MinSpeed, HeadOn);
            Assert.AreEqual(RamType.Rear, outcome.type);
            Assert.AreEqual(1, outcome.attacker);
        }

        [Test]
        public void Resolve_IntoAParkedCarsNose_IsHeadOn()
        {
            var outcome = RamRules.Resolve(P(RamRegion.Front, Vector3.forward, 10f), P(RamRegion.Front, Vector3.back, 0f), MinSpeed, HeadOn);
            Assert.AreEqual(RamType.HeadOn, outcome.type);
            Assert.AreEqual(-1, outcome.attacker);
        }

        [Test]
        public void Resolve_BothNosesAtRightAngles_FasterCarAttacks()
        {
            var outcome = RamRules.Resolve(P(RamRegion.FrontCorner, Vector3.forward, 8f), P(RamRegion.FrontCorner, Vector3.right, 12f), MinSpeed, HeadOn);
            Assert.AreEqual(RamType.Flank, outcome.type);
            Assert.AreEqual(1, outcome.attacker);
        }

        [Test]
        public void Resolve_BothNosesAtRightAngles_ExactTie_IsHeadOn()
        {
            var outcome = RamRules.Resolve(P(RamRegion.FrontCorner, Vector3.forward, 8f), P(RamRegion.FrontCorner, Vector3.right, 8f), MinSpeed, HeadOn);
            Assert.AreEqual(RamType.HeadOn, outcome.type);
        }

        // --- Shove ------------------------------------------------------------

        [Test]
        public void ScaleFor_PicksTheScaleForEachType()
        {
            Assert.AreEqual(0.2f, RamRules.ScaleFor(RamType.HeadOn, 0.2f, 1.5f, 1.2f));
            Assert.AreEqual(1.5f, RamRules.ScaleFor(RamType.Flank, 0.2f, 1.5f, 1.2f));
            Assert.AreEqual(1.2f, RamRules.ScaleFor(RamType.Rear, 0.2f, 1.5f, 1.2f));
            Assert.AreEqual(0f, RamRules.ScaleFor(RamType.None, 0.2f, 1.5f, 1.2f));
        }

        [Test]
        public void DamageFor_OnlyFlankAndRearDealDamage()
        {
            Assert.AreEqual(30f, RamRules.DamageFor(RamType.Flank, 30f, 20f));
            Assert.AreEqual(20f, RamRules.DamageFor(RamType.Rear, 30f, 20f));
            Assert.AreEqual(0f, RamRules.DamageFor(RamType.HeadOn, 30f, 20f), "head-ons never deal damage");
            Assert.AreEqual(0f, RamRules.DamageFor(RamType.None, 30f, 20f));
        }

        [Test]
        public void ShoveDelta_IsStrengthOverResistanceTimesSpeedTimesScale_AlongHeading()
        {
            Vector3 shove = RamRules.ShoveDelta(Vector3.forward, strength: 2f, resistance: 1f, speed: 10f, scale: 1.5f);
            Assert.AreEqual(0f, shove.x, 1e-4f);
            Assert.AreEqual(0f, shove.y, 1e-4f);
            Assert.AreEqual(30f, shove.z, 1e-4f);
        }

        [Test]
        public void ShoveDelta_HigherResistanceShrinksItProportionally()
        {
            Vector3 shove = RamRules.ShoveDelta(Vector3.right, strength: 1f, resistance: 2f, speed: 10f, scale: 1f);
            Assert.AreEqual(5f, shove.x, 1e-4f);
        }

        [Test]
        public void ShoveDelta_IsNeverVertical()
        {
            Vector3 shove = RamRules.ShoveDelta(new Vector3(0f, 0.3f, 1f), 1f, 1f, 10f, 1f);
            Assert.AreEqual(0f, shove.y);
        }

        [Test]
        public void ShoveDelta_ZeroResistanceIsClampedInsteadOfDividingByZero()
        {
            Vector3 shove = RamRules.ShoveDelta(Vector3.forward, 1f, 0f, 1f, 1f);
            Assert.AreEqual(1f / RamRules.MinResistance, shove.z, 1e-2f);
        }

        // --- Spin -------------------------------------------------------------

        [Test]
        public void SpinDelta_ThroughTheCentre_IsZero()
        {
            Assert.AreEqual(0f, RamRules.SpinDelta(Vector3.zero, Vector3.zero, new Vector3(5f, 0f, 0f), W, L, 1f), 1e-5f);
        }

        [Test]
        public void SpinDelta_RearRamStraightAlongTheLength_IsZero()
        {
            Assert.AreEqual(0f, RamRules.SpinDelta(new Vector3(0f, 0f, -2f), Vector3.zero, new Vector3(0f, 0f, 10f), W, L, 1f), 1e-5f);
        }

        /// <summary>
        /// Tail pushed toward +X: the nose swings toward −X, which is negative yaw
        /// in Unity. k² = (2² + 4²) / 12 = 5/3, so (−2 × 1) / (5/3) = −1.2 rad/s.
        /// </summary>
        [Test]
        public void SpinDelta_TailPushedRight_SpinsNoseLeft()
        {
            Assert.AreEqual(-1.2f, RamRules.SpinDelta(new Vector3(0f, 0f, -2f), Vector3.zero, new Vector3(1f, 0f, 0f), W, L, 1f), 1e-4f);
        }

        [Test]
        public void SpinDelta_ScalesLinearlyWithSpinScale_AndIgnoresHeight()
        {
            float spin = RamRules.SpinDelta(new Vector3(0f, 5f, -2f), Vector3.zero, new Vector3(1f, 0f, 0f), W, L, 2f);
            Assert.AreEqual(-2.4f, spin, 1e-4f);
        }

        [Test]
        public void DecaySpin_IsTimestepIndependent()
        {
            float oneStep = RamRules.DecaySpin(3f, 2f, 0.02f);
            float twoSteps = RamRules.DecaySpin(RamRules.DecaySpin(3f, 2f, 0.01f), 2f, 0.01f);
            Assert.AreEqual(oneStep, twoSteps, 1e-5f);
            Assert.Less(oneStep, 3f);
        }
    }
}
