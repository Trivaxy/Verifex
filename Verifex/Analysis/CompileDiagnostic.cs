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
          .Append("  | ")
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

public record InvalidFunctionCall()
    : CompileDiagnostic(DiagnosticLevel.Error, "function call must be an identifier");

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

public enum DiagnosticLevel
{
    Error,
    Warning,
}

