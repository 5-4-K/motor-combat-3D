namespace MotorCombat.Core
{
    /// <summary>Raised when an effect is applied or restarted.</summary>
    public struct EffectReport
    {
        public CarController source;
        public CarController target;
        public string sourceTag;
        public EffectType type;
        public EffectOutcome outcome;
        public float magnitude;
        public float duration;
    }
}
