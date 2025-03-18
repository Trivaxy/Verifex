using Verifex.CodeGen.Types;

namespace Verifex.Parsing.Nodes;

public abstract class AstNode
{
    public Range Location { get; set; }
    
    public VerifexType? Type { get; set; } // Set by the type checker
}