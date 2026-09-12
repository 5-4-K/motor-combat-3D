namespace MotorCombat.Core
{
    /// <summary>
    /// A swappable behaviour attached to a car. Modules never talk to each
    /// other; they read the input struct and the controller's public state.
    /// </summary>
    public interface ICarModule
    {
        /// <summary>Called from FixedUpdate. Use for anything touching the Rigidbody.</summary>
        void Tick(in CarInput input, float dt);

        /// <summary>Called from Update. Use for anything consuming per-frame deltas.</summary>
        void FrameTick(in CarInput input, float dt);
    }
}
