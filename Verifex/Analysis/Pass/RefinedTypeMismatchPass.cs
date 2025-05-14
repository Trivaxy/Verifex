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
    private readonly HashSet<BasicBlock> _visitedBlocks;

    public RefinedTypeMismatchPass(SymbolTable symbols) : base(symbols)
    {
        _z3Ctx = new Context();
        _solver = _z3Ctx.MkSolver();
        _z3Mapper = new Z3Mapper(_z3Ctx, _solver, _symbolsAsTerms, symbols);
        _visitedBlocks = [];
    }
    
    protected override void Visit(FunctionDeclNode node)
    {
        _symbolsAsTerms.Clear();
        _solver.Reset();
        _currentFunction = (node.Symbol as FunctionSymbol)!.Function;
        _visitedBlocks.Clear();
        
        foreach (ParamDeclNode param in node.Parameters)
        {
            if (param.ResolvedType == null) continue;
            _symbolsAsTerms[param.Symbol!] = _z3Mapper.CreateTerm(param.Symbol!.ResolvedType!, param.Identifier);;
        }
        
        ControlFlowGraph cfg = CFGBuilder.Build(node);
        VisitBasicBlock(cfg.Entry); // Traversing important AST nodes will happen via traversing the CFG
    }

    protected override void Visit(RefinedTypeDeclNode node)
    {
        if (node.Expression.FundamentalType is not BoolType)
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
                    Visit(varDecl);
                    break;
                
                case AssignmentNode assNode when assNode.Target.ResolvedType != null && assNode.Value.ResolvedType != null:
                    Visit(assNode);
                    break;
                
                case FunctionCallNode callNode:
                    Visit(callNode);
                    break;
                
                case ReturnNode retNode when retNode.Value != null && retNode.Value.ResolvedType != null:
                    Visit(retNode);
                    break;
                
                case InitializerNode initNode when initNode.Type.ResolvedType != null:
                    Visit(initNode);
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

    protected override void Visit(VarDeclNode node)
    {
        if (_symbolsAsTerms.ContainsKey(node.Symbol!)) return; // erroneous duplicate definition, ignore
        
        Visit(node.Value);
        
        Z3Expr declValue = LowerAstNodeToZ3(node.Value);
        if (!IsTypeCompatible(node.Symbol!.ResolvedType!, node.Value!.ResolvedType!, declValue))
            LogDiagnostic(new VarDeclTypeMismatch(node.Name, node.Symbol!.ResolvedType!.Name, node.Value!.ResolvedType!.Name) { Location = node.Location });
        else
            AssertAssignment(node.Symbol!, declValue);
    }

    protected override void Visit(AssignmentNode node)
    {
        Visit(node.Value);
        
        Z3Expr assignValue = LowerAstNodeToZ3(node.Value);
        if (!IsTypeCompatible(node.Target!.Symbol!.ResolvedType!, node.Value!.ResolvedType!, assignValue))
            LogDiagnostic(new AssignmentTypeMismatch(node.Target!.ResolvedType!.Name, node.Value!.ResolvedType!.Name) { Location = node.Location });
        else
            AssertAssignment(node.Target.Symbol!, assignValue);
    }

    protected override void Visit(FunctionCallNode node)
    {
        FunctionSymbol function = (node.Callee.Symbol as FunctionSymbol)!;
        for (int i = 0; i < Math.Min(node.Arguments.Count, function.Function.Parameters.Count); i++)
        {
            AstNode argument = node.Arguments[i];
            ParameterInfo param = function.Function.Parameters[i];
                        
            // no resolved type means some type mismatch happened earlier; skip
            if (argument.ResolvedType == null) continue;
            
            Visit(argument);
                        
            if (!IsTypeCompatible(param.Type, argument.ResolvedType!, LowerAstNodeToZ3(argument)))
                LogDiagnostic(new ParamTypeMismatch(param.Name, param.Type.Name, argument.ResolvedType!.Name) { Location = argument.Location });
        }
    }
    
    protected override void Visit(ReturnNode node)
    {
        if (node.Value != null)
            Visit(node.Value);
        
        if (!IsTypeCompatible(_currentFunction.ReturnType, node.Value!.ResolvedType!, LowerAstNodeToZ3(node.Value)))
            LogDiagnostic(new ReturnTypeMismatch(_currentFunction.Name, _currentFunction.ReturnType.Name, node.Value.ResolvedType!.Name) { Location = node.Location });
    }

    protected override void Visit(InitializerNode node)
    {
        foreach (InitializerFieldNode field in node.InitializerList.Values)
        {
            if (!IsTypeCompatible(field.Name.ResolvedType!, field.Value.ResolvedType!, LowerAstNodeToZ3(field.Value)))
                LogDiagnostic(new InitializerFieldTypeMismatch(field.Name.Identifier, field.Name.ResolvedType!.Name, field.Value.ResolvedType!.Name) { Location = field.Location });
        }
    }

    private void AssertAssignment(Symbol target, Z3Expr value)
    {
        _symbolsAsTerms[target] = _z3Mapper.CreateTerm(target.ResolvedType!, target.Name);
        
        // if the target is of the 'Any' type but the source isn't, we can't assert that target = value
        // ... unless both are 'Any'
        if (target.ResolvedType!.EffectiveType is not AnyType || (_symbolsAsTerms[target].Sort == value.Sort))
            _solver.Assert(_z3Ctx.MkEq(_symbolsAsTerms[target], value));
    }
    
    
    private bool AreTypesBasicCompatible(VerifexType target, VerifexType source)
        => target.EffectiveType is not VoidType
           && source.EffectiveType is not VoidType // void is not assignable to anything
           && (target.EffectiveType is AnyType // you can assign anything to Any
               || target == source // same type obviously
               || target.FundamentalType == source.FundamentalType // same fundamental type
               || target.FundamentalType is RealType && source.FundamentalType is IntegerType); // integer to real is implicitly allowed

    private bool IsTermAssignable(VerifexType target, Z3Expr value)
    {
        Z3BoolExpr assertion = _z3Ctx.MkTrue();
        if (target.EffectiveType is RefinedType refinedType) 
            assertion = _z3Ctx.MkNot(_z3Mapper.CreateRefinedTypeConstraintExpr(value, refinedType));
        
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
        
        return _z3Mapper.ConvertExpr(node);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _z3Ctx.Dispose();
        _solver.Dispose();
    }
}