// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
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
    UseShellExecute = false,
    CreateNoWindow = true,
    RedirectStandardInput = true,
    RedirectStandardOutput = true,
    RedirectStandardError = true
};

Console.WriteLine("Running...");

using (var process = Process.Start(processInfo))
{
    if (process == null)
    {
        Console.WriteLine("Error: Failed to start the process.");
        return;
    }
    
    // Set up output reading
    process.OutputDataReceived += (sender, e) => {
        if (e.Data != null)
            Console.WriteLine(e.Data);
    };

    process.ErrorDataReceived += (sender, e) => {
        if (e.Data != null)
            Console.WriteLine(e.Data);
    };

    process.BeginOutputReadLine();
    process.BeginErrorReadLine();

    // Forward console input to the process until it exits
    while (!process.HasExited)
    {
        if (Console.KeyAvailable)
        {
            var key = Console.ReadKey(true);
            
            // Handle Enter key specially
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine(); // Echo the newline to console
                process.StandardInput.WriteLine(); // Send newline to process
            }
            else
            {
                Console.Write(key.KeyChar); // Echo other characters
                process.StandardInput.Write(key.KeyChar);
            }
            
            process.StandardInput.Flush();
        }
        
        Thread.Sleep(10); // Avoid busy waiting
    }
    
    Console.WriteLine("----------------------------");
    Console.WriteLine($"Program exited with code: {process.ExitCode}");
}