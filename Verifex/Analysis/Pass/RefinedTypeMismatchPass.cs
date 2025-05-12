using Microsoft.Z3;
using Verifex.CodeGen;
using Verifex.CodeGen.Types;
using Verifex.Parsing;

namespace Verifex.Analysis.Pass;

public class RefinedTypeMismatchPass : VerificationPass, IDisposable
{
    private readonly Dictionary<Symbol, Z3Expr> _symbolsAsTerms = [];
    private readonly Context _z3Ctx;
    private readonly Solver _solver;
    private readonly Z3Mapper _z3Mapper;
    private readonly UninterpretedSort _anySort;
    private readonly UninterpretedSort _voidSort;
    private readonly Dictionary<VerifexType, FuncDecl> _z3ToStringFuncDecls;
    private VerifexFunction _currentFunction = null!;
    private int _nextTermId;
    private readonly HashSet<BasicBlock> _visitedBlocks;

    public RefinedTypeMismatchPass(SymbolTable symbols) : base(symbols)
    {
        _z3Ctx = new Context();
        _solver = _z3Ctx.MkSolver();
        _anySort = _z3Ctx.MkUninterpretedSort("Any");
        _voidSort = _z3Ctx.MkUninterpretedSort("Void");
        _z3ToStringFuncDecls = CreateZ3ToStringFuncDecls(_z3Ctx, symbols);
        _z3Mapper = new Z3Mapper(_z3Ctx, _symbolsAsTerms, _z3ToStringFuncDecls);
        _nextTermId = 0;
        _visitedBlocks = [];
    }
    
    protected override void Visit(FunctionDeclNode node)
    {
        _symbolsAsTerms.Clear();
        _solver.Reset();
        _nextTermId = 0;
        _currentFunction = (node.Symbol as FunctionSymbol)!.Function;
        _visitedBlocks.Clear();
        
        foreach (ParamDeclNode param in node.Parameters)
        {
            if (param.ResolvedType == null) continue;
            _symbolsAsTerms[param.Symbol!] = CreateTerm(param.Symbol!.ResolvedType!, param.Identifier);;
        }
        
        ControlFlowGraph cfg = CFGBuilder.Build(node);
        VisitBasicBlock(cfg.Entry);
    }

    protected override void Visit(RefinedTypeDeclNode node)
    {
        if (node.Expression.ResolvedType?.Name != "Bool")
            LogDiagnostic(new ConditionMustBeBool("refined type") { Location = node.Expression.Location });
    }

    private void VisitBasicBlock(BasicBlock block)
    {
        if (block.IsExit || !_visitedBlocks.Add(block)) return;

        foreach (AstNode statement in block.Statements)
        {
            if (statement == block.Statements[^1] && block.HasConditionalSuccessors)
                break; // the last statement is just a condition expression if there are conditional successors, ignore
            
            switch (statement)
            {
                case VarDeclNode varDecl when varDecl.ResolvedType != null && varDecl.Value.ResolvedType != null:
                    if (_symbolsAsTerms.ContainsKey(varDecl.Symbol!)) continue; // duplicate definition, ignore
                    
                    Z3Expr declValue = LowerAstNodeToZ3(varDecl.Value);
                    if (!IsTypeCompatible(varDecl.Symbol!.ResolvedType!, varDecl.Value.ResolvedType, declValue))
                        LogDiagnostic(new VarDeclTypeMismatch(varDecl.Name, varDecl.Symbol!.ResolvedType!.Name, varDecl.Value.ResolvedType.Name) { Location = varDecl.Location });
                    else
                        AssertAssignment(varDecl.Symbol!, declValue);
                    break;
                
                case AssignmentNode assNode when assNode.Target.ResolvedType != null && assNode.Value.ResolvedType != null:
                    Z3Expr assignValue = LowerAstNodeToZ3(assNode.Value);
                    if (!IsTypeCompatible(assNode.Target.Symbol!.ResolvedType, assNode.Value.ResolvedType, assignValue))
                        LogDiagnostic(new AssignmentTypeMismatch(assNode.Target.Identifier, assNode.Target.ResolvedType.Name, assNode.Value.ResolvedType.Name) { Location = assNode.Location });
                    else
                        AssertAssignment(assNode.Target.Symbol!, assignValue);
                    break;
                
                case FunctionCallNode callNode:
                    FunctionSymbol function = (callNode.Callee.Symbol as FunctionSymbol)!;
                    for (int i = 0; i < Math.Min(callNode.Arguments.Count, function.Function.Parameters.Count); i++)
                    {
                        AstNode argument = callNode.Arguments[i];
                        ParameterInfo param = function.Function.Parameters[i];
                        
                        // no resolved type means some type mismatch happened earlier; skip
                        if (argument.ResolvedType == null) continue;
                        
                        if (!IsTypeCompatible(param.Type, argument.ResolvedType!, LowerAstNodeToZ3(argument)))
                            LogDiagnostic(new ParamTypeMismatch(param.Name, param.Type.Name, argument.ResolvedType!.Name) { Location = argument.Location });
                    }
                    break;
                
                case ReturnNode retNode when retNode.Value != null && retNode.Value.ResolvedType != null:
                    if (!IsTypeCompatible(_currentFunction.ReturnType, retNode.Value.ResolvedType, LowerAstNodeToZ3(retNode.Value)))
                        LogDiagnostic(new ReturnTypeMismatch(_currentFunction.Name, _currentFunction.ReturnType.Name, retNode.Value.ResolvedType!.Name) { Location = retNode.Location });
                    break;
            }
        }

        if (block.HasConditionalSuccessors)
        {
            AstNode rawCondition = block.Statements[^1];
            if (rawCondition.ResolvedType?.EffectiveType is BoolType)
            {
                Z3BoolExpr z3Cond = (LowerAstNodeToZ3(rawCondition) as Z3BoolExpr)!;
                
                // Visit the true branch first
                _solver.Push();
                _solver.Assert(z3Cond);
                VisitBasicBlock(block.TrueSuccessor!);
                _solver.Pop();
                
                // Visit the false branch second
                _solver.Push();
                _solver.Assert(_z3Ctx.MkNot(z3Cond));
                VisitBasicBlock(block.FalseSuccessor!);
                _solver.Pop();
            }
        }

        if (block.UnconditionalSuccessor != null)
            VisitBasicBlock(block.UnconditionalSuccessor);
    }

    private Z3Expr CreateTerm(VerifexType type, string name)
    {
        Type ilType = type.IlType;
        
        Z3Expr term;
        string termName = $"{name}_{_nextTermId++}";
        if (ilType == typeof(int))
            term = _z3Ctx.MkIntConst(termName);
        else if (ilType == typeof(double))
            term = _z3Ctx.MkRealConst(termName);
        else if (ilType == typeof(bool))
            term = _z3Ctx.MkBoolConst(termName);
        else if (ilType == typeof(string))
            term = _z3Ctx.MkString(termName);
        else if (ilType == typeof(object))
            term = _z3Ctx.MkConst(termName, _anySort);
        else
            throw new NotImplementedException();

        if (type.EffectiveType is RefinedType refinedType)
            _solver.Assert(CreateRefinedTypeConstraintExpr(term, refinedType));

        return term;
    }

    private void AssertAssignment(Symbol target, Z3Expr value)
    {
        _symbolsAsTerms[target] = CreateTerm(target.ResolvedType!, target.Name);
        
        // if the target is of the 'Any' type but the source isn't, we can't assert that target = value
        // ... unless both are 'Any'
        if (target.ResolvedType!.EffectiveType is not AnyType || (_symbolsAsTerms[target].Sort == value.Sort))
            _solver.Assert(_z3Ctx.MkEq(_symbolsAsTerms[target], value));
    }

    private Sort AsSort(VerifexType type)
    {
        return type.FundamentalType switch
        {
            IntegerType => _z3Ctx.IntSort,
            RealType => _z3Ctx.RealSort,
            BoolType => _z3Ctx.BoolSort,
            StringType => _z3Ctx.StringSort,
            AnyType => _anySort,
            VoidType => _voidSort,
            _ => throw new NotImplementedException($"Type has no known sort: {type.Name}")
        };
    }
    
    // Generates a Z3 expression by substituting the given term into the 'value' in a refined type's constraint expression
    // If the refined type has another refined type as a base type, the expression is ANDed with the base type's constraint
    private Z3BoolExpr CreateRefinedTypeConstraintExpr(Z3Expr term, RefinedType refinedType)
    {
        RefinedTypeValueSymbol valueSymbol = Symbols.GetGlobalSymbol<RefinedTypeSymbol>(refinedType.Name).ValueSymbol;
        _symbolsAsTerms[valueSymbol] = term;
        
        Z3Mapper mapper = new Z3Mapper(_z3Ctx, _symbolsAsTerms, _z3ToStringFuncDecls);
        Z3BoolExpr assertion = (mapper.ConvertExpr(refinedType.RawConstraint) as Z3BoolExpr)!;
        
        if (refinedType.BaseType.EffectiveType is RefinedType baseRefinedType)
            assertion = _z3Ctx.MkAnd(assertion, CreateRefinedTypeConstraintExpr(term, baseRefinedType));
        
        _symbolsAsTerms.Remove(valueSymbol);
        return assertion;
    }

    private bool AreTypesBasicCompatible(VerifexType target, VerifexType source)
        => target.EffectiveType is not VoidType && source.EffectiveType is not VoidType && (target.EffectiveType is AnyType || target == source || target.IlType == source.IlType);

    private bool IsTermAssignable(VerifexType target, Z3Expr value)
    {
        Z3BoolExpr assertion = _z3Ctx.MkTrue();
        if (target.EffectiveType is RefinedType refinedType) 
            assertion = _z3Ctx.MkNot(CreateRefinedTypeConstraintExpr(value, refinedType));
        
        _solver.Push();
        _solver.Assert(assertion);
        Status status = _solver.Check();
        _solver.Pop();
        
        return status == Status.UNSATISFIABLE;
    }
    
    private bool IsTypeCompatible(VerifexType target, VerifexType source, Z3Expr sourceValue)
    {
        if (target.EffectiveType is not RefinedType && source.EffectiveType is not RefinedType)
            return AreTypesBasicCompatible(target, source);
        
        return AreTypesBasicCompatible(target, source) && IsTermAssignable(target, sourceValue);
    }

    private Z3Expr LowerAstNodeToZ3(AstNode node)
    {
        if (node.ResolvedType == null)
            throw new InvalidOperationException("Cannot lower AST node without a resolved type");
        
        if (node is FunctionCallNode)
            return CreateTerm(node.ResolvedType, "t");
        
        return _z3Mapper.ConvertExpr(node);
    }
    
    private Dictionary<VerifexType, FuncDecl> CreateZ3ToStringFuncDecls(Context ctx, SymbolTable symbols)
    {
        Dictionary<VerifexType, FuncDecl> funcDecls = [];
        
        foreach (VerifexType type in symbols.GetTypes())
        {
            if (type is not RefinedType)
                funcDecls[type] = ctx.MkFuncDecl($"{type.Name}ToString", AsSort(type), ctx.StringSort);
        }

        foreach (VerifexType type in symbols.GetTypes())
        {
            if (type is not RefinedType) continue;
            funcDecls[type] = funcDecls[type.FundamentalType]; // use the fundamental type's function decl
        }
        
        return funcDecls;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _z3Ctx.Dispose();
        _solver.Dispose();
    }
}