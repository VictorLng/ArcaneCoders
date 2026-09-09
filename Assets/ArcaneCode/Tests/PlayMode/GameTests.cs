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
            foreach (string cls in new[] {"MagoDeFogo","MagoDeGelo"})
            {
                game.StartRun(712,cls); Dungeon dungeon=Get<Dungeon>("dungeon");
                string code=SpellCompiler.Charged(cls); Set("draft",code); Call("ApplyDraft");
                Assert.That(Get<SpellProgram>("applied").Cost,Is.EqualTo(8));
                Assert.That(Get<SpellMachine>("machine"),Is.Not.Null);
                int combat=dungeon.Rooms.FindIndex(r=>r.Kind==RoomKind.Combat);
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
                Assert.That(dungeon.Rooms[combat].UncollectedExperience,Is.GreaterThan(0));
                Assert.That(Get<int>("pendingLevels"),Is.Zero); Assert.That(Get<object>("mode").ToString(),Is.EqualTo("Run"));
                // Moving through a door must not turn uncollected crystals into experience.
                int neighbor=dungeon.Rooms[combat].Neighbors[0]; Call("EnterRoom",neighbor,Vector2.zero);
                Call("EnterRoom",combat,Vector2.zero); Assert.That(Get<IList>("orbs").Count,Is.GreaterThan(0));
                Assert.That(Get<int>("pendingLevels"),Is.Zero);
                // XP is granted only when the player touches the restored crystal.
                Set("playerPosition",Vector2.zero); Get<Transform>("player").position=Vector2.zero; Call("UpdateCombat",.02f); Call("Update");
                Assert.That(Get<int>("pendingLevels"),Is.GreaterThan(0)); Assert.That(Get<object>("mode").ToString(),Is.EqualTo("Rewards"));
                IList rewards=Get<IList>("rewards"); Assert.That(rewards.Count,Is.EqualTo(3));
                Call("ChooseReward",rewards[0]); Assert.That(Get<object>("mode").ToString(),Is.EqualTo("Editor"));
                Set("draft",code); Call("ApplyDraft"); Assert.That(Get<SpellProgram>("applied").Source,Is.EqualTo(code));
                Call("EnterRoom",combat,Vector2.zero); Assert.That(Get<IList>("enemies").Count,Is.Zero);
                int boss=dungeon.Rooms.FindIndex(r=>r.Kind==RoomKind.Boss); Call("EnterRoom",boss,Vector2.zero); SetMode("Run");
                Assert.That(Get<IList>("enemies").Count,Is.EqualTo(1)); Call("Kill",Get<IList>("enemies")[0]); Call("Update");
                Assert.That(Get<object>("mode").ToString(),Is.EqualTo("Result")); Assert.That(Get<bool>("won"),Is.True);
                Profile profile=Get<Profile>("profile"); int coins=profile.Coins; Call("FinishRun",true); Assert.That(profile.Coins,Is.EqualTo(coins));
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
        public IEnumerator PermanentPurchasesPersistAndNewRunCanCastImmediately()
        {
            Profile profile=Get<Profile>("profile"); profile.Coins=300;
            Call("Buy","budget"); Call("Buy","fire"); Call("Buy","speedCast"); Assert.That(profile.BudgetRank,Is.EqualTo(1)); Assert.That(profile.FireUnlocked,Is.True); Assert.That(profile.SpeedCastUnlocked,Is.True);
            Call("LoadProfile"); profile=Get<Profile>("profile"); Assert.That(profile.FireUnlocked,Is.True); Assert.That(profile.SpeedCastUnlocked,Is.True); Assert.That(profile.BudgetRank,Is.EqualTo(1));
            game.StartRun(44,"MagoDeFogo"); Assert.That(Get<HashSet<string>>("unlocked").Contains("flameWave"),Is.True);
            string speedCode="class MagoDeFogo extends Mago { void attackOne() { this.speedCast(1); this.fireball().cast(); } }";
            Set("draft",speedCode); Call("ApplyDraft"); Assert.That(Get<SpellProgram>("applied").Cost,Is.EqualTo(5));
            Set("elapsed",90f); Set("lastCast",89f); Call("FinishRun",false); game.StartRun(44,"MagoDeGelo");
            Assert.That(Get<float>("lastCast"),Is.LessThan(0)); Assert.That(Get<HashSet<string>>("unlocked").Contains("flameWave"),Is.False);
            yield return null;
        }
    }
}
