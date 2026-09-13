using NUnit.Framework;
using MotorCombat.Core;

namespace MotorCombat.Tests
{
    public class CarStatusTests
    {
        [Test]
        public void NewStatus_CanDriveAndAttack()
        {
            var status = new CarStatus();
            Assert.IsTrue(status.CanDrive);
            Assert.IsTrue(status.CanAttack);
            Assert.IsFalse(status.IsLocked);
            Assert.IsFalse(status.IsReeling);
        }

        [Test]
        public void Lock_BlocksDrivingAndAttackingUntilItExpires()
        {
            var status = new CarStatus();
            status.Lock(0.5f);

            status.Advance(0.3f);
            Assert.IsTrue(status.IsLocked);
            Assert.IsFalse(status.CanDrive);
            Assert.IsFalse(status.CanAttack);

            status.Advance(0.3f);
            Assert.IsFalse(status.IsLocked);
            Assert.IsTrue(status.CanDrive);
        }

        [Test]
        public void Reel_BlocksDrivingAndAttackingUntilItExpires()
        {
            var status = new CarStatus();
            status.Reel(1f);

            status.Advance(0.9f);
            Assert.IsTrue(status.IsReeling);
            Assert.IsFalse(status.CanDrive);
            Assert.IsFalse(status.CanAttack);

            status.Advance(0.2f);
            Assert.IsFalse(status.IsReeling);
            Assert.IsTrue(status.CanAttack);
        }

        [Test]
        public void Reel_RestartsWhenAppliedAgain()
        {
            var status = new CarStatus();
            status.Reel(1f);
            status.Advance(0.8f);
            status.Reel(1f);
            status.Advance(0.5f);

            Assert.IsTrue(status.IsReeling, "a second ram restarts the reel from full");
            Assert.AreEqual(0.5f, status.ReelRemaining, 1e-5f);
        }

        [Test]
        public void Lock_NeverShortensALongerLock()
        {
            var status = new CarStatus();
            status.Lock(1f);
            status.Lock(0.2f);
            status.Advance(0.5f);

            Assert.IsTrue(status.IsLocked);
            Assert.AreEqual(0.5f, status.LockRemaining, 1e-5f);
        }

        [Test]
        public void Advance_ClampsTimersAtZero()
        {
            var status = new CarStatus();
            status.Lock(0.1f);
            status.Reel(0.1f);
            status.Advance(5f);

            Assert.AreEqual(0f, status.LockRemaining);
            Assert.AreEqual(0f, status.ReelRemaining);
        }

        [Test]
        public void LockAndReel_AreIndependent()
        {
            var status = new CarStatus();
            status.Lock(0.5f);
            status.Reel(1f);
            status.Advance(0.6f);

            Assert.IsFalse(status.IsLocked);
            Assert.IsTrue(status.IsReeling);
            Assert.IsFalse(status.CanDrive);
        }
    }
}
