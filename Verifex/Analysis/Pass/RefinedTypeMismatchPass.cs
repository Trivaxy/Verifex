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
    private VerifexFunction _currentFunction = null!;
    private int _nextTermId;
    private readonly HashSet<BasicBlock> _visitedBlocks;

    public RefinedTypeMismatchPass(SymbolTable symbols) : base(symbols)
    {
        _z3Ctx = new Context();
        _solver = _z3Ctx.MkSolver();
        _z3Mapper = new Z3Mapper(_z3Ctx, _symbolsAsTerms);
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
                    for (int i = 0; i < callNode.Arguments.Count; i++)
                    {
                        AstNode argument = callNode.Arguments[i];
                        ParameterInfo param = function.Function.Parameters[i];
                        
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

        if (block.TrueSuccessor != null)
        {
            _solver.Push();
            _solver.Assert(LowerAstNodeToZ3(block.Statements[^1]) as Z3BoolExpr);
            VisitBasicBlock(block.TrueSuccessor);
            _solver.Pop();
        }
        
        if (block.FalseSuccessor != null)
        {
            _solver.Push();
            _solver.Assert(_z3Ctx.MkNot(LowerAstNodeToZ3(block.Statements[^1]) as Z3BoolExpr));
            VisitBasicBlock(block.FalseSuccessor);
            _solver.Pop();
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
        else
            throw new NotImplementedException();

        if (type.EffectiveType is RefinedType refinedType)
            _solver.Assert(CreateRefinedTypeConstraintExpr(term, refinedType));

        return term;
    }

    private void AssertAssignment(Symbol target, Z3Expr value)
    {
        _symbolsAsTerms[target] = CreateTerm(target.ResolvedType!, target.Name);;
        _solver.Assert(_z3Ctx.MkEq(_symbolsAsTerms[target], value));
    }
    
    // Generates a Z3 expression by substituting the given term into the 'value' in a refined type's constraint expression
    // If the refined type has another refined type as a base type, the expression is ANDed with the base type's constraint
    private Z3BoolExpr CreateRefinedTypeConstraintExpr(Z3Expr term, RefinedType refinedType)
    {
        RefinedTypeValueSymbol valueSymbol = Symbols.GetGlobalSymbol<RefinedTypeSymbol>(refinedType.Name).ValueSymbol;
        _symbolsAsTerms[valueSymbol] = term;
        
        Z3Mapper mapper = new Z3Mapper(_z3Ctx, _symbolsAsTerms);
        Z3BoolExpr assertion = (mapper.ConvertExpr(refinedType.RawConstraint) as Z3BoolExpr)!;
        
        if (refinedType.BaseType.EffectiveType is RefinedType baseRefinedType)
            assertion = _z3Ctx.MkAnd(assertion, CreateRefinedTypeConstraintExpr(term, baseRefinedType));
        
        _symbolsAsTerms.Remove(valueSymbol);
        return assertion;
    }

    private bool AreTypesBasicCompatible(VerifexType left, VerifexType right)
        => left.EffectiveType is not VoidType && right.EffectiveType is not VoidType && (left == right || left.IlType == right.IlType);

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

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _z3Ctx.Dispose();
        _solver.Dispose();
    }
}