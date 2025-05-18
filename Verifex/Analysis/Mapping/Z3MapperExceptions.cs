namespace Verifex.Analysis.Mapping;

public abstract class Z3MapperException(string message) : Exception(message);

public class SymbolNotBoundException(string symbolName) : Z3MapperException($"Symbol '{symbolName}' is not bound to a type");

public class CannotUseIsOnNonMaybeTypeException() : Z3MapperException("Cannot use 'is' operator on a non-maybe type");

public class MismatchedTypesException(string expectedType, string actualType) : Z3MapperException($"Expected type '{expectedType}', but got '{actualType}'");

public class MissingFieldException() : Z3MapperException("Missing field");