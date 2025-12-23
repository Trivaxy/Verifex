// See https://aka.ms/new-console-template for more information

using Verifex.Analysis;
using Verifex.Analysis.Pass;
using Verifex.CodeGen;
using Verifex.Parsing;

if (args.Length < 1)
{
    Console.WriteLine("Usage: Verifex <file>");
    return;
}

string filePath = args[0];
if (!File.Exists(filePath))
{
    Console.WriteLine($"Error: File '{filePath}' does not exist.");
    return;
}

string program = File.ReadAllText(filePath);
const string builtinPrelude = @"type NonNegativeReal = Real where value >= 0.0;
type PositiveReal = Real where value > 0.0;
type NonPositiveReal = Real where value <= 0.0;
type NegativeReal = Real where value < 0.0;

type NonNegativeInt = Int where value >= 0;
type PositiveInt = Int where value > 0;
type NonPositiveInt = Int where value <= 0;
type NegativeInt = Int where value < 0;
";
if (!program.EndsWith("\n", StringComparison.Ordinal))
    program += "\n";
program += builtinPrelude;
string outputFileName = Path.GetFileNameWithoutExtension(filePath);
string outputPath = $"{outputFileName}.exe";

Console.WriteLine("Compiling...");

var tokenStream = new TokenStream(program);
var parser = new Parser(tokenStream, program.AsMemory());
var ast = parser.Program();
var passes = VerificationPass.CreateRegularPasses(out VerificationContext context);
var diagnostics = parser.Diagnostics.ToList();

foreach (var pass in passes)
    pass.Run(ast);

diagnostics.AddRange(context.Diagnostics);

if (diagnostics.Count > 0)
{
    foreach (var diagnostic in diagnostics)
        Console.WriteLine(diagnostic.GetMessage(program.AsSpan()));
    return;
}

var gen = new AssemblyGen(context.Symbols);
gen.Consume(ast);
gen.Save(outputPath);

// Generate the runtime config file
string runtimeConfigPath = $"{outputFileName}.runtimeconfig.json";
string runtimeConfigContent = @"{
  ""runtimeOptions"": {
    ""tfm"": ""net9.0"",
    ""framework"": {
      ""name"": ""Microsoft.NETCore.App"",
      ""version"": ""9.0.0""
    }
  }
}";
File.WriteAllText(runtimeConfigPath, runtimeConfigContent);

Console.WriteLine($"Successfully compiled {filePath} to {outputPath}");
