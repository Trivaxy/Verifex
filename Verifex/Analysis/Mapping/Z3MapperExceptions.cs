namespace Verifex.Analysis.Mapping;

public abstract class Z3MapperException(string message) : Exception(message);

public class SymbolNotBoundException(string symbolName) : Z3MapperException($"Symbol '{symbolName}' is not bound to a type");

public class CannotUseIsOnNonMaybeTypeException() : Z3MapperException("Cannot use 'is' operator on a non-maybe type");