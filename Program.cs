// See https://aka.ms/new-console-template for more information

using Verifex;

string program = "let foobar = 992;";

var tokenStream = new TokenStream(program);

while (tokenStream.Peek().Type is not TokenType.EOF)
{
    var token = tokenStream.Next();
    Console.WriteLine($"[{token.Type}: {token.Start}..{token.End}] {program[token.Start..token.End]}");
}