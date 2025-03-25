using System.Text;

namespace Verifex.Analysis;

public record CompileDiagnostic(Range Location, string Message, DiagnosticLevel Level)
{
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

public enum DiagnosticLevel
{
    Error,
    Warning,
}
