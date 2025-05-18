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

    private VerificationContext RunPassesUntil<TPass>(ProgramNode ast, out VerificationPass[] passes)
        where TPass : VerificationPass
    {
        passes = VerificationPass.CreateRegularPasses(out var context);

        foreach (var pass in passes.TakeWhile(pass => pass is not TPass))
            pass.Run(ast);

        return context;
    }

    private VerificationContext RunAllPasses(ProgramNode ast, out VerificationPass[] passes)
    {
        passes = VerificationPass.CreateRegularPasses(out var context);

        foreach (var pass in passes)
            pass.Run(ast);

        return context;
    }

    private T AssertHasDiagnostic<T>(VerificationContext context) where T : CompileDiagnostic
    {
        var diagnostic = context.Diagnostics.FirstOrDefault(d => d is T);
        Assert.NotNull(diagnostic);
        return (T)diagnostic;
    }

    private T AssertHasDiagnostic<T>(VerificationContext context, Action<T> validator) where T : CompileDiagnostic
    {
        var diagnostic = context.Diagnostics.FirstOrDefault(d => d is T);
        Assert.NotNull(diagnostic);
        validator((T)diagnostic);
        return (T)diagnostic;
    }

    [Fact]
    public void TopLevelGatheringPass_DetectsDuplicateFunctionDeclarations()
    {
        var source = "fn test() {} fn test() {}";
        var ast = Parse(source);

        var passes = VerificationPass.CreateRegularPasses(out var context);
        var pass = passes.OfType<TopLevelGatheringPass>().First();
        pass.Run(ast);

        Assert.Single(context.Diagnostics);
        AssertHasDiagnostic<DuplicateTopLevelSymbol>(context, diagnostic =>
        {
            Assert.Equal("test", diagnostic.Name);
            Assert.Equal(DiagnosticLevel.Error, diagnostic.Level);
        });
    }

    [Fact]
    public void FirstBindingPass_DetectsUndefinedVariable()
    {
        var source = "fn test() { let x = y; }";
        var ast = Parse(source);

        var passes = VerificationPass.CreateRegularPasses(out var context);
        var gatheringPass = passes.OfType<TopLevelGatheringPass>().First();
        gatheringPass.Run(ast);

        var bindingPass = passes.OfType<FirstBindingPass>().First();
        bindingPass.Run(ast);

        Assert.Single(context.Diagnostics);
        AssertHasDiagnostic<UnknownIdentifier>(context, diagnostic =>
        {
            Assert.Equal("y", diagnostic.Name);
            Assert.Equal(DiagnosticLevel.Error, diagnostic.Level);
        });
    }

    [Fact]
    public void FirstBindingPass_DetectsRedeclaredVariable()
    {
        var source = "fn test() { let x = 1; let x = 2; }";
        var ast = Parse(source);

        var passes = VerificationPass.CreateRegularPasses(out var context);
        var gatheringPass = passes.OfType<TopLevelGatheringPass>().First();
        gatheringPass.Run(ast);

        var bindingPass = passes.OfType<FirstBindingPass>().First();
        bindingPass.Run(ast);

        Assert.Single(context.Diagnostics);
        AssertHasDiagnostic<VarNameAlreadyDeclared>(context, diagnostic =>
        {
            Assert.Equal("x", diagnostic.VarName);
            Assert.Equal(DiagnosticLevel.Error, diagnostic.Level);
        });
    }

    [Fact]
    public void TopLevelGatheringPass_GathersRefinedTypeSymbol()
    {
        var source = "type PositiveInt = Int where value > 0;";
        var ast = Parse(source);

        var passes = VerificationPass.CreateRegularPasses(out var context);
        var gatheringPass = passes.OfType<TopLevelGatheringPass>().First();
        gatheringPass.Run(ast);

        Assert.True(context.Symbols.TryLookupGlobalSymbol("PositiveInt", out var symbol));

        var refinedTypeSymbol = Assert.IsType<RefinedTypeSymbol>(symbol);
        Assert.Equal(ast.Nodes[0], refinedTypeSymbol.DeclaringNode);
        Assert.Equal("PositiveInt", refinedTypeSymbol.Name);
    }

    [Fact]
    public void FirstBindingPass_BindsValueIdentifierInRefinedType()
    {
        var source = "type PositiveInt = Int where value > 0;";
        var ast = Parse(source);

        var passes = VerificationPass.CreateRegularPasses(out var context);
        var gatheringPass = passes.OfType<TopLevelGatheringPass>().First();
        gatheringPass.Run(ast);

        var bindingPass = passes.OfType<FirstBindingPass>().First();
        bindingPass.Run(ast);

        var refinedTypeDeclNode = Assert.IsType<RefinedTypeDeclNode>(ast.Nodes[0]);
        var expression = Assert.IsType<BinaryOperationNode>(refinedTypeDeclNode.Expression);
        var valueIdentifierNode = Assert.IsType<IdentifierNode>(expression.Left);

        Assert.Equal("value", valueIdentifierNode.Identifier);

        var valueSymbol = Assert.IsType<RefinedTypeValueSymbol>(valueIdentifierNode.Symbol);
        Assert.Equal(refinedTypeDeclNode, valueSymbol.DeclaringNode);
    }

    [Fact]
    public void TypeAnnotationPass_DetectsUnknownType()
    {
        var source = "fn test() { let x: NonExistentType = 5; }";
        var ast = Parse(source);

        var context = RunPassesUntil<TypeAnnotationPass>(ast, out var passes);
        var typeAnnotationPass = passes.OfType<TypeAnnotationPass>().First();
        typeAnnotationPass.Run(ast);

        Assert.Single(context.Diagnostics);
        AssertHasDiagnostic<UnknownType>(context, diagnostic =>
        {
            Assert.Equal("NonExistentType", diagnostic.TypeName);
            Assert.Equal(DiagnosticLevel.Error, diagnostic.Level);
        });
    }

    [Fact]
    public void TypeMismatchPass_DetectsIncompatibleBinaryOperationTypes()
    {
        var source = """
            fn test() {
                let w = "foo";
                let x = 6;
                let y = 6.1;
                let z = true;
                let result1 = x + y; // ok (Int + Real)
                let result2 = x + z; // bad (Int + Bool)
                let result3 = x + w + z; // ok (Int + String + Bool) string concat
            }
            """;

        var ast = Parse(source);
        var context = RunPassesUntil<BasicTypeMismatchPass>(ast, out var passes);

        var typeMismatchPass = passes.OfType<BasicTypeMismatchPass>().First();
        typeMismatchPass.Run(ast);

        // Check that Int + Bool is detected
        AssertHasDiagnostic<TypeCannotDoArithmetic>(context, diagnostic =>
        {
            Assert.Equal("Bool", diagnostic.Type);
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
        var context = RunAllPasses(ast, out var passes);

        AssertHasDiagnostic<ReturnTypeMismatch>(context, diagnostic =>
        {
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
        var context = RunAllPasses(ast, out var passes);

        AssertHasDiagnostic<ParamTypeMismatch>(context, diagnostic =>
        {
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
        var context = RunAllPasses(ast, out var passes);

        AssertHasDiagnostic<NotEnoughArguments>(context, diagnostic =>
        {
            Assert.Equal("add", diagnostic.FunctionName);
            Assert.Equal(2, diagnostic.Expected);
            Assert.Equal(1, diagnostic.Actual);
            Assert.Equal(DiagnosticLevel.Error, diagnostic.Level);
        });

        AssertHasDiagnostic<TooManyArguments>(context, diagnostic =>
        {
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
        var context = RunAllPasses(ast, out var passes);

        AssertHasDiagnostic<VarDeclTypeMismatch>(context, diagnostic =>
        {
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
        var context = RunAllPasses(ast, out var passes);

        // Find all parameter type mismatch diagnostics
        var paramMismatches = context.Diagnostics
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
        var context = RunPassesUntil<FirstBindingPass>(ast, out var passes);

        var bindingPass = passes.OfType<FirstBindingPass>().First();
        bindingPass.Run(ast);

        var diagnostic = context.Diagnostics.OfType<UnknownIdentifier>().First();

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
        var context = RunAllPasses(ast, out var passes);

        // Find all TypeCannotDoBoolOps diagnostics
        var boolOpErrors = context.Diagnostics
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
        var context = RunAllPasses(ast, out var passes);

        // Find all TypeCannotDoComparison diagnostics
        var comparisonErrors = context.Diagnostics
            .Where(d => d is TypeCannotDoComparison or BinaryOpTypeMismatch)
            .ToList();

        Assert.Equal(3, comparisonErrors.Count(error => error is TypeCannotDoComparison));

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
        var context = RunAllPasses(ast, out var passes);

        // Find all BinaryOpTypeMismatch diagnostics for equality operators
        var typeMismatches = context.Diagnostics
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
        var context = RunAllPasses(ast, out var passes);

        // Find all ConditionMustBeBool diagnostics
        var conditionErrors = context.Diagnostics
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
        var context = RunAllPasses(ast, out var passes);

        // Find all ConditionMustBeBool diagnostics
        var conditionErrors = context.Diagnostics
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
        var context = RunAllPasses(ast, out var passes);

        // Find all ConditionMustBeBool diagnostics
        var conditionErrors = context.Diagnostics
            .OfType<ConditionMustBeBool>()
            .ToList();

        // Should have one error for (x + y) which is Int, not Bool
        Assert.Single(conditionErrors);
        Assert.Equal("if", conditionErrors[0].StatementType);
        Assert.Equal(DiagnosticLevel.Error, conditionErrors[0].Level);
    }

    [Fact]
    public void MutationCheckPass_DetectsImmutableVarReassignment()
    {
        var source = """
            fn test() {
                let x = 5;
                mut y = 10;
                
                x = 7;
                y = 15;
            }
            """;

        var ast = Parse(source);
        var context = RunAllPasses(ast, out var passes);

        // Find ImmutableVarReassignment diagnostics
        var reassignmentErrors = context.Diagnostics
            .OfType<ImmutableVarReassignment>()
            .ToList();

        // Should have one error for reassigning 'x'
        Assert.Single(reassignmentErrors);

        var error = reassignmentErrors[0];
        Assert.Equal("x", error.VarName);
        Assert.Equal(DiagnosticLevel.Error, error.Level);
    }

    [Fact]
    public void MutationCheckPass_AllowsMutableVarReassignment()
    {
        var source = """
            fn test() {
                mut counter = 0;
                
                counter = counter + 1;
                counter = 5;
            }
            """;

        var ast = Parse(source);
        var context = RunAllPasses(ast, out var passes);

        // Find ImmutableVarReassignment diagnostics
        var reassignmentErrors = context.Diagnostics
            .OfType<ImmutableVarReassignment>()
            .ToList();

        // Should have no errors for reassigning 'counter' since it's mutable
        Assert.Empty(reassignmentErrors);
    }

    [Fact]
    public void TypeMismatchPass_DetectsNonBooleanWhileCondition()
    {
        var source = """
            fn test() {
                let x = 5;
                let s = "hello";
                let b = true;
                
                while (b) { }
                while (x) { }
                while (s) { }
                while (x > 0) { }
            }
            """;

        var ast = Parse(source);
        var context = RunAllPasses(ast, out var passes);

        // Find all ConditionMustBeBool diagnostics for 'while' statements
        var conditionErrors = context.Diagnostics
            .OfType<ConditionMustBeBool>()
            .Where(e => e.StatementType == "while")
            .ToList();

        // Should have two errors: one for Int type and one for String type
        Assert.Equal(2, conditionErrors.Count);

        // Verify all are for 'while' statements
        Assert.All(conditionErrors, error => Assert.Equal("while", error.StatementType));

        // Verify diagnostic level
        Assert.All(conditionErrors, error => Assert.Equal(DiagnosticLevel.Error, error.Level));
    }

    [Fact]
    public void TopLevelGatheringPass_BindsStructDeclaration()
    {
        var source = """
            struct Person {
                name: String,
                age: Int,
                
                fn! new(name: String, age: Int) -> Person {
                    return Person { name: name, age: age };
                }
            }
            """;

        var ast = Parse(source);
        var passes = VerificationPass.CreateRegularPasses(out var context);

        var topLevelPass = passes.OfType<TopLevelGatheringPass>().First();
        topLevelPass.Run(ast);

        // Verify the struct symbol was created and is in the symbol table
        Assert.True(context.Symbols.TryLookupGlobalSymbol("Person", out var symbol));
        var structSymbol = Assert.IsType<StructSymbol>(symbol);

        // Verify struct has the correct fields
        Assert.Equal(2, structSymbol.Fields.Count);
        Assert.True(structSymbol.Fields.ContainsKey("name"));
        Assert.True(structSymbol.Fields.ContainsKey("age"));

        // Verify field symbols have correct names and types
        var nameField = structSymbol.Fields["name"];
        Assert.Equal("name", nameField.Name);

        var ageField = structSymbol.Fields["age"];
        Assert.Equal("age", ageField.Name);
        
        // Verify the function is bound correctly
        var newMethod = structSymbol.Methods["new"];
        Assert.Equal("new", newMethod.Name);
    }

    [Fact]
    public void TypeAnnotationPass_DetectsUnknownTypeInStructField()
    {
        var source = """
            struct Person {
                name: String,
                age: Int,
                address: NonExistentType
            }
            """;

        var ast = Parse(source);
        var context = RunPassesUntil<TypeAnnotationPass>(ast, out var passes);

        // Run the type annotation pass which should detect the unknown type
        var typeAnnotationPass = passes.OfType<TypeAnnotationPass>().First();
        typeAnnotationPass.Run(ast);

        // Verify there's a diagnostic for unknown type
        AssertHasDiagnostic<UnknownType>(context, diagnostic =>
        {
            Assert.Equal("NonExistentType", diagnostic.TypeName);
            Assert.Equal(DiagnosticLevel.Error, diagnostic.Level);
        });
    }

    [Fact]
    public void SecondBindingPass_BindsMemberAccess()
    {
        var source = """
            struct Person {
                name: String,
                age: Int
            }

            fn test() {
                let person: Person = true;
                let personName = person.name;
                let personAge = person.age;
                let nonExistentField = person.address;
            }
            """;

        var ast = Parse(source);
        var context = RunPassesUntil<BasicTypeMismatchPass>(ast, out var passes);

        // Verify error for non-existent field access
        AssertHasDiagnostic<UnknownStructField>(context, diagnostic =>
        {
            Assert.Equal("Person", diagnostic.StructTypeName);
            Assert.Equal("address", diagnostic.MemberIdentifier);
            Assert.Equal(DiagnosticLevel.Error, diagnostic.Level);
        });

        // Find all member access nodes in the AST
        var functionNode = ast.Nodes[1] as FunctionDeclNode;
        var memberAccessNodes = functionNode?.Body.Nodes
            .Where(n => n is VarDeclNode varDecl && varDecl.Value is MemberAccessNode)
            .Select(n => (n as VarDeclNode)!.Value as MemberAccessNode)
            .ToList();

        Assert.Equal(3, memberAccessNodes!.Count);

        // Verify successful field bindings
        var nameAccess = memberAccessNodes.First(n => n?.Member.Identifier == "name");
        Assert.NotNull(nameAccess.Symbol);
        Assert.Equal("name", nameAccess.Symbol?.Name);

        var ageAccess = memberAccessNodes.First(n => n?.Member.Identifier == "age");
        Assert.NotNull(ageAccess.Symbol);
        Assert.Equal("age", ageAccess.Symbol?.Name);
    }

    [Fact]
    public void SecondBindingPass_BindsInitializerFields()
    {
        var source = """
            struct Person {
                name: String,
                age: Int
            }

            fn test() {
                let person = Person { 
                    name: "John", 
                    age: 30 
                };
            }
            """;

        var ast = Parse(source);
        var passes = VerificationPass.CreateRegularPasses(out var context);

        var gatheringPass = passes.OfType<TopLevelGatheringPass>().First();
        gatheringPass.Run(ast);

        var firstBindingPass = passes.OfType<FirstBindingPass>().First();
        firstBindingPass.Run(ast);

        var typeAnnotationPass = passes.OfType<TypeAnnotationPass>().First();
        typeAnnotationPass.Run(ast);

        var secondBindingPass = passes.OfType<SecondBindingPass>().First();
        secondBindingPass.Run(ast);

        // Find the initializer node
        var functionNode = ast.Nodes[1] as FunctionDeclNode;
        var varDeclNode = functionNode?.Body.Nodes[0] as VarDeclNode;
        var initializerNode = varDeclNode?.Value as InitializerNode;

        Assert.NotNull(initializerNode);

        // Verify type binding
        Assert.NotNull(initializerNode.Type.Symbol);
        var typeSymbol = Assert.IsType<StructSymbol>(initializerNode.Type.Symbol);
        Assert.Equal("Person", typeSymbol.Name);

        // Verify field bindings
        var nameField = initializerNode.InitializerList.Values.First(f => f.Name.Identifier == "name");
        var ageField = initializerNode.InitializerList.Values.First(f => f.Name.Identifier == "age");

        Assert.NotNull(nameField.Name.Symbol);
        Assert.NotNull(ageField.Name.Symbol);

        var nameFieldSymbol = Assert.IsType<StructFieldSymbol>(nameField.Name.Symbol);
        var ageFieldSymbol = Assert.IsType<StructFieldSymbol>(ageField.Name.Symbol);

        Assert.Equal("name", nameFieldSymbol.Name);
        Assert.Equal("age", ageFieldSymbol.Name);
    }
}
