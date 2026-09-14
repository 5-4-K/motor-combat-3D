using NUnit.Framework;
using UnityEngine;
using MotorCombat.Core;
using MotorCombat.HUD;

namespace MotorCombat.Tests
{
    public class WeaponSlotsWidgetTests
    {
        sealed class FakeSlots : IWeaponSlots
        {
            public readonly WeaponSlotStatus[] statuses = new WeaponSlotStatus[3];
            public int SlotCount => 3;
            public WeaponSlotStatus GetStatus(int slot) => statuses[slot];
        }

        GameObject _parent;
        WeaponSlotsWidget _widget;

        [SetUp]
        public void SetUp()
        {
            _parent = new GameObject("Canvas", typeof(RectTransform));
            _widget = _parent.AddComponent<WeaponSlotsWidget>();
            _widget.Build((RectTransform)_parent.transform);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_parent);
        }

        [Test]
        public void Show_DrawsCooldownBlockedAndEmptySlots()
        {
            var slots = new FakeSlots();
            slots.statuses[0] = new WeaponSlotStatus { assigned = true, cooldownRemaining = 1f, cooldownDuration = 4f };
            slots.statuses[1] = new WeaponSlotStatus { assigned = true, cooldownRemaining = 2f, cooldownDuration = 4f, blocked = true };

            _widget.Show(slots);

            Assert.AreEqual(3, _widget.SlotViews);
            Assert.AreEqual(0.25f, _widget.OverlayFill(0), 1e-5f);
            Assert.IsFalse(_widget.SlashShown(0));
            Assert.IsTrue(_widget.LabelShown(0));

            Assert.AreEqual(0.5f, _widget.OverlayFill(1), 1e-5f, "cooldown and blocked show together");
            Assert.IsTrue(_widget.SlashShown(1));
            Assert.AreEqual(WeaponSlotsWidget.BlockedColour, _widget.BorderColour(1));

            Assert.IsFalse(_widget.LabelShown(2), "empty slot");
            Assert.IsFalse(_widget.SlashShown(2));
            Assert.AreEqual(WeaponSlotsWidget.EmptyRingColour, _widget.BorderColour(2));
        }

        [Test]
        public void Show_Null_DrawsThreeEmptySlots()
        {
            _widget.Show(null);

            for (int i = 0; i < 3; i++)
            {
                Assert.IsFalse(_widget.LabelShown(i));
                Assert.AreEqual(0f, _widget.OverlayFill(i));
            }
        }
    }
}
