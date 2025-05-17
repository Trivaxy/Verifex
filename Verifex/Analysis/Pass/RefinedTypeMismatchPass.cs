using System.Diagnostics;
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
    private readonly Dictionary<VerifexType, Dictionary<VerifexType, CompatibilityStatus>> _typeCompatibilityCache; // key = target, value = sources

    public RefinedTypeMismatchPass(SymbolTable symbols) : base(symbols)
    {
        _z3Ctx = new Context();
        _solver = _z3Ctx.MkSolver();
        _z3Mapper = new Z3Mapper(_z3Ctx, _solver, _symbolsAsTerms, symbols);
        _visitedBlocks = [];
        _typeCompatibilityCache = [];
    }
    
    protected override void Visit(FunctionDeclNode node)
    {
        _symbolsAsTerms.Clear();
        _solver.Reset();
        _currentFunction = (node.Symbol as FunctionSymbol)!.Function;
        _visitedBlocks.Clear();

        if (_currentFunction.Owner?.EffectiveType is StructType structType && !_currentFunction.IsStatic)
        {
            _z3Mapper.CurrentSelfTerm = _z3Ctx.MkConst("self", _z3Mapper.DatatypeSortForStruct(structType));
            
            foreach (StructFieldSymbol field in Symbols.GetSymbol<StructSymbol>(structType.Name).Fields.Values)
            {
                if (field.ResolvedType == null) continue;
                AssertAssignment(field, _z3Mapper.CreateTerm(field.ResolvedType!, field.Name));
                
                // we also have to link the field node with an equality to the accessor on the self term
                _solver.Assert(_z3Ctx.MkEq(
                    _z3Ctx.MkApp(_z3Mapper.DatatypeSortForStruct(structType).Accessors[0][field.Index], _z3Mapper.CurrentSelfTerm),
                    _symbolsAsTerms[field]
                    ));
            }
        }
        
        foreach (ParamDeclNode param in node.Parameters)
        {
            if (param.ResolvedType == null) continue;
            _symbolsAsTerms[param.Symbol!] = _z3Mapper.CreateTerm(param.Symbol!.ResolvedType!, param.Identifier);;
        }
        
        ControlFlowGraph cfg = CFGBuilder.Build(node);
        VisitBasicBlock(cfg.Entry); // Traversing important AST nodes will happen via traversing the CFG

        _z3Mapper.CurrentSelfTerm = null;
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
                case VarDeclNode varDecl when varDecl.EffectiveType != null && varDecl.Value.EffectiveType != null:
                    Visit(varDecl);
                    break;
                
                case AssignmentNode assNode when assNode.Target.EffectiveType != null && assNode.Value.EffectiveType != null:
                    Visit(assNode);
                    break;
                
                case FunctionCallNode callNode:
                    Visit(callNode);
                    break;
                
                case ReturnNode retNode when retNode.Value != null && retNode.Value.ResolvedType != null:
                    Visit(retNode);
                    break;
                
                case InitializerNode initNode when initNode.Type.EffectiveType != null:
                    Visit(initNode);
                    break;
            }
        }

        if (block.HasConditionalSuccessors)
        {
            AstNode rawCondition = block.Statements[^1];
            if (rawCondition.EffectiveType is BoolType)
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
        
        if (!IsValueAssignable(node.Symbol!.ResolvedType!, node.Value))
            LogDiagnostic(new VarDeclTypeMismatch(node.Name, node.Symbol!.ResolvedType!.Name, node.Value!.ResolvedType!.Name) { Location = node.Location });
        else
            AssertAssignment(node.Symbol!, LowerAstNodeToZ3(node.Value));
    }

    protected override void Visit(AssignmentNode node)
    {
        Visit(node.Value);
        
        if (!IsValueAssignable(node.Target!.Symbol!.ResolvedType!, node.Value))
            LogDiagnostic(new AssignmentTypeMismatch(node.Target!.ResolvedType!.Name, node.Value!.ResolvedType!.Name) { Location = node.Location });
        else
            AssertAssignment(node.Target.Symbol!, LowerAstNodeToZ3(node.Value));
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
                        
            if (!IsValueAssignable(param.Type, argument))
                LogDiagnostic(new ParamTypeMismatch(param.Name, param.Type.Name, argument.ResolvedType!.Name) { Location = argument.Location });
        }
    }
    
    protected override void Visit(ReturnNode node)
    {
        if (node.Value != null)
            Visit(node.Value);
        
        if (!IsValueAssignable(_currentFunction.ReturnType, node.Value!))
            LogDiagnostic(new ReturnTypeMismatch(_currentFunction.Name, _currentFunction.ReturnType.Name, node.Value.ResolvedType!.Name) { Location = node.Location });
    }

    protected override void Visit(InitializerNode node)
    {
        foreach (InitializerFieldNode field in node.InitializerList.Values)
        {
            if (field.EffectiveType == null) continue; // some error happened earlier, continue
            if (!IsValueAssignable(field.Name.ResolvedType!, field.Value))
                LogDiagnostic(new InitializerFieldTypeMismatch(field.Name.Identifier, field.Name.ResolvedType!.Name, field.Value.ResolvedType!.Name) { Location = field.Location });
        }
    }

    private void AssertAssignment(Symbol target, Z3Expr value)
    {
        _symbolsAsTerms[target] = _z3Mapper.CreateTerm(target.ResolvedType!, target.Name);
        
        if (_symbolsAsTerms[target].Sort == value.Sort)
            _solver.Assert(_z3Ctx.MkEq(_symbolsAsTerms[target], value));
        else if (target.ResolvedType!.EffectiveType is MaybeType maybeType)
        {
            Constructor constructor = _z3Mapper.GetMaybeTypeZ3Info(maybeType).Constructors[value.Sort];
            _solver.Assert(_z3Ctx.MkEq(_symbolsAsTerms[target], _z3Ctx.MkApp(constructor.ConstructorDecl, value)));
        }
        else if (_z3Mapper.TryGetMaybeTypeInfo(value, out Z3Mapper.MaybeTypeZ3Info? info))
        {
            FuncDecl accessor = info!.Constructors[_z3Mapper.AsSort(target.ResolvedType!.EffectiveType)].AccessorDecls[0];
            _solver.Assert(_z3Ctx.MkEq(_symbolsAsTerms[target], _z3Ctx.MkApp(accessor, value)));
        }
        else
            throw new InvalidOperationException("Unknown types for assignment");
    }

    private bool IsValueAssignable(VerifexType target, AstNode rawValue)
    {
        CompatibilityStatus compatibility = GetTypeCompatibility(target, rawValue.ResolvedType!);
        if (compatibility == CompatibilityStatus.Incompatible) return false;
        if (compatibility == CompatibilityStatus.Compatible) return true;
        
        // it's contextual, so use the path condition
        Z3Expr value = LowerAstNodeToZ3(rawValue);
        if (target.EffectiveType is RefinedType refinedType)
        {
            Z3BoolExpr assertion = _z3Ctx.MkNot(_z3Mapper.CreateRefinedTypeConstraintExpr(value, refinedType));
            _solver.Push();
            _solver.Assert(assertion);
            Status status = _solver.Check();
            _solver.Pop();
        
            return status == Status.UNSATISFIABLE;
        }
        
        if (target.EffectiveType is MaybeType maybeType)
            return maybeType.Types.Any(t => IsValueAssignable(t, rawValue));

        return true;
    }

    private CompatibilityStatus GetTypeCompatibility(VerifexType target, VerifexType source)
    {
        if (!_typeCompatibilityCache.ContainsKey(target.EffectiveType))
            _typeCompatibilityCache[target.EffectiveType] = new Dictionary<VerifexType, CompatibilityStatus>();
        
        if (_typeCompatibilityCache[target.EffectiveType].TryGetValue(source.EffectiveType, out CompatibilityStatus status))
            return status;
        
        status = ComputeTypeCompatibility(target, source);
        _typeCompatibilityCache[target.EffectiveType][source.EffectiveType] = status;

        return status;
    }

    private CompatibilityStatus ComputeTypeCompatibility(VerifexType target, VerifexType source)
    {
        if (target == source) return CompatibilityStatus.Compatible;
        if (target.FundamentalType is AnyType) return CompatibilityStatus.Compatible;
        if (target.FundamentalType is CodeGen.Types.UnknownType) return CompatibilityStatus.Incompatible;
        if (target.FundamentalType is not MaybeType && source.FundamentalType is not MaybeType && target.FundamentalType != source.FundamentalType) return CompatibilityStatus.Incompatible;
        
        // target is a maybe-type, we need to check if the source is a subtype of any of the types in the maybe-type
        if (target.EffectiveType is MaybeType maybeType)
        {
            if (source.EffectiveType is MaybeType sourceMaybe)
            {
                CompatibilityStatus status = CompatibilityStatus.Compatible;
                foreach (VerifexType potential in sourceMaybe.Types)
                {
                    CompatibilityStatus innerStatus = GetTypeCompatibility(target, potential);
                    if (innerStatus == CompatibilityStatus.Incompatible) return CompatibilityStatus.Incompatible;
                    if (innerStatus == CompatibilityStatus.Contextual) status = CompatibilityStatus.Contextual;
                }

                return status;
            }
            else
            {
                if (maybeType.Types.All(t => GetTypeCompatibility(t, source) == CompatibilityStatus.Incompatible))
                    return CompatibilityStatus.Incompatible;
                if (maybeType.Types.All(t => GetTypeCompatibility(t, source) == CompatibilityStatus.Compatible))
                    return CompatibilityStatus.Compatible;

                return CompatibilityStatus.Contextual;
            }
        }
        
        // target is a refined type, solve for it
        if (target.EffectiveType is RefinedType refinedType)
        {
            CompatibilityStatus status = CompatibilityStatus.Incompatible;
            
            using Solver freshSolver = _z3Ctx.MkSolver();
            Z3Expr sourceTerm = _z3Mapper.CreateTerm(source, "source");
            Z3BoolExpr sourcePredicate = _z3Ctx.MkTrue();
            
            // if the source is a refined type, we need to create its assertion
            if (source.EffectiveType is RefinedType sourceRefined)
                sourcePredicate = _z3Mapper.CreateRefinedTypeConstraintExpr(sourceTerm, sourceRefined);
            else if (source.EffectiveType is MaybeType sourceMaybe) // if it's a maybe type, we need to test it and unbox
            {
                sourcePredicate = _z3Ctx.MkAnd(_z3Mapper.CreateMaybeTypeConstraintExpr(sourceTerm, sourceMaybe, target.EffectiveType));
                sourceTerm = _z3Ctx.MkApp(_z3Mapper.GetMaybeTypeZ3Info(sourceMaybe).Constructors[_z3Mapper.AsSort(target.EffectiveType)].AccessorDecls[0], sourceTerm);
            }
            
            Z3BoolExpr targetPredicate = _z3Mapper.CreateRefinedTypeConstraintExpr(sourceTerm, refinedType);
            Z3BoolExpr assertion = _z3Ctx.MkAnd(targetPredicate, sourcePredicate);
            
            // if source condition and target condition are both true, then it's at least contextual
            if (freshSolver.Check(assertion) == Status.SATISFIABLE)
                status = CompatibilityStatus.Contextual;
            
            // now we check if we can promote to compatible if the source implies the target
            if (status == CompatibilityStatus.Contextual)
            {
                freshSolver.Reset();
                assertion = _z3Ctx.MkAnd(sourcePredicate, _z3Ctx.MkNot(targetPredicate));
                if (freshSolver.Check(assertion) == Status.UNSATISFIABLE)
                    status = CompatibilityStatus.Compatible;
            }

            return status;
        }
        
        // the target isn't a refined or maybe type, but the source might be a maybe type
        if (source.EffectiveType is MaybeType sourceMaybe2)
        {
            if (sourceMaybe2.Types.All(s => GetTypeCompatibility(target, s) == CompatibilityStatus.Compatible))
                return CompatibilityStatus.Compatible;
            if (sourceMaybe2.Types.All(s => GetTypeCompatibility(target, s) == CompatibilityStatus.Incompatible))
                return CompatibilityStatus.Incompatible;
            
            return CompatibilityStatus.Contextual;
        }
        
        // the target isn't a refined or maybe type, but the source might be a refined type, then just check if the fundamental type is the same
        if (source.EffectiveType is RefinedType)
        {
            if (source.FundamentalType == target.FundamentalType)
                return CompatibilityStatus.Compatible;
        }

        return CompatibilityStatus.Incompatible;
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

    private enum CompatibilityStatus
    {
        Incompatible,
        Contextual,
        Compatible
    }
}