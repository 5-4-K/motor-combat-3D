using System;
using NUnit.Framework;
using UnityEngine;
using MotorCombat.Core;
using MotorCombat.Effects;

namespace MotorCombat.Tests
{
    public class EffectsConfigTests
    {
        EffectsConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<EffectsConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_config);
        }

        [Test]
        public void Stacks_DefaultsToReelingOnly()
        {
            foreach (EffectType type in Enum.GetValues(typeof(EffectType)))
            {
                Assert.AreEqual(type == EffectType.Reeling, _config.Stacks(type), type.ToString());
            }
        }

        /// <summary>Catches a copy-paste slip: each type must read its own field and only its own field.</summary>
        [Test]
        public void Stacks_ReadsEachEffectsOwnField()
        {
            foreach (EffectType type in Enum.GetValues(typeof(EffectType)))
            {
                if (type == EffectType.Overhauled) continue;

                string name = type.ToString();
                var field = typeof(EffectsConfig).GetField(char.ToLowerInvariant(name[0]) + name.Substring(1) + "Stacks");
                Assert.IsNotNull(field, name);

                bool original = (bool)field.GetValue(_config);
                field.SetValue(_config, !original);

                foreach (EffectType other in Enum.GetValues(typeof(EffectType)))
                {
                    bool expected = other == EffectType.Reeling;
                    if (other == type) expected = !original;
                    Assert.AreEqual(expected, _config.Stacks(other), $"after flipping {name}, {other}");
                }

                field.SetValue(_config, original);
            }
        }
    }
}
