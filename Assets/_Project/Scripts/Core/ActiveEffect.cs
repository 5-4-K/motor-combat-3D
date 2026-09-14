namespace MotorCombat.Core
{
    /// <summary>A snapshot of one active effect, for the HUD and anything else that only reads.</summary>
    public struct ActiveEffect
    {
        public EffectType type;
        public float remaining;
        public float duration;
        public float magnitude;
        public CarController source;
    }
}
