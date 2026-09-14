using NUnit.Framework;
using MotorCombat.Weapons;

namespace MotorCombat.Tests
{
    public class ShotRulesTests
    {
        static ShotCandidate Wall(float d) => new ShotCandidate { distance = d, wall = true };
        static ShotCandidate Enemy(float d) => new ShotCandidate { distance = d, targetable = true, enemy = true };

        [Test]
        public void Resolve_NothingHit_IsMinusOne()
        {
            Assert.AreEqual(-1, ShotRules.Resolve(new ShotCandidate[0], 0));
        }

        [Test]
        public void Resolve_PassesOwnCar_WrecksAndNonEnemies()
        {
            var candidates = new[]
            {
                new ShotCandidate { distance = 0f, ownCar = true, targetable = true, enemy = false },
                new ShotCandidate { distance = 1f, targetable = false, enemy = true },   // wreck
                new ShotCandidate { distance = 2f, targetable = true, enemy = false },   // teammate
            };
            Assert.AreEqual(-1, ShotRules.Resolve(candidates, candidates.Length));
        }

        [Test]
        public void Resolve_TheNearestStopperWins_InAnyOrder()
        {
            var candidates = new[] { Wall(9f), Enemy(4f), Enemy(6f) };
            Assert.AreEqual(1, ShotRules.Resolve(candidates, candidates.Length));
        }

        [Test]
        public void Resolve_AWallInFront_StopsBeforeAnEnemyBehindIt()
        {
            var candidates = new[] { Enemy(6f), Wall(3f) };
            Assert.AreEqual(1, ShotRules.Resolve(candidates, candidates.Length));
        }

        [Test]
        public void Resolve_EqualDistance_TheEnemyBeatsTheWall()
        {
            var candidates = new[] { Wall(5f), Enemy(5f) };
            Assert.AreEqual(1, ShotRules.Resolve(candidates, candidates.Length));
        }

        [Test]
        public void Resolve_OnlyReadsTheFirstCountEntries()
        {
            var candidates = new[] { new ShotCandidate { distance = 1f, ownCar = true }, Enemy(2f) };
            Assert.AreEqual(-1, ShotRules.Resolve(candidates, 1));
        }

        [Test]
        public void StepDistance_IsClippedToTheRemainingRange()
        {
            Assert.AreEqual(1f, ShotRules.StepDistance(50f, 0.02f, 10f), 1e-5f);
            Assert.AreEqual(0.3f, ShotRules.StepDistance(50f, 0.02f, 0.3f), 1e-5f);
            Assert.AreEqual(0f, ShotRules.StepDistance(50f, 0.02f, -1f), 1e-5f);
        }
    }
}
