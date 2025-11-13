# Verifex

Verifex is an experimental programming language and compiler that treats correctness as a first‑class feature. It combines a simple, familiar syntax with powerful concepts that let you catch entire classes of bugs at compile time, eliminate nasty surprises, and encode business logic directly in your types.

If that sounds interesting, you can read a quick tutorial and try Verifex for yourself over at [VerifexPad](https://trivaxy.github.io/VerifexPad/).

## Repository Layout

```
VerifexProject/
├── Verifex/           # Compiler implementation (parsing, analysis, codegen)
├── Verifex.Tests/     # xUnit test suite (tokenizer, parser, verification passes)
├── VerifexPad/        # Playground app (backend + React UI) and language reference
├── CODEBASE.md        # In‑depth codebase guide for contributors
└── README.md          # You are here
```

## Prerequisites

- Just a .NET 9 SDK or later (compiler targets `net9.0`).

## Build & Run

Clone the repo, then:

```bash
dotnet restore Verifex.sln
dotnet build Verifex.sln
```

Compile a Verifex source file into a .NET executable:

```bash
dotnet run --project Verifex -- path/to/program.vfx
```

The compiler emits `program.exe` plus a `.runtimeconfig.json` next to your source file.

### Example With Refined & Maybe Types

```rust
type NonZeroReal = Real where value != 0;

fn divide(a: Real, b: NonZeroReal) -> Real {
    return a / b;
}

fn try_divide(num: Real, denom: Real) -> Real or String {
    if (denom != 0)
        return divide(num, denom);
    else
        return "denominator was zero";
}
```

The verifier tracks the `if (denom != 0)` guard, proves `denom` is a `NonZeroReal` inside the true branch, and warns if any path may still divide by zero.

## Verification Pipeline

Each compilation pass is explicit (see `Verifex/Analysis/Pass/*.cs`):

1. **TopLevelGatheringPass** – collect functions, structs, refined types, archetypes.
2. **CfgConstructionPass** – build basic-block control‑flow graphs per function.
3. **First/Second Binding Passes** – bind identifiers to symbols (locals, fields, members) with scope tracking.
4. **Static + Subsequent Annotation Passes** – infer/propagate types, construct `MaybeType`, `ArrayType`, `RefinedType` instances.
5. **RefiningPass** – run Z3 to narrow types, validate refined constraints, catch hazards (e.g., possible divide-by-zero, impossible assignments).
6. **BasicTypeMismatch / MutationCheck / ReturnFlow / …** – ensure arithmetic compatibility, enforce immutability, demand returns on all paths, verify struct initializers, etc.
7. **AssemblyGen** – lower the verified AST to IL and package it as a runnable assembly.

Diagnostics from any stage are surfaced together after compilation, keeping the workflow tight.

## Tests

Run all tests:

```bash
dotnet test Verifex.sln
```

The suite currently covers tokenization, parsing (including recovery), control‑flow/verification passes, and the Z3 mapper.
