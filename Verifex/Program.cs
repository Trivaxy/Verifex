// See https://aka.ms/new-console-template for more information

using Verifex.Analysis;
using Verifex.Analysis.Pass;
using Verifex.CodeGen;
using Verifex.Parsing;

string program = """
fn add(x: Int, y: Int) -> Int {
    return x + y;
}

fn main() {
    let a: Int = 5;
    let b: Int = 2;
    let c: Int = add(a, b);
    print(c);
}
""";

var tokenStream = new TokenStream(program);
var parser = new Parser(tokenStream, program.AsMemory());
var ast = parser.Program();
var passes = VerificationPass.CreateRegularPasses(out SymbolTable symbols);
var diagnostics = parser.Diagnostics.ToList();

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

var gen = new AssemblyGen(symbols);
gen.Consume(ast);
gen.Save("Generated.exe");