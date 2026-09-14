using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using MotorCombat.Core;
using MotorCombat.HUD;

namespace MotorCombat.Tests
{
    public class EffectChipRowTests
    {
        sealed class FakeReceiver : IEffectReceiver
        {
            public readonly List<ActiveEffect> active = new List<ActiveEffect>();

            public EffectOutcome Apply(in EffectRequest request) => EffectOutcome.Invalid;
            public bool Has(EffectType type) => active.Exists(e => e.type == type);
            public float Remaining(EffectType type) => 0f;

            public void GetActive(List<ActiveEffect> into)
            {
                into.Clear();
                into.AddRange(active);
            }

            public event Action<EffectReport> Applied { add { } remove { } }
            public event Action<EffectType> Ended { add { } remove { } }
        }

        static int ActiveChildren(Transform parent)
        {
            int count = 0;
            foreach (Transform child in parent)
            {
                if (child.gameObject.activeSelf) count++;
            }
            return count;
        }

        [Test]
        public void Show_ShowsOneChipPerActiveEffect_AndHidesTheRest()
        {
            var parent = new GameObject("Parent", typeof(RectTransform));
            try
            {
                var row = new EffectChipRow((RectTransform)parent.transform, "Row", 40f, 14f, 3f, 10);
                var receiver = new FakeReceiver();
                receiver.active.Add(new ActiveEffect { type = EffectType.Stunned, remaining = 1.2f, duration = 3f });
                receiver.active.Add(new ActiveEffect { type = EffectType.Corroded, remaining = 2f, duration = 3f });
                receiver.active.Add(new ActiveEffect { type = EffectType.Armored, remaining = 0.5f, duration = 3f });

                row.Show(receiver);
                Assert.AreEqual(3, row.VisibleCount);
                Assert.AreEqual(3, ActiveChildren(row.Root));
                Assert.AreEqual("STUN 1.2s", row.TextAt(0));

                receiver.active.RemoveRange(1, 2);
                row.Show(receiver);
                Assert.AreEqual(1, row.VisibleCount);
                Assert.AreEqual(1, ActiveChildren(row.Root), "pooled chips are hidden, not destroyed");

                row.Show(null);
                Assert.AreEqual(0, row.VisibleCount);
                Assert.AreEqual(0, ActiveChildren(row.Root));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }
    }
}
