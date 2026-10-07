using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ArcaneCode.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ArcaneCode.Tests
{
    public sealed class GameTests
    {
        ArcaneGame game;
        string temp, previousDirectory;
        static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        T Get<T>(string name)=>(T)typeof(ArcaneGame).GetField(name,Flags).GetValue(game);
        void Set(string name,object value)=>typeof(ArcaneGame).GetField(name,Flags).SetValue(game,value);
        object Call(string name,params object[] args)=>typeof(ArcaneGame).GetMethod(name,Flags).Invoke(game,args);
        void SetMode(string name)
        { FieldInfo f=typeof(ArcaneGame).GetField("mode",Flags); f.SetValue(game,Enum.Parse(f.FieldType,name)); }
        [UnitySetUp]
        public IEnumerator Before()
        {
            previousDirectory=Environment.GetEnvironmentVariable("ARCANE_PROFILE_DIR");
            temp=Path.Combine(Path.GetTempPath(),"arcane-playtest-"+Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("ARCANE_PROFILE_DIR",temp);
            foreach (var old in Object.FindObjectsByType<ArcaneGame>()) Object.Destroy(old.gameObject);
            yield return null;
            game=new GameObject("Test Game").AddComponent<ArcaneGame>(); game.enabled=false;
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator After()
        {
            if (game!=null) { Transform room=Get<Transform>("roomRoot"); if (room!=null) Object.Destroy(room.gameObject); Object.Destroy(game.gameObject); }
            yield return null;
            Environment.SetEnvironmentVariable("ARCANE_PROFILE_DIR",previousDirectory);
            // Keep the tiny isolated profile as a test artifact; never delete user saves.
        }
        [UnityTest]
        public IEnumerator BothClassesTraverseRoomsAndReturnToBase()
        {
            foreach (string cls in new[] {"MagoDeFogo"})
            {
                game.StartRun(712,cls); Dungeon dungeon=Get<Dungeon>("dungeon");
                string code=SpellCompiler.Charged("MagoArcanista",cls=="MagoDeFogo"?"fireball":"icebolt"); Set("draft",code); Call("ApplyDraft");
                Assert.That(Get<SpellProgram>("applied").Cost,Is.EqualTo(8));
                Assert.That(Get<SpellMachine>("machine"),Is.Not.Null);
                int combat=dungeon.Rooms.FindIndex(r=>r.Kind==RoomKind.Combat);
                int shop=dungeon.Rooms.FindIndex(r=>r.Kind==RoomKind.Shop);
                Call("EnterRoom",shop,Vector2.zero); Assert.That(Get<object>("mode").ToString(),Is.EqualTo("Run"));
                Assert.That(Get<IList>("shopOffers").Count,Is.EqualTo(3));
                Call("EnterRoom",combat,Vector2.zero); SetMode("Run");
                IList enemies=Get<IList>("enemies"); Assert.That(enemies.Count,Is.GreaterThan(0));
                // Exercise the real cast/projectile path from a guaranteed visible position.
                object enemy=enemies[0]; Type type=enemy.GetType(); Vector2 position=(Vector2)type.GetField("Position").GetValue(enemy);
                Vector2 from=position+Vector2.down*.8f; Set("playerPosition",from); Get<Transform>("player").position=from;
                float initialHP=(float)type.GetField("HP").GetValue(enemy);
                Assert.That(game.Cast(cls=="MagoDeFogo"?"fireball":"icebolt"),Is.True);
                for (int tick=0;tick<20;tick++) Call("UpdateCombat",.02f);
                Assert.That((float)type.GetField("HP").GetValue(enemy),Is.LessThan(initialHP));
                // Clear remaining enemies through the same death/reward handler.
                foreach (object remaining in enemies.Cast<object>().ToArray()) Call("Kill",remaining);
                Call("Update"); Assert.That(dungeon.Rooms[combat].Cleared,Is.True);
                Assert.That(dungeon.Rooms[combat].UncollectedExperience,Is.Zero);
                Assert.That(dungeon.Rooms[combat].UncollectedCoins,Is.Zero);
                Assert.That(Get<IList>("orbs").Count,Is.Zero);
                Assert.That(Get<int>("pendingLevels"),Is.GreaterThan(0)); Assert.That(Get<object>("mode").ToString(),Is.EqualTo("Rewards"));
                // Revisiting a cleared room must not duplicate collected resources.
                int remainingXP=Get<int>("xp"), collectedLevels=Get<int>("pendingLevels");
                int neighbor=dungeon.Rooms[combat].Neighbors[0]; Call("EnterRoom",neighbor,Vector2.zero);
                Call("EnterRoom",combat,Vector2.zero); Assert.That(Get<IList>("orbs").Count,Is.Zero);
                Assert.That(Get<int>("xp"),Is.EqualTo(remainingXP)); Assert.That(Get<int>("pendingLevels"),Is.EqualTo(collectedLevels));
                IList rewards=Get<IList>("rewards"); Assert.That(rewards.Count,Is.EqualTo(3));
                Call("ChooseReward",rewards[0]); Assert.That(Get<object>("mode").ToString(),Is.EqualTo("Inventory"));
                Set("draft",code); Call("ApplyDraft"); Assert.That(Get<SpellProgram>("applied").Source,Is.EqualTo(code));
                Call("EnterRoom",combat,Vector2.zero); Assert.That(Get<IList>("enemies").Count,Is.Zero);
                int boss=dungeon.Rooms.FindIndex(r=>r.Kind==RoomKind.Boss); Call("EnterRoom",boss,Vector2.zero); SetMode("Run");
                Assert.That(Get<IList>("enemies").Count,Is.EqualTo(1)); Call("Kill",Get<IList>("enemies")[0]); Call("Update");
                Assert.That(Get<object>("mode").ToString(),Is.Not.EqualTo("Result")); Assert.That(Get<bool>("won"),Is.False);
                Assert.That(dungeon.Rooms[boss].Cleared,Is.True); Assert.That(dungeon.Rooms[boss].UncollectedCoins,Is.Zero); Assert.That(Get<int>("runCoins"),Is.GreaterThanOrEqualTo(10));
                Profile profile=Get<Profile>("profile"); int coins=profile.Coins, collectedCoins=Get<int>("runCoins"); Call("FinishRun",false); Assert.That(profile.Coins,Is.EqualTo(coins+collectedCoins));
                Call("PrepareHub"); Assert.That(Get<int>("budgetBonus"),Is.Zero); Assert.That(Get<int>("level"),Is.EqualTo(1)); Assert.That(Get<int>("pendingLevels"),Is.Zero); Assert.That(Get<Dungeon>("dungeon"),Is.Null);
                Assert.That(File.Exists(Path.Combine(temp,"profile.json")),Is.True);
                yield return null;
            }
        }
        [UnityTest]
        public IEnumerator InvalidDraftPreservesProgramAndPauseFreezesWorld()
        {
            game.StartRun(10,"MagoDeFogo"); SpellProgram original=Get<SpellProgram>("applied");
            Set("draft","class broken"); Call("ApplyDraft"); Assert.That(Get<SpellProgram>("applied"),Is.SameAs(original));
            Call("EnterRoom",Get<Dungeon>("dungeon").Rooms.FindIndex(r=>r.Kind==RoomKind.Combat),Vector2.zero);
            SetMode("Pause"); float time=Get<float>("elapsed"); Vector2 position=Get<Vector2>("playerPosition");
            for (int i=0;i<10;i++) { Call("Update"); yield return null; }
            Assert.That(Get<float>("elapsed"),Is.EqualTo(time)); Assert.That(Get<Vector2>("playerPosition"),Is.EqualTo(position));
            game.AddEnergy(3); Call("EnterRoom",0,Vector2.zero); Assert.That(game.Energy,Is.Zero);
        }
        [UnityTest]
        public IEnumerator SafeStaffSwapRebuildsTheGenericMageSpellbook()
        {
            Call("SelectStartingGrimoire",new object[] { null });
            game.StartRun(91,"MagoDeFogo");
            var loadout=Get<MageLoadout>("loadout");
            Assert.That(loadout.Staff.DefinitionId,Is.EqualTo("staff-fire"));
            Assert.That(Get<HashSet<string>>("unlocked").Contains("fireball"),Is.True);

            bool equipped=(bool)Call("EquipStaffDuringRun",new StaffInstance { DefinitionId="staff-ice",Level=2 });
            loadout=Get<MageLoadout>("loadout");
            Assert.That(equipped,Is.True); Assert.That(loadout.Staff.DefinitionId,Is.EqualTo("staff-ice"));
            Assert.That(loadout.Grimoire,Is.Null);
            Assert.That(Get<HashSet<string>>("unlocked").Contains("icebolt"),Is.True);
            Assert.That(Get<HashSet<string>>("unlocked").Contains("fireball"),Is.False);
            StringAssert.Contains("class MagoArcanista extends Mago",Get<string>("source"));
            yield return null;
        }
        [UnityTest]
        public IEnumerator PermanentPurchasesPersistAndNewRunCanCastImmediately()
        {
            Call("SelectStartingGrimoire",new object[] { null });
            Profile profile=Get<Profile>("profile"); profile.Coins=300;
            Call("Buy","budget"); Call("Buy","fire"); Call("Buy","speedCast"); Assert.That(profile.BudgetRank,Is.EqualTo(1)); Assert.That(profile.FireUnlocked,Is.True); Assert.That(profile.SpeedCastUnlocked,Is.True);
            Call("LoadProfile"); profile=Get<Profile>("profile"); Assert.That(profile.FireUnlocked,Is.True); Assert.That(profile.SpeedCastUnlocked,Is.True); Assert.That(profile.BudgetRank,Is.EqualTo(1));
            game.StartRun(44,"MagoDeFogo"); Assert.That(Get<HashSet<string>>("unlocked").Contains("flameWave"),Is.True);
            string speedCode="class MagoArcanista extends Mago { void constructor() { this.speedCast(1); this.fireball().cast(); } }";
            Set("draft",speedCode); Call("ApplyDraft"); Assert.That(Get<SpellProgram>("applied").Cost,Is.EqualTo(5));
            Set("elapsed",90f); Set("lastCast",89f); Call("FinishRun",false); game.StartRun(44,"MagoDeGelo");
            Assert.That(Get<float>("lastCast"),Is.LessThan(0)); Assert.That(Get<HashSet<string>>("unlocked").Contains("flameWave"),Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FloorsSpawnTheirAssignedEnemyFamilies()
        {
            game.StartRun(219,"MagoDeFogo"); Dungeon dungeon=Get<Dungeon>("dungeon");
            string[] first=dungeon.UndeadFirst ? new[] {"Esqueleto","Morcego"} : new[] {"Goblin","Rato"};
            string[] second=dungeon.UndeadFirst ? new[] {"Goblin","Rato"} : new[] {"Esqueleto","Morcego"};
            string[][] expected = { first, second, new[] {"Mini mago","Aranha"}, new[] {"Cavaleiro negro","Cavaleiro sem cabeça","Sapo basilisco"} };
            for (int floor=1;floor<=4;floor++)
            {
                int room=dungeon.Rooms.FindIndex(r=>r.Kind==RoomKind.Combat && r.Floor==floor);
                Call("EnterRoom",room,Vector2.zero); SetMode("Run");
                string[] labels=Get<IList>("enemies").Cast<object>()
                    .Select(enemy=>((EnemyDefinition)enemy.GetType().GetField("Definition").GetValue(enemy)).Label).Distinct().ToArray();
                CollectionAssert.IsSubsetOf(labels,expected[floor-1]);
                CollectionAssert.IsSupersetOf(labels,expected[floor-1]);
            }
            int boss=dungeon.Rooms.FindIndex(r=>r.Kind==RoomKind.Boss); Call("EnterRoom",boss,Vector2.zero);
            object superBoss=Get<IList>("enemies")[0];
            Assert.That(((EnemyDefinition)superBoss.GetType().GetField("Definition").GetValue(superBoss)).Label,Is.EqualTo("Cavaleiro Negro Supremo"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator GrimoireSwapsKeepCharacterCodeAndInvalidDraft()
        {
            game.StartRun(71,"MagoDeFogo");
            Call("OpenEditor",Get<object>("mode"));
            string code=SpellCompiler.Charged("MagoArcanista","fireball");
            Set("draft",code); Call("ApplyDraft");
            Set("draft","class unfinished"); Call("CloseEditor");
            var inventory=Get<RunInventory>("inventory");
            foreach (string id in new[] { "grimoire-ice","grimoire-flame-wave" })
            {
                var item=new RunItemInstance { DefinitionId=id }; inventory.Add(item);
                Assert.That((bool)Call("EquipInventoryItem",item),Is.True);
                Assert.That(Get<string>("source"),Is.EqualTo(code));
                Assert.That(Get<SpellProgram>("applied").Source,Is.EqualTo(code));
                Assert.That(Get<string>("draft"),Is.EqualTo("class unfinished"));
                Assert.That(Get<Profile>("profile").ProgramFor("mage").Draft,Is.EqualTo("class unfinished"));
            }
            Call("LoadProfile");
            Assert.That(Get<Profile>("profile").ProgramFor("mage").Code,Is.EqualTo(code));
            Assert.That(Get<Profile>("profile").ProgramFor("mage").Draft,Is.EqualTo("class unfinished"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator MissingSpellUsesTemporaryAttackAndReequipRestoresProgram()
        {
            game.StartRun(72,"MagoDeFogo");
            var inventory=Get<RunInventory>("inventory");
            var ice=new RunItemInstance { DefinitionId="grimoire-ice" };
            var fire=new RunItemInstance { DefinitionId="grimoire-fire" };
            inventory.Add(ice); inventory.Add(fire); Call("EquipInventoryItem",ice);
            string code=SpellCompiler.Starter("MagoArcanista","icebolt");
            Set("draft",code); Call("ApplyDraft");
            Set("draft","class unfinished"); Call("SaveDraft");
            Call("EquipInventoryItem",fire);
            Assert.That(Get<HashSet<string>>("unlocked").Contains("icebolt"),Is.False);
            Assert.That(Get<string>("source"),Is.EqualTo(code));
            Assert.That(Get<Profile>("profile").ProgramFor("mage").Code,Is.EqualTo(code));
            Assert.That(Get<string>("programLoadoutError"),Is.Not.Empty);
            StringAssert.Contains("this.fireball()",Get<SpellProgram>("applied").Source);
            Call("EquipInventoryItem",ice);
            Assert.That(Get<SpellProgram>("applied").Source,Is.EqualTo(code));
            Assert.That(Get<string>("programLoadoutError"),Is.Empty);
            Assert.That(Get<Profile>("profile").ProgramFor("mage").Draft,Is.EqualTo("class unfinished"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CharacterCodeAndLibraryPersistWithoutAGrimoire()
        {
            Call("SelectStartingGrimoire",new object[] { null });
            game.StartRun(73,"MagoDeFogo");
            string code=SpellCompiler.Charged("MagoArcanista","fireball");
            Set("draft",code); Call("ApplyDraft");
            Set("draft","class unfinished");
            Assert.That(Get<RunInventory>("inventory").EquippedGrimoire,Is.Null);
            Call("FinishRun",false);
            Assert.That(Get<object>("mode").ToString(),Is.EqualTo("SaveProgram"));
            Set("programSaveName","Carga de teste"); Call("SaveCharacterProgram");
            Assert.That(Get<object>("mode").ToString(),Is.EqualTo("Result"));
            Call("LoadProfile"); Profile profile=Get<Profile>("profile");
            Assert.That(profile.ProgramFor("mage").Code,Is.EqualTo(code));
            Assert.That(profile.ProgramFor("mage").Draft,Is.EqualTo("class unfinished"));
            Assert.That(profile.ProgramLibrary.Single().ClassId,Is.EqualTo("mage"));
            Assert.That(profile.ProgramLibrary.Single().Code,Is.EqualTo(code));
            Call("PrepareHub"); game.StartRun(74,"MagoDeFogo");
            Assert.That(Get<RunInventory>("inventory").EquippedGrimoire,Is.Null);
            Assert.That(Get<SpellProgram>("applied").Source,Is.EqualTo(code));
            Call("OpenEditor",Get<object>("mode"));
            Assert.That(Get<string>("draft"),Is.EqualTo("class unfinished"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator LegacyProfilesMigrateProgramsAndLibraryWithoutLosingSettings()
        {
            string legacyCode=SpellCompiler.Starter("MagoDeFogo","fireball");
            string migratedCode=SpellCompiler.Starter("MagoArcanista","fireball");
            for (int version=1;version<=4;version++)
            {
                var profile=new Profile { Version=version,MageCode=legacyCode,MageDraft="class unfinished",FireCode=legacyCode,FireDraft="class unfinished",TutorialsEnabled=false };
                profile.ProgramFor("warrior").Code="other class preserved";
                profile.Library.Add(new SavedGrimoire { Name="Mesmo nome",DefinitionId="grimoire-fire",Code=legacyCode });
                profile.Library.Add(new SavedGrimoire { Name="Mesmo nome",DefinitionId="grimoire-ice",Code=SpellCompiler.Starter("MagoDeGelo","icebolt") });
                Set("profile",profile); Call("MigrateProfile");
                Assert.That(profile.Version,Is.EqualTo(Profile.CurrentVersion));
                Assert.That(profile.ProgramFor("mage").Code,Is.EqualTo(migratedCode));
                Assert.That(profile.ProgramFor("mage").Draft,Is.EqualTo("class unfinished"));
                Assert.That(profile.ProgramFor("warrior").Code,Is.EqualTo("other class preserved"));
                Assert.That(profile.ProgramLibrary.Count,Is.EqualTo(2));
                Assert.That(profile.ProgramLibrary.Select(entry=>entry.Name).Distinct().Count(),Is.EqualTo(2));
                if (version==4) Assert.That(profile.TutorialsEnabled,Is.False);
                Call("MigrateProfile"); Assert.That(profile.ProgramLibrary.Count,Is.EqualTo(2));
                Call("SaveProfile"); Call("LoadProfile");
                Assert.That(Get<Profile>("profile").ProgramFor("mage").Code,Is.EqualTo(migratedCode));
                Assert.That(Get<Profile>("profile").ProgramLibrary.Count,Is.EqualTo(2));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator EveryStartingGrimoireIsEquippedWithoutChangingCharacterCode()
        {
            string code=SpellCompiler.Charged("MagoArcanista","fireball");
            Call("OpenEditor",Get<object>("mode")); Set("draft",code); Call("ApplyDraft");
            Set("draft","class unfinished"); Call("CloseEditor");
            var identities=new HashSet<string>();
            foreach (GrimoireDefinition definition in MageEquipmentCatalog.StartingGrimoires)
            {
                Assert.That((bool)Call("SelectStartingGrimoire",definition.Id),Is.True);
                Assert.That(Get<HashSet<string>>("unlocked").Contains(definition.BaseSpellId),Is.True);
                game.StartRun(75,"MagoDeFogo");
                RunItemInstance item=Get<RunInventory>("inventory").EquippedGrimoire;
                Assert.That(item.DefinitionId,Is.EqualTo(definition.Id));
                Assert.That(item.Level,Is.EqualTo(1));
                Assert.That(identities.Add(item.InstanceId),Is.True);
                Assert.That(Get<HashSet<string>>("unlocked").Contains(definition.BaseSpellId),Is.True);
                Assert.That(Get<string>("source"),Is.EqualTo(code));
                Assert.That(Get<SpellProgram>("applied").Source,Is.EqualTo(code));
                Assert.That(Get<Profile>("profile").ProgramFor("mage").Draft,Is.EqualTo("class unfinished"));
                Call("FinishRun",false); Call("PrepareHub");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartingChoicePersistsAndRunLootDoesNotReplaceIt()
        {
            Call("SelectStartingGrimoire","grimoire-ice"); game.StartRun(76,"MagoDeFogo");
            string initialIdentity=Get<RunInventory>("inventory").EquippedGrimoire.InstanceId;
            var loot=new RunItemInstance { DefinitionId="grimoire-frost-nova",Level=7 };
            Get<RunInventory>("inventory").Add(loot); Call("EquipInventoryItem",loot);
            Assert.That(Get<Profile>("profile").MageStartingGrimoireId,Is.EqualTo("grimoire-ice"));
            Call("FinishRun",false); Call("LoadProfile"); Call("PrepareHub");
            Assert.That(Get<Profile>("profile").MageStartingGrimoireId,Is.EqualTo("grimoire-ice"));
            Assert.That(Get<RunInventory>("inventory").EquippedGrimoire.DefinitionId,Is.EqualTo("grimoire-ice"));
            game.StartRun(77,"MagoDeFogo");
            RunItemInstance next=Get<RunInventory>("inventory").EquippedGrimoire;
            Assert.That(next.DefinitionId,Is.EqualTo("grimoire-ice"));
            Assert.That(next.Level,Is.EqualTo(1));
            Assert.That(next.InstanceId,Is.Not.EqualTo(initialIdentity));
            Assert.That(Get<RunInventory>("inventory").Items.Count,Is.EqualTo(2));
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartingChoiceRejectsOtherItemsAndCannotChangeDuringRun()
        {
            Assert.That(Get<Profile>("profile").MageStartingGrimoireId,Is.EqualTo("grimoire-fire"));
            Assert.That((bool)Call("SelectStartingGrimoire","staff-ice"),Is.False);
            Assert.That((bool)Call("SelectStartingGrimoire","unknown-grimoire"),Is.False);
            game.StartRun(78,"MagoDeFogo");
            Assert.That((bool)Call("SelectStartingGrimoire","grimoire-ice"),Is.False);
            Assert.That(Get<Profile>("profile").MageStartingGrimoireId,Is.EqualTo("grimoire-fire"));
            Assert.That(Get<RunInventory>("inventory").EquippedGrimoire.DefinitionId,Is.EqualTo("grimoire-fire"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator VersionFiveGetsDefaultGrimoireAndKeepsCharacterPrograms()
        {
            string code=SpellCompiler.Charged("MagoArcanista","fireball");
            var profile=new Profile { Version=5,MageStartingGrimoireId=null,TutorialsEnabled=false };
            profile.ProgramFor("mage").Code=code; profile.ProgramFor("mage").Draft="class unfinished";
            profile.ProgramLibrary.Add(new SavedCharacterProgram { Name="Cópia existente",ClassId="mage",Code=code });
            Set("profile",profile); Call("MigrateProfile");
            Assert.That(profile.Version,Is.EqualTo(Profile.CurrentVersion));
            Assert.That(profile.MageStartingGrimoireId,Is.EqualTo("grimoire-fire"));
            Assert.That(profile.ProgramFor("mage").Code,Is.EqualTo(code));
            Assert.That(profile.ProgramFor("mage").Draft,Is.EqualTo("class unfinished"));
            Assert.That(profile.ProgramLibrary.Count,Is.EqualTo(1));
            Assert.That(profile.TutorialsEnabled,Is.False);
            Call("SaveProfile"); Call("LoadProfile");
            Assert.That(Get<Profile>("profile").MageStartingGrimoireId,Is.EqualTo("grimoire-fire"));
            Get<Profile>("profile").MageStartingGrimoireId="invalid-saved-choice";
            Call("SaveProfile"); Call("LoadProfile");
            Assert.That(Get<Profile>("profile").MageStartingGrimoireId,Is.EqualTo("grimoire-fire"));
            Assert.That(Get<Profile>("profile").ProgramFor("mage").Code,Is.EqualTo(code));
            yield return null;
        }

        bool HistoryKey(KeyCode key,bool shift=false,bool control=true)
        {
            return (bool)Call("HandleDraftHistoryShortcut",new Event {
                type=EventType.KeyDown,keyCode=key,
                modifiers=(control?EventModifiers.Control:EventModifiers.None)|(shift?EventModifiers.Shift:EventModifiers.None)
            });
        }

        [UnityTest]
        public IEnumerator VolumetricLootSpinsWithoutMovingInteractionAndShopAnchors()
        {
            game.StartRun(91,"MagoDeFogo"); Dungeon dungeon=Get<Dungeon>("dungeon");
            int treasure=dungeon.Rooms.FindIndex(r=>r.Kind==RoomKind.Treasure);
            Call("EnterRoom",treasure,Vector2.zero); SetMode("Run");
            object chest=Get<object>("chest");
            Transform chestView=(Transform)chest.GetType().GetField("View").GetValue(chest);
            Assert.That(chestView.GetComponent<WorldPropArt>().Parts.Length,Is.GreaterThan(1));
            Assert.That((bool)Call("TryInteract"),Is.True);
            Assert.That(dungeon.Rooms[treasure].ChestOpened,Is.True);
            IList pickups=Get<IList>("itemPickups"); Assert.That(pickups.Count,Is.EqualTo(1));
            object pickup=pickups[0]; Type type=pickup.GetType();
            WorldPropArt art=(WorldPropArt)type.GetField("Art").GetValue(pickup);
            Vector2 anchor=(Vector2)type.GetField("Position").GetValue(pickup);
            Quaternion before=art.Pivot.localRotation;
            Call("UpdateCombat",.25f);
            Assert.That(Quaternion.Angle(before,art.Pivot.localRotation),Is.GreaterThan(10));
            Assert.That((Vector2)art.transform.position,Is.EqualTo(anchor));
            Assert.That(art.Pivot.localPosition.y,Is.GreaterThan(.5f));
            foreach (MeshFilter mesh in art.GetComponentsInChildren<MeshFilter>())
                Assert.That(mesh.sharedMesh.bounds.size.z,Is.GreaterThan(0));
            Assert.That(art.GetComponentsInChildren<Collider>().Length,Is.Zero);
            Assert.That(art.Parts[0].sharedMaterial.shader.name,Is.EqualTo("ArcaneCode/WorldProp"));
            Set("playerPosition",anchor); int count=Get<RunInventory>("inventory").Items.Count;
            Assert.That((bool)Call("TryInteract"),Is.True);
            Assert.That(Get<RunInventory>("inventory").Items.Count,Is.EqualTo(count+1));
            Assert.That(dungeon.Rooms[treasure].UncollectedItems,Is.Empty);
            int shop=dungeon.Rooms.FindIndex(r=>r.Kind==RoomKind.Shop);
            Call("EnterRoom",shop,Vector2.zero);
            foreach (object offer in Get<IList>("shopOffers"))
            {
                Type offerType=offer.GetType(); WorldPropArt model=(WorldPropArt)offerType.GetField("Art").GetValue(offer);
                Vector2 position=(Vector2)offerType.GetField("Position").GetValue(offer);
                Set("playerPosition",position); Set("runCoins",0);
                Assert.That((bool)Call("TryBuyShopOffer"),Is.True);
                Assert.That((bool)offerType.GetField("Sold").GetValue(offer),Is.False);
                Quaternion rotation=model.Pivot.localRotation;
                Call("UpdateCombat",.25f);
                Assert.That(Quaternion.Angle(rotation,model.Pivot.localRotation),Is.GreaterThan(10));
                Assert.That((Vector2)model.transform.position,Is.EqualTo(position));
                int price=(int)offerType.GetField("Price").GetValue(offer);
                Set("runCoins",price);
                Assert.That((bool)Call("TryBuyShopOffer"),Is.True);
                Assert.That((bool)offerType.GetField("Sold").GetValue(offer),Is.True);
                Assert.That(Get<int>("runCoins"),Is.Zero);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator PropAnimationIsTimeBasedAndProjectilePresentationKeepsItsHitAnchor()
        {
            Transform root=Get<Transform>("roomRoot");
            WorldPropArt one=WorldProps.Item(root,new Vector2(2,1),RunItemKind.Grimoire);
            WorldPropArt many=WorldProps.Item(root,new Vector2(4,1),RunItemKind.Grimoire);
            one.Animate(1); for (int i=0;i<100;i++) many.Animate(.01f);
            Assert.That(Quaternion.Angle(one.Pivot.localRotation,many.Pivot.localRotation),Is.LessThan(.01f));
            Assert.That(Mathf.Abs(one.Pivot.localPosition.y-many.Pivot.localPosition.y),Is.LessThan(.0001f));
            Quaternion paused=one.Pivot.localRotation; one.Animate(0);
            Assert.That(one.Pivot.localRotation,Is.EqualTo(paused));
            WorldPropArt shot=WorldProps.Projectile(root,new Vector2(-2,1),.4f,Color.red,true,Vector2.right);
            shot.Aim(Vector2.up); shot.Animate(.1f); shot.SetOrder(350);
            Assert.That(shot.transform.position,Is.EqualTo(new Vector3(-2,1,0)));
            Assert.That(shot.Pivot.localPosition,Is.EqualTo(Vector3.zero));
            Assert.That(shot.Parts[0].sortingOrder,Is.EqualTo(350));
            Assert.That(shot.GetComponentsInChildren<Collider>().Length,Is.Zero);
            Assert.That(one.Parts[0].sharedMaterial,Is.SameAs(shot.Parts[0].sharedMaterial));
            yield return null;
        }

        [UnityTest]
        public IEnumerator VolumetricPropsRenderThroughTheExistingTwoDimensionalPipeline()
        {
            if (SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("Validação visual requer um dispositivo gráfico; execute sem -nographics.");
            Camera camera=Get<Camera>("gameCamera");
            Vector3 previousPosition=camera.transform.position;
            float previousSize=camera.orthographicSize;
            RenderTexture previousTarget=camera.targetTexture, previousActive=RenderTexture.active;
            var target=new RenderTexture(1200,400,24); var pixels=new Texture2D(1200,400,TextureFormat.RGB24,false);
            Transform root=Get<Transform>("roomRoot");
            WorldPropArt[] gallery={
                WorldProps.Chest(root,new Vector2(495,0)),
                WorldProps.Item(root,new Vector2(496.6f,0),RunItemKind.Ring),
                WorldProps.Item(root,new Vector2(498.2f,0),RunItemKind.Staff),
                WorldProps.Item(root,new Vector2(499.8f,0),RunItemKind.Grimoire),
                WorldProps.Card(root,new Vector2(501.4f,0)),
                WorldProps.Projectile(root,new Vector2(503,0),.5f,new Color(1,.43f,.14f),false,Vector2.right),
                WorldProps.Projectile(root,new Vector2(504.6f,0),.5f,new Color(.32f,.8f,1),true,Vector2.right)
            };
            try
            {
                camera.transform.position=new Vector3(500,.35f,-10); camera.orthographicSize=2;
                target.Create(); camera.targetTexture=target;
                yield return null; yield return null;
                RenderTexture.active=target; pixels.ReadPixels(new Rect(0,0,1200,400),0,0); pixels.Apply();
                foreach (WorldPropArt art in gallery)
                {
                    Vector3 screen=camera.WorldToViewportPoint(art.Pivot.position);
                    int cx=Mathf.RoundToInt(screen.x*1200),cy=Mathf.RoundToInt(screen.y*400), visible=0, magenta=0;
                    for (int y=Mathf.Max(0,cy-48);y<Mathf.Min(400,cy+48);y++)
                        for (int x=Mathf.Max(0,cx-65);x<Mathf.Min(1200,cx+65);x++)
                        {
                            Color color=pixels.GetPixel(x,y);
                            if (color.maxColorComponent>.15f) visible++;
                            if (color.r>.8f && color.b>.8f && color.g<.1f) magenta++;
                        }
                    Assert.That(magenta,Is.Zero,"Shader de erro (magenta) na galeria.");
                    Assert.That(visible,Is.GreaterThan(30),art.name+" deve aparecer no renderer 2D.");
                }
                Directory.CreateDirectory(temp);
                string path=Path.Combine(temp,"props-25d-preview.png"); File.WriteAllBytes(path,pixels.EncodeToPNG());
                Debug.Log("PROP_PREVIEW "+path);
            }
            finally
            {
                camera.targetTexture=previousTarget; camera.transform.position=previousPosition; camera.orthographicSize=previousSize;
                RenderTexture.active=previousActive; target.Release(); Object.Destroy(target); Object.Destroy(pixels);
            }
        }

        [UnityTest]
        public IEnumerator EditorUndoRestoresSelectionWithoutChangingAppliedProgram()
        {
            Call("OpenEditor",Get<object>("mode"));
            string original=Get<string>("draft");
            Set("caret",12); Set("selectionCaret",6);
            string charged=SpellCompiler.Charged("MagoArcanista","fireball");
            Call("ChangeDraft",charged,charged.Length,charged.Length);
            Call("ApplyDraft");
            SpellProgram applied=Get<SpellProgram>("applied");
            Assert.That(HistoryKey(KeyCode.Z,control:false),Is.False);
            Assert.That(Get<string>("draft"),Is.EqualTo(charged));
            Assert.That(HistoryKey(KeyCode.Z),Is.True);
            Assert.That(Get<string>("draft"),Is.EqualTo(original));
            Assert.That(Get<int>("caret"),Is.EqualTo(12));
            Assert.That(Get<int>("selectionCaret"),Is.EqualTo(6));
            Assert.That(Get<string>("source"),Is.EqualTo(charged));
            Assert.That(Get<SpellProgram>("applied"),Is.SameAs(applied));
            Assert.That(Get<Profile>("profile").ProgramFor("mage").Code,Is.EqualTo(charged));
            HistoryKey(KeyCode.Z,shift:true);
            Assert.That(Get<string>("draft"),Is.EqualTo(charged));
            Assert.That(Get<int>("caret"),Is.EqualTo(charged.Length));
            HistoryKey(KeyCode.Z);
            Call("CloseEditor"); Call("OpenEditor",Get<object>("mode"));
            HistoryKey(KeyCode.Y);
            Assert.That(Get<string>("draft"),Is.EqualTo(original),"Reabrir não reaproveita o histórico de outra sessão.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator EditorUndoCoversInsertionsCompletionIndentAndRedoBranching()
        {
            Call("OpenEditor",Get<object>("mode"));
            string original=Get<string>("draft");
            Set("caret",original.Length); Set("selectionCaret",original.Length);
            Call("InsertCode","\nthis.fi");
            string partial=Get<string>("draft");
            Call("RefreshCompletions"); Call("AcceptCompletion");
            string completed=Get<string>("draft");
            Assert.That(completed,Does.EndWith("this.fireball()"));
            Call("InsertIndentedNewline");
            string newline=Get<string>("draft");
            Call("IndentSelection",false);
            Assert.That(Get<string>("draft"),Is.EqualTo(newline+"    "));
            HistoryKey(KeyCode.Z); Assert.That(Get<string>("draft"),Is.EqualTo(newline));
            HistoryKey(KeyCode.Z); Assert.That(Get<string>("draft"),Is.EqualTo(completed));
            HistoryKey(KeyCode.Z); Assert.That(Get<string>("draft"),Is.EqualTo(partial));
            HistoryKey(KeyCode.Z); Assert.That(Get<string>("draft"),Is.EqualTo(original));
            HistoryKey(KeyCode.Y); Assert.That(Get<string>("draft"),Is.EqualTo(partial));
            // The text field uses this same edit path for typing, deletion and paste.
            Call("ChangeDraft",partial+"X",partial.Length+1,partial.Length+1);
            HistoryKey(KeyCode.Y); Assert.That(Get<string>("draft"),Is.EqualTo(partial+"X"));
            HistoryKey(KeyCode.Z); Assert.That(Get<string>("draft"),Is.EqualTo(partial));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ControllerFocusUsesButtonGeometry()
        {
            List<Rect> targets=Get<List<Rect>>("controllerFocusTargets");
            targets.Clear();
            targets.Add(new Rect(20,20,80,40));
            targets.Add(new Rect(180,20,80,40));
            targets.Add(new Rect(20,120,80,40));
            Set("controllerFocus",0);

            Call("MoveControllerFocus",Vector2.right);
            Assert.That(Get<int>("controllerFocus"),Is.EqualTo(1));
            Call("MoveControllerFocus",Vector2.down);
            Assert.That(Get<int>("controllerFocus"),Is.EqualTo(2));
            yield return null;
        }
    }
}
