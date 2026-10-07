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

    // Items share an inventory model; class restrictions are checked when equipping.
    public enum RunItemKind { Staff, Grimoire, Ring }
    [Serializable]
    public sealed class RunItemInstance
    {
        public string InstanceId = Guid.NewGuid().ToString("N");
        public string DefinitionId;
        public int Level = 1;
    }
    [Serializable]
    public sealed class SavedGrimoire
    {
        // Legacy save format. New snapshots use SavedCharacterProgram.
        public string Name, DefinitionId, Code;
    }
    public sealed class RunItemDefinition
    {
        public string Id, Label, Description, OwnerClassId;
        public RunItemKind Kind;
        public int MaxLevel;
    }
    public static class RunItemCatalog
    {
        static readonly Dictionary<string, RunItemDefinition> Definitions = new Dictionary<string, RunItemDefinition>
        {
            { "staff-fire", new RunItemDefinition { Id="staff-fire", Label="Staff de Fogo", Description="Projéteis flamejantes e queimadura.", OwnerClassId="mage", Kind=RunItemKind.Staff, MaxLevel=10 } },
            { "staff-ice", new RunItemDefinition { Id="staff-ice", Label="Staff de Gelo", Description="Lanças de gelo, lentidão e congelamento.", OwnerClassId="mage", Kind=RunItemKind.Staff, MaxLevel=10 } },
            { "grimoire-fire", new RunItemDefinition { Id="grimoire-fire", Label="Grimório da Bola de Fogo", Description="Adiciona Fireball à pool de feitiços do mago.", OwnerClassId="mage", Kind=RunItemKind.Grimoire, MaxLevel=10 } },
            { "grimoire-ice", new RunItemDefinition { Id="grimoire-ice", Label="Grimório da Lança de Gelo", Description="Adiciona Icebolt à pool de feitiços do mago.", OwnerClassId="mage", Kind=RunItemKind.Grimoire, MaxLevel=10 } },
            { "grimoire-flame-wave", new RunItemDefinition { Id="grimoire-flame-wave", Label="Grimório da Onda de Chamas", Description="Adiciona Flame Wave à pool de feitiços do mago.", OwnerClassId="mage", Kind=RunItemKind.Grimoire, MaxLevel=10 } },
            { "grimoire-frost-nova", new RunItemDefinition { Id="grimoire-frost-nova", Label="Grimório da Nova Congelante", Description="Adiciona Frost Nova à pool de feitiços do mago.", OwnerClassId="mage", Kind=RunItemKind.Grimoire, MaxLevel=10 } },
            { "ring-ricochet", new RunItemDefinition { Id="ring-ricochet", Label="Anel de Ricochete", Description="Concede ricochetes adicionais aos projéteis.", OwnerClassId="mage", Kind=RunItemKind.Ring, MaxLevel=3 } }
        };
        public static bool TryGet(string id, out RunItemDefinition definition) => Definitions.TryGetValue(id ?? string.Empty, out definition);
        public static IEnumerable<RunItemDefinition> OfKind(RunItemKind kind) { foreach (var item in Definitions.Values) if (item.Kind == kind) yield return item; }
    }

    [Serializable]
    public sealed class RunInventory
    {
        public List<RunItemInstance> Items = new List<RunItemInstance>();
        public string EquippedStaffId, EquippedGrimoireId;
        public RunItemInstance EquippedStaff => Find(EquippedStaffId);
        public RunItemInstance EquippedGrimoire => Find(EquippedGrimoireId);
        public RunItemInstance Find(string instanceId) => Items.Find(item => item != null && item.InstanceId == instanceId);
        public void BeginRun(string classId = "mage",string startingGrimoireId = null)
        {
            Items.Clear(); EquippedGrimoireId = null;
            var starter = new RunItemInstance { DefinitionId="staff-fire", Level=1 };
            Items.Add(starter); EquippedStaffId = starter.InstanceId;
            if (RunItemCatalog.TryGet(startingGrimoireId,out RunItemDefinition definition) && definition.Kind == RunItemKind.Grimoire && definition.OwnerClassId == classId && MageEquipmentCatalog.TryGetGrimoire(startingGrimoireId,out _))
            {
                var grimoire=new RunItemInstance { DefinitionId=startingGrimoireId,Level=1 };
                Add(grimoire); Equip(grimoire.InstanceId,classId);
            }
        }
        public bool Add(RunItemInstance item)
        {
            if (item == null || !RunItemCatalog.TryGet(item.DefinitionId, out RunItemDefinition definition)) return false;
            item.Level = Math.Max(1, Math.Min(definition.MaxLevel, item.Level));
            if (string.IsNullOrEmpty(item.InstanceId) || Find(item.InstanceId) != null) item.InstanceId = Guid.NewGuid().ToString("N");
            Items.Add(item); return true;
        }
        public bool Equip(string instanceId,string classId)
        {
            RunItemInstance item = Find(instanceId);
            if (item == null || !RunItemCatalog.TryGet(item.DefinitionId, out RunItemDefinition definition)) return false;
            if (definition.Kind == RunItemKind.Grimoire && classId != definition.OwnerClassId) return false;
            if (definition.Kind == RunItemKind.Staff) EquippedStaffId = item.InstanceId;
            else if (definition.Kind == RunItemKind.Grimoire) EquippedGrimoireId = item.InstanceId;
            else return false;
            return true;
        }
        public int RicochetCount
        {
            get { int total=0; foreach (RunItemInstance item in Items) if (item != null && item.DefinitionId == "ring-ricochet") total += item.Level; return total; }
        }
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
            { "grimoire-fire", new GrimoireDefinition { Id="grimoire-fire", Label="Grimório de fogo", Element=ArcaneElement.Fire, BaseSpellId="fireball", AdvancedSpellId="flameWave" } },
            { "grimoire-ice", new GrimoireDefinition { Id="grimoire-ice", Label="Grimório de gelo", Element=ArcaneElement.Ice, BaseSpellId="icebolt", AdvancedSpellId="frostNova" } },
            { "grimoire-flame-wave", new GrimoireDefinition { Id="grimoire-flame-wave", Label="Grimório da onda de chamas", Element=ArcaneElement.Fire, BaseSpellId="flameWave", AdvancedSpellId="flameWave" } },
            { "grimoire-frost-nova", new GrimoireDefinition { Id="grimoire-frost-nova", Label="Grimório da nova congelante", Element=ArcaneElement.Ice, BaseSpellId="frostNova", AdvancedSpellId="frostNova" } }
        };
        static readonly Dictionary<string, RuneDefinition> Runes = new Dictionary<string, RuneDefinition>
        {
            { "rune-ricochet", new RuneDefinition { Id="rune-ricochet", Label="Runa de ricochete", MaxRank=3 } }
        };

        public static bool TryGetStaff(string id, out StaffDefinition definition) => Staffs.TryGetValue(id ?? string.Empty, out definition);
        public static bool TryGetGrimoire(string id, out GrimoireDefinition definition) => Grimoires.TryGetValue(id ?? string.Empty, out definition);
        public static IEnumerable<GrimoireDefinition> StartingGrimoires => Grimoires.Values;
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
