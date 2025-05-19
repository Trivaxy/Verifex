using System.Collections.ObjectModel;
using System.Text;
using Verifex.Parsing;

namespace Verifex.Analysis;

public class ControlFlowGraph
{
    public BasicBlock Entry { get; }
    public BasicBlock Exit { get; }
    public ReadOnlyCollection<BasicBlock> Blocks { get; }

    internal ControlFlowGraph(BasicBlock entry, BasicBlock exit, List<BasicBlock> blocks)
    {
        Entry = entry;
        Exit = exit;
        Blocks = blocks.AsReadOnly();
    }
    
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine("CFG:");
        
        foreach (var block in Blocks)
            sb.AppendLine(block.ToString());

        return sb.ToString();
    }
}

public class BasicBlock
{
    private static int _nextId = 0;
    
    public int Id { get; } = _nextId++;
    
    public List<AstNode> Statements { get; } = [];
    
    public List<BasicBlock> Predecessors { get; } = [];
    
    public BasicBlock? UnconditionalSuccessor { get; private set; }
    
    public BasicBlock? TrueSuccessor { get; private set; }
    
    public BasicBlock? FalseSuccessor { get; private set; }

    public bool IsEntry => Id == 0; 
    
    public bool IsExit => Id == 1;

    public bool IsTerminated => UnconditionalSuccessor != null || TrueSuccessor != null || FalseSuccessor != null;
    
    public bool HasConditionalSuccessors => TrueSuccessor != null || FalseSuccessor != null;

    // Special constructor for Entry/Exit sentinel blocks
    internal BasicBlock(bool isEntry)
    {
        Id = isEntry ? 0 : 1;
        if (!isEntry) _nextId = 2; // Reset counter after exit is created
    }

    internal BasicBlock()
    {
    } // Regular block constructor

    internal void AddStatement(AstNode node) => Statements.Add(node);

    internal void SetUnconditionalSuccessor(BasicBlock successor)
    {
        if (IsTerminated) throw new InvalidOperationException($"Block {Id} is already terminated.");
        
        UnconditionalSuccessor = successor;
        successor?.Predecessors.Add(this);
    }

    internal void SetConditionalSuccessors(BasicBlock trueSuccessor, BasicBlock falseSuccessor)
    {
        if (IsTerminated) throw new InvalidOperationException($"Block {Id} is already terminated.");
        
        TrueSuccessor = trueSuccessor;
        FalseSuccessor = falseSuccessor;
        trueSuccessor?.Predecessors.Add(this);
        falseSuccessor?.Predecessors.Add(this);
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Block {Id}{(IsEntry ? " (Entry)" : IsExit ? " (Exit)" : "")}:");
        sb.AppendLine($"  Predecessors: [{string.Join(", ", Predecessors.Select(p => p.Id))}]");
        
        foreach (var stmt in Statements)
        {
            // Basic representation, could be improved
            sb.AppendLine($"    {stmt.GetType().Name}");
        }

        if (UnconditionalSuccessor != null)
            sb.AppendLine($"  Successor: -> Block {UnconditionalSuccessor.Id}");
        if (TrueSuccessor != null)
            sb.AppendLine($"  True Successor: -> Block {TrueSuccessor.Id}");
        if (FalseSuccessor != null)
            sb.AppendLine($"  False Successor: -> Block {FalseSuccessor.Id}");

        return sb.ToString();
    }
}

public class CFGBuilder
{
    private readonly List<BasicBlock> _blocks = [];
    private readonly BasicBlock _entryBlock;
    private readonly BasicBlock _exitBlock;
    private BasicBlock? _currentBlock; // The block currently being built

    private CFGBuilder()
    {
        _entryBlock = new BasicBlock(isEntry: true);
        _exitBlock = new BasicBlock(isEntry: false);
        _blocks.Add(_entryBlock);
        _blocks.Add(_exitBlock);
        _currentBlock = _entryBlock; // Start building from the entry block
    }

    public static ControlFlowGraph Build(FunctionDeclNode function)
    {
        var builder = new CFGBuilder();
        builder.Visit(function.Body);

        // Ensure the last block processed connects to the exit if not already terminated
        // (Handles functions ending without an explicit return)
        if (builder._currentBlock != null && !builder._currentBlock.IsTerminated)
            builder.ConnectBlocks(builder._currentBlock, builder._exitBlock);

        // Prune unreachable blocks (optional but good practice)
        var reachableBlocks = PruneUnreachable(builder._entryBlock, builder._blocks);

        return new ControlFlowGraph(builder._entryBlock, builder._exitBlock, reachableBlocks);
    }

    // --- Visitor Methods ---

    private void Visit(AstNode node)
    {
        // If the current flow path was terminated (e.g., by return),
        // later statements might be unreachable unless targeted by a jump.
        // For simplicity here, we'll just skip visiting if _currentBlock is null.
        // A more robust implementation might create unreachable blocks.
        if (_currentBlock == null) return;

        switch (node)
        {
            case BlockNode block: VisitBlock(block); break;
            case IfElseNode ifElse: VisitIfElse(ifElse); break;
            case WhileNode whileNode: VisitWhile(whileNode); break;
            case ReturnNode returnNode: VisitReturn(returnNode); break;
            // Add other statement types that don't affect control flow
            case VarDeclNode varDecl: VisitStatement(varDecl); break;
            case AssignmentNode assignment: VisitStatement(assignment); break;
            case FunctionCallNode funcCall: VisitStatement(funcCall); break;
        }
    }

    private void VisitBlock(BlockNode node)
    {
        foreach (var statement in node.Nodes)
        {
            Visit(statement);
            // If a statement terminated the block (like return), stop processing this block
            if (_currentBlock == null) break;
        }
    }

    private void VisitStatement(AstNode node)
    {
        EnsureCurrentBlock(); // Make sure we have a block to add to
        _currentBlock!.AddStatement(node);
    }

    private void VisitReturn(ReturnNode node)
    {
        EnsureCurrentBlock();
        _currentBlock!.AddStatement(node);
        ConnectBlocks(_currentBlock, _exitBlock);
        _currentBlock = null; // Mark current path as terminated
    }

    private void VisitIfElse(IfElseNode node)
    {
        EnsureCurrentBlock();
        var conditionBlock = _currentBlock!;
        conditionBlock.AddStatement(node.Condition); // Condition evaluation ends the preceding block

        var trueBlock = StartNewBlock();
        var joinBlock = StartNewBlock(); // Block for convergence after the if/else

        BasicBlock falsePathTarget;
        
        if (node.ElseBody != null)
        {
            var elseBlock = StartNewBlock();
            falsePathTarget = elseBlock;
            
            _currentBlock = elseBlock;
            Visit(node.ElseBody);
            
            var lastElseBlock = _currentBlock; // Capture the last block of the else branch
            if (lastElseBlock != null && !lastElseBlock.IsTerminated)
                ConnectBlocks(lastElseBlock, joinBlock); // Connect end of else branch to join
        }
        else
        {
            // If there is NO else body, the false path goes DIRECTLY to the join block
            falsePathTarget = joinBlock;
            // No empty false block is created or connected here
        }
        
        // Now connect the condition block using the determined targets
        ConnectBlocks(conditionBlock, trueSuccessor: trueBlock, falseSuccessor: falsePathTarget);
        
        _currentBlock = trueBlock;
        Visit(node.IfBody);
        var lastTrueBlock = _currentBlock; // Capture the last block of the true branch
        if (lastTrueBlock != null && !lastTrueBlock.IsTerminated)
            ConnectBlocks(lastTrueBlock, joinBlock); // Connect end of true branch to join
        
        // Resume building from the join block
        _currentBlock = joinBlock;
    }

    private void VisitWhile(WhileNode node)
    {
        EnsureCurrentBlock();
        var preLoopBlock = _currentBlock!;

        // Condition Block
        var conditionBlock = StartNewBlock();
        ConnectBlocks(preLoopBlock, conditionBlock); // Connect block before loop to condition
        conditionBlock.AddStatement(node.Condition);
        _currentBlock = conditionBlock; // Temporarily set for connection setup

        // Blocks for Body and After Loop
        var bodyBlock = StartNewBlock();
        var afterLoopBlock = StartNewBlock();

        // Connect condition block T/F exits
        ConnectBlocks(conditionBlock, trueSuccessor: bodyBlock, falseSuccessor: afterLoopBlock);

        // Visit Loop Body
        _currentBlock = bodyBlock;
        Visit(node.Body);
        var lastBodyBlock = _currentBlock; // Capture the last block of the body

        if (lastBodyBlock != null && !lastBodyBlock.IsTerminated)
        {
            // Connect end of loop body back to the condition
            ConnectBlocks(lastBodyBlock, conditionBlock);
        }

        // Resume building after the loop
        _currentBlock = afterLoopBlock;
    }


    // --- Helper Methods ---

    private BasicBlock StartNewBlock()
    {
        var newBlock = new BasicBlock();
        _blocks.Add(newBlock);
        return newBlock;
    }

    // Ensures _currentBlock is not null, creating a new one if needed
    // (e.g., after a return or at the very beginning)
    private void EnsureCurrentBlock()
    {
        if (_currentBlock == null)
        {
            _currentBlock = StartNewBlock();
            // Note: If we just created a block after a return, it might be
            // unreachable unless targeted by a jump (like an else branch).
            // The PruneUnreachable step handles this.
        }
        // If the current block is already terminated (e.g. connected to exit by return)
        // we need to start a new one for subsequent statements.
        else if (_currentBlock.IsTerminated)
        {
            // Only start a new block if the previous one wasn't connected to exit
            // (prevents creating unnecessary blocks right before exit)
            if (_currentBlock.UnconditionalSuccessor != _exitBlock)
            {
                var previousBlock = _currentBlock;
                _currentBlock = StartNewBlock();
                // If the previous block had an unconditional successor (like after an if/while join)
                // connect it to the newly started block.
                if (previousBlock.UnconditionalSuccessor != null)
                    ConnectBlocks(previousBlock, _currentBlock);
                
                // This logic might need refinement depending on how joins are handled exactly.
            }
        }
    }


    private void ConnectBlocks(BasicBlock from, BasicBlock to)
    {
        if (from.IsTerminated) return; // Avoid overwriting existing connections
        from.SetUnconditionalSuccessor(to);
    }

    private void ConnectBlocks(BasicBlock from, BasicBlock trueSuccessor, BasicBlock falseSuccessor)
    {
        if (from.IsTerminated) return; // Avoid overwriting existing connections
        from.SetConditionalSuccessors(trueSuccessor, falseSuccessor);
    }

    // --- Unreachable Block Pruning ---
    private static List<BasicBlock> PruneUnreachable(BasicBlock entry, List<BasicBlock> allBlocks)
    {
        var reachable = new HashSet<BasicBlock>();
        var queue = new Queue<BasicBlock>();

        queue.Enqueue(entry);
        reachable.Add(entry);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            var successors = new[] { current.UnconditionalSuccessor, current.TrueSuccessor, current.FalseSuccessor };
            foreach (var successor in successors)
                if (successor != null && reachable.Add(successor))
                    queue.Enqueue(successor);
        }

        // Filter the original list and also remove dangling predecessor links
        var reachableList = allBlocks.Where(reachable.Contains).ToList();
        foreach (var block in reachableList)
            block.Predecessors.RemoveAll(p => !reachable.Contains(p));

        return reachableList;
    }
}