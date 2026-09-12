using NUnit.Framework;
using UnityEngine;
using MotorCombat.Driving;

namespace MotorCombat.Tests
{
    public class DrivePhysicsTests
    {
        static readonly Vector3 Forward = Vector3.forward;

        // --- Grip -------------------------------------------------------------

        [Test]
        public void Grip_LeavesLongitudinalVelocityUntouched()
        {
            var velocity = new Vector3(3f, 0f, 10f);   // 3 sideways, 10 forward
            var result = DrivePhysics.ApplyGrip(velocity, Forward, 6f, 0.02f);
            Assert.AreEqual(10f, Vector3.Dot(result, Forward), 1e-4f);
        }

        [Test]
        public void Grip_BleedsLateralVelocity()
        {
            var velocity = new Vector3(3f, 0f, 10f);
            var result = DrivePhysics.ApplyGrip(velocity, Forward, 6f, 0.02f);
            float lateral = Vector3.Dot(result, Vector3.right);
            Assert.Less(lateral, 3f);
            Assert.Greater(lateral, 0f);
        }

        /// <summary>
        /// The property that matters most: handling must not change when the
        /// physics timestep changes. One 0.02s step must equal two 0.01s steps.
        /// </summary>
        [Test]
        public void Grip_IsTimestepIndependent()
        {
            var velocity = new Vector3(5f, 0f, 12f);

            var oneBigStep = DrivePhysics.ApplyGrip(velocity, Forward, 6f, 0.02f);

            var twoSmallSteps = DrivePhysics.ApplyGrip(velocity, Forward, 6f, 0.01f);
            twoSmallSteps = DrivePhysics.ApplyGrip(twoSmallSteps, Forward, 6f, 0.01f);

            Assert.AreEqual(Vector3.Dot(oneBigStep, Vector3.right),
                            Vector3.Dot(twoSmallSteps, Vector3.right), 1e-5f);
        }

        [Test]
        public void Grip_PreservesVerticalVelocity()
        {
            // Gravity gives a grounded car a nonzero y velocity every frame; grip
            // must bleed only the sideways component and leave falling untouched.
            var velocity = new Vector3(3f, -4f, 10f);
            var result = DrivePhysics.ApplyGrip(velocity, Forward, 6f, 0.02f);

            Assert.AreEqual(-4f, result.y, 1e-4f, "vertical velocity must pass through untouched");
            Assert.AreEqual(10f, Vector3.Dot(result, Forward), 1e-4f, "longitudinal must be untouched");
            Assert.Less(Vector3.Dot(result, Vector3.right), 3f, "lateral must decay");
        }

        // --- Drag -------------------------------------------------------------

        [Test]
        public void Drag_IsTimestepIndependent()
        {
            var velocity = new Vector3(0f, 0f, 20f);

            var oneBigStep = DrivePhysics.ApplyDrag(velocity, 1f, 0.02f);

            var twoSmallSteps = DrivePhysics.ApplyDrag(velocity, 1f, 0.01f);
            twoSmallSteps = DrivePhysics.ApplyDrag(twoSmallSteps, 1f, 0.01f);

            Assert.AreEqual(oneBigStep.z, twoSmallSteps.z, 1e-5f);
        }

        [Test]
        public void TerminalSpeed_MatchesTheConfiguredFormula()
        {
            // 30000 N on a 1200 kg car with drag rate 1.0 /s  ->  25 m/s
            Assert.AreEqual(25f, DrivePhysics.TerminalSpeed(30000f, 1200f, 1f), 1e-4f);
        }

        [Test]
        public void IntegratingThrustAgainstDrag_ConvergesOnTerminalSpeed()
        {
            const float mass = 1200f, power = 30000f, drag = 1f, dt = 0.02f;
            var velocity = Vector3.zero;

            for (int i = 0; i < 1500; i++)   // 30 simulated seconds
            {
                var force = DrivePhysics.DriveForce(Forward, velocity, 1f, power, 40000f, 12000f, 0.5f);
                velocity += force / mass * dt;
                velocity = DrivePhysics.ApplyDrag(velocity, drag, dt);
            }

            // Discrete stepping settles a little under the continuous limit:
            // applying drag after the force gives v = a*dt*k / (1-k) with
            // k = exp(-drag*dt), which is ~24.75 rather than exactly 25.
            // The tolerance covers that without hiding a real regression.
            Assert.AreEqual(DrivePhysics.TerminalSpeed(power, mass, drag), velocity.z, 0.5f);
        }

        // --- Throttle, brake, reverse -----------------------------------------

        [Test]
        public void PositiveThrottle_PushesForward()
        {
            var force = DrivePhysics.DriveForce(Forward, Vector3.zero, 1f, 30000f, 40000f, 12000f, 0.5f);
            Assert.AreEqual(30000f, force.z, 1e-3f);
        }

        [Test]
        public void NegativeThrottle_WhileMovingForward_Brakes()
        {
            var velocity = new Vector3(0f, 0f, 10f);
            var force = DrivePhysics.DriveForce(Forward, velocity, -1f, 30000f, 40000f, 12000f, 0.5f);
            Assert.AreEqual(-40000f, force.z, 1e-3f, "should oppose motion with brakeForce");
        }

        [Test]
        public void NegativeThrottle_WhenNearlyStopped_Reverses()
        {
            var velocity = new Vector3(0f, 0f, 0.1f);   // below the 0.5 epsilon
            var force = DrivePhysics.DriveForce(Forward, velocity, -1f, 30000f, 40000f, 12000f, 0.5f);
            Assert.AreEqual(-12000f, force.z, 1e-3f, "should apply reversePower, not brakeForce");
        }

        [Test]
        public void NoThrottle_ProducesNoForce()
        {
            var force = DrivePhysics.DriveForce(Forward, new Vector3(0f, 0f, 10f), 0f, 30000f, 40000f, 12000f, 0.5f);
            Assert.AreEqual(Vector3.zero, force);
        }

        // --- Yaw --------------------------------------------------------------

        /// <summary>
        /// Turn-in-place works because yaw never consults speed MAGNITUDE. Full
        /// steer yields the full configured rate at a dead stop.
        /// </summary>
        [Test]
        public void YawRate_IsFullRateAtFullSteer_WhenStationary()
        {
            Assert.AreEqual(90f, DrivePhysics.YawRate(1f, 90f, 0f, true, 0.5f), 1e-4f);
            Assert.AreEqual(-90f, DrivePhysics.YawRate(-1f, 90f, 0f, true, 0.5f), 1e-4f);
            Assert.AreEqual(0f, DrivePhysics.YawRate(0f, 90f, 0f, true, 0.5f), 1e-4f);
        }

        [Test]
        public void YawRate_IsNotInvertedWhenDrivingForward()
        {
            Assert.AreEqual(90f, DrivePhysics.YawRate(1f, 90f, 20f, true, 0.5f), 1e-4f);
        }

        /// <summary>
        /// A real car's steering sense inverts in reverse: yaw goes as
        /// v/L * tan(delta), so a negative v flips it. Turn right while backing
        /// up and the rear swings right.
        /// </summary>
        [Test]
        public void YawRate_InvertsWhenTravellingBackwards()
        {
            Assert.AreEqual(-90f, DrivePhysics.YawRate(1f, 90f, -5f, true, 0.5f), 1e-4f);
            Assert.AreEqual(90f, DrivePhysics.YawRate(-1f, 90f, -5f, true, 0.5f), 1e-4f);
        }

        /// <summary>
        /// Below the reverse threshold the car is still essentially stationary,
        /// so it must keep the intuitive turn-in-place sense rather than flipping
        /// the moment it drifts a few centimetres backwards.
        /// </summary>
        [Test]
        public void YawRate_DoesNotInvertBelowTheReverseThreshold()
        {
            Assert.AreEqual(90f, DrivePhysics.YawRate(1f, 90f, -0.2f, true, 0.5f), 1e-4f);
        }

        [Test]
        public void YawRate_NeverInvertsWhenTheFlipIsDisabled()
        {
            Assert.AreEqual(90f, DrivePhysics.YawRate(1f, 90f, -20f, false, 0.5f), 1e-4f);
        }
    }
}
