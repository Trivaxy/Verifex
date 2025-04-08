using Verifex.Analysis;
using Verifex.Analysis.Pass;
using Verifex.Parsing;

namespace Verifex.Tests;

public class VerificationPassTests
{
    private ProgramNode Parse(string source)
    {
        var tokenStream = new TokenStream(source);
        var parser = new Parser(tokenStream, source.AsMemory());
        return parser.Program();
    }

    private SymbolTable RunPassesUntil<TPass>(ProgramNode ast, out VerificationPass[] passes) where TPass : VerificationPass
    {
        passes = VerificationPass.CreateRegularPasses(out SymbolTable symbols);
        
        foreach (var pass in passes.TakeWhile(pass => pass is not TPass))
            pass.Visit(ast);
            
        return symbols;
    }
    
    private SymbolTable RunAllPasses(ProgramNode ast, out VerificationPass[] passes)
    {
        passes = VerificationPass.CreateRegularPasses(out SymbolTable symbols);
        
        foreach (var pass in passes)
            pass.Visit(ast);
            
        return symbols;
    }
    
    private T AssertHasDiagnostic<T>(IEnumerable<VerificationPass> passes) where T : CompileDiagnostic
    {
        var diagnostic = passes
            .SelectMany(p => p.Diagnostics)
            .FirstOrDefault(d => d is T);
            
        Assert.NotNull(diagnostic);
        return (T)diagnostic;
    }
    
    private T AssertHasDiagnostic<T>(VerificationPass pass) where T : CompileDiagnostic
    {
        var diagnostic = pass.Diagnostics.FirstOrDefault(d => d is T);
        Assert.NotNull(diagnostic);
        return (T)diagnostic;
    }

    private static void AssertHasDiagnostic<T>(VerificationPass pass, Action<T> validator) where T : CompileDiagnostic
    {
        var diagnostic = pass.Diagnostics.FirstOrDefault(d => d is T);
        Assert.NotNull(diagnostic);
        validator((T)diagnostic);
    }

    private static void AssertHasDiagnostic<T>(IEnumerable<VerificationPass> passes, Action<T> validator) where T : CompileDiagnostic
    {
        var diagnostic = passes
            .SelectMany(p => p.Diagnostics)
            .FirstOrDefault(d => d is T);
            
        Assert.NotNull(diagnostic);
        validator((T)diagnostic);
    }

    [Fact]
    public void TopLevelGatheringPass_DetectsDuplicateFunctionDeclarations()
    {
        var source = "fn test() {} fn test() {}";
        var ast = Parse(source);
        
        var symbolTable = new SymbolTable();
        var pass = new TopLevelGatheringPass(symbolTable);
        pass.Visit(ast);
        
        Assert.Single(pass.Diagnostics);
        AssertHasDiagnostic<DuplicateTopLevelSymbol>(pass, diagnostic =>
        {
            Assert.Equal("test", diagnostic.Name);
            Assert.Equal(DiagnosticLevel.Error, diagnostic.Level);
        });
    }

    [Fact]
    public void BindingPass_DetectsUndefinedVariable()
    {
        var source = "fn test() { let x = y; }";
        var ast = Parse(source);
        
        var symbolTable = new SymbolTable();
        var gatheringPass = new TopLevelGatheringPass(symbolTable);
        gatheringPass.Visit(ast);
        
        var bindingPass = new BindingPass(symbolTable);
        bindingPass.Visit(ast);
        
        Assert.Single(bindingPass.Diagnostics);
        AssertHasDiagnostic<UnknownIdentifier>(bindingPass, diagnostic => {
            Assert.Equal("y", diagnostic.Name);
            Assert.Equal(DiagnosticLevel.Error, diagnostic.Level);
        });
    }

    [Fact]
    public void BindingPass_DetectsRedeclaredVariable()
    {
        var source = "fn test() { let x = 1; let x = 2; }";
        var ast = Parse(source);
        
        var symbolTable = new SymbolTable();
        var gatheringPass = new TopLevelGatheringPass(symbolTable);
        gatheringPass.Visit(ast);
        
        var bindingPass = new BindingPass(symbolTable);
        bindingPass.Visit(ast);
        
        Assert.Single(bindingPass.Diagnostics);
        AssertHasDiagnostic<VarNameAlreadyDeclared>(bindingPass, diagnostic => {
            Assert.Equal("x", diagnostic.VarName);
            Assert.Equal(DiagnosticLevel.Error, diagnostic.Level);
        });
    }

    [Fact]
    public void TypeAnnotationPass_DetectsUnknownType()
    {
        var source = "fn test() { let x: NonExistentType = 5; }";
        var ast = Parse(source);
        
        var symbolTable = SymbolTable.CreateDefaultTable();
        new TopLevelGatheringPass(symbolTable).Visit(ast);
        new BindingPass(symbolTable).Visit(ast);
        new PrimitiveTypeAnnotationPass(symbolTable).Visit(ast);
        
        var typeAnnotationPass = new TypeAnnotationPass(symbolTable);
        typeAnnotationPass.Visit(ast);
        
        Assert.Single(typeAnnotationPass.Diagnostics);
        AssertHasDiagnostic<UnknownType>(typeAnnotationPass, diagnostic => {
            Assert.Equal("NonExistentType", diagnostic.TypeName);
            Assert.Equal(DiagnosticLevel.Error, diagnostic.Level);
        });
    }

    [Fact]
    public void TypeMismatchPass_DetectsIncompatibleBinaryOperationTypes()
    {
        var source = """
        fn test() {
            let x = 6;
            let y = 6.1;
            let z = "hello";
            let result1 = x + y;
            let result2 = x + z;
        }
        """;
        
        var ast = Parse(source);
        var symbols = RunPassesUntil<TypeMismatchPass>(ast, out var passes);
        
        var typeMismatchPass = new TypeMismatchPass(symbols);
        typeMismatchPass.Visit(ast);
        
        // Check for the specific binary operation type mismatch (Int and Real)
        AssertHasDiagnostic<BinaryOpTypeMismatch>(typeMismatchPass, diagnostic => {
            Assert.Equal("+", diagnostic.Operator);
            Assert.Equal("Int", diagnostic.LeftType);
            Assert.Equal("Real", diagnostic.RightType);
            Assert.Equal(DiagnosticLevel.Error, diagnostic.Level);
        });
        
        // Check for the type that cannot do arithmetic (String)
        AssertHasDiagnostic<TypeCannotDoArithmetic>(typeMismatchPass, diagnostic => {
            Assert.Equal("String", diagnostic.Type);
            Assert.Equal(DiagnosticLevel.Error, diagnostic.Level);
        });
    }

    [Fact]
    public void TypeMismatchPass_DetectsWrongReturnType()
    {
        var source = """
        fn test() -> Int {
            return "hello";
        }
        """;
        
        var ast = Parse(source);
        RunAllPasses(ast, out var passes);
        
        AssertHasDiagnostic<ReturnTypeMismatch>(passes, diagnostic => {
            Assert.Equal("test", diagnostic.FunctionName);
            Assert.Equal("Int", diagnostic.ExpectedType);
            Assert.Equal("String", diagnostic.ActualType);
            Assert.Equal(DiagnosticLevel.Error, diagnostic.Level);
        });
    }

    [Fact]
    public void TypeMismatchPass_DetectsWrongFunctionArguments()
    {
        var source = """
        fn add(a: Int, b: Int) -> Int {
            return a + b;
        }
        
        fn test() {
            add("hello", 5);
        }
        """;
        
        var ast = Parse(source);
        RunAllPasses(ast, out var passes);
        
        AssertHasDiagnostic<ParamTypeMismatch>(passes, diagnostic => {
            Assert.Equal("a", diagnostic.ParamName);
            Assert.Equal("Int", diagnostic.ExpectedType);
            Assert.Equal("String", diagnostic.ActualType);
            Assert.Equal(DiagnosticLevel.Error, diagnostic.Level);
        });
    }

    [Fact]
    public void TypeMismatchPass_DetectsWrongArgumentCount()
    {
        var source = """
        fn add(a: Int, b: Int) -> Int {
            return a + b;
        }
        
        fn test() {
            add(5);
            add(5, 10, 15);
        }
        """;
        
        var ast = Parse(source);
        RunAllPasses(ast, out var passes);
        
        AssertHasDiagnostic<NotEnoughArguments>(passes, diagnostic => {
            Assert.Equal("add", diagnostic.FunctionName);
            Assert.Equal(2, diagnostic.Expected);
            Assert.Equal(1, diagnostic.Actual);
            Assert.Equal(DiagnosticLevel.Error, diagnostic.Level);
        });
        
        AssertHasDiagnostic<TooManyArguments>(passes, diagnostic => {
            Assert.Equal("add", diagnostic.FunctionName);
            Assert.Equal(2, diagnostic.Expected);
            Assert.Equal(3, diagnostic.Actual);
            Assert.Equal(DiagnosticLevel.Error, diagnostic.Level);
        });
    }

    [Fact]
    public void TypeMismatchPass_DetectsVarDeclTypeMismatch()
    {
        var source = """
        fn test() {
            let x: Int = "string value";
        }
        """;
        
        var ast = Parse(source);
        RunAllPasses(ast, out var passes);
        
        AssertHasDiagnostic<VarDeclTypeMismatch>(passes, diagnostic => {
            Assert.Equal("x", diagnostic.VarName);
            Assert.Equal("Int", diagnostic.ExpectedType);
            Assert.Equal("String", diagnostic.ActualType);
            Assert.Equal(DiagnosticLevel.Error, diagnostic.Level);
        });
    }

    [Fact]
    public void TypeMismatchPass_DetectsMultipleParameterMismatches()
    {
        var source = """
        fn calculate(a: Real, b: Int, c: String) -> Int {
            return a;
        }
        
        fn test() {
            calculate("wrong", 10, 5);
        }
        """;
        
        var ast = Parse(source);
        RunAllPasses(ast, out var passes);
        
        // Find all parameter type mismatch diagnostics
        var paramMismatches = passes
            .SelectMany(p => p.Diagnostics)
            .OfType<ParamTypeMismatch>()
            .ToList();
        
        // Should have two mismatches: a should be Real but is String, c should be String but is Int
        Assert.Equal(2, paramMismatches.Count);
        
        var firstParamMismatch = paramMismatches.First(d => d.ParamName == "a");
        Assert.Equal("Real", firstParamMismatch.ExpectedType);
        Assert.Equal("String", firstParamMismatch.ActualType);
        Assert.Equal(DiagnosticLevel.Error, firstParamMismatch.Level);
        
        var secondParamMismatch = paramMismatches.First(d => d.ParamName == "c");
        Assert.Equal("String", secondParamMismatch.ExpectedType);
        Assert.Equal("Int", secondParamMismatch.ActualType);
        Assert.Equal(DiagnosticLevel.Error, secondParamMismatch.Level);
    }

    [Fact]
    public void Diagnostics_ReportCorrectLocations()
    {
        var source = """
        fn test() {
            let x = 10;
            let y = undefinedVar;
        }
        """;
        
        var ast = Parse(source);
        var symbolTable = RunPassesUntil<BindingPass>(ast, out var passes);
        
        var bindingPass = passes.OfType<BindingPass>().First();
        bindingPass.Visit(ast);
        
        var diagnostic = bindingPass.Diagnostics.OfType<UnknownIdentifier>().First();
        
        // The diagnostic should be located at the undefinedVar identifier
        Assert.Equal("undefinedVar", diagnostic.Name);
        Assert.Equal(DiagnosticLevel.Error, diagnostic.Level);
        
        // The location should start at the beginning of 'undefinedVar'
        int undefinedVarPosition = source.IndexOf("undefinedVar");
        Assert.NotEqual(-1, undefinedVarPosition);
        
        // Verify that the error location points to the actual error position
        Assert.Equal(undefinedVarPosition, diagnostic.Location.Start.Value);
        Assert.Equal(undefinedVarPosition + "undefinedVar".Length, diagnostic.Location.End.Value);
    }

    [Fact]
    public void TypeMismatchPass_DetectsTypeCannotDoBoolOps()
    {
        var source = """
        fn test() {
            let x = 5;
            let y = "hello";
            let z = true;
            
            let result1 = x && z;
            let result2 = y || z;
        }
        """;
        
        var ast = Parse(source);
        RunAllPasses(ast, out var passes);
        
        // Find all TypeCannotDoBoolOps diagnostics
        var boolOpErrors = passes
            .SelectMany(p => p.Diagnostics)
            .OfType<TypeCannotDoBoolOps>()
            .ToList();
        
        // Should have two errors: one for Int type and one for String type
        Assert.Equal(2, boolOpErrors.Count);
        
        Assert.Contains(boolOpErrors, error => error.Type == "Int");
        Assert.Contains(boolOpErrors, error => error.Type == "String");
        
        // Verify diagnostic level
        Assert.All(boolOpErrors, error => Assert.Equal(DiagnosticLevel.Error, error.Level));
    }

    [Fact]
    public void TypeMismatchPass_DetectsTypeCannotDoComparison()
    {
        var source = """
        fn test() {
            let x = 5;
            let y = "hello";
            let z = "world";
            let b = true;
            
            let result1 = y < z;
            let result2 = b > x;
        }
        """;
        
        var ast = Parse(source);
        RunAllPasses(ast, out var passes);
        
        // Find all TypeCannotDoComparison diagnostics
        var comparisonErrors = passes
            .SelectMany(p => p.Diagnostics)
            .OfType<TypeCannotDoComparison>()
            .ToList();
        
        // Should have three errors: two for String type and one for Bool type
        Assert.Equal(3, comparisonErrors.Count);
        
        Assert.Equal(2, comparisonErrors.Count(error => error.Type == "String"));
        Assert.Equal(1, comparisonErrors.Count(error => error.Type == "Bool"));
        
        // Verify diagnostic level
        Assert.All(comparisonErrors, error => Assert.Equal(DiagnosticLevel.Error, error.Level));
    }

    [Fact]
    public void TypeMismatchPass_HandlesEqualityOperators()
    {
        var source = """
        fn test() {
            let a = 5;
            let b = 10;
            let c = "hello";
            let d = true;
            
            // These should be valid
            let r1 = a == b;
            let r2 = c == "world";
            let r3 = d != false;
            
            // These should produce errors
            let r4 = a == c;
            let r5 = b != d;
        }
        """;
        
        var ast = Parse(source);
        RunAllPasses(ast, out var passes);
        
        // Find all BinaryOpTypeMismatch diagnostics for equality operators
        var typeMismatches = passes
            .SelectMany(p => p.Diagnostics)
            .OfType<BinaryOpTypeMismatch>()
            .Where(d => d.Operator is "==" or "!=")
            .ToList();
        
        // Should have two errors: Int == String and Int != Bool
        Assert.Equal(2, typeMismatches.Count);
        
        var firstMismatch = typeMismatches.First(d => d.Operator == "==");
        Assert.Equal("Int", firstMismatch.LeftType);
        Assert.Equal("String", firstMismatch.RightType);
        
        var secondMismatch = typeMismatches.First(d => d.Operator == "!=");
        Assert.Equal("Int", secondMismatch.LeftType);
        Assert.Equal("Bool", secondMismatch.RightType);
        
        // Verify all diagnostics are errors
        Assert.All(typeMismatches, error => Assert.Equal(DiagnosticLevel.Error, error.Level));
    }

    [Fact]
    public void TypeMismatchPass_DetectsNonBooleanIfCondition()
    {
        var source = """
        fn test() {
            let x = 5;
            let s = "hello";
            if (true) {}
            if (x) {}
            if (s) {}
        }
        """;
        
        var ast = Parse(source);
        RunAllPasses(ast, out var passes);
        
        // Find all ConditionMustBeBool diagnostics
        var conditionErrors = passes
            .SelectMany(p => p.Diagnostics)
            .OfType<ConditionMustBeBool>()
            .ToList();
        
        // Should have two errors: one for Int type and one for String type
        Assert.Equal(2, conditionErrors.Count);
        
        // All errors should be for 'if' statements
        Assert.All(conditionErrors, error => Assert.Equal("if", error.StatementType));
        
        // Verify diagnostic level
        Assert.All(conditionErrors, error => Assert.Equal(DiagnosticLevel.Error, error.Level));
    }

    [Fact]
    public void TypeMismatchPass_ValidatesComplexIfConditions()
    {
        var source = """
        fn test() {
            let x = 5;
            let y = 10;
            let s = "hello";
            if (x > y) {}
            else if (x == 5 && y < 20) {}
            else {}

            if (x + y) {}
        }
        """;
        
        var ast = Parse(source);
        RunAllPasses(ast, out var passes);
        
        // Find all ConditionMustBeBool diagnostics
        var conditionErrors = passes
            .SelectMany(p => p.Diagnostics)
            .OfType<ConditionMustBeBool>()
            .ToList();
        
        // Should have one error for (x + y) which is Int, not Bool
        Assert.Single(conditionErrors);
        Assert.Equal("if", conditionErrors[0].StatementType);
        Assert.Equal(DiagnosticLevel.Error, conditionErrors[0].Level);
    }

    [Fact]
    public void TypeMismatchPass_NestedIfStatements()
    {
        var source = """
        fn test() {
            let x = 5;
            let y = 10;
            let flag = true;

            if (flag) {
                if (x < y) {}
            }
            
            if (x + y) {}
        }
        """;
        
        var ast = Parse(source);
        RunAllPasses(ast, out var passes);
        
        // Find all ConditionMustBeBool diagnostics
        var conditionErrors = passes
            .SelectMany(p => p.Diagnostics)
            .OfType<ConditionMustBeBool>()
            .ToList();
        
        // Should have one error for (x + y) which is Int, not Bool
        Assert.Single(conditionErrors);
        Assert.Equal("if", conditionErrors[0].StatementType);
        Assert.Equal(DiagnosticLevel.Error, conditionErrors[0].Level);
    }
}
