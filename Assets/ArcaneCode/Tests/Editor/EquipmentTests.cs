using ArcaneCode.Core;
using NUnit.Framework;

namespace ArcaneCode.Tests
{
    public sealed class EquipmentTests
    {
        [Test]
        public void StaffAndGrimoireComposeTheMageSpellbook()
        {
            var loadout = new MageLoadout(
                new StaffInstance { DefinitionId = "staff-ice", Level = 3 },
                new GrimoireInstance { DefinitionId = "grimoire-fire", Level = 2 });

            CollectionAssert.AreEquivalent(new[] { "icebolt", "fireball" }, loadout.BaseSpellIds);
            Assert.That(loadout.DamageMultiplierFor("icebolt"), Is.EqualTo(1.24f).Within(.001f));
            Assert.That(loadout.DamageMultiplierFor("fireball"), Is.EqualTo(1.08f).Within(.001f));
            Assert.That(loadout.DamageMultiplierFor("frostNova"), Is.EqualTo(1.24f).Within(.001f));
            Assert.That(loadout.DamageMultiplierFor("unknownSpell"), Is.EqualTo(1f));
        }

        [Test]
        public void StaffRunesAreValidatedAndExposeCombatEffects()
        {
            var ricochetStaff = new StaffInstance
            {
                DefinitionId = "staff-fire",
                Level = 1,
                Runes = { new RuneInstance { DefinitionId = "rune-ricochet", Rank = 2 } }
            };
            var loadout = new MageLoadout(ricochetStaff);

            Assert.That(loadout.RicochetCount, Is.EqualTo(2));
            Assert.That(loadout.Staff.Runes.Count, Is.EqualTo(1));

            var overSocketed = new StaffInstance
            {
                DefinitionId = "staff-fire",
                Runes =
                {
                    new RuneInstance { DefinitionId = "rune-ricochet" },
                    new RuneInstance { DefinitionId = "rune-ricochet" },
                    new RuneInstance { DefinitionId = "rune-ricochet" }
                }
            };
            Assert.That(loadout.EquipStaff(overSocketed), Is.False);
            Assert.That(loadout.Staff.DefinitionId, Is.EqualTo("staff-fire"));
        }

        [Test]
        public void StaffSwapIsAtomicAndKeepsTheGrimoire()
        {
            var loadout = new MageLoadout(
                new StaffInstance { DefinitionId = "staff-fire", Level = 1 },
                new GrimoireInstance { DefinitionId = "grimoire-ice", Level = 4 });

            Assert.That(loadout.EquipStaff(new StaffInstance { DefinitionId = "staff-ice", Level = 5 }), Is.True);
            Assert.That(loadout.Staff.DefinitionId, Is.EqualTo("staff-ice"));
            Assert.That(loadout.Staff.Level, Is.EqualTo(5));
            Assert.That(loadout.Grimoire.DefinitionId, Is.EqualTo("grimoire-ice"));
            Assert.That(loadout.DamageMultiplierFor("icebolt"), Is.EqualTo(1.48f).Within(.001f));

            Assert.That(loadout.EquipStaff(new StaffInstance { DefinitionId = "staff-unknown" }), Is.False);
            Assert.That(loadout.Staff.DefinitionId, Is.EqualTo("staff-ice"));
        }

        [Test]
        public void GrimoireCanBeEquippedOrRemovedWithoutChangingTheStaff()
        {
            var loadout = new MageLoadout(new StaffInstance { DefinitionId = "staff-fire" });

            Assert.That(loadout.EquipGrimoire(new GrimoireInstance { DefinitionId = "grimoire-ice", Level = 3 }), Is.True);
            CollectionAssert.AreEquivalent(new[] { "fireball", "icebolt" }, loadout.BaseSpellIds);
            Assert.That(loadout.EquipGrimoire(null), Is.True);
            CollectionAssert.AreEquivalent(new[] { "fireball" }, loadout.BaseSpellIds);
            Assert.That(loadout.Grimoire, Is.Null);
        }
    }
}
