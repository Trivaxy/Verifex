// See https://aka.ms/new-console-template for more information

using Verifex;
using Verifex.Parser;

string program = "let three = (3 + 2) / 3;";
var tokenStream = new TokenStream(program);
var parser = new Parser(tokenStream);
var ast = parser.Statement();

Console.WriteLine(ast);