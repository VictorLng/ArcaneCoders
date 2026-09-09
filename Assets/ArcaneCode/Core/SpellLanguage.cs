using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ArcaneCode.Core
{
    public sealed class Diagnostic
    {
        public readonly int Line, Column;
        public readonly string Message;
        public Diagnostic(int line, int column, string message) { Line = line; Column = column; Message = message; }
        public override string ToString() => $"Linha {Line}, coluna {Column}: {Message}";
    }

    public sealed class CompileOptions
    {
        public string ClassName = "MagoDeFogo";
        public int Budget = 10;
        public HashSet<string> Spells = new HashSet<string> { "fireball" };
        public bool SpeedCastUnlocked;
    }

    public sealed class CompileResult
    {
        public SpellProgram Program;
        public Diagnostic Error;
        public int Cost;
        public bool Success => Program != null;
    }

    public sealed class SpellProgram
    {
        internal Dictionary<string, Statement> Methods;
        public string Source { get; internal set; }
        public int Cost { get; internal set; }
    }

    internal sealed class LanguageError : Exception
    {
        public readonly Token Token;
        public LanguageError(Token token, string message) : base(message) { Token = token; }
    }

    internal struct Token
    {
        public string Text;
        public int Line, Column;
        public Token(string text, int line, int column) { Text = text; Line = line; Column = column; }
    }

    internal sealed class Expression
    {
        public Token Token;
        public string Kind, Name, Type;
        public Expression Left, Right;
        public List<Expression> Args = new List<Expression>();
    }

    internal sealed class Statement
    {
        public Token Token;
        public string Kind, Name, Type;
        public Expression Value;
        public Statement Body, Else;
        public int Count;
        public List<Statement> Children = new List<Statement>();
    }

    public static class SpellCompiler
    {
        public static readonly Dictionary<string, string> SpellTypes = new Dictionary<string, string>
        { { "fireball", "Fireball" }, { "flameWave", "FlameWave" }, { "icebolt", "Icebolt" }, { "frostNova", "FrostNova" } };

        public static string Starter(string className)
        {
            bool fire = className == "MagoDeFogo";
            return "class " + className + " extends Mago {\n    void attackOne() {\n        " + (fire ? "Fireball bola = this.fireball();" : "Icebolt bola = this.icebolt();") + "\n        bola.cast();\n    }\n}";
        }

        public static string Charged(string className)
        {
            string type = className == "MagoDeFogo" ? "Fireball" : "Icebolt";
            string method = className == "MagoDeFogo" ? "fireball" : "icebolt";
            return "class " + className + " extends Mago {\n    void attackOne() {\n        for (int i = 0; i < 3; i++) {\n            this.charge(1);\n        }\n        if (this.energia >= 3) {\n            " + type + " bola = this." + method + "();\n            bola.cast();\n        }\n    }\n}";
        }

        public static CompileResult Compile(string source, CompileOptions options)
        {
            var result = new CompileResult();
            if (source == null || source.Length > 12000)
            { result.Error = new Diagnostic(1, 1, "O programa deve ter até 12000 caracteres."); return result; }
            try
            {
                var parser = new Parser(Lex(source));
                var methods = parser.Parse(options.ClassName);
                var checker = new Checker(methods, options);
                checker.Validate();
                result.Cost = checker.MethodCost("attackOne", new HashSet<string>(), 0);
                // Unused helpers still occupy equipment; used helpers are expanded at every call site.
                foreach (string name in methods.Keys.Where(n => n != "attackOne" && !checker.Reachable.Contains(n)))
                    result.Cost += checker.MethodCost(name, new HashSet<string>(), 0);
                if (result.Cost > options.Budget)
                    throw new LanguageError(new Token("", 1, 1), $"Complexidade {result.Cost}/{options.Budget}. Simplifique o código ou obtenha mais pontos.");
                result.Program = new SpellProgram { Methods = methods, Source = source, Cost = result.Cost };
            }
            catch (LanguageError e) { result.Error = new Diagnostic(e.Token.Line, e.Token.Column, e.Message); }
            return result;
        }

        static List<Token> Lex(string source)
        {
            var tokens = new List<Token>();
            int i = 0, line = 1, col = 1;
            while (i < source.Length)
            {
                char c = source[i];
                if (c == '\n') { i++; line++; col = 1; continue; }
                if (char.IsWhiteSpace(c)) { i++; col++; continue; }
                if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
                { while (i < source.Length && source[i] != '\n') { i++; col++; } continue; }
                int start = i, column = col;
                if (char.IsLetter(c) || c == '_')
                    while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_')) { i++; col++; }
                else if (char.IsDigit(c))
                {
                    bool dot = false;
                    while (i < source.Length && (char.IsDigit(source[i]) || (!dot && source[i] == '.')))
                    { if (source[i] == '.') dot = true; i++; col++; }
                }
                else
                {
                    string pair = i + 1 < source.Length ? source.Substring(i, 2) : "";
                    if (new[] { "==", "!=", "<=", ">=", "&&", "||", "++" }.Contains(pair)) { i += 2; col += 2; }
                    else if ("{}();,.=+-*/%!<>".Contains(c)) { i++; col++; }
                    else throw new LanguageError(new Token(c.ToString(), line, col), "Caractere não reconhecido.");
                }
                tokens.Add(new Token(source.Substring(start, i - start), line, column));
                if (tokens.Count > 2500) throw new LanguageError(tokens.Last(), "Programa muito grande: máximo de 2500 tokens.");
            }
            tokens.Add(new Token("<fim>", line, col));
            return tokens;
        }

        sealed class Parser
        {
            readonly List<Token> tokens;
            int pos, depth;
            Token Current => tokens[pos];
            Token Next => tokens[Math.Min(pos + 1, tokens.Count - 1)];
            static readonly HashSet<string> Types = new HashSet<string> { "int", "float", "bool", "var", "Fireball", "FlameWave", "Icebolt", "FrostNova" };
            public Parser(List<Token> tokens) { this.tokens = tokens; }
            Token Take() { Token t = Current; if (pos < tokens.Count - 1) pos++; return t; }
            bool Match(string s) { if (Current.Text != s) return false; Take(); return true; }
            Token Expect(string s)
            { if (Current.Text != s) throw new LanguageError(Current, $"Esperado '{s}', encontrado '{Current.Text}'."); return Take(); }
            Token Identifier()
            {
                Token t = Take();
                if (!(char.IsLetter(t.Text[0]) || t.Text[0] == '_') || new[] { "this", "class", "if", "else", "for", "return", "true", "false", "void", "extends" }.Contains(t.Text) || Types.Contains(t.Text))
                    throw new LanguageError(t, "Esperado um nome de variável ou método.");
                return t;
            }
            void Enter() { if (++depth > 32) throw new LanguageError(Current, "Expressão ou bloco aninhado demais (máximo 32)."); }
            public Dictionary<string, Statement> Parse(string className)
            {
                Expect("class"); Token cls = Take();
                if (cls.Text != className) throw new LanguageError(cls, "Esta tentativa usa a classe " + className + ".");
                Expect("extends"); Expect("Mago"); Expect("{");
                var methods = new Dictionary<string, Statement>();
                while (Current.Text != "}" && Current.Text != "<fim>")
                {
                    Token returnType = Take();
                    if ((returnType.Text != "void" && !Types.Contains(returnType.Text)) || returnType.Text == "var")
                        throw new LanguageError(returnType, "Esperado void ou um tipo de retorno válido.");
                    Token name = Identifier(); Expect("("); Expect(")");
                    if (methods.ContainsKey(name.Text) || SpellTypes.ContainsKey(name.Text) || name.Text == "charge" || name.Text == "speedCast")
                        throw new LanguageError(name, "Nome de método duplicado ou reservado pelo kernel.");
                    Statement method = Block(); method.Name = name.Text; method.Type = returnType.Text; methods.Add(name.Text, method);
                    if (methods.Count > 16) throw new LanguageError(name, "Máximo de 16 métodos.");
                }
                Expect("}"); Expect("<fim>");
                if (!methods.ContainsKey("attackOne")) throw new LanguageError(cls, "Declare void attackOne() para iniciar os ataques.");
                return methods;
            }
            Statement Block()
            {
                Enter(); var s = new Statement { Kind = "block", Token = Expect("{") };
                while (Current.Text != "}" && Current.Text != "<fim>") s.Children.Add(ParseStatement());
                Expect("}"); depth--; return s;
            }
            Statement ParseStatement()
            {
                Token t = Current;
                if (t.Text == "{") return Block();
                if (Match("return"))
                {
                    Expression value = null;
                    if (Current.Text != ";") value = Expr();
                    Expect(";"); return new Statement { Kind = "return", Token = t, Value = value };
                }
                if (Match("if"))
                {
                    Expect("("); Expression condition = Expr(); Expect(")");
                    var s = new Statement { Kind = "if", Token = t, Value = condition, Body = Block() };
                    if (Match("else")) s.Else = Block(); return s;
                }
                if (Match("for"))
                {
                    Expect("("); Expect("int"); string name = Identifier().Text; Expect("="); Expect("0"); Expect(";");
                    Expect(name); Expect("<"); Token limit = Take();
                    if (!int.TryParse(limit.Text, out int count) || count < 1 || count > 10)
                        throw new LanguageError(limit, "Use um limite inteiro literal de 1 a 10 no for.");
                    Expect(";"); Expect(name); Expect("++"); Expect(")");
                    return new Statement { Kind = "for", Token = t, Name = name, Count = count, Body = Block() };
                }
                if (Types.Contains(t.Text))
                {
                    Take(); string name = Identifier().Text; Expect("="); Expression value = Expr(); Expect(";");
                    return new Statement { Kind = "declare", Token = t, Type = t.Text, Name = name, Value = value };
                }
                if (Next.Text == "=")
                {
                    string name = Identifier().Text; Expect("="); Expression value = Expr(); Expect(";");
                    return new Statement { Kind = "assign", Token = t, Name = name, Value = value };
                }
                Expression e = Expr(); Expect(";"); return new Statement { Kind = "expr", Token = t, Value = e };
            }
            static int Precedence(string op)
            {
                switch (op) { case "||": return 1; case "&&": return 2; case "==": case "!=": return 3;
                    case "<": case ">": case "<=": case ">=": return 4; case "+": case "-": return 5;
                    case "*": case "/": case "%": return 6; default: return 0; }
            }
            Expression Expr(int min = 1)
            {
                Enter(); Expression left = Primary();
                while (Precedence(Current.Text) >= min)
                { Token op = Take(); left = new Expression { Kind = "binary", Token = op, Name = op.Text, Left = left, Right = Expr(Precedence(op.Text) + 1) }; }
                depth--; return left;
            }
            Expression Primary()
            {
                Token t = Take(); Expression e;
                if (t.Text == "(" ) { e = Expr(); Expect(")"); }
                else if (t.Text == "!" || t.Text == "-")
                { Enter(); e = new Expression { Kind = "unary", Token = t, Name = t.Text, Left = Primary() }; depth--; }
                else if (t.Text == "true" || t.Text == "false" || char.IsDigit(t.Text[0]))
                    e = new Expression { Kind = "literal", Name = t.Text, Token = t };
                else if (char.IsLetter(t.Text[0]) || t.Text[0] == '_') e = new Expression { Kind = "name", Name = t.Text, Token = t };
                else throw new LanguageError(t, "Esperada uma expressão.");
                while (Current.Text == "." || Current.Text == "(")
                {
                    if (Match(".")) { Token member = Identifier(); e = new Expression { Kind = "member", Token = member, Name = member.Text, Left = e }; }
                    else
                    {
                        Expect("("); var call = new Expression { Kind = "call", Token = e.Token, Left = e, Name = e.Name };
                        if (Current.Text != ")") { do { call.Args.Add(Expr()); } while (Match(",")); }
                        Expect(")"); e = call;
                    }
                }
                return e;
            }
        }

        sealed class Checker
        {
            readonly Dictionary<string, Statement> methods;
            readonly CompileOptions options;
            public readonly HashSet<string> Reachable = new HashSet<string>();
            public Checker(Dictionary<string, Statement> methods, CompileOptions options) { this.methods = methods; this.options = options; }
            static bool Number(string t) => t == "int" || t == "float";
            static bool Assignable(string target, string value) => target == value || target == "float" && value == "int";
            static void Require(bool condition, Token token, string message) { if (!condition) throw new LanguageError(token, message); }
            static int SpeedCastRank(Expression call)
            {
                Require(call.Args.Count == 1 && call.Args[0].Kind == "literal" && !call.Args[0].Name.Contains("."), call.Token, "speedCast(int nivel) exige um inteiro literal de 1 a 10.");
                Require(int.TryParse(call.Args[0].Name, out int rank) && rank >= 1 && rank <= 10, call.Token, "speedCast(int nivel) aceita de 1 a 10.");
                return rank;
            }
            public void Validate()
            {
                foreach (var method in methods)
                {
                    bool returned = CheckStatement(method.Value, new Dictionary<string, string>(), method.Value.Type);
                    Require(method.Value.Type == "void" || returned, method.Value.Token, "O método " + method.Key + " precisa retornar " + method.Value.Type + " em todos os caminhos.");
                    MethodCost(method.Key, new HashSet<string>(), 0);
                }
                Reachable.Clear();
            }
            bool CheckStatement(Statement s, Dictionary<string, string> scope, string returnType)
            {
                if (s.Kind == "block")
                {
                    var local = new Dictionary<string, string>(scope);
                    bool returned = false;
                    foreach (var child in s.Children)
                    {
                        if (returned) throw new LanguageError(child.Token, "Código inalcançável após return.");
                        returned = CheckStatement(child, local, returnType);
                    }
                    return returned;
                }
                if (s.Kind == "for")
                {
                    Require(!scope.ContainsKey(s.Name), s.Token, "O contador já existe neste escopo.");
                    var local = new Dictionary<string, string>(scope) { [s.Name] = "int" };
                    return CheckStatement(s.Body, local, returnType);
                }
                if (s.Kind == "return")
                {
                    if (returnType == "void")
                    {
                        Require(s.Value == null, s.Token, "Um método void usa return; sem valor.");
                    }
                    else
                    {
                        Require(s.Value != null, s.Token, "Retorne um valor do tipo " + returnType + ".");
                        string returnedType = TypeOf(s.Value, scope);
                        Require(Assignable(returnType, returnedType), s.Token, "Não é possível retornar " + returnedType + " de um método " + returnType + ".");
                    }
                    return true;
                }
                string type = TypeOf(s.Value, scope);
                if (s.Kind == "declare")
                {
                    Require(!scope.ContainsKey(s.Name), s.Token, "Variável já declarada.");
                    if (s.Type == "var") s.Type = type;
                    Require(type != "void" && type != "Mago" && Assignable(s.Type, type), s.Token, $"Não é possível atribuir {type} a {s.Type}."); scope.Add(s.Name, s.Type); return false;
                }
                else if (s.Kind == "assign")
                {
                    Require(scope.ContainsKey(s.Name), s.Token, "Variável não declarada.");
                    Require(Assignable(scope[s.Name], type), s.Token, "Tipos incompatíveis na atribuição."); return false;
                }
                else if (s.Kind == "if")
                {
                    Require(type == "bool", s.Token, "A condição do if precisa ser bool.");
                    bool thenReturns = CheckStatement(s.Body, scope, returnType);
                    bool elseReturns = s.Else != null && CheckStatement(s.Else, scope, returnType);
                    return thenReturns && elseReturns;
                }
                else
                {
                    Require(s.Value.Kind == "call", s.Token, "Use uma chamada de método como instrução.");
                    Require(type == "void", s.Token, "Use o valor retornado por " + s.Value.Name + " em uma variável, cast() ou return.");
                    Require(returnType == "void", s.Token, "Métodos com retorno só podem calcular e retornar um valor; lance a magia em attackOne().");
                    return false;
                }
            }
            string TypeOf(Expression e, Dictionary<string, string> scope)
            {
                string t;
                switch (e.Kind)
                {
                    case "literal":
                        if (e.Name == "true" || e.Name == "false") t = "bool";
                        else
                        {
                            Require(double.TryParse(e.Name, NumberStyles.Float, CultureInfo.InvariantCulture, out double n) && !double.IsInfinity(n) && Math.Abs(n) <= 1000000, e.Token, "Número inválido ou maior que 1000000.");
                            t = e.Name.Contains(".") ? "float" : "int";
                        }
                        break;
                    case "name":
                        if (e.Name == "this") t = "Mago";
                        else { Require(scope.ContainsKey(e.Name), e.Token, "Variável não declarada: " + e.Name); t = scope[e.Name]; }
                        break;
                    case "member":
                        Require(TypeOf(e.Left, scope) == "Mago", e.Token, "Propriedade não disponível.");
                        Require(new[] { "energia", "vida", "inimigos" }.Contains(e.Name), e.Token, "Propriedade não disponível: " + e.Name);
                        t = e.Name == "vida" ? "float" : "int"; break;
                    case "unary":
                        t = TypeOf(e.Left, scope);
                        Require(e.Name == "!" ? t == "bool" : Number(t), e.Token, "Operador incompatível com o tipo."); break;
                    case "binary":
                        string a = TypeOf(e.Left, scope), b = TypeOf(e.Right, scope);
                        if (e.Name == "&&" || e.Name == "||") { Require(a == "bool" && b == "bool", e.Token, "Use operandos bool."); t = "bool"; }
                        else if (e.Name == "==" || e.Name == "!=") { Require((Number(a) && Number(b)) || a == "bool" && b == "bool", e.Token, "Comparação incompatível."); t = "bool"; }
                        else { Require(Number(a) && Number(b), e.Token, "Use operandos numéricos."); t = new[] { "<", ">", "<=", ">=" }.Contains(e.Name) ? "bool" : a == "float" || b == "float" ? "float" : "int"; }
                        break;
                    case "call":
                        Expression receiver = e.Left.Kind == "member" ? e.Left.Left : null;
                        string owner = receiver == null ? "Mago" : TypeOf(receiver, scope);
                        if (e.Name == "cast")
                        { Require(SpellTypes.Values.Contains(owner), e.Token, "cast() exige um objeto de magia."); Require(e.Args.Count == 0, e.Token, "cast() não recebe argumentos."); t = "void"; }
                        else
                        {
                            Require(owner == "Mago", e.Token, "Método não disponível neste objeto.");
                            if (e.Name == "charge")
                            { Require(e.Args.Count == 1 && TypeOf(e.Args[0], scope) == "int", e.Token, "charge(int unidades) exige um inteiro."); t = "void"; }
                            else if (e.Name == "speedCast")
                            {
                                Require(options.SpeedCastUnlocked, e.Token, "speedCast() ainda não foi desbloqueado para a família Mago.");
                                SpeedCastRank(e); t = "void";
                            }
                            else if (SpellTypes.TryGetValue(e.Name, out t))
                            { Require(options.Spells.Contains(e.Name), e.Token, "Magia não desbloqueada: " + e.Name); Require(e.Args.Count == 0, e.Token, "A magia não recebe argumentos."); }
                            else
                            { Require(methods.ContainsKey(e.Name), e.Token, "Método não encontrado: " + e.Name); Require(e.Args.Count == 0, e.Token, "Métodos auxiliares não recebem argumentos."); t = methods[e.Name].Type; }
                        }
                        break;
                    default: throw new LanguageError(e.Token, "Expressão inválida.");
                }
                e.Type = t; return t;
            }
            public int MethodCost(string name, HashSet<string> stack, int loops)
            {
                Require(stack.Add(name), methods[name].Token, "Recursão não é permitida: " + name);
                Reachable.Add(name); int cost = StatementCost(methods[name], stack, loops); stack.Remove(name); return cost;
            }
            int StatementCost(Statement s, HashSet<string> stack, int loops)
            {
                int cost = s.Kind == "for" ? 3 : s.Kind == "if" ? 2 : 0;
                if (s.Kind == "for") Require(++loops <= 2, s.Token, "Máximo de dois loops aninhados, incluindo métodos auxiliares.");
                if (s.Value != null) cost += ExpressionCost(s.Value, stack, loops);
                foreach (var child in s.Children) cost += StatementCost(child, stack, loops);
                if (s.Body != null) cost += StatementCost(s.Body, stack, loops);
                if (s.Else != null) cost += StatementCost(s.Else, stack, loops);
                Require(cost <= 10000, s.Token, "Programa complexo demais."); return cost;
            }
            int ExpressionCost(Expression e, HashSet<string> stack, int loops)
            {
                int cost = 0;
                if (e.Kind == "call")
                {
                    if (methods.ContainsKey(e.Name)) cost += MethodCost(e.Name, stack, loops);
                    else if (SpellTypes.ContainsKey(e.Name)) cost += 2;
                    else if (e.Name == "charge") cost++;
                    else if (e.Name == "speedCast") cost += SpeedCastRank(e) * 3;
                }
                if (e.Left != null) cost += ExpressionCost(e.Left, stack, loops);
                if (e.Right != null) cost += ExpressionCost(e.Right, stack, loops);
                foreach (var arg in e.Args) cost += ExpressionCost(arg, stack, loops);
                return cost;
            }
        }
    }
}
