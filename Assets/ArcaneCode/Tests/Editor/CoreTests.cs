using System;
using System.Linq;
using ArcaneCode.Core;
using NUnit.Framework;

namespace ArcaneCode.Tests
{
    public sealed class CoreTests
    {
        static CompileOptions Options(int budget=10,bool speedCast=false) => new CompileOptions {Budget=budget,SpeedCastUnlocked=speedCast};
        static string Code(string body,string helpers="") => "class MagoDeFogo extends Mago { void constructor() { "+body+" } "+helpers+" }";
        sealed class World : ISpellWorld
        {
            public int Energy {get;private set;}
            public float Health => 80;
            public int EnemyCount => 2;
            public int Casts, Charged; public float LastSpeed;
            public void AddEnergy(int units) {Energy=Math.Min(5,Energy+units);}
            public bool Cast(string spell,float speedMultiplier) { Casts++; Charged=Energy; LastSpeed=speedMultiplier; Energy=0; return true; }
        }
        [TestCase("MagoDeFogo", "fireball")]
        [TestCase("MagoDeGelo", "icebolt")]
        public void BothClassesStartWithWorkingPrograms(string name,string spell)
        {
            var options=new CompileOptions { ClassName=name,Spells=new System.Collections.Generic.HashSet<string>{spell} };
            var result=SpellCompiler.Compile(SpellCompiler.Starter(name),options);
            Assert.That(result.Success,Is.True,result.Error?.ToString()); Assert.That(result.Cost,Is.EqualTo(2));
            var world=new World(); new SpellMachine(result.Program,world).Tick(.01f); Assert.That(world.Casts,Is.EqualTo(1));
        }
        [Test]
        public void ConstructorIsTheRequiredEntryJobAndIsWhatTheMachineExecutes()
        {
            string missingConstructor="class MagoDeFogo extends Mago { void attackOne() { this.fireball().cast(); } }";
            var result=SpellCompiler.Compile(missingConstructor,Options());
            Assert.That(result.Success,Is.False); StringAssert.Contains("constructor",result.Error.Message);

            string source="class MagoDeFogo extends Mago { void constructor() { this.fireball().cast(); } void attackOne() { this.charge(1); } }";
            result=SpellCompiler.Compile(source,Options());
            Assert.That(result.Success,Is.True,result.Error?.ToString());
            var world=new World(); new SpellMachine(result.Program,world).Tick(.01f);
            Assert.That(world.Casts,Is.EqualTo(1)); Assert.That(world.Energy,Is.Zero);
        }
        [Test]
        public void ChargeTakesCombatTimeAndCastConsumesIt()
        {
            var result=SpellCompiler.Compile(SpellCompiler.Charged("MagoDeFogo"),Options());
            Assert.That(result.Success,Is.True,result.Error?.ToString()); Assert.That(result.Cost,Is.EqualTo(8));
            var world=new World(); var vm=new SpellMachine(result.Program,world); vm.Tick(.01f);
            Assert.That(world.Energy,Is.Zero); Assert.That(world.Casts,Is.Zero);
            for (int i=0;i<20;i++) vm.Tick(.05f);
            Assert.That(world.Casts,Is.Zero);
            for (int i=0;i<5;i++) vm.Tick(.05f);
            Assert.That(world.Casts,Is.EqualTo(1)); Assert.That(world.Charged,Is.EqualTo(3)); Assert.That(world.Energy,Is.Zero);
        }
        [TestCase("int x = true;", "atribuir")]
        [TestCase("if (3) { this.fireball().cast(); }", "bool")]
        [TestCase("this.frostNova().cast();", "desbloqueada")]
        [TestCase("for (int i=0;i<11;i++) { this.charge(1); }", "1 a 10")]
        [TestCase("for (int i=0;i<2;i++) { for (int j=0;j<2;j++) { for (int k=0;k<2;k++) { this.charge(1); } } }", "dois loops")]
        [TestCase("this.charge(true);", "inteiro")]
        [TestCase("this.energia = 10;", "Esperado")]
        public void InvalidProgramsAreRejected(string body,string expected)
        {
            var result=SpellCompiler.Compile(Code(body),Options(100));
            Assert.That(result.Success,Is.False); StringAssert.Contains(expected,result.Error.Message); Assert.That(result.Error.Line,Is.GreaterThan(0));
        }
        [Test]
        public void DiagnosticsHaveLineAndColumn()
        {
            var result=SpellCompiler.Compile("class MagoDeFogo extends Mago {\nvoid constructor() {\n  int x = false;\n}\n}",Options());
            Assert.That(result.Error.Line,Is.EqualTo(3)); Assert.That(result.Error.Column,Is.EqualTo(3));
        }
        [Test]
        public void HelpersCannotHideCostOrRecursion()
        {
            var result=SpellCompiler.Compile(Code("this.helper(); this.helper();","void helper() { this.fireball().cast(); }"),Options());
            Assert.That(result.Success,Is.True,result.Error?.ToString()); Assert.That(result.Cost,Is.EqualTo(4));
            result=SpellCompiler.Compile(Code("this.helper();","void helper() { this.constructor(); }"),Options());
            Assert.That(result.Success,Is.False); StringAssert.Contains("Recursão",result.Error.Message);
        }
        [Test]
        public void TypedHelpersCanReturnSpellsAndMustReturnOnEveryPath()
        {
            string helper="Fireball bolaForte() { return this.fireball(); }";
            var result=SpellCompiler.Compile(Code("Fireball bola = this.bolaForte(); bola.cast();",helper),Options());
            Assert.That(result.Success,Is.True,result.Error?.ToString());
            var world=new World(); new SpellMachine(result.Program,world).Tick(.01f);
            Assert.That(world.Casts,Is.EqualTo(1));

            result=SpellCompiler.Compile(Code("this.fireball().cast();","Fireball semRetorno() { int x = 1; }"),Options());
            Assert.That(result.Success,Is.False); StringAssert.Contains("precisa retornar",result.Error.Message);
            result=SpellCompiler.Compile(Code("this.fireball().cast();","Fireball erro() { return 1; }"),Options());
            Assert.That(result.Success,Is.False); StringAssert.Contains("retornar int",result.Error.Message);
        }
        [Test]
        public void SpeedCastRequiresSharedUnlockScalesCostAndSpeedsExecution()
        {
            string body="this.speedCast(2); this.fireball().cast();";
            var result=SpellCompiler.Compile(Code(body),Options(100));
            Assert.That(result.Success,Is.False); StringAssert.Contains("desbloqueado",result.Error.Message);

            result=SpellCompiler.Compile(Code(body),Options(100,true));
            Assert.That(result.Success,Is.True,result.Error?.ToString()); Assert.That(result.Cost,Is.EqualTo(8));
            var world=new World(); new SpellMachine(result.Program,world).Tick(.01f);
            Assert.That(world.Casts,Is.EqualTo(1)); Assert.That(world.LastSpeed,Is.EqualTo(1.4f).Within(.001f));

            result=SpellCompiler.Compile(Code("this.speedCast(0); this.fireball().cast();"),Options(100,true));
            Assert.That(result.Success,Is.False); StringAssert.Contains("1 a 10",result.Error.Message);
        }
        [Test]
        public void ExpandedHelpersRespectNestedLoopLimit()
        {
            var result=SpellCompiler.Compile(Code("for (int a=0;a<2;a++) { this.helper(); }","void helper() { for (int b=0;b<2;b++) { for (int c=0;c<2;c++) { this.charge(1); } } }"),Options(100));
            Assert.That(result.Success,Is.False); StringAssert.Contains("dois loops",result.Error.Message);
        }
        [Test]
        public void BudgetIsCheckedBeforeActivation()
        {
            var result=SpellCompiler.Compile(SpellCompiler.Charged("MagoDeFogo"),Options(7));
            Assert.That(result.Success,Is.False); Assert.That(result.Cost,Is.EqualTo(8));
        }
        [Test]
        public void ZeroDeltaDoesNotAccumulateEnergy()
        {
            var world=new World(); var vm=new SpellMachine(SpellCompiler.Compile(Code("this.charge(1);"),Options()).Program,world);
            vm.Tick(.01f); for (int i=0;i<100;i++) vm.Tick(0);
            Assert.That(world.Energy,Is.Zero); vm.Reset(); vm.Tick(.01f); Assert.That(world.Energy,Is.Zero);
        }
        [Test]
        public void RuntimeErrorsStopTheProgramWithoutThrowing()
        {
            var world=new World(); var vm=new SpellMachine(SpellCompiler.Compile(Code("int x = 1 / 0;"),Options()).Program,world);
            Assert.DoesNotThrow(()=>vm.Tick(.01f)); StringAssert.Contains("zero",vm.Error);
            vm=new SpellMachine(SpellCompiler.Compile(Code("this.charge(0);"),Options()).Program,world);
            vm.Tick(.01f); StringAssert.Contains("1 a 10",vm.Error);
        }
        [Test]
        public void IntegerArithmeticAndBooleanShortCircuitWork()
        {
            string body="int x = 5 / 2; bool ok = true || (1 / 0 > 0); if (x == 2 && ok) { this.fireball().cast(); }";
            var result=SpellCompiler.Compile(Code(body),Options()); Assert.That(result.Success,Is.True,result.Error?.ToString());
            var world=new World(); var vm=new SpellMachine(result.Program,world); vm.Tick(.1f);
            Assert.That(vm.Error,Is.Null); Assert.That(world.Casts,Is.EqualTo(1));
        }
        [Test]
        public void ExecutionLimitsBoundExpensivePrograms()
        {
            string body="for (int i=0;i<10;i++) { for (int j=0;j<10;j++) { int x=1+2+3+4+5+6+7+8+9+10+11+12; } }";
            var result=SpellCompiler.Compile(Code(body),Options()); Assert.That(result.Success,Is.True,result.Error?.ToString());
            var vm=new SpellMachine(result.Program,new World());
            for (int i=0;i<40 && vm.Error==null;i++) vm.Tick(.01f);
            StringAssert.Contains("2048",vm.Error);
        }
        [Test]
        public void DungeonGuaranteesAcrossOneThousandSeeds()
        {
            for (int seed=0;seed<1000;seed++)
            {
                Dungeon dungeon=Dungeon.Generate(seed); int[] distances=dungeon.Distances();
                Assert.That(dungeon.Rooms.Count,Is.EqualTo(8)); Assert.That(distances.All(d=>d>=0),Is.True,"seed "+seed);
                Assert.That(dungeon.Rooms.Any(r=>r.Neighbors.Count>=3),Is.True);
                Assert.That(dungeon.Rooms.Count(r=>r.Kind==RoomKind.Combat),Is.EqualTo(4));
                Assert.That(dungeon.Rooms.Count(r=>r.Kind==RoomKind.Rest),Is.EqualTo(1));
                Assert.That(dungeon.Rooms.Count(r=>r.Kind==RoomKind.Reward),Is.EqualTo(1));
                int boss=dungeon.Rooms.FindIndex(r=>r.Kind==RoomKind.Boss);
                Assert.That(distances[boss],Is.EqualTo(distances.Max())); Assert.That(dungeon.Rooms[boss].Neighbors.Count,Is.EqualTo(1));
                foreach (Room room in dungeon.Rooms) foreach (int n in room.Neighbors) Assert.That(dungeon.Rooms[n].Neighbors.Contains(dungeon.Rooms.IndexOf(room)),Is.True);
                var again=Dungeon.Generate(seed); Assert.That(again.Rooms.Select(r=>$"{r.X},{r.Y},{r.Kind},{r.Template}"),Is.EqualTo(dungeon.Rooms.Select(r=>$"{r.X},{r.Y},{r.Kind},{r.Template}")));
            }
        }
        [Test]
        public void BankingIsIdempotentAndDoesNotPersistRunBonuses()
        {
            var profile=new Profile {BudgetRank=1}; Assert.That(profile.Bank("run-1",40,true),Is.True);
            Assert.That(profile.Bank("run-1",40,true),Is.False); Assert.That(profile.Coins,Is.EqualTo(40)); Assert.That(profile.Runs,Is.EqualTo(1));
            Assert.That(profile.Bank("run-2",10,false),Is.True); Assert.That(profile.Coins,Is.EqualTo(50)); Assert.That(profile.Wins,Is.EqualTo(1));
            Assert.That(profile.BudgetRank,Is.EqualTo(1));
        }
    }
}
