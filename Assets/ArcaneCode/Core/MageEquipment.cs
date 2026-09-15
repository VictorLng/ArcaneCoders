using System;
using System.Collections.Generic;

namespace ArcaneCode.Core
{
    public enum ArcaneElement { Fire, Ice, Lightning }

    [Serializable]
    public sealed class StaffDefinition
    {
        public string Id, Label, BaseSpellId, AdvancedSpellId;
        public ArcaneElement Element;
        public int RuneSlots;
        public float PowerPerLevel;
    }

    [Serializable]
    public sealed class GrimoireDefinition
    {
        public string Id, Label, BaseSpellId, AdvancedSpellId;
        public ArcaneElement Element;
        public float PowerPerLevel;
    }

    [Serializable]
    public sealed class RuneDefinition
    {
        public string Id, Label;
        public int MaxRank;
    }

    [Serializable]
    public sealed class RuneInstance
    {
        public string DefinitionId;
        public int Rank = 1;
    }

    [Serializable]
    public sealed class StaffInstance
    {
        public string DefinitionId = "staff-fire";
        public int Level = 1;
        public List<RuneInstance> Runes = new List<RuneInstance>();
    }

    [Serializable]
    public sealed class GrimoireInstance
    {
        public string DefinitionId;
        public int Level = 1;
    }

    public static class MageEquipmentCatalog
    {
        static readonly Dictionary<string, StaffDefinition> Staffs = new Dictionary<string, StaffDefinition>
        {
            { "staff-fire", new StaffDefinition { Id="staff-fire", Label="Staff de fogo", Element=ArcaneElement.Fire, BaseSpellId="fireball", AdvancedSpellId="flameWave", RuneSlots=2, PowerPerLevel=.12f } },
            { "staff-ice", new StaffDefinition { Id="staff-ice", Label="Staff de gelo", Element=ArcaneElement.Ice, BaseSpellId="icebolt", AdvancedSpellId="frostNova", RuneSlots=2, PowerPerLevel=.12f } }
        };
        static readonly Dictionary<string, GrimoireDefinition> Grimoires = new Dictionary<string, GrimoireDefinition>
        {
            { "grimoire-fire", new GrimoireDefinition { Id="grimoire-fire", Label="Grimório de fogo", Element=ArcaneElement.Fire, BaseSpellId="fireball", AdvancedSpellId="flameWave", PowerPerLevel=.08f } },
            { "grimoire-ice", new GrimoireDefinition { Id="grimoire-ice", Label="Grimório de gelo", Element=ArcaneElement.Ice, BaseSpellId="icebolt", AdvancedSpellId="frostNova", PowerPerLevel=.08f } }
        };
        static readonly Dictionary<string, RuneDefinition> Runes = new Dictionary<string, RuneDefinition>
        {
            { "rune-ricochet", new RuneDefinition { Id="rune-ricochet", Label="Runa de ricochete", MaxRank=3 } }
        };

        public static bool TryGetStaff(string id, out StaffDefinition definition) => Staffs.TryGetValue(id ?? string.Empty, out definition);
        public static bool TryGetGrimoire(string id, out GrimoireDefinition definition) => Grimoires.TryGetValue(id ?? string.Empty, out definition);
        public static bool TryGetRune(string id, out RuneDefinition definition) => Runes.TryGetValue(id ?? string.Empty, out definition);

        public static ArcaneElement? ElementForSpell(string spellId)
        {
            foreach (StaffDefinition staff in Staffs.Values)
                if (staff.BaseSpellId == spellId || staff.AdvancedSpellId == spellId) return staff.Element;
            return null;
        }
    }

    [Serializable]
    public sealed class MageLoadout
    {
        public const int MaxEquipmentLevel = 10;
        public StaffInstance Staff { get; private set; }
        public GrimoireInstance Grimoire { get; private set; }

        public MageLoadout(StaffInstance staff = null, GrimoireInstance grimoire = null)
        {
            Staff = IsValid(staff) ? Copy(staff) : new StaffInstance();
            if (!IsValid(Staff)) Staff = new StaffInstance { DefinitionId = "staff-fire", Level = 1 };
            if (grimoire != null && IsValid(grimoire)) Grimoire = Copy(grimoire);
        }

        public IEnumerable<string> BaseSpellIds
        {
            get
            {
                var spells = new HashSet<string>();
                if (MageEquipmentCatalog.TryGetStaff(Staff.DefinitionId, out StaffDefinition staff)) spells.Add(staff.BaseSpellId);
                if (Grimoire != null && MageEquipmentCatalog.TryGetGrimoire(Grimoire.DefinitionId, out GrimoireDefinition grimoire)) spells.Add(grimoire.BaseSpellId);
                return spells;
            }
        }

        public IEnumerable<ArcaneElement> Elements
        {
            get
            {
                var elements = new HashSet<ArcaneElement>();
                if (MageEquipmentCatalog.TryGetStaff(Staff.DefinitionId, out StaffDefinition staff)) elements.Add(staff.Element);
                if (Grimoire != null && MageEquipmentCatalog.TryGetGrimoire(Grimoire.DefinitionId, out GrimoireDefinition grimoire)) elements.Add(grimoire.Element);
                return elements;
            }
        }

        public int RicochetCount
        {
            get
            {
                int count = 0;
                foreach (RuneInstance rune in Staff.Runes)
                    if (rune != null && rune.DefinitionId == "rune-ricochet") count += rune.Rank;
                return count;
            }
        }

        public bool EquipStaff(StaffInstance candidate)
        {
            if (!IsValid(candidate)) return false;
            Staff = Copy(candidate);
            return true;
        }

        public bool EquipGrimoire(GrimoireInstance candidate)
        {
            if (candidate == null) { Grimoire = null; return true; }
            if (!IsValid(candidate)) return false;
            Grimoire = Copy(candidate);
            return true;
        }

        public float DamageMultiplierFor(string spellId)
        {
            ArcaneElement? element = MageEquipmentCatalog.ElementForSpell(spellId);
            if (!element.HasValue) return 1;
            float multiplier = 1;
            if (MageEquipmentCatalog.TryGetStaff(Staff.DefinitionId, out StaffDefinition staff) && staff.Element == element.Value)
                multiplier = Math.Max(multiplier, 1 + (Staff.Level - 1) * staff.PowerPerLevel);
            if (Grimoire != null && MageEquipmentCatalog.TryGetGrimoire(Grimoire.DefinitionId, out GrimoireDefinition grimoire) && grimoire.Element == element.Value)
                multiplier = Math.Max(multiplier, 1 + (Grimoire.Level - 1) * grimoire.PowerPerLevel);
            return multiplier;
        }

        public static bool IsValid(StaffInstance instance)
        {
            if (instance == null || !MageEquipmentCatalog.TryGetStaff(instance.DefinitionId, out StaffDefinition definition) || instance.Level < 1 || instance.Level > MaxEquipmentLevel || instance.Runes == null || instance.Runes.Count > definition.RuneSlots) return false;
            foreach (RuneInstance rune in instance.Runes)
                if (rune == null || !MageEquipmentCatalog.TryGetRune(rune.DefinitionId, out RuneDefinition runeDefinition) || rune.Rank < 1 || rune.Rank > runeDefinition.MaxRank) return false;
            return true;
        }

        public static bool IsValid(GrimoireInstance instance) => instance != null && MageEquipmentCatalog.TryGetGrimoire(instance.DefinitionId, out _) && instance.Level >= 1 && instance.Level <= MaxEquipmentLevel;

        static StaffInstance Copy(StaffInstance source)
        {
            var copy = new StaffInstance { DefinitionId = source.DefinitionId, Level = source.Level };
            foreach (RuneInstance rune in source.Runes) copy.Runes.Add(new RuneInstance { DefinitionId = rune.DefinitionId, Rank = rune.Rank });
            return copy;
        }

        static GrimoireInstance Copy(GrimoireInstance source) => new GrimoireInstance { DefinitionId = source.DefinitionId, Level = source.Level };
    }
}
