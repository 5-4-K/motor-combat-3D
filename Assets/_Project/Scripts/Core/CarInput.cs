namespace MotorCombat.Core
{
    /// <summary>
    /// One frame of intent for a car. Produced by an <see cref="IInputProvider"/>,
    /// consumed by every <see cref="ICarModule"/>.
    /// </summary>
    public struct CarInput
    {
        /// <summary>-1 (S, brake/reverse) .. +1 (W, accelerate).</summary>
        public float throttle;

        /// <summary>-1 (A, left) .. +1 (D, right).</summary>
        public float steer;

        /// <summary>
        /// Raw horizontal mouse movement in pixels since the last Update.
        /// This is a displacement, NOT a rate — never multiply it by deltaTime.
        /// </summary>
        public float aimDeltaX;

        public static CarInput None => new CarInput();
    }
}
