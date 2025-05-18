using System.Diagnostics.CodeAnalysis;
using Microsoft.Z3;
using Verifex.Analysis.Mapping;
using Verifex.CodeGen;
using Verifex.CodeGen.Types;
using Verifex.Parsing;

namespace Verifex.Analysis.Pass;

public class RefiningPass : VerificationPass, IDisposable
{
    private readonly TermStack _termStack;
    private readonly Context _z3Ctx;
    private readonly Solver _solver;
    private readonly Z3Mapper _z3Mapper;
    private VerifexFunction _currentFunction = null!;
    private readonly HashSet<BasicBlock> _visitedBlocks;
    private readonly Dictionary<VerifexType, Dictionary<VerifexType, CompatibilityStatus>> _typeCompatibilityCache; // key = target, value = sources
    private readonly TypeAnnotationPass _miniTypeAnnotationPass;

    public RefiningPass(VerificationContext context) : base(context)
    {
        _termStack = new TermStack();
        _z3Ctx = new Context();
        _solver = _z3Ctx.MkSolver();
        _z3Mapper = new Z3Mapper(_z3Ctx, _solver, _termStack, context.Symbols);
        _visitedBlocks = [];
        _typeCompatibilityCache = [];
        _miniTypeAnnotationPass = new TypeAnnotationPass(context);
    }
    
    protected override void Visit(FunctionDeclNode node)
    {
        _termStack.Clear();
        _solver.Reset();
        _currentFunction = (node.Symbol as FunctionSymbol)!.Function;
        _visitedBlocks.Clear();

        if (_currentFunction.Owner?.EffectiveType is StructType structType && !_currentFunction.IsStatic)
        {
            _z3Mapper.CurrentSelfTerm = _z3Ctx.MkConst("self", _z3Mapper.DatatypeSortForStruct(structType));
            
            foreach (StructFieldSymbol field in Symbols.GetSymbol<StructSymbol>(structType.Name).Fields.Values)
            {
                if (field.ResolvedType == VerifexType.Unknown) continue;
                AssertAssignment(field, _z3Mapper.CreateTerm(field.ResolvedType, field.Name));
                
                // we also have to link the field node with an equality to the accessor on the self term
                _solver.Assert(_z3Ctx.MkEq(
                    _z3Ctx.MkApp(_z3Mapper.DatatypeSortForStruct(structType).Accessors[0][field.Index], _z3Mapper.CurrentSelfTerm),
                    _termStack.GetTerm(field)
                    ));
            }
        }
        
        foreach (ParamDeclNode param in node.Parameters)
        {
            if (param.ResolvedType == VerifexType.Unknown) continue;
            _termStack.SetTerm(param.Symbol!, _z3Mapper.CreateTerm(param.Symbol!.ResolvedType, param.Identifier));
        }

        ControlFlowGraph cfg = Context.ControlFlowGraphs[(node.Symbol as FunctionSymbol)!];
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
            {
                // the last statement is just a condition expression if there are conditional successors
                // we don't need to emit anything for it but we do need to visit it at least once to try and narrow types
                VisitValue(statement);
                break;
            }

            switch (statement)
            {
                case VarDeclNode varDecl when varDecl.EffectiveType != null && varDecl.Value.EffectiveType != null:
                    Visit(varDecl);
                    break;
                
                case AssignmentNode assNode when assNode.Target.EffectiveType != VerifexType.Unknown && assNode.Value.EffectiveType != VerifexType.Unknown:
                    Visit(assNode);
                    break;
                
                case FunctionCallNode callNode:
                    Visit(callNode);
                    break;
                
                case ReturnNode retNode when retNode.Value != null && retNode.Value.ResolvedType != VerifexType.Unknown:
                    Visit(retNode);
                    break;
                
                case InitializerNode initNode when initNode.Type.EffectiveType != VerifexType.Unknown:
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
                _termStack.Push();
                _solver.Push();
                _solver.Assert(z3Cond);
                VisitBasicBlock(block.TrueSuccessor!);
                _solver.Pop();
                _termStack.Pop();
                
                // Visit the false branch second
                _termStack.Push();
                _solver.Push();
                _solver.Assert(_z3Ctx.MkNot(z3Cond));
                VisitBasicBlock(block.FalseSuccessor!);
                _solver.Pop();
                _termStack.Pop();
            }
        }

        if (block.UnconditionalSuccessor != null)
        {
            _termStack.Push();
            VisitBasicBlock(block.UnconditionalSuccessor);
            _termStack.Pop();
        }
    }

    protected override void Visit(VarDeclNode node)
    {
        if (_termStack.Contains(node.Symbol!)) return; // erroneous duplicate definition, ignore
        
        VisitValue(node.Value);
        
        if (!IsValueAssignable(node.Symbol!.ResolvedType, node.Value))
        {
            // just assign a bland term
            AssertAssignment(node.Symbol, _z3Mapper.CreateTerm(node.Symbol!.ResolvedType, node.Name));
            LogDiagnostic(new VarDeclTypeMismatch(node.Name, node.Symbol!.ResolvedType.Name, node.Value.ResolvedType.Name) { Location = node.Location });
        }
        else
            AssertAssignment(node.Symbol!, LowerAstNodeToZ3(node.Value)!);
    }

    protected override void Visit(AssignmentNode node)
    {
        VisitValue(node.Value);
        
        if (!IsValueAssignable(node.Target.Symbol!.ResolvedType, node.Value))
        {
            // just make a bland term
            _termStack.SetTerm(node.Target.Symbol!, _z3Mapper.CreateTerm(node.Target.ResolvedType, "arbitrary"));
            LogDiagnostic(new AssignmentTypeMismatch(node.Target.ResolvedType.Name, node.Value.ResolvedType.Name) { Location = node.Location });
        }
        else
            AssertAssignment(node.Target.Symbol!, LowerAstNodeToZ3(node.Value)!);
    }

    protected override void Visit(FunctionCallNode node)
    {
        FunctionSymbol function = (node.Callee.Symbol as FunctionSymbol)!;
        for (int i = 0; i < Math.Min(node.Arguments.Count, function.Function.Parameters.Count); i++)
        {
            AstNode argument = node.Arguments[i];
            ParameterInfo param = function.Function.Parameters[i];
            
            VisitValue(argument);

            if (argument.ResolvedType == VerifexType.Unknown) continue;
                        
            if (!IsValueAssignable(param.Type, argument))
                LogDiagnostic(new ParamTypeMismatch(param.Name, param.Type.Name, argument.ResolvedType.Name) { Location = argument.Location });
        }
    }
    
    protected override void Visit(ReturnNode node)
    {
        if (node.Value != null)
            VisitValue(node.Value);

        if (_currentFunction.ReturnType == null)
        {
            if (node.Value != null)
                LogDiagnostic(new ReturnTypeMismatch(_currentFunction.Name, "Void", node.Value.ResolvedType.Name) { Location = node.Location });
        }
        else
        {
            if (node.Value == null)
                LogDiagnostic(new ReturnTypeMismatch(_currentFunction.Name, _currentFunction.ReturnType.Name, "Void") { Location = node.Location });
            else if (!IsValueAssignable(_currentFunction.ReturnType, node.Value!))
                LogDiagnostic(new ReturnTypeMismatch(_currentFunction.Name, _currentFunction.ReturnType.Name, node.Value.ResolvedType.Name) { Location = node.Location });
        }
    }

    protected override void Visit(InitializerNode node)
    {
        foreach (InitializerFieldNode field in node.InitializerList.Values)
        {
            if (field.Name.ResolvedType == VerifexType.Unknown || field.Value.ResolvedType == VerifexType.Unknown) continue; // some error happened earlier, continue
            
            VisitValue(field.Value);
            
            if (!IsValueAssignable(field.Name.ResolvedType, field.Value))
                LogDiagnostic(new InitializerFieldTypeMismatch(field.Name.Identifier, field.Name.ResolvedType.Name, field.Value.ResolvedType.Name) { Location = field.Location });
        }
    }

    protected override void Visit(MemberAccessNode node)
    {
        base.Visit(node);
        UpdateResolvedTypeInZ3Context(node);
    }

    protected override void Visit(IdentifierNode node)
    {
        if (node.FundamentalType is not MaybeType maybeType) return;
        if (node.Symbol is not LocalVarSymbol local) return;

        node.ResolvedType = NarrowTypeFor(_z3Mapper.ConvertExpr(node), maybeType);
    }

    private void VisitValue(AstNode node)
    {
        // visit the node normally to reach everything
        Visit(node);
        
        // run the type annotator pass on it, in case any type got narrowed or refined, so we propagate changes
        _miniTypeAnnotationPass.Run(node);
        
        if (node is IdentifierNode && node.Symbol is not LocalVarSymbol)
            LogDiagnostic(new NotAValue() { Location = node.Location });
    }

    private void AssertAssignment(Symbol target, Z3Expr value)
    {
        _termStack.SetTerm(target, _z3Mapper.CreateTerm(target.ResolvedType, target.Name));
        
        if (_termStack.GetTerm(target).Sort == value.Sort)
            _solver.Assert(_z3Ctx.MkEq(_termStack.GetTerm(target), value));
        else if (target.ResolvedType.EffectiveType is MaybeType maybeType)
        {
            Constructor constructor = _z3Mapper.GetMaybeTypeZ3Info(maybeType).Constructors.First(c => _z3Mapper.AsSort(c.Key) == value.Sort).Value;
            _solver.Assert(_z3Ctx.MkEq(_termStack.GetTerm(target), _z3Ctx.MkApp(constructor.ConstructorDecl, value)));
        }
        else if (_z3Mapper.TryGetMaybeTypeInfo(value, out Z3Mapper.MaybeTypeZ3Info? info))
        {
            FuncDecl accessor = info!.Constructors[target.ResolvedType.EffectiveType].AccessorDecls[0];
            _solver.Assert(_z3Ctx.MkEq(_termStack.GetTerm(target), _z3Ctx.MkApp(accessor, value)));
        }
        else
            throw new InvalidOperationException("Unknown types for assignment");
    }

    private bool IsValueAssignable(VerifexType target, AstNode rawValue)
    {
        CompatibilityStatus compatibility = GetTypeCompatibility(target, rawValue.ResolvedType);
        if (compatibility == CompatibilityStatus.Incompatible) return false;
        if (compatibility == CompatibilityStatus.Compatible) return true;
        
        // it's contextual, so use the path condition
        Z3Expr value = LowerAstNodeToZ3(rawValue);
        if (target.EffectiveType is RefinedType refinedType)
        {
            if (refinedType.RawConstraint.ResolvedType == VerifexType.Unknown) return false; // constraint is invalid, dont bother
            
            if (rawValue.EffectiveType is MaybeType maybeType)
            {
                Z3Mapper.MaybeTypeZ3Info info = _z3Mapper.GetMaybeTypeZ3Info(maybeType);
                FuncDecl tester = info.Constructors[target].TesterDecl;
                Z3BoolExpr testAssertion = (_z3Ctx.MkApp(tester, value) as Z3BoolExpr)!;
                
                _solver.Assert(testAssertion);
                value = _z3Ctx.MkApp(info.Constructors[target].AccessorDecls[0], value);
            }
            
            Z3BoolExpr assertion = _z3Ctx.MkNot(_z3Mapper.CreateRefinedTypeConstraintExpr(value, refinedType));
            
            _solver.Push();
            _solver.Assert(assertion);
            Status status = _solver.Check();
            _solver.Pop();
        
            return status == Status.UNSATISFIABLE;
        }
        
        // if target is a maybe type, check if the value can be applied to any of its components
        if (target.EffectiveType is MaybeType maybeType2)
            return maybeType2.Types.Any(t => IsValueAssignable(t, rawValue));
        
        // target isn't a maybe type or refined type, but if the source is a maybe type, we need to know if the path condition allows narrowing
        if (rawValue.EffectiveType is MaybeType maybeType3)
        {
            Z3BoolExpr narrowingAssertion = _z3Mapper.CreateMaybeTypeConstraintExpr(value, maybeType3, target.EffectiveType);
            
            _solver.Push();
            _solver.Assert(_z3Ctx.MkNot(narrowingAssertion));
            Status status = _solver.Check();
            _solver.Pop();
            
            return status == Status.UNSATISFIABLE;
        }

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
            // if the source is a refined type, we need to create its assertion
            if (source.EffectiveType is RefinedType sourceRefined)
            {
                CompatibilityStatus status = CompatibilityStatus.Incompatible;
                
                using Solver freshSolver = _z3Ctx.MkSolver();
                Z3Expr sourceTerm = _z3Mapper.CreateTerm(source, "source");
                Z3BoolExpr sourcePredicate = _z3Mapper.CreateRefinedTypeConstraintExpr(sourceTerm, sourceRefined);
                
                Z3BoolExpr targetPredicate = _z3Mapper.CreateRefinedTypeConstraintExpr(sourceTerm, refinedType);
                Z3BoolExpr assertion = _z3Ctx.MkAnd(targetPredicate, sourcePredicate);
            
                // if source condition and target condition are both true, then it's at least contextual
                if (freshSolver.Check(assertion) == Status.SATISFIABLE)
                    status = CompatibilityStatus.Contextual;
            
                // now we check if we can promote to compatible if the source implies the target
                if (status == CompatibilityStatus.Contextual)
                {
                    freshSolver.Assert(sourcePredicate);
                    freshSolver.Assert(_z3Ctx.MkNot(targetPredicate));
                    if (freshSolver.Check() == Status.UNSATISFIABLE)
                        status = CompatibilityStatus.Compatible;
                }

                return status;
            }
            
            if (source.EffectiveType is MaybeType sourceMaybe) // if it's a maybe type, we need to test its arms
            {
                if (sourceMaybe.Types.All(s => GetTypeCompatibility(target, s) == CompatibilityStatus.Incompatible))
                    return CompatibilityStatus.Incompatible;
                if (sourceMaybe.Types.All(s => GetTypeCompatibility(target, s) == CompatibilityStatus.Compatible))
                    return CompatibilityStatus.Compatible;

                return CompatibilityStatus.Contextual;
            }
            
            // target is a refined type but source is neither refined or maybe, so it's contextual
            return target.FundamentalType == source.FundamentalType ? CompatibilityStatus.Contextual : CompatibilityStatus.Incompatible;
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

    private void UpdateResolvedTypeInZ3Context(MemberAccessNode memberAccessNode)
    {
        AstNode target = memberAccessNode.Target;
        VerifexType? targetEffectiveType = target.EffectiveType;

        if (targetEffectiveType is MaybeType maybeTargetType)
        {
            Z3Expr z3TargetTerm = _z3Mapper.ConvertExpr(target);

            VerifexType? definiteType = null;
            int possibleOptions = 0;
            
            foreach (VerifexType concreteOption in maybeTargetType.Types)
            {
                if (concreteOption.EffectiveType is not StructType potentialStructType) continue;

                _solver.Push();
                _solver.Assert(_z3Mapper.CreateMaybeTypeConstraintExpr(z3TargetTerm, maybeTargetType, potentialStructType));

                if (_solver.Check() == Status.SATISFIABLE)
                {
                    possibleOptions++;
                    definiteType = potentialStructType; // store the candidate
                }

                _solver.Pop();
            }

            // was the candidate uniquely determined?
            if (possibleOptions == 1 && definiteType is StructType singleStructType)
            {
                StructSymbol structSymbol = Symbols.GetSymbol<StructSymbol>(singleStructType.Name);
                if (structSymbol.Fields.TryGetValue(memberAccessNode.Member.Identifier,
                        out StructFieldSymbol? fieldSymbol))
                {
                    memberAccessNode.ResolvedType = fieldSymbol.ResolvedType;
                    memberAccessNode.Symbol = fieldSymbol;
                }
                else if (structSymbol.Methods.TryGetValue(memberAccessNode.Member.Identifier, 
                             out FunctionSymbol? methodSymbol) && !methodSymbol.Function.IsStatic)
                    memberAccessNode.Symbol = methodSymbol;
                else
                {
                    // the type was unique, but unknown specified member
                    LogDiagnostic(new UnknownStructField(singleStructType.Name, memberAccessNode.Member.Identifier) { Location = memberAccessNode.Location });
                    memberAccessNode.ResolvedType = VerifexType.Unknown;
                }
            }
            else if (possibleOptions > 1)
            {
                LogDiagnostic(new MemberAccessOnAmbiguousType(memberAccessNode.Member.Identifier, maybeTargetType.Name) { Location = memberAccessNode.Location });
                memberAccessNode.ResolvedType = VerifexType.Unknown;
            }
            else
            {
                LogDiagnostic(new UnknownStructField(maybeTargetType.Name, memberAccessNode.Member.Identifier) { Location = memberAccessNode.Location });
                memberAccessNode.ResolvedType = VerifexType.Unknown;
            }
        }
    }

    private VerifexType NarrowTypeFor(Z3Expr value, MaybeType maybeType)
    {
        if (_solver.Check() != Status.SATISFIABLE)
            return VerifexType.Unknown;

        List<VerifexType> possibleTypes = [];
        Z3Mapper.MaybeTypeZ3Info info = _z3Mapper.GetMaybeTypeZ3Info(maybeType);

        foreach (VerifexType component in maybeType.Types)
        {
            _solver.Push();
            
            FuncDecl tester = info.Constructors[component].TesterDecl;
            Z3BoolExpr isComponentAssertion = (_z3Ctx.MkApp(tester, value) as Z3BoolExpr)!;
            
            if (component.EffectiveType is RefinedType refinedComponent)
            {
                Z3Expr unboxedTerm = _z3Ctx.MkApp(info.Constructors[component].AccessorDecls[0], value);
                Z3BoolExpr refinementConstraint = _z3Mapper.CreateRefinedTypeConstraintExpr(unboxedTerm, refinedComponent);
                _solver.Assert(_z3Ctx.MkAnd(isComponentAssertion, refinementConstraint));
            }
            else
                _solver.Assert(isComponentAssertion);
            
            if (_solver.Check() == Status.SATISFIABLE)
                possibleTypes.Add(component);
            
            _solver.Pop();
        }
        
        if (possibleTypes.Count == 0) return VerifexType.Unknown;
        if (possibleTypes.Count == 1) return possibleTypes[0];
        if (possibleTypes.Count == maybeType.Types.Count) return maybeType;

        return new MaybeType(possibleTypes);
    }

    private Z3Expr LowerAstNodeToZ3(AstNode node)
    {
        if (node.ResolvedType == null)
            throw new InvalidOperationException("Cannot lower AST node without a resolved type");

        try
        {
            return _z3Mapper.ConvertExpr(node);
        }
        catch (Z3MapperException)
        {
            // not much we can do - there's an error in the node. it's caught elsewhere, so the best we can do
            // is return a fresh term of the expected sort.
            return _z3Mapper.CreateTerm(node.ResolvedType, "arbitrary");
        }
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

public class TermStack
{
    private readonly Stack<Dictionary<Symbol, Z3Expr>> _stack = [];

    public void Push() => _stack.Push([]);
    
    public void SetTerm(Symbol symbol, Z3Expr value) => _stack.Peek()[symbol] = value;
    
    public Z3Expr GetTerm(Symbol symbol)
    {
        foreach (Dictionary<Symbol, Z3Expr> dict in _stack)
        {
            if (dict.TryGetValue(symbol, out Z3Expr? value))
                return value;
        }
        
        throw new InvalidOperationException($"Symbol {symbol.Name} not found");
    }
    
    public bool TryGetTerm(Symbol symbol, [MaybeNullWhen(false)] out Z3Expr value)
    {
        foreach (Dictionary<Symbol, Z3Expr> dict in _stack)
        {
            if (dict.TryGetValue(symbol, out value))
                return true;
        }
        
        value = null;
        return false;
    }

    public bool Contains(Symbol symbol)
    {
        foreach (Dictionary<Symbol, Z3Expr> dict in _stack)
        {
            if (dict.TryGetValue(symbol, out Z3Expr? value))
                return true;
        }
        
        return false;
    }
    
    public void Pop() => _stack.Pop();

    public void Clear()
    {
        _stack.Clear();
        _stack.Push([]);
    }
}