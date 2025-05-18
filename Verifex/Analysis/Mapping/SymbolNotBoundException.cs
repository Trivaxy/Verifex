namespace Verifex.Analysis.Mapping;

public class SymbolNotBoundException(string symbolName) : Exception($"Symbol '{symbolName}' is not bound to a type.");