namespace MotorCombat.Core
{
    /// <summary>What an applied hit did. Raised to the HUD now and to game modes (kill credit) later.</summary>
    public struct DamageReport
    {
        public CarController source;
        public CarController target;
        public string sourceTag;
        public DamageKind kind;

        /// <summary>Hit points actually removed.</summary>
        public float dealt;

        public float healthAfter;
    }
}
