// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
using System.Text.Json;
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
string outputFileName = Path.GetFileNameWithoutExtension(filePath);
string outputPath = $"{outputFileName}.exe";

Console.WriteLine("Compiling...");

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

// Run the compiled assembly
var processInfo = new ProcessStartInfo
{
    FileName = "dotnet",
    Arguments = outputPath,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    UseShellExecute = false,
    CreateNoWindow = true
};

Console.WriteLine("Running...");

using (var process = Process.Start(processInfo))
{
    if (process == null)
    {
        Console.WriteLine("Error: Failed to start the process.");
        return;
    }

    string output = process.StandardOutput.ReadToEnd();
    string error = process.StandardError.ReadToEnd();
    
    process.WaitForExit();

    if (!string.IsNullOrEmpty(output))
        Console.WriteLine(output);
    
    if (!string.IsNullOrEmpty(error))
        Console.WriteLine($"Error: {error}");
    
    Console.WriteLine("----------------------------");
    Console.WriteLine($"Program exited with code: {process.ExitCode}");
}