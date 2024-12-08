// See https://aka.ms/new-console-template for more information

using Verifex;
using Verifex.Parser;

string program = "abc + 8 * 9 / 3;";
var tokenStream = new TokenStream(program);
var parser = new Parser(tokenStream);
var ast = parser.Expression(0);

Console.WriteLine(ast);