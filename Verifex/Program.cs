// See https://aka.ms/new-console-template for more information

using Verifex;
using Verifex.Analysis.Symbols;
using Verifex.CodeGen;
using Verifex.Parsing;

string program = """
fn add(x: Int, y: Int) -> Int {
    return x + y;
}

fn main() {
    let a: Int = 5;
    let b: Int = 10;
    let c: Int = add(a, b);
    print(c);
}
""";
var tokenStream = new TokenStream(program);
var parser = new Parser(tokenStream, program.AsMemory());
var ast = parser.Program();
var symbols = SymbolGatherer.Gather(ast);
var gen = new AssemblyGen(symbols);

gen.Visit(ast);
gen.Save("Generated.dll");