using System.Text;
using Verifex.Parsing;

namespace Verifex.Analysis;

public record CompileDiagnostic(DiagnosticLevel Level, string Message)
{
    public required Range Location { get; init; }
    
    public override string ToString()
        => $"{Location} {(Level == DiagnosticLevel.Error ? "error" : "warning")}: {Message}";

    public string GetMessage(ReadOnlySpan<char> source)
    {
        int lineStart = Location.Start.Value;
        while (lineStart > 0 && source[lineStart - 1] != '\n')
            lineStart--;
        
        int lineEnd = Location.End.Value;
        while (lineEnd < source.Length && source[lineEnd] != '\n')
            lineEnd++;
        
        ReadOnlySpan<char> line = source.Slice(lineStart, lineEnd - lineStart);
        
        int lineNumber = 1;
        for (int i = 0; i < lineStart; i++)
        {
            if (source[i] == '\n')
                lineNumber++;
        }
        
        int columnOffset = Location.Start.Value - lineStart;
        int underlineLength = Math.Max(1, Location.End.Value - Location.Start.Value);
        
        StringBuilder sb = new(128);
        sb.Append(Level == DiagnosticLevel.Error ? "error" : "warning")
          .Append(": ")
          .Append(Message)
          .Append('\n')
          .Append(lineNumber)
          .Append(" | ")
          .Append(line)
          .Append('\n')
          .Append(new string(' ', lineNumber.ToString().Length + 1))
          .Append("| ")
          .Append(' ', columnOffset)
          .Append('~', underlineLength);

        return sb.ToString();
    }
}

// Parser errors
public record ExpectedToken(string What)
    : CompileDiagnostic(DiagnosticLevel.Error, $"expected {What}");

public record UnexpectedToken(string What)
    : CompileDiagnostic(DiagnosticLevel.Error, $"unexpected token {What}");

// Verification errors

public record VarNameAlreadyDeclared(string VarName)
    : CompileDiagnostic(DiagnosticLevel.Error, $"variable '{VarName}' already declared");

public record ParameterAlreadyDeclared(string ParamName)
    : CompileDiagnostic(DiagnosticLevel.Error, $"parameter {ParamName} already declared");

public record UnknownIdentifier(string Name)
    : CompileDiagnostic(DiagnosticLevel.Error, $"unknown variable, function, or type {Name}");

public record DuplicateTopLevelSymbol(string Name)
    : CompileDiagnostic(DiagnosticLevel.Error, $"a top-level symbol with the same name {Name} already exists");

public record UnknownType(string TypeName)
    : CompileDiagnostic(DiagnosticLevel.Error, $"unknown type '{TypeName}'");

public record BinaryOpTypeMismatch(string Operator, string LeftType, string RightType)
    : CompileDiagnostic(DiagnosticLevel.Error, $"cannot apply operator '{Operator}' to types '{LeftType}' and '{RightType}'");

public record TypeCannotDoArithmetic(string Type)
    : CompileDiagnostic(DiagnosticLevel.Error, $"cannot do arithmetic on type '{Type}'");

public record TypeCannotDoBoolOps(string Type)
    : CompileDiagnostic(DiagnosticLevel.Error, $"cannot apply boolean operators on type '{Type}'");

public record TypeCannotDoComparison(string Type)
    : CompileDiagnostic(DiagnosticLevel.Error, $"cannot apply comparison operators on type '{Type}'");

public record ReturnTypeMismatch(string FunctionName, string ExpectedType, string ActualType)
    : CompileDiagnostic(DiagnosticLevel.Error, $"function '{FunctionName}' has return type '{ExpectedType}', but got '{ActualType}'");

public record VarDeclTypeMismatch(string VarName, string ExpectedType, string ActualType)
    : CompileDiagnostic(DiagnosticLevel.Error, $"variable '{VarName}' has type '{ExpectedType}', but got '{ActualType}'");

public record ParamTypeMismatch(string ParamName, string ExpectedType, string ActualType)
    : CompileDiagnostic(DiagnosticLevel.Error, $"parameter '{ParamName}' has type '{ExpectedType}', but got '{ActualType}'");

public record NotEnoughArguments(string FunctionName, int Expected, int Actual)
    : CompileDiagnostic(DiagnosticLevel.Error, $"function '{FunctionName}' expects {Expected} arguments, but got {Actual}");

public record TooManyArguments(string FunctionName, int Expected, int Actual)
    : CompileDiagnostic(DiagnosticLevel.Error, $"function '{FunctionName}' expects {Expected} arguments, but got {Actual}");

public record ConditionMustBeBool(string StatementType)
    : CompileDiagnostic(DiagnosticLevel.Error, $"condition in {StatementType} statement must be of type 'Bool'");

public record ImmutableVarReassignment(string VarName)
    : CompileDiagnostic(DiagnosticLevel.Error, $"cannot reassign to immutable variable '{VarName}'");

public record AssignmentTypeMismatch(string ExpectedType, string ActualType)
    : CompileDiagnostic(DiagnosticLevel.Error, $"cannot assign value of type '{ActualType}' to '{ExpectedType}'");

public record DuplicateMember(string MemberName)
    : CompileDiagnostic(DiagnosticLevel.Error, $"duplicate member '{MemberName}' in struct");

public record MemberAccessOnNonStruct(string Type, string MemberName)
    : CompileDiagnostic(DiagnosticLevel.Error, $"type '{Type}' does not have member '{MemberName}'");

public record UnknownStructField(string StructTypeName, string MemberIdentifier)
    : CompileDiagnostic(DiagnosticLevel.Error, $"struct '{StructTypeName}' does not have field '{MemberIdentifier}'");

public record TypeCannotHaveInitializer(string Type)
    : CompileDiagnostic(DiagnosticLevel.Error, $"type '{Type}' cannot have an initializer, as it is not a struct");

public record InitializerFieldTypeMismatch(string FieldName, string ExpectedType, string ActualType)
    : CompileDiagnostic(DiagnosticLevel.Error, $"field '{FieldName}' has type '{ExpectedType}', but got '{ActualType}'");

public record StaticFunctionOutsideStruct()
    : CompileDiagnostic(DiagnosticLevel.Error, "static functions cannot be defined outside of a struct");

public record IsCheckOnNonMaybeType()
    : CompileDiagnostic(DiagnosticLevel.Error, $"type checks can only be used maybe-types");

public record MemberAccessOnAmbiguousType(string FieldName, string TypeName)
    : CompileDiagnostic(DiagnosticLevel.Error, $"cannot access member '{FieldName}' on an ambiguous type '{TypeName}'");

public record NotAllPathsReturn(string FunctionName)
    : CompileDiagnostic(DiagnosticLevel.Error, $"function '{FunctionName}' does not return a value on all paths");

public record StructFieldNotInitialized(string TypeName, string FieldName)
    : CompileDiagnostic(DiagnosticLevel.Error, $"field '{FieldName}' for {TypeName} is missing");

public record NotAValue()
    : CompileDiagnostic(DiagnosticLevel.Error, "not a value");

public record NotAFunction()
    : CompileDiagnostic(DiagnosticLevel.Error, "not a function");

public enum DiagnosticLevel
{
    Error,
    Warning,
}

