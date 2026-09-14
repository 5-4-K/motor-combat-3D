using MotorCombat.Core;

namespace MotorCombat.Effects
{
    /// <summary>
    /// Switches abilities off for as long as the effect lasts. The block is
    /// untimed; EffectSet decides when it ends. Suppressed uses this directly.
    /// </summary>
    public class BlockEffect : EffectBehaviour
    {
        readonly CarAbility _mask;

        public BlockEffect(EffectType type)
        {
            _mask = EffectRules.BlockMask(type);
        }

        public override void OnStart(EffectHost host, in EffectSet.Entry entry)
        {
            host.Car.Abilities.Block(this, _mask, float.PositiveInfinity, BlockRefresh.KeepLonger);
        }

        public override void OnEnd(EffectHost host)
        {
            host.Car.Abilities.Unblock(this);
        }
    }
}
