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
            Assert.That(loadout.DamageMultiplierFor("fireball"), Is.EqualTo(1f));
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

        [Test]
        public void RunInventoryKeepsGenericItemsAndAppliesEveryRicochetRing()
        {
            var inventory = new RunInventory();
            inventory.BeginRun();
            Assert.That(inventory.EquippedStaff.DefinitionId, Is.EqualTo("staff-fire"));
            var staff = new RunItemInstance { DefinitionId="staff-ice",Level=3 };
            var grimoire = new RunItemInstance { DefinitionId="grimoire-flame-wave",Level=2 };
            Assert.That(inventory.Add(staff), Is.True);
            Assert.That(inventory.Add(grimoire), Is.True);
            Assert.That(inventory.Add(new RunItemInstance { DefinitionId="ring-ricochet",Level=1 }), Is.True);
            Assert.That(inventory.Add(new RunItemInstance { DefinitionId="ring-ricochet",Level=3 }), Is.True);
            Assert.That(inventory.Equip(staff.InstanceId,"mage"), Is.True);
            Assert.That(inventory.Equip(grimoire.InstanceId,"mage"), Is.True);
            Assert.That(inventory.EquippedStaff.DefinitionId, Is.EqualTo("staff-ice"));
            Assert.That(inventory.EquippedGrimoire.DefinitionId, Is.EqualTo("grimoire-flame-wave"));
            Assert.That(inventory.RicochetCount, Is.EqualTo(4));
        }

        [Test]
        public void GrimoireIsExclusiveToMageAndOnlyAddsSpells()
        {
            var inventory = new RunInventory(); inventory.BeginRun();
            var grimoire = new RunItemInstance { DefinitionId="grimoire-ice",Level=10 };
            inventory.Add(grimoire);
            Assert.That(inventory.Equip(grimoire.InstanceId,"warrior"), Is.False);
            Assert.That(inventory.EquippedGrimoire, Is.Null);
            Assert.That(inventory.Equip(grimoire.InstanceId,"mage"), Is.True);
            var loadout = new MageLoadout(new StaffInstance(),new GrimoireInstance { DefinitionId="grimoire-ice",Level=10 });
            CollectionAssert.Contains(loadout.BaseSpellIds,"icebolt");
            Assert.That(loadout.DamageMultiplierFor("icebolt"), Is.EqualTo(1f));
        }

        [Test]
        public void CharacterProgramsAndDraftsAreIndependentForEachClass()
        {
            var profile = new Profile();
            CharacterProgramState mage=profile.ProgramFor("mage"), warrior=profile.ProgramFor("warrior");
            mage.Code="mage applied"; mage.Draft="mage unfinished";
            warrior.Code="warrior applied"; warrior.Draft="warrior unfinished";
            Assert.That(profile.ProgramFor("mage"), Is.SameAs(mage));
            Assert.That(profile.ProgramFor("warrior").Code, Is.EqualTo("warrior applied"));
            Assert.That(profile.ProgramFor("mage").Code, Is.EqualTo("mage applied"));
            Assert.That(profile.ProgramFor("mage").Draft, Is.EqualTo("mage unfinished"));
            Assert.That(profile.ProgramFor("warrior").Draft, Is.EqualTo("warrior unfinished"));
        }

        [Test]
        public void StartingGrimoireCreatesFreshLevelOneEquipmentForMageOnly()
        {
            var inventory=new RunInventory();
            var identities=new System.Collections.Generic.HashSet<string>();
            foreach (GrimoireDefinition definition in MageEquipmentCatalog.StartingGrimoires)
            {
                inventory.BeginRun("mage",definition.Id);
                Assert.That(inventory.Items.Count,Is.EqualTo(2));
                Assert.That(inventory.EquippedStaff.Level,Is.EqualTo(1));
                Assert.That(inventory.EquippedGrimoire.DefinitionId,Is.EqualTo(definition.Id));
                Assert.That(inventory.EquippedGrimoire.Level,Is.EqualTo(1));
                Assert.That(identities.Add(inventory.EquippedGrimoire.InstanceId),Is.True);
                inventory.EquippedGrimoire.Level=10;
                inventory.Add(new RunItemInstance { DefinitionId="ring-ricochet",Level=3 });
            }
            inventory.BeginRun("warrior","grimoire-ice");
            Assert.That(inventory.EquippedGrimoire,Is.Null);
            Assert.That(inventory.RicochetCount,Is.Zero);
            inventory.BeginRun("mage","staff-fire");
            Assert.That(inventory.EquippedGrimoire,Is.Null);
        }
    }
}
