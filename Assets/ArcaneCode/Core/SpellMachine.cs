using System;
using System.Collections.Generic;
using System.Globalization;

namespace ArcaneCode.Core
{
    public interface ISpellWorld
    {
        int Energy { get; }
        float Health { get; }
        int EnemyCount { get; }
        void AddEnergy(int units);
        bool Cast(string spell, float speedMultiplier);
    }

    public sealed class SpellMachine
    {
        readonly SpellProgram program;
        readonly ISpellWorld world;
        readonly float chargeSeconds, castInterval;
        IEnumerator<Instruction> execution;
        float wait;
        Instruction pending;
        int steps;
        int speedCastRank;
        float activeChargeSeconds;
        public string Error { get; private set; }
        public int CurrentLine { get; private set; }
        public bool Charging => pending != null && pending.Kind == "charge";
        public float ChargeProgress => Charging && activeChargeSeconds > 0 ? 1 - wait / activeChargeSeconds : 0;
        public const int FrameLimit = 128, ExecutionLimit = 2048;

        sealed class Instruction { public string Kind, Spell; public int Line, Amount; }
        sealed class SpellValue { public string Name; }
        sealed class Scope
        {
            readonly Dictionary<string, object> values = new Dictionary<string, object>();
            readonly Scope parent;
            public Scope(Scope parent = null) { this.parent = parent; }
            public object Get(string key) => values.TryGetValue(key, out object value) ? value : parent.Get(key);
            public void Declare(string key, object value) { values.Add(key, value); }
            public void Set(string key, object value) { if (values.ContainsKey(key)) values[key] = value; else parent.Set(key, value); }
        }

        public SpellMachine(SpellProgram program, ISpellWorld world, float chargeSeconds = .35f, float castInterval = .6f)
        {
            this.program = program; this.world = world;
            this.chargeSeconds = Math.Max(.01f,chargeSeconds); this.castInterval = Math.Max(.01f,castInterval);
        }
        public void Reset() { execution?.Dispose(); execution = null; pending = null; wait = 0; steps = speedCastRank = 0; activeChargeSeconds = 0; Error = null; }
        void Step() { if (++steps > ExecutionLimit) throw new InvalidOperationException("Limite de 2048 instruções por execução excedido."); }
        float SpeedMultiplier => 1 + speedCastRank * .2f;
        public void Tick(float deltaTime)
        {
            if (Error != null || deltaTime <= 0) return;
            try
            {
                if (wait > 0)
                {
                    wait -= deltaTime;
                    if (wait > 0) return;
                    if (pending != null && pending.Kind == "charge") world.AddEnergy(1);
                    pending = null;
                }
                if (execution == null) { steps = speedCastRank = 0; execution = Run(program.Methods[SpellCompiler.EntryMethod], new Scope()).GetEnumerator(); }
                for (int frame = 0; frame < FrameLimit; frame++)
                {
                    if (!execution.MoveNext()) { execution.Dispose(); execution = null; wait = .05f; return; }
                    Instruction instruction = execution.Current; CurrentLine = instruction.Line;
                    if (instruction.Kind == "speedCast") { speedCastRank = instruction.Amount; continue; }
                    if (instruction.Kind == "charge") { pending = instruction; activeChargeSeconds = chargeSeconds / SpeedMultiplier; wait = activeChargeSeconds; return; }
                    if (instruction.Kind == "cast") { world.Cast(instruction.Spell, SpeedMultiplier); wait = castInterval / SpeedMultiplier; return; }
                }
            }
            catch (Exception e)
            { Error = $"Linha {CurrentLine}: {e.Message}"; execution?.Dispose(); execution = null; pending = null; }
        }

        IEnumerable<Instruction> Run(Statement s, Scope scope)
        {
            Step(); yield return new Instruction { Kind = "step", Line = s.Token.Line };
            switch (s.Kind)
            {
                case "block":
                    var local = new Scope(scope);
                    foreach (var child in s.Children) foreach (var step in Run(child, local)) yield return step;
                    break;
                case "declare": scope.Declare(s.Name, Eval(s.Value, scope)); break;
                case "assign": scope.Set(s.Name, Eval(s.Value, scope)); break;
                case "if":
                    Statement branch = (bool)Eval(s.Value, scope) ? s.Body : s.Else;
                    if (branch != null) foreach (var step in Run(branch, scope)) yield return step;
                    break;
                case "for":
                    var loop = new Scope(scope); loop.Declare(s.Name, 0d);
                    for (int i = 0; i < s.Count; i++)
                    { loop.Set(s.Name, (double)i); foreach (var step in Run(s.Body, loop)) yield return step; }
                    break;
                case "expr":
                    Expression e = s.Value;
                    if (e.Name == "charge")
                    {
                        double amount = (double)Eval(e.Args[0], scope);
                        if (amount < 1 || amount > 10 || amount != Math.Truncate(amount))
                            throw new InvalidOperationException("charge() aceita de 1 a 10 unidades inteiras.");
                        for (int i = 0; i < (int)amount; i++) { Step(); yield return new Instruction { Kind = "charge", Line = e.Token.Line }; }
                    }
                    else if (e.Name == "speedCast")
                    {
                        double rank=(double)Eval(e.Args[0],scope);
                        if (rank < 1 || rank > 10 || rank != Math.Truncate(rank)) throw new InvalidOperationException("speedCast() aceita de 1 a 10.");
                        yield return new Instruction { Kind="speedCast",Line=e.Token.Line,Amount=(int)rank };
                    }
                    else if (e.Name == "cast")
                    {
                        var spell = (SpellValue)Eval(e.Left.Left, scope);
                        yield return new Instruction { Kind = "cast", Spell = spell.Name, Line = e.Token.Line };
                    }
                    else if (program.Methods.ContainsKey(e.Name))
                    {
                        if (program.Methods[e.Name].Type != "void")
                            throw new InvalidOperationException("Métodos com retorno precisam ser usados em uma variável, cast() ou return.");
                        foreach (var step in Run(program.Methods[e.Name], new Scope())) yield return step;
                    }
                    else Eval(e, scope);
                    break;
            }
        }

        object EvaluateValueMethod(Statement method)
        {
            bool returned;
            object value = EvaluateValueStatement(method, new Scope(), out returned);
            if (!returned) throw new InvalidOperationException("O método " + method.Name + " terminou sem retornar um valor.");
            return value;
        }

        object EvaluateValueStatement(Statement s, Scope scope, out bool returned)
        {
            Step(); returned = false;
            switch (s.Kind)
            {
                case "block":
                    var local = new Scope(scope);
                    foreach (Statement child in s.Children)
                    {
                        object value = EvaluateValueStatement(child, local, out returned);
                        if (returned) return value;
                    }
                    return null;
                case "declare": scope.Declare(s.Name, Eval(s.Value, scope)); return null;
                case "assign": scope.Set(s.Name, Eval(s.Value, scope)); return null;
                case "if":
                    Statement branch = (bool)Eval(s.Value, scope) ? s.Body : s.Else;
                    return branch == null ? null : EvaluateValueStatement(branch, scope, out returned);
                case "for":
                    var loop = new Scope(scope); loop.Declare(s.Name, 0d);
                    for (int i = 0; i < s.Count; i++)
                    {
                        loop.Set(s.Name, (double)i);
                        object value = EvaluateValueStatement(s.Body, loop, out returned);
                        if (returned) return value;
                    }
                    return null;
                case "return": returned = true; return Eval(s.Value, scope);
                default: throw new InvalidOperationException("Métodos com retorno não podem executar ações; retorne um valor.");
            }
        }

        object Eval(Expression e, Scope scope)
        {
            Step();
            switch (e.Kind)
            {
                case "literal":
                    if (e.Name == "true" || e.Name == "false") return e.Name == "true";
                    return double.Parse(e.Name, CultureInfo.InvariantCulture);
                case "name": return scope.Get(e.Name);
                case "member":
                    if (e.Name == "energia") return (double)world.Energy;
                    if (e.Name == "vida") return (double)world.Health;
                    return (double)world.EnemyCount;
                case "call":
                    if (program.Methods.TryGetValue(e.Name, out Statement method))
                    {
                        if (method.Type == "void") throw new InvalidOperationException("O método " + e.Name + " não retorna valor.");
                        return EvaluateValueMethod(method);
                    }
                    return new SpellValue { Name = e.Name };
                case "unary": return e.Name == "!" ? (object)!(bool)Eval(e.Left, scope) : -(double)Eval(e.Left, scope);
                case "binary":
                    object left = Eval(e.Left, scope);
                    if (e.Name == "&&") return (bool)left && (bool)Eval(e.Right, scope);
                    if (e.Name == "||") return (bool)left || (bool)Eval(e.Right, scope);
                    object right = Eval(e.Right, scope);
                    if (e.Name == "==") return left.Equals(right);
                    if (e.Name == "!=") return !left.Equals(right);
                    double a = (double)left, b = (double)right, value;
                    switch (e.Name)
                    {
                        case "<": return a < b; case ">": return a > b; case "<=": return a <= b; case ">=": return a >= b;
                        case "+": value = a + b; break; case "-": value = a - b; break; case "*": value = a * b; break;
                        case "/": if (b == 0) throw new InvalidOperationException("Divisão por zero."); value = a / b; break;
                        default: if (b == 0) throw new InvalidOperationException("Resto por zero."); value = a % b; break;
                    }
                    if (double.IsInfinity(value) || double.IsNaN(value) || Math.Abs(value) > 1000000)
                        throw new InvalidOperationException("Resultado numérico fora do limite de ±1000000.");
                    return e.Type == "int" ? Math.Truncate(value) : value;
                default: throw new InvalidOperationException("Expressão não executável.");
            }
        }
    }
}
