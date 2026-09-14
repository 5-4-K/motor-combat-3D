using System;
using System.Collections.Generic;
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Effects
{
    /// <summary>
    /// A car's effects. Thin adapter: EffectRules decides, EffectSet times,
    /// one EffectBehaviour per effect acts. Ticks as the car's last module, so
    /// it runs after driving and ramming each physics step.
    ///
    /// Every effect ends on death (Health has already cleared the car's blocks
    /// and modifiers by then) and on respawn.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class CarEffects : MonoBehaviour, IEffectReceiver, ICarModule, IRespawnable
    {
        public EffectsConfig config;

        [Tooltip("Logs every applied, restarted, rejected and ended effect on this car.")]
        public bool logEffects;

        public event Action<EffectReport> Applied;
        public event Action<EffectType> Ended;

        readonly EffectSet _set = new EffectSet();
        readonly List<EffectType> _expired = new List<EffectType>();
        readonly List<EffectType> _stepping = new List<EffectType>();
        readonly List<EffectType> _ending = new List<EffectType>();

        EffectHost _host;
        EffectBehaviour[] _behaviours;

        // Built on first use rather than in Awake: EditMode never runs Awake, and
        // a collision may apply an effect before Start.
        EffectHost Host
        {
            get
            {
                if (_host == null)
                {
                    _host = new EffectHost(GetComponent<CarController>(), GetComponent<Rigidbody>(), GetComponent<IDamageable>(), new TickSchedule());
                    _behaviours = CreateBehaviours();
                    if (_host.Health != null) _host.Health.Destroyed += OnDestroyedByDamage;
                }

                _host.Config = config;
                return _host;
            }
        }

        static EffectBehaviour[] CreateBehaviours()
        {
            var behaviours = new EffectBehaviour[EffectInfo.Count];
            behaviours[(int)EffectType.Stunned] = new StunnedEffect();
            behaviours[(int)EffectType.Suppressed] = new BlockEffect(EffectType.Suppressed);
            behaviours[(int)EffectType.Overheated] = new OverheatedEffect();
            behaviours[(int)EffectType.Corroded] = new StatEffect(EffectType.Corroded);
            behaviours[(int)EffectType.Reeling] = new ReelingEffect();
            behaviours[(int)EffectType.Spiked] = new StatEffect(EffectType.Spiked);
            behaviours[(int)EffectType.Fortified] = new StatEffect(EffectType.Fortified);
            behaviours[(int)EffectType.Armored] = new ArmoredEffect();
            behaviours[(int)EffectType.Exhausted] = new StatEffect(EffectType.Exhausted);
            // Overhauled is instant and never active, so it has no behaviour.
            return behaviours;
        }

        void Start()
        {
            // Checked in Start, not Awake — CarFactory assigns config after AddComponent.
            if (config == null)
            {
                Debug.LogError($"[MotorCombat] CarEffects on '{name}' has no EffectsConfig assigned — every effect applied to this car will be rejected.", this);
            }
        }

        void OnDestroy()
        {
            if (_host != null && _host.Health != null) _host.Health.Destroyed -= OnDestroyedByDamage;
        }

        // --- IEffectReceiver ------------------------------------------------------

        public EffectOutcome Apply(in EffectRequest request)
        {
            if (config == null)
            {
                Log(request, EffectOutcome.Invalid);
                return EffectOutcome.Invalid;
            }

            EffectHost host = Host;
            host.Now = Time.fixedTime;
            CarController car = host.Car;

            bool targetable = car.Abilities.Has(CarAbility.Targetable) && (host.Health == null || !host.Health.IsDestroyed);
            EffectOutcome outcome = EffectRules.Decide(
                request,
                targetable,
                Hostility.AreEnemies(request.source, car),
                _set.IsActive(request.type),
                config.Stacks(request.type));

            switch (outcome)
            {
                case EffectOutcome.Applied when request.type == EffectType.Overhauled:
                    EndAll();
                    break;

                case EffectOutcome.Applied:
                {
                    EffectSet.Entry entry = ToEntry(request);
                    _set.Set(entry);
                    _behaviours[(int)request.type].OnStart(host, entry);
                    break;
                }

                case EffectOutcome.Restarted:
                {
                    EffectSet.Entry entry = ToEntry(request);
                    _set.Set(entry);
                    _behaviours[(int)request.type].OnRefresh(host, entry);
                    break;
                }

                default:
                    Log(request, outcome);
                    return outcome;
            }

            Log(request, outcome);
            Applied?.Invoke(new EffectReport
            {
                source = request.source,
                target = car,
                sourceTag = request.sourceTag,
                type = request.type,
                outcome = outcome,
                magnitude = request.magnitude,
                duration = request.duration
            });
            return outcome;
        }

        public bool Has(EffectType type)
        {
            return _set.IsActive(type);
        }

        public float Remaining(EffectType type)
        {
            return _set.Remaining(type);
        }

        public void GetActive(List<ActiveEffect> into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));

            into.Clear();
            for (int i = 0; i < EffectInfo.Count; i++)
            {
                if (!_set.TryGet((EffectType)i, out EffectSet.Entry entry)) continue;

                into.Add(new ActiveEffect
                {
                    type = entry.type,
                    remaining = entry.remaining,
                    duration = entry.duration,
                    magnitude = entry.magnitude,
                    source = entry.source
                });
            }
        }

        // --- ICarModule -------------------------------------------------------------

        public void Tick(in CarInput input, float dt)
        {
            Step(dt, Time.fixedTime);
        }

        public void FrameTick(in CarInput input, float dt) { }

        /// <summary>One physics step: expire, then step what is still active. Public so tests can drive time.</summary>
        public void Step(float dt, float now)
        {
            if (_host == null || config == null) return;   // nothing has ever been applied

            EffectHost host = Host;
            host.Now = now;

            _set.Advance(dt, _expired);
            for (int i = 0; i < _expired.Count; i++)
            {
                NotifyEnded(_expired[i]);
            }

            _set.ActiveTypes(_stepping);
            for (int i = 0; i < _stepping.Count; i++)
            {
                EffectType type = _stepping[i];

                // An earlier step this loop may have ended it — Overheated killing the car ends everything.
                if (!_set.TryGet(type, out EffectSet.Entry entry)) continue;
                _behaviours[(int)type].OnStep(host, entry, dt);
            }
        }

        // --- IRespawnable -----------------------------------------------------------

        public void ResetForRespawn()
        {
            EndAll();
        }

        // --- Internals ------------------------------------------------------------

        void OnDestroyedByDamage(DamageReport report)
        {
            EndAll();
        }

        void EndAll()
        {
            if (_host == null) return;

            _set.ActiveTypes(_ending);
            for (int i = 0; i < _ending.Count; i++)
            {
                if (_set.Remove(_ending[i])) NotifyEnded(_ending[i]);
            }
        }

        /// <summary>The entry is already gone from the set.</summary>
        void NotifyEnded(EffectType type)
        {
            _behaviours[(int)type].OnEnd(Host);
            if (logEffects) Debug.Log($"[Effects] {name}: {type} ended", this);
            Ended?.Invoke(type);
        }

        static EffectSet.Entry ToEntry(in EffectRequest request)
        {
            CarController source = request.source;
            bool hasSource = source != null;

            return new EffectSet.Entry
            {
                type = request.type,
                remaining = request.duration,
                duration = request.duration,
                magnitude = request.magnitude,
                source = source,
                sourceTag = request.sourceTag,
                attack = EffectRules.AttackSnapshot(request.attack, hasSource, hasSource ? source.Stats.Effective(CarStat.Attack) : 0f),
                allowNonEnemy = request.allowNonEnemy
            };
        }

        void Log(in EffectRequest request, EffectOutcome outcome)
        {
            if (!logEffects) return;

            string from = request.source != null ? request.source.name : "environment";
            Debug.Log($"[Effects] {name}: {request.type} from {from} ({request.sourceTag}) → {outcome}", this);
        }

#if UNITY_EDITOR
        [ContextMenu("Debug: apply Stunned")] void DebugStunned() => DebugApply(EffectType.Stunned);
        [ContextMenu("Debug: apply Suppressed")] void DebugSuppressed() => DebugApply(EffectType.Suppressed);
        [ContextMenu("Debug: apply Overheated")] void DebugOverheated() => DebugApply(EffectType.Overheated);
        [ContextMenu("Debug: apply Corroded")] void DebugCorroded() => DebugApply(EffectType.Corroded);
        [ContextMenu("Debug: apply Reeling")] void DebugReeling() => DebugApply(EffectType.Reeling);
        [ContextMenu("Debug: apply Spiked")] void DebugSpiked() => DebugApply(EffectType.Spiked);
        [ContextMenu("Debug: apply Fortified")] void DebugFortified() => DebugApply(EffectType.Fortified);
        [ContextMenu("Debug: apply Armored")] void DebugArmored() => DebugApply(EffectType.Armored);
        [ContextMenu("Debug: apply Overhauled")] void DebugOverhauled() => DebugApply(EffectType.Overhauled);
        [ContextMenu("Debug: apply Exhausted")] void DebugExhausted() => DebugApply(EffectType.Exhausted);

        /// <summary>Applies an effect to this car with the debug sizes from EffectsConfig, from no source.</summary>
        void DebugApply(EffectType type)
        {
            if (config == null)
            {
                Debug.LogError($"[MotorCombat] CarEffects on '{name}' has no EffectsConfig — cannot apply a debug effect.", this);
                return;
            }

            EffectOutcome outcome = Apply(new EffectRequest
            {
                sourceTag = "debug",
                type = type,
                magnitude = type == EffectType.Overheated ? config.debugOverheatAmount : config.debugPercent,
                duration = config.debugDuration,
                allowNonEnemy = true
            });
            Debug.Log($"[Effects] {name}: debug {type} → {outcome}", this);
        }
#endif
    }
}
