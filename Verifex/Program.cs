// See https://aka.ms/new-console-template for more information

using Verifex;
using Verifex.Analysis.Symbols;
using Verifex.CodeGen;
using Verifex.Parsing;

string program = "fn test() { let a: Real = 10.0 + 2.0; let b: Real = a * (a - a); }";
var tokenStream = new TokenStream(program);
var parser = new Parser(tokenStream, program.AsMemory());
var ast = parser.Program();
var symbols = SymbolGatherer.Gather(ast);
var gen = new AssemblyGen(symbols);

gen.Visit(ast);
gen.Save("Generated.dll");