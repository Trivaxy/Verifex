using System.Collections.ObjectModel;
using Microsoft.Z3;
using Verifex.Analysis.Pass;
using Verifex.CodeGen.Types;
using Verifex.Parsing;

namespace Verifex.Analysis.Mapping;

public class Z3Mapper
{
    private readonly Context _ctx;
    private readonly Solver _solver;
    private readonly TermStack _termStack;
    private readonly SymbolTable _symbols;
    private readonly UninterpretedSort _anySort;
    private readonly UninterpretedSort _voidSort;
    private readonly UninterpretedSort _unknownSort;
    private readonly Dictionary<VerifexType, FuncDecl> _toStringFuncDecls;
    private readonly Dictionary<StructType, DatatypeSort> _structToDatatypeSort;
    private readonly Dictionary<MaybeType, MaybeTypeZ3Info> _maybeTypeZ3Infos;
    private int _nextTermId = 0;
    
    public Z3Expr? CurrentSelfTerm { get; set; }

    public Z3Mapper(Context ctx, Solver solver, TermStack termStack, SymbolTable symbols)
    {
        _ctx = ctx;
        _solver = solver;
        _termStack = termStack;
        _symbols = symbols;
        _anySort = ctx.MkUninterpretedSort("Any");
        _voidSort = ctx.MkUninterpretedSort("Void");
        _unknownSort = ctx.MkUninterpretedSort("Unknown");
        _toStringFuncDecls = CreateZ3ToStringFuncDecls();
        _structToDatatypeSort = [];
        _maybeTypeZ3Infos = [];
        CurrentSelfTerm = null;
    }
    
    public Z3Expr ConvertExpr(AstNode node)
    {
        switch (node)
        {
            case NumberNode n: return ConvertNumber(n);
            case BoolLiteralNode b: return ConvertBool(b);
            case StringLiteralNode s: return ConvertString(s);
            case IdentifierNode id: return ConvertIdentifier(id);
            case BinaryOperationNode binOp: return ConvertBinaryOperation(binOp);
            case MinusNegationNode unOp: return ConvertMinusNegation(unOp);
            case NotNegationNode notOp: return ConvertNotNegation(notOp);
            case InitializerNode init: return ConvertInitializer(init);
            case FunctionCallNode call: return ConvertFunctionCall(call);
            case MemberAccessNode member: return ConvertMemberAccess(member);
            case IsCheckNode check: return ConvertIsCheck(check);
            default: 
                throw new NotImplementedException($"Z3 conversion not implemented for AST node type: {node.GetType().Name}");
        }
    }

    private Z3Expr ConvertNumber(NumberNode node)
    {
        if (node.NumberType == NumberType.Integer)
            return _ctx.MkInt(node.AsInteger());

        return _ctx.MkReal(node.AsDouble().ToString());
    }
    
    private Z3Expr ConvertBool(BoolLiteralNode node) => node.Value ? _ctx.MkTrue() : _ctx.MkFalse();
    
    private Z3Expr ConvertString(StringLiteralNode node) => _ctx.MkString(node.Value);

    private Z3Expr ConvertIdentifier(IdentifierNode node)
    {
        if (node.Symbol == null)
            throw new SymbolNotBoundException(node.Identifier);

        if (node.Symbol is StructFieldSymbol field)
        {
            if (CurrentSelfTerm == null)
                throw new InvalidOperationException($"Cannot access field '{field.Name}' without a current self term");
            
            return _ctx.MkApp(DatatypeSortForStruct((field.Owner.ResolvedType as StructType)!).Accessors[0][field.Index], CurrentSelfTerm);
        }

        if (_termStack.TryGetTerm(node.Symbol, out Z3Expr? z3Expr))
        {
            // the type has been narrowed down, we need to take it out of the maybe type
            if (node.Symbol.ResolvedType != node.ResolvedType)
                return CreateUnbox(z3Expr, (node.Symbol.ResolvedType.EffectiveType as MaybeType)!, node.ResolvedType);
            return z3Expr;
        }

        throw new SymbolNotAValueException(node.Identifier);
    }

    private Z3Expr ConvertBinaryOperation(BinaryOperationNode node)
    {
        Z3Expr left = ConvertExpr(node.Left);
        Z3Expr right = ConvertExpr(node.Right);

        switch (node.Operator.Type)
        {
            case TokenType.Plus:
                if (node.FundamentalType is StringType)
                {
                    if (node.Left.FundamentalType is not StringType)
                        left = _ctx.MkApp(_toStringFuncDecls[node.Left.FundamentalType!], left);
                    if (node.Right.FundamentalType is not StringType)
                        right = _ctx.MkApp(_toStringFuncDecls[node.Right.FundamentalType!], right);
                    
                    return _ctx.MkConcat((Z3SeqExpr)left, (Z3SeqExpr)right);
                }

                return _ctx.MkAdd((Z3ArithExpr)left, (Z3ArithExpr)right);
            case TokenType.Minus: return _ctx.MkSub((Z3ArithExpr)left, (Z3ArithExpr)right);
            case TokenType.Star: return _ctx.MkMul((Z3ArithExpr)left, (Z3ArithExpr)right);
            case TokenType.Slash: return _ctx.MkDiv((Z3ArithExpr)left, (Z3ArithExpr)right);

            case TokenType.EqualEqual: return _ctx.MkEq(left, right); // Generic equality
            case TokenType.NotEqual: return _ctx.MkNot(_ctx.MkEq(left, right));
            case TokenType.LessThan: return _ctx.MkLt((Z3ArithExpr)left, (Z3ArithExpr)right);
            case TokenType.LessThanOrEqual: return _ctx.MkLe((Z3ArithExpr)left, (Z3ArithExpr)right);
            case TokenType.GreaterThan: return _ctx.MkGt((Z3ArithExpr)left, (Z3ArithExpr)right);
            case TokenType.GreaterThanOrEqual: return _ctx.MkGe((Z3ArithExpr)left, (Z3ArithExpr)right);

            case TokenType.And: return _ctx.MkAnd((Z3BoolExpr)left, (Z3BoolExpr)right);
            case TokenType.Or: return _ctx.MkOr((Z3BoolExpr)left, (Z3BoolExpr)right);

            default:
                throw new NotImplementedException($"Z3 conversion not implemented for binary operator: {node.Operator.Type}");
        }
    }

    private Z3Expr ConvertMinusNegation(MinusNegationNode node)
    {
        Z3Expr operand = ConvertExpr(node.Operand);
        return _ctx.MkUnaryMinus((Z3ArithExpr)operand);
    }
    
    private Z3Expr ConvertNotNegation(NotNegationNode node)
    {
        Z3Expr operand = ConvertExpr(node.Operand);
        return _ctx.MkNot((Z3BoolExpr)operand);
    }

    private Z3Expr ConvertInitializer(InitializerNode node)
    {
        StructType structType = (node.FundamentalType as StructType)!;
        DatatypeSort datatype = DatatypeSortForStruct(structType);
        InitializerListNode initializers = node.InitializerList;
        Z3Expr?[] args = new Z3Expr[initializers.Values.Count];

        foreach (InitializerFieldNode fieldInit in initializers.Values)
        {
            if (!structType.Fields.ContainsKey(fieldInit.Name.Identifier)) continue; // the field doesn't even exist, ignore
            
            Z3Expr value = ConvertExpr(fieldInit.Value);
            FieldInfo targetFieldInfo = structType.Fields[fieldInit.Name.Identifier];
            int index = (node.Type.Symbol as StructSymbol)!.Fields[fieldInit.Name.Identifier].Index;
            if (targetFieldInfo.Type.FundamentalType is MaybeType maybeType)
            {
                MaybeTypeZ3Info maybeInfo = GetMaybeTypeZ3Info(maybeType);
                if (!maybeInfo.Constructors.TryGetValue(fieldInit.Value.EffectiveType, out Constructor? constructor))
                    throw new MismatchedTypesException(maybeType.Name, fieldInit.Value.ResolvedType.Name);

                FuncDecl constructorDecl = maybeInfo.Constructors[fieldInit.Value.EffectiveType].ConstructorDecl;
                args[index] = _ctx.MkApp(constructorDecl, value);
            }
            else
                args[index] = value;
        }
        
        // if any of the arguments is null, means the user didn't even specify the field, so stop
        if (args.Any(a => a == null))
            throw new MissingFieldException();
        
        return _ctx.MkApp(datatype.Constructors[0], args);
    }

    private Z3Expr ConvertFunctionCall(FunctionCallNode node)
    {
        return CreateTerm(node.ResolvedType!, "t");
    }

    private Z3Expr ConvertMemberAccess(MemberAccessNode node)
    {
        StructType structType = (node.Target.FundamentalType as StructType)!;
        DatatypeSort datatype = DatatypeSortForStruct(structType);
        Z3Expr target = ConvertExpr(node.Target);

        int i = 0;
        foreach (FieldInfo field in structType.Fields.Values)
        {
            if (field.Name == node.Member.Identifier)
                return _ctx.MkApp(datatype.Accessors[0][i], target);
            i++;
        }
        
        throw new InvalidOperationException($"Field '{node.Member.Identifier}' not found in struct '{structType.Name}'");
    }

    private Z3Expr ConvertIsCheck(IsCheckNode node)
    {
        if (node.Value.EffectiveType is not MaybeType maybeType)
            throw new CannotUseIsOnNonMaybeTypeException();
        
        Z3Expr value = ConvertExpr(node.Value);
        MaybeTypeZ3Info maybe = GetMaybeTypeZ3Info(maybeType);

        if (!maybe.Testers.ContainsKey(node.TestedType.EffectiveType))
            throw new NotComponentOfMaybeType(node.TestedType.Identifier);
        
        FuncDecl tester = maybe.Testers[node.TestedType.EffectiveType!];
        Z3BoolExpr typeCheck = (_ctx.MkApp(tester, value) as Z3BoolExpr)!;

        if (node.TestedType.EffectiveType is RefinedType refinedType)
        {
            Z3Expr unboxed = _ctx.MkApp(maybe.Constructors[refinedType].AccessorDecls[0], value);
            typeCheck = _ctx.MkAnd(typeCheck, CreateRefinedTypeConstraintExpr(unboxed, refinedType));
        }

        return typeCheck;
    }
    
    public Z3Expr CreateTerm(VerifexType type, string name)
    {
        Z3Expr term;
        string termName = $"{name}_{_nextTermId++}";
        if (type.FundamentalType is IntegerType)
            term = _ctx.MkIntConst(termName);
        else if (type.FundamentalType is RealType)
            term = _ctx.MkRealConst(termName);
        else if (type.FundamentalType is BoolType)
            term = _ctx.MkBoolConst(termName);
        else if (type.FundamentalType is StringType)
            term = _ctx.MkString(termName);
        else if (type.FundamentalType is AnyType)
            term = _ctx.MkConst(termName, _anySort);
        else if (type.FundamentalType is StructType structType)
            term = _ctx.MkConst(termName, DatatypeSortForStruct(structType));
        else if (type.FundamentalType is MaybeType maybeType)
            term = _ctx.MkConst(termName, GetMaybeTypeZ3Info(maybeType).Sort);
        else if (type.FundamentalType is CodeGen.Types.UnknownType)
            term = _ctx.MkConst(termName, _voidSort);
        else
            throw new NotImplementedException();

        if (type.EffectiveType is RefinedType refinedType)
            _solver.Assert(CreateRefinedTypeConstraintExpr(term, refinedType));

        return term;
    }
    
    // Generates a Z3 expression by substituting the given term into the 'value' in a refined type's constraint expression
    // If the refined type has another refined type as a base type, the expression is ANDed with the base type's constraint
    public Z3BoolExpr CreateRefinedTypeConstraintExpr(Z3Expr term, RefinedType refinedType)
    {
        RefinedTypeValueSymbol valueSymbol = _symbols.GetGlobalSymbol<RefinedTypeSymbol>(refinedType.Name).ValueSymbol;
        _termStack.Push();
        _termStack.SetTerm(valueSymbol, term);
        
        Z3BoolExpr? assertion = ConvertExpr(refinedType.RawConstraint) as Z3BoolExpr;
        if (assertion == null) return _ctx.MkFalse(); // if the lowering of the constraint fails, it's not a bool expression, return false constant
        
        if (refinedType.BaseType.EffectiveType is RefinedType baseRefinedType)
            assertion = _ctx.MkAnd(assertion, CreateRefinedTypeConstraintExpr(term, baseRefinedType));
        else if (refinedType.BaseType.EffectiveType is MaybeType maybeType)
            assertion = _ctx.MkAnd(assertion, CreateMaybeTypeConstraintExpr(term, maybeType, refinedType.BaseType));
        
        _termStack.Pop();
        return assertion;
    }

    public Z3BoolExpr CreateMaybeTypeConstraintExpr(Z3Expr term, MaybeType maybeType, VerifexType testedType)
    {
        if (maybeType.Types.All(t => t.FundamentalType != testedType.FundamentalType))
            throw new InvalidOperationException($"Type '{testedType.Name}' is not a valid type for MaybeType '{maybeType.Name}'");
        
        MaybeTypeZ3Info maybeInfo = GetMaybeTypeZ3Info(maybeType);
        FuncDecl tester = maybeInfo.Testers[testedType];
        FuncDecl accessor = maybeInfo.Constructors[testedType].AccessorDecls[0];
        Z3BoolExpr typeCheck = (_ctx.MkApp(tester, term) as Z3BoolExpr)!;
        
        if (testedType.EffectiveType is RefinedType refinedType)
            typeCheck = _ctx.MkAnd(typeCheck, CreateRefinedTypeConstraintExpr(_ctx.MkApp(accessor, term), refinedType));
        
        return typeCheck;
    }
    
    private Dictionary<VerifexType, FuncDecl> CreateZ3ToStringFuncDecls()
    {
        Dictionary<VerifexType, FuncDecl> funcDecls = [];
        
        foreach (VerifexType type in _symbols.GetTypes())
        {
            if (type is not RefinedType)
                funcDecls[type] = _ctx.MkFuncDecl($"{type.Name}ToString", AsSort(type), _ctx.StringSort);
        }

        foreach (VerifexType type in _symbols.GetTypes())
        {
            if (type is not RefinedType) continue;
            funcDecls[type] = funcDecls[type.FundamentalType]; // use the fundamental type's function decl
        }
        
        return funcDecls;
    }

    private Z3Expr CreateUnbox(Z3Expr value, MaybeType maybeType, VerifexType chosenType)
    {
        MaybeTypeZ3Info info = GetMaybeTypeZ3Info(maybeType);
        return _ctx.MkApp(info.Constructors[chosenType].AccessorDecls[0], value);
    }
    
    public Sort AsSort(VerifexType type)
    {
        return type.FundamentalType switch
        {
            IntegerType => _ctx.IntSort,
            RealType => _ctx.RealSort,
            BoolType => _ctx.BoolSort,
            StringType => _ctx.StringSort,
            AnyType => _anySort,
            StructType structType => DatatypeSortForStruct(structType),
            MaybeType maybeType => GetMaybeTypeZ3Info(maybeType).Sort,
            CodeGen.Types.UnknownType => _anySort,
            _ => throw new NotImplementedException($"Type has no known sort: {type.Name}")
        };
    }
    
    public DatatypeSort DatatypeSortForStruct(StructType type)
    {
        if (_structToDatatypeSort.TryGetValue(type, out DatatypeSort? sort))
            return sort;
        
        _structToDatatypeSort[type] = CreateDatatypeSortForStruct(type);
        return _structToDatatypeSort[type];
    }

    private DatatypeSort CreateDatatypeSortForStruct(StructType type)
    {
        Constructor constructor = _ctx.MkConstructor(
            "Mk" + type.Name,
            "Is" + type.Name,
            type.Fields.Keys.ToArray(),
            type.Fields.Values.Select(f => AsSort(f.Type)).ToArray()
        );
        
        DatatypeSort sort = _ctx.MkDatatypeSort(type.Name, [constructor]);
        return sort;
    }
    
    public MaybeTypeZ3Info GetMaybeTypeZ3Info(MaybeType type)
    {
        if (_maybeTypeZ3Infos.TryGetValue(type, out MaybeTypeZ3Info? info))
            return info;
        
        _maybeTypeZ3Infos[type] = CreateInfoForMaybeType(type);
        return _maybeTypeZ3Infos[type];
    }

    private MaybeTypeZ3Info CreateInfoForMaybeType(MaybeType type)
    {
        Dictionary<VerifexType, Constructor> constructors = [];
        Dictionary<VerifexType, FuncDecl> testers = [];
        
        foreach (VerifexType potentialType in type.Types)
        {
            Constructor constructor = _ctx.MkConstructor(
                "Mk" + type.Name + "As" + potentialType.Name,
                "Is" + type.Name + "As" + potentialType.Name,
                [potentialType.Name],
                [AsSort(potentialType)]
            );
            
            constructors[potentialType] = constructor;
        }
        
        DatatypeSort sort = _ctx.MkDatatypeSort(type.Name, constructors.Values.ToArray());
        foreach (var kvp in constructors)
            testers[kvp.Key] = kvp.Value.TesterDecl;
        
        return new MaybeTypeZ3Info(sort, constructors.AsReadOnly(), testers.AsReadOnly());
    }

    public bool TryGetMaybeTypeInfo(Z3Expr expr, out MaybeTypeZ3Info? info)
    {
        info = _maybeTypeZ3Infos.Values.FirstOrDefault(info => info.Sort.Equals(expr.Sort));
        return info != null;
    }

    public record MaybeTypeZ3Info(
        DatatypeSort Sort,
        ReadOnlyDictionary<VerifexType, Constructor> Constructors,
        ReadOnlyDictionary<VerifexType, FuncDecl> Testers);
}