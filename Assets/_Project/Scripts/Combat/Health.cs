using System;
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Combat
{
    /// <summary>
    /// A car's health. Thin adapter: gathers targetable, hostility, attack and
    /// defense from the car, runs <see cref="HealthState"/>, raises events, and on
    /// the killing hit turns the car into a wreck.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class Health : MonoBehaviour, IDamageable, IRespawnable
    {
        /// <summary>Source key for the wreck's untimed ability block.</summary>
        public static readonly object WreckBlock = new object();

        const CarAbility WreckMask = CarAbility.Throttle | CarAbility.Steer | CarAbility.Fire
                                     | CarAbility.Ram | CarAbility.Targetable;

        [Tooltip("Set from CarDefinition by CarFactory.")]
        public float maxHealth = 1000f;

        public event Action<DamageReport> Damaged;
        public event Action<DamageReport> Destroyed;

        HealthState _state;
        CarController _car;

        // Created on first use, after CarFactory has set maxHealth.
        HealthState State => _state ?? (_state = new HealthState(maxHealth));
        CarController Car => _car != null ? _car : (_car = GetComponent<CarController>());

        public float Current => State.Current;
        public float Max => State.Max;
        public bool IsDestroyed => State.IsDestroyed;

        public void AddGate(object source, Func<DamageRequest, bool> blocks) => State.AddGate(source, blocks);
        public void RemoveGate(object source) => State.RemoveGate(source);

        /// <summary>
        /// Revives the car: drops the wreck block and anything else blocked or
        /// modified, and refills health. Effects end their own state through
        /// CarEffects' own reset.
        /// </summary>
        public void ResetForRespawn()
        {
            CarController car = Car;
            car.Abilities.UnblockAll();
            car.Stats.RemoveAll();
            State.Revive();
        }

        public DamageResult Apply(in DamageRequest request)
        {
            CarController car = Car;

            DamageResult result = State.Apply(
                request,
                car.Abilities.Has(CarAbility.Targetable),
                Hostility.AreEnemies(request.source, car),
                request.attack ?? (request.source != null ? request.source.Stats.Effective(CarStat.Attack) : DamageRules.NeutralAttack),
                car.Stats.Effective(CarStat.Defense));

            if (result.outcome != DamageOutcome.Applied) return result;

            var report = new DamageReport
            {
                source = request.source,
                target = car,
                sourceTag = request.sourceTag,
                kind = request.kind,
                dealt = result.dealt,
                healthAfter = State.Current
            };

            if (result.killed)
            {
                // The car must become a wreck even if a Damaged subscriber
                // throws, so this runs before Damaged fires rather than
                // after. A wreck carries nothing over: ram lock, reel and
                // every effect end. Event order is still Damaged → Destroyed.
                car.Abilities.UnblockAll();
                car.Stats.RemoveAll();
                car.Abilities.Block(WreckBlock, WreckMask, float.PositiveInfinity, BlockRefresh.KeepLonger);
            }

            Damaged?.Invoke(report);

            if (result.killed)
            {
                Destroyed?.Invoke(report);
            }

            return result;
        }

#if UNITY_EDITOR
        [ContextMenu("Debug: take 25% max HP")]
        void DebugTakeQuarter()
        {
            Apply(new DamageRequest { sourceTag = "debug", kind = DamageKind.MaxHealthPercent, amount = 25f });
        }

        [ContextMenu("Debug: destroy")]
        void DebugDestroy()
        {
            Apply(new DamageRequest { sourceTag = "debug", kind = DamageKind.Flat, amount = 1e9f });
        }
#endif
    }
}
