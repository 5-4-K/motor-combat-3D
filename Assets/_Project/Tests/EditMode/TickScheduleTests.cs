using NUnit.Framework;
using MotorCombat.Core;

namespace MotorCombat.Tests
{
    public class TickScheduleTests
    {
        static readonly object Zone = new object();
        static readonly object OtherZone = new object();
        static readonly object Car = new object();
        static readonly object OtherCar = new object();

        [Test]
        public void FirstTick_IsImmediate()
        {
            Assert.IsTrue(new TickSchedule().TryTick(Zone, Car, 3.7f, 0.5f));
        }

        [Test]
        public void NextTick_WaitsForTheInterval()
        {
            var schedule = new TickSchedule();
            Assert.IsTrue(schedule.TryTick(Zone, Car, 0f, 0.5f));
            Assert.IsFalse(schedule.TryTick(Zone, Car, 0.3f, 0.5f));
            Assert.IsTrue(schedule.TryTick(Zone, Car, 0.5f, 0.5f));
        }

        /// <summary>Leaving and re-entering must not tick faster than the interval.</summary>
        [Test]
        public void Schedule_SurvivesContactBreaks()
        {
            var schedule = new TickSchedule();
            schedule.TryTick(Zone, Car, 0f, 0.5f);
            Assert.IsFalse(schedule.TryTick(Zone, Car, 0.2f, 0.5f), "re-entry at 0.2 s");
        }

        [Test]
        public void Targets_AreIndependent()
        {
            var schedule = new TickSchedule();
            schedule.TryTick(Zone, Car, 0f, 0.5f);
            Assert.IsTrue(schedule.TryTick(Zone, OtherCar, 0.1f, 0.5f));
        }

        [Test]
        public void Sources_AreIndependent()
        {
            var schedule = new TickSchedule();
            schedule.TryTick(Zone, Car, 0f, 0.5f);
            Assert.IsTrue(schedule.TryTick(OtherZone, Car, 0.1f, 0.5f));
        }

        [Test]
        public void Forget_ClearsATarget()
        {
            var schedule = new TickSchedule();
            schedule.TryTick(Zone, Car, 0f, 0.5f);
            schedule.Forget(Car);
            Assert.IsTrue(schedule.TryTick(Zone, Car, 0.1f, 0.5f));
        }

        /// <summary>25 physics steps of 0.02 s sum to slightly under 0.5 in float.</summary>
        [Test]
        public void AccumulatedFixedSteps_StillTickOnTime()
        {
            var schedule = new TickSchedule();
            schedule.TryTick(Zone, Car, 0f, 0.5f);

            float now = 0f;
            for (int i = 0; i < 25; i++) now += 0.02f;

            Assert.IsTrue(schedule.TryTick(Zone, Car, now, 0.5f));
        }

        /// <summary>
        /// Two distinct targets with equal content (never a literal, so CLR
        /// interning can't hide a bug) must tick independently — the key
        /// compares by reference identity, not by value.
        /// </summary>
        [Test]
        public void Keys_AreComparedByIdentity()
        {
            string targetA = new string(new[] { 't', 'a', 'r', 'g', 'e', 't' });
            string targetB = new string(new[] { 't', 'a', 'r', 'g', 'e', 't' });
            Assert.AreNotSame(targetA, targetB);
            Assert.AreEqual(targetA, targetB);

            var schedule = new TickSchedule();
            Assert.IsTrue(schedule.TryTick(Zone, targetA, 0f, 0.5f));

            // targetB is a different instance, so it has never ticked.
            Assert.IsTrue(schedule.TryTick(Zone, targetB, 0.1f, 0.5f));

            // targetA already ticked at 0, so it is not due again yet.
            Assert.IsFalse(schedule.TryTick(Zone, targetA, 0.2f, 0.5f));
        }
    }
}
