namespace MotorCombat.Core
{
    /// <summary>
    /// The seam that keeps driving code ignorant of where input came from.
    /// Local keyboard today; a network or bot provider later, with no change
    /// to any module.
    /// </summary>
    public interface IInputProvider
    {
        /// <summary>
        /// Called exactly once per Update by <see cref="CarController"/>.
        /// Implementations that accumulate deltas may clear them here.
        /// </summary>
        CarInput Sample();
    }
}
