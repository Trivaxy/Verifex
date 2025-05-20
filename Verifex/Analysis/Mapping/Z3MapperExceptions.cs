namespace Verifex.Analysis.Mapping;

public abstract class Z3MapperException(string message) : Exception(message);

public class SymbolNotBoundException(string symbolName) : Z3MapperException($"Symbol '{symbolName}' is not bound to a type");

public class CannotUseIsOnNonMaybeTypeException() : Z3MapperException("Cannot use 'is' operator on a non-maybe type");

public class MismatchedTypesException(string expectedType, string actualType) : Z3MapperException($"Expected type '{expectedType}', but got '{actualType}'");

public class SymbolNotAValueException(string symbolName) : Z3MapperException($"Symbol '{symbolName}' is not a value");

public class NotComponentOfMaybeType(string type) : Z3MapperException($"{type} is not part of the maybe type");

public class NotSequence() : Z3MapperException("Not a sequence type");

public class InvalidIndexType(string type) : Z3MapperException($"Invalid index type {type}");

public class CannotAccessMember() : Z3MapperException("Type does not have members");

public class TypeHasNoField(string name) : Z3MapperException($"Type has no field '{name}'");

public class UnspecifiedField(string name) : Z3MapperException($"Field '{name}' is not specified");