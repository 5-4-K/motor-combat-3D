using UnityEngine;

namespace MotorCombat.Ramming
{
    /// <summary>Which part of a car's collider box a contact landed on.</summary>
    public enum RamRegion { Front, FrontCorner, Side, RearCorner, Rear }

    public enum RamType { None, HeadOn, Flank, Rear }

    /// <summary>One car's side of a contact, reduced to plain values.</summary>
    public struct RamParticipant
    {
        public RamRegion region;

        /// <summary>Horizontal unit heading.</summary>
        public Vector3 flatForward;

        /// <summary>Pre-contact speed along the heading, never negative.</summary>
        public float forwardSpeed;

        /// <summary>False while locked or reeling.</summary>
        public bool canAttack;
    }

    public struct RamOutcome
    {
        public RamType type;

        /// <summary>0 = first participant, 1 = second, -1 = nobody (None) or both (HeadOn).</summary>
        public int attacker;

        public static RamOutcome None => new RamOutcome { type = RamType.None, attacker = -1 };
    }

    /// <summary>
    /// Every ram decision as a pure function. No Rigidbody, no scene — the
    /// adapter in RammingModule gathers values, calls these, and writes results.
    /// </summary>
    public static class RamRules
    {
        /// <summary>Floor for defense so a misconfigured zero cannot divide by zero.</summary>
        public const float MinDefense = 0.01f;

        public static Vector3 FlatForward(Vector3 forward)
        {
            forward.y = 0f;
            return forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
        }

        /// <summary>
        /// Region by which FACE of the box the local contact point is nearest,
        /// with a corner band where a front or rear face meets a side.
        ///
        /// Faces, not volumes: a front zone deep enough to catch corner hits would
        /// also swallow the side panel behind the bumper and call a T-bone a head-on.
        /// </summary>
        public static RamRegion Region(Vector3 localPoint, float width, float length, float cornerBand)
        {
            float halfWidth = width * 0.5f;
            float halfLength = length * 0.5f;

            float toFront = halfLength - localPoint.z;
            float toRear = halfLength + localPoint.z;
            float toSide = halfWidth - Mathf.Abs(localPoint.x);

            if (toSide <= cornerBand && toFront <= cornerBand) return RamRegion.FrontCorner;
            if (toSide <= cornerBand && toRear <= cornerBand) return RamRegion.RearCorner;

            if (toFront <= toRear && toFront <= toSide) return RamRegion.Front;
            if (toRear <= toSide) return RamRegion.Rear;
            return RamRegion.Side;
        }

        public static bool IsAttackRegion(RamRegion region)
        {
            return region == RamRegion.Front || region == RamRegion.FrontCorner;
        }

        public static float ForwardSpeed(Vector3 velocity, Vector3 flatForward)
        {
            return Mathf.Max(0f, Vector3.Dot(velocity, flatForward));
        }

        /// <summary>
        /// Type of ram from the region the victim was struck in and the angle
        /// between headings. A side hit is always a flank; a front or rear hit is
        /// head-on or rear only when the cars line up within <paramref name="headOnAngle"/>.
        /// </summary>
        public static RamType Classify(RamRegion victimRegion, Vector3 attackerForward, Vector3 victimForward, float headOnAngle)
        {
            switch (victimRegion)
            {
                case RamRegion.Front:
                case RamRegion.FrontCorner:
                    return Vector3.Angle(attackerForward, -victimForward) <= headOnAngle ? RamType.HeadOn : RamType.Flank;

                case RamRegion.Rear:
                case RamRegion.RearCorner:
                    return Vector3.Angle(attackerForward, victimForward) <= headOnAngle ? RamType.Rear : RamType.Flank;

                default:
                    return RamType.Flank;
            }
        }

        public static bool Qualifies(in RamParticipant participant, float minRamSpeed)
        {
            return participant.canAttack
                   && IsAttackRegion(participant.region)
                   && participant.forwardSpeed >= minRamSpeed;
        }

        /// <summary>
        /// Resolves one contact between two cars. Each qualifying car is evaluated
        /// as attacker against the other. Any head-on evaluation makes it a head-on
        /// for both — including a parked car. Two non-head-on attackers: the faster
        /// one wins; an exact tie is treated as a head-on.
        /// </summary>
        public static RamOutcome Resolve(in RamParticipant a, in RamParticipant b, float minRamSpeed, float headOnAngle)
        {
            bool aQualifies = Qualifies(a, minRamSpeed);
            bool bQualifies = Qualifies(b, minRamSpeed);

            if (!aQualifies && !bQualifies) return RamOutcome.None;

            RamType aType = aQualifies ? Classify(b.region, a.flatForward, b.flatForward, headOnAngle) : RamType.None;
            RamType bType = bQualifies ? Classify(a.region, b.flatForward, a.flatForward, headOnAngle) : RamType.None;

            if (aType == RamType.HeadOn || bType == RamType.HeadOn)
            {
                return new RamOutcome { type = RamType.HeadOn, attacker = -1 };
            }

            if (aQualifies && bQualifies)
            {
                if (a.forwardSpeed > b.forwardSpeed) return new RamOutcome { type = aType, attacker = 0 };
                if (b.forwardSpeed > a.forwardSpeed) return new RamOutcome { type = bType, attacker = 1 };
                return new RamOutcome { type = RamType.HeadOn, attacker = -1 };
            }

            return aQualifies
                ? new RamOutcome { type = aType, attacker = 0 }
                : new RamOutcome { type = bType, attacker = 1 };
        }

        public static float ScaleFor(RamType type, float headOnScale, float flankScale, float rearScale)
        {
            switch (type)
            {
                case RamType.HeadOn: return headOnScale;
                case RamType.Flank: return flankScale;
                case RamType.Rear: return rearScale;
                default: return 0f;
            }
        }

        /// <summary>
        /// Velocity change for the victim. Mass is deliberately ignored: attack and
        /// defense are the only balance levers. With equal stats and scale 1 the
        /// victim leaves at the attacker's speed. Never vertical.
        /// </summary>
        /// <param name="attackerFlatForward">Horizontal unit heading of the attacker.</param>
        public static Vector3 ShoveDelta(Vector3 attackerFlatForward, float attack, float defense, float speed, float scale)
        {
            Vector3 direction = attackerFlatForward;
            direction.y = 0f;
            return direction * (attack / Mathf.Max(MinDefense, defense) * speed * scale);
        }

        /// <summary>
        /// Yaw-rate change (rad/s, about world up) from applying the shove at the
        /// contact point. With <paramref name="spinScale"/> 1 this equals what a
        /// real impulse m·Δv at that offset gives a solid box of this footprint:
        /// Δω = (r × Δv)·up / k², with k² = (width² + length²) / 12.
        /// </summary>
        public static float SpinDelta(Vector3 contactPoint, Vector3 victimCentre, Vector3 shoveDelta, float width, float length, float spinScale)
        {
            Vector3 offset = contactPoint - victimCentre;
            offset.y = 0f;

            float radiusOfGyrationSquared = (width * width + length * length) / 12f;
            if (radiusOfGyrationSquared <= 0f) return 0f;

            return spinScale * Vector3.Cross(offset, shoveDelta).y / radiusOfGyrationSquared;
        }

        /// <summary>Exponential spin decay. <paramref name="rate"/> is in 1/s.</summary>
        public static float DecaySpin(float yawRate, float rate, float dt)
        {
            return yawRate * Mathf.Exp(-rate * dt);
        }
    }
}
