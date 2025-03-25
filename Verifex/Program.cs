// See https://aka.ms/new-console-template for more information

using Verifex;
using Verifex.Analysis;
using Verifex.Analysis.Symbols;
using Verifex.Analysis.Verification.Pass;
using Verifex.CodeGen;
using Verifex.Parsing;

string program = """
fn add(x: Int, y: Int) -> Int {
    return x + y;
}

fn main() {
    let a: Int = 5;
    let b: Int = 10;
    let b: Int = 20;
    let c: Int = add(a, b);
    print(c);
}
""";

var tokenStream = new TokenStream(program);
var parser = new Parser(tokenStream, program.AsMemory());
var ast = parser.Program();
var symbolTable = new SymbolTable();
var passes = VerificationPass.CreateRegularPasses(symbolTable);
var diagnostics = new List<CompileDiagnostic>();

foreach (var pass in passes)
{
    pass.Visit(ast);
    diagnostics.AddRange(pass.Diagnostics);
}

if (diagnostics.Count > 0)
{
    foreach (var diagnostic in diagnostics)
        Console.WriteLine(diagnostic.GetMessage(program.AsSpan()));
    return;
}

var gen = new AssemblyGen(symbolTable);

gen.Visit(ast);
gen.Save("Generated.exe");