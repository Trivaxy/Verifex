// See https://aka.ms/new-console-template for more information

using Verifex;
using Verifex.CodeGen;
using Verifex.Parser;

string program = "fn test() { let a = 10 + 2; }";
var tokenStream = new TokenStream(program);
var parser = new Parser(tokenStream, program.AsMemory());
var ast = parser.Program();
var gen = new AssemblyGen();

gen.Visit(ast);
gen.Save("Generated.dll");