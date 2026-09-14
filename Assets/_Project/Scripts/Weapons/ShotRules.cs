using UnityEngine;

namespace MotorCombat.Weapons
{
    /// <summary>One thing a shot's sweep touched this step.</summary>
    public struct ShotCandidate
    {
        public float distance;

        /// <summary>The arena (floor or wall).</summary>
        public bool wall;

        public bool ownCar;

        /// <summary>Has Targetable and is not destroyed — false for a wreck.</summary>
        public bool targetable;

        public bool enemy;
    }

    public static class ShotRules
    {
        public const float RangeTolerance = 1e-4f;
        const float SameDistance = 1e-5f;

        /// <summary>A wall, or a live enemy that isn't the shot's own car. Everything else is passed through.</summary>
        public static bool IsStopper(in ShotCandidate candidate)
        {
            return candidate.wall || (!candidate.ownCar && candidate.targetable && candidate.enemy);
        }

        /// <summary>Index of the nearest stopper among the first <paramref name="count"/> entries, or −1. At equal distance a car beats a wall.</summary>
        public static int Resolve(ShotCandidate[] candidates, int count)
        {
            int best = -1;
            for (int i = 0; i < count; i++)
            {
                if (!IsStopper(candidates[i])) continue;

                if (best < 0)
                {
                    best = i;
                    continue;
                }

                float delta = candidates[i].distance - candidates[best].distance;
                bool nearer = delta < -SameDistance;
                bool tieCarOverWall = Mathf.Abs(delta) <= SameDistance && !candidates[i].wall && candidates[best].wall;
                if (nearer || tieCarOverWall) best = i;
            }

            return best;
        }

        public static float StepDistance(float speed, float dt, float remaining)
        {
            return Mathf.Max(0f, Mathf.Min(speed * dt, remaining));
        }
    }
}
