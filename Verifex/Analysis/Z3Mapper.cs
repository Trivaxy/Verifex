using Microsoft.Z3;
using Verifex.CodeGen.Types;
using Verifex.Parsing;

namespace Verifex.Analysis;

public class Z3Mapper
{
    private readonly Context _ctx;
    private readonly Solver _solver;
    private readonly Dictionary<Symbol, Z3Expr> _termMap;
    private readonly SymbolTable _symbols;
    private readonly UninterpretedSort _anySort;
    private readonly UninterpretedSort _voidSort;
    private readonly Dictionary<VerifexType, FuncDecl> _toStringFuncDecls;
    private readonly Dictionary<StructType, DatatypeSort> _structToDatatypeSort;
    private int _nextTermId = 0;
    
    public Z3Expr? CurrentSelfTerm { get; set; }

    public Z3Mapper(Context ctx, Solver solver, Dictionary<Symbol, Z3Expr> termMap, SymbolTable symbols)
    {
        _ctx = ctx;
        _solver = solver;
        _termMap = termMap;
        _symbols = symbols;
        _anySort = ctx.MkUninterpretedSort("Any");
        _voidSort = ctx.MkUninterpretedSort("Void");
        _toStringFuncDecls = CreateZ3ToStringFuncDecls();
        _structToDatatypeSort = [];
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
            throw new InvalidOperationException($"Identifier '{node.Identifier}' has no associated symbol");

        if (node.Symbol is StructFieldSymbol field)
        {
            if (CurrentSelfTerm == null)
                throw new InvalidOperationException($"Cannot access field '{field.Name}' without a current self term");
            
            return _ctx.MkApp(DatatypeSortForStruct((field.Owner.ResolvedType as StructType)!).Accessors[0][field.Index], CurrentSelfTerm);
        }
        
        if (_termMap.TryGetValue(node.Symbol, out Z3Expr? z3Expr))
            return z3Expr;

        throw new InvalidOperationException($"No Z3 expression found for symbol '{node.Symbol.Name}' in the current context");
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
        DatatypeSort datatype = DatatypeSortForStruct((node.FundamentalType as StructType)!);
        Z3Expr[] args = new Z3Expr[node.InitializerList.Values.Count];
        
        for (int i = 0; i < node.InitializerList.Values.Count; i++)
            args[i] = ConvertExpr(node.InitializerList.Values[i].Value);

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
        _termMap[valueSymbol] = term;
        
        Z3BoolExpr assertion = (ConvertExpr(refinedType.RawConstraint) as Z3BoolExpr)!;
        
        if (refinedType.BaseType.EffectiveType is RefinedType baseRefinedType)
            assertion = _ctx.MkAnd(assertion, CreateRefinedTypeConstraintExpr(term, baseRefinedType));
        
        _termMap.Remove(valueSymbol);
        return assertion;
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
    
    private Sort AsSort(VerifexType type)
    {
        return type.FundamentalType switch
        {
            IntegerType => _ctx.IntSort,
            RealType => _ctx.RealSort,
            BoolType => _ctx.BoolSort,
            StringType => _ctx.StringSort,
            AnyType => _anySort,
            VoidType => _voidSort,
            StructType structType => DatatypeSortForStruct(structType),
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
}