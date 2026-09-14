using NUnit.Framework;
using MotorCombat.Core;
using MotorCombat.Effects;

namespace MotorCombat.Tests
{
    public class EffectRulesTests
    {
        static EffectRequest Request(EffectType type, float magnitude = 30f, float duration = 3f)
        {
            return new EffectRequest { type = type, magnitude = magnitude, duration = duration, sourceTag = "test" };
        }

        static void AssertInvalid(EffectRequest request, string label)
        {
            Assert.AreEqual(EffectOutcome.Invalid, EffectRules.Decide(request, true, true, false, false), label);
        }

        [Test]
        public void Decide_NotTargetableComesFirst()
        {
            Assert.AreEqual(EffectOutcome.NotTargetable,
                EffectRules.Decide(Request(EffectType.Stunned), targetable: false, hostile: false, alreadyActive: true, stacks: true));
        }

        [Test]
        public void Decide_NotHostile_UnlessAllowNonEnemy()
        {
            var request = Request(EffectType.Fortified);
            Assert.AreEqual(EffectOutcome.NotHostile, EffectRules.Decide(request, true, false, false, false));

            request.allowNonEnemy = true;
            Assert.AreEqual(EffectOutcome.Applied, EffectRules.Decide(request, true, false, false, false));
        }

        [Test]
        public void Decide_RejectsInvalidRequests()
        {
            AssertInvalid(Request(EffectType.Corroded, magnitude: -1f), "negative magnitude");
            AssertInvalid(Request(EffectType.Corroded, magnitude: float.NaN), "NaN magnitude");
            AssertInvalid(Request(EffectType.Overheated, magnitude: float.PositiveInfinity), "infinite magnitude");
            AssertInvalid(Request(EffectType.Stunned, duration: 0f), "zero duration");
            AssertInvalid(Request(EffectType.Stunned, duration: float.NaN), "NaN duration");
            AssertInvalid(Request((EffectType)42), "unknown type");
        }

        [Test]
        public void Decide_IgnoresMagnitudeForEffectsThatDoNotUseIt()
        {
            Assert.AreEqual(EffectOutcome.Applied, EffectRules.Decide(Request(EffectType.Stunned, magnitude: -5f), true, true, false, false));
        }

        [Test]
        public void Decide_AllowsAnUntimedDuration()
        {
            Assert.AreEqual(EffectOutcome.Applied,
                EffectRules.Decide(Request(EffectType.Fortified, duration: float.PositiveInfinity), true, true, false, false));
        }

        [Test]
        public void Decide_OverhauledNeedsNoDuration_AndIsNeverAlreadyActive()
        {
            Assert.AreEqual(EffectOutcome.Applied,
                EffectRules.Decide(Request(EffectType.Overhauled, magnitude: 0f, duration: 0f), true, true, alreadyActive: true, stacks: false));
        }

        [Test]
        public void Decide_AlreadyActive_RestartsOnlyWhenItStacks()
        {
            var request = Request(EffectType.Reeling);
            Assert.AreEqual(EffectOutcome.Restarted, EffectRules.Decide(request, true, true, alreadyActive: true, stacks: true));
            Assert.AreEqual(EffectOutcome.AlreadyActive, EffectRules.Decide(request, true, true, alreadyActive: true, stacks: false));
        }

        [Test]
        public void Decide_New_IsApplied()
        {
            Assert.AreEqual(EffectOutcome.Applied, EffectRules.Decide(Request(EffectType.Suppressed), true, true, false, true));
        }

        [Test]
        public void AttackSnapshot_PrefersTheRequest_ThenTheSource_ThenNeutral()
        {
            Assert.AreEqual(200f, EffectRules.AttackSnapshot(200f, true, 150f));
            Assert.AreEqual(150f, EffectRules.AttackSnapshot(null, true, 150f));
            Assert.AreEqual(100f, EffectRules.AttackSnapshot(null, false, 150f));
        }

        [Test]
        public void StatChange_MapsTheFourStatEffects()
        {
            AssertStat(EffectType.Corroded, CarStat.Defense, -30f);
            AssertStat(EffectType.Fortified, CarStat.Defense, 30f);
            AssertStat(EffectType.Spiked, CarStat.TopSpeed, -30f);
            AssertStat(EffectType.Exhausted, CarStat.Attack, -30f);
            Assert.IsFalse(EffectRules.StatChange(EffectType.Stunned, 30f, out _, out _));
            Assert.IsFalse(EffectRules.StatChange(EffectType.Overheated, 30f, out _, out _));
        }

        static void AssertStat(EffectType type, CarStat expectedStat, float expectedPercent)
        {
            Assert.IsTrue(EffectRules.StatChange(type, 30f, out CarStat stat, out float percent), type.ToString());
            Assert.AreEqual(expectedStat, stat, type.ToString());
            Assert.AreEqual(expectedPercent, percent, 1e-5f, type.ToString());
        }

        [Test]
        public void BlockMask_MatchesTheSpec()
        {
            Assert.AreEqual(CarAbility.Throttle | CarAbility.Steer | CarAbility.Fire | CarAbility.Ram, EffectRules.BlockMask(EffectType.Stunned));
            Assert.AreEqual(CarAbility.Fire, EffectRules.BlockMask(EffectType.Suppressed));
            Assert.AreEqual(CarAbility.Throttle | CarAbility.Steer | CarAbility.YawHold | CarAbility.Grip | CarAbility.Ram, EffectRules.BlockMask(EffectType.Reeling));
            Assert.AreEqual(CarAbility.None, EffectRules.BlockMask(EffectType.Corroded));
            Assert.AreEqual(CarAbility.None, EffectRules.BlockMask(EffectType.Armored));
        }

        [Test]
        public void DecaySpin_IsTimestepIndependent()
        {
            float oneStep = EffectRules.DecaySpin(3f, 2f, 0.02f);
            float twoSteps = EffectRules.DecaySpin(EffectRules.DecaySpin(3f, 2f, 0.01f), 2f, 0.01f);
            Assert.AreEqual(oneStep, twoSteps, 1e-5f);
            Assert.Less(oneStep, 3f);
        }
    }
}
