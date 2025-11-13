# Verifex Compiler Codebase Guide

## Overview

Verifex is an experimental programming language built on the .NET platform. The compiler follows a standard pipeline: tokenization → parsing → binding → verification → code generation. This guide provides a concise overview of the codebase structure and key components.

## Key Components

### 1. Parsing (Lexical & Syntax Analysis)
- **`TokenType.cs`**: Defines token types (keywords, operators, etc.)
- **`TokenStream.cs`**: Tokenizer that converts source code to token stream
- **`Parser.cs`**: Recursive descent parser that builds an AST
- **`Nodes.cs`**: AST node classes representing language constructs

### 2. Semantic Analysis
- **`NodeVisitor.cs`**: Base visitor pattern for traversing the AST
- **`DefaultNodeVisitor.cs`**: Default implementation that visits all nodes
- **`Symbol.cs`**: Symbol definitions (variables, functions, types)
- **`Scope.cs`**: Symbol scope management and resolution
- **`Pass/*.cs`**: Analysis passes that validate program semantics:
  - `TopLevelGatheringPass.cs`: Collects top-level declarations
  - `BindingPass.cs`: Connects identifiers to their declarations
  - `TypeAnnotationPass.cs`: Resolves type annotations
  - `TypeMismatchPass.cs`: Type checking
  - `MutationCheckPass.cs`: Validates variable mutability rules
  - `VerificationPass.cs`: Base class for all verification passes

### 3. Code Generation
- **`AssemblyGen.cs`**: Generates IL code from the AST
- **`Types/*.cs`**: Type system implementation
- **`VerifexFunction.cs`/`BuiltinFunction.cs`**: Function representation
- **`ParameterInfo.cs`**: Function parameter information

## Feature Implementation Pattern

When implementing a new feature in Verifex:

1. **Add token types** in `Verifex/Parsing/TokenType.cs` if needed
2. **Update tokenizer** in `Verifex/Parsing/TokenStream.cs` to recognize new syntax
3. **Create AST nodes** in `Verifex/Parsing/Nodes.cs` for new language constructs
4. **Update parser** in `Verifex/Parsing/Parser.cs` to build nodes from tokens
5. **Add visitor methods** in `Verifex/Analysis/NodeVisitor.cs` and `Verifex/Analysis/DefaultNodeVisitor.cs`
6. **Implement semantic checks** in appropriate verification passes
7. **Add code generation** in `Verifex/CodeGen/AssemblyGen.cs`
8. **Write tests** in corresponding test files

## Key Design Patterns

- **Visitor Pattern**: Used for traversing the AST (`NodeVisitor.cs`)
- **Pipeline Architecture**: Sequential passes transform the program
- **Composition over Inheritance**: Symbol and type system design

## Important Concepts

- **Mutability**: Variables can be immutable (`let`) or mutable (`mut`)
- **Symbols and Binding**: All identifiers are resolved to symbols during binding
- **Verification Passes**: Modular semantic checks that can report diagnostics
- **IL Generation**: Direct generation of .NET IL for execution

## Testing Strategy

Tests are organized by compilation stage:
- `Verifex.Tests/TokenizerTests.cs`: Tests for lexical analysis
- `Verifex.Tests/ParserTests.cs`: Tests for syntax analysis
- `Verifex.Tests/VerificationPassTests.cs`: Tests for semantic analysis

## Example Usage Pattern

To add a new language feature like a ternary operator:
1. Add appropriate token types (`Question`, `Colon`)
2. Create a `TernaryOperationNode` class
3. Update parser with expression handling logic
4. Add visitor methods for traversal
5. Implement type checking in `TypeMismatchPass`
6. Add IL generation in `AssemblyGen`
7. Create tests for each component

This modular design makes it straightforward to extend the language with new features while maintaining a clear separation of concerns.
