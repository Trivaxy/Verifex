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
        
        if (!IsTypeCompatible(node.Symbol!.ResolvedType!, node.Value!.ResolvedType!, node.Value))
            LogDiagnostic(new VarDeclTypeMismatch(node.Name, node.Symbol!.ResolvedType!.Name, node.Value!.ResolvedType!.Name) { Location = node.Location });
        else
            AssertAssignment(node.Symbol!, LowerAstNodeToZ3(node.Value));
    }

    protected override void Visit(AssignmentNode node)
    {
        Visit(node.Value);
        
        if (!IsTypeCompatible(node.Target!.Symbol!.ResolvedType!, node.Value!.ResolvedType!, node.Value))
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
                        
            if (!IsTypeCompatible(param.Type, argument.ResolvedType!, argument))
                LogDiagnostic(new ParamTypeMismatch(param.Name, param.Type.Name, argument.ResolvedType!.Name) { Location = argument.Location });
        }
    }
    
    protected override void Visit(ReturnNode node)
    {
        if (node.Value != null)
            Visit(node.Value);
        
        if (!IsTypeCompatible(_currentFunction.ReturnType, node.Value!.ResolvedType!, node.Value))
            LogDiagnostic(new ReturnTypeMismatch(_currentFunction.Name, _currentFunction.ReturnType.Name, node.Value.ResolvedType!.Name) { Location = node.Location });
    }

    protected override void Visit(InitializerNode node)
    {
        foreach (InitializerFieldNode field in node.InitializerList.Values)
        {
            if (field.EffectiveType == null) continue; // some error happened earlier, continue
            if (!IsTypeCompatible(field.Name.ResolvedType!, field.Value.ResolvedType!, field.Value))
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
            FuncDecl accessor = info!.Constructors[_z3Mapper.AsSort(target.ResolvedType!.EffectiveType)]
                .AccessorDecls[0];
            _solver.Assert(_z3Ctx.MkEq(_symbolsAsTerms[target], _z3Ctx.MkApp(accessor, value)));
        }
        else
            throw new InvalidOperationException("Unknown types for assignment");
    }
    
    
    private bool AreTypesBasicCompatible(VerifexType target, VerifexType source)
        => target.EffectiveType is not VoidType
           && source.EffectiveType is not VoidType // void is not assignable to anything
           && (target.EffectiveType is AnyType // you can assign anything to Any
               || target == source // same type obviously
               || target.FundamentalType == source.FundamentalType // same fundamental type
               || target.FundamentalType is RealType && source.FundamentalType is IntegerType // integer to real is implicitly allowed
               || target.FundamentalType is MaybeType maybe && maybe.Types.Contains(source) // "Foo" can be assigned to "Foo or Bar"
               || source.FundamentalType is MaybeType maybe2 && maybe2.Types.Contains(target) // "Foo or Bar" can be assigned to "Foo", depending on context
               );

    private bool IsTermAssignable(VerifexType target, Z3Expr value)
    {
        Z3BoolExpr assertion = _z3Ctx.MkTrue();
        if (target.EffectiveType is RefinedType refinedType) 
            assertion = _z3Ctx.MkNot(_z3Mapper.CreateRefinedTypeConstraintExpr(value, refinedType));
        else if (_z3Mapper.TryGetMaybeTypeInfo(value, out Z3Mapper.MaybeTypeZ3Info? maybeInfo))
            assertion = _z3Ctx.MkNot(_z3Ctx.MkApp(maybeInfo!.Testers[_z3Mapper.AsSort(target.EffectiveType)], value) as Z3BoolExpr);
        
        _solver.Push();
        _solver.Assert(assertion);
        Status status = _solver.Check();
        _solver.Pop();
        
        return status == Status.UNSATISFIABLE;
    }
    
    private bool IsTypeCompatible(VerifexType target, VerifexType source, AstNode sourceValue)
    {
        if (target.FundamentalType is AnyType) return true;
        if (target.FundamentalType is CodeGen.Types.UnknownType) return false;

        if (target.EffectiveType is MaybeType targetMaybe)
        {
            if (targetMaybe.Types.Contains(source))
                return true;
            
            if (source.EffectiveType is MaybeType sourceMaybe)
                return sourceMaybe.Types.All(s => targetMaybe.Types.Contains(s));
        }
        
        if (target.EffectiveType is not RefinedType && source.EffectiveType is not RefinedType && source.EffectiveType is not MaybeType)
            return AreTypesBasicCompatible(target, source);
        
        return AreTypesBasicCompatible(target, source) && IsTermAssignable(target, LowerAstNodeToZ3(sourceValue));
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