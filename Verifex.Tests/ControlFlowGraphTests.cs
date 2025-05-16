using System.Collections.ObjectModel;
using Verifex.Analysis;
using Verifex.Parsing;

namespace Verifex.Tests;

public class ControlFlowGraphTests
{
    private static FunctionDeclNode CreateTestFunction(string content)
    {
        var parser = new Parser(new TokenStream(content), content.AsMemory());
        
        return new FunctionDeclNode(
            "test",
            false,
            new ReadOnlyCollection<ParamDeclNode>(new List<ParamDeclNode>()),
            null,
            parser.Block()
        );
    }
    
    private static void VerifyConnection(BasicBlock from, BasicBlock to, bool isConditional = false, bool isTrue = true)
    {
        if (!isConditional)
        {
            Assert.Equal(to, from.UnconditionalSuccessor);
            Assert.Contains(from, to.Predecessors);
        }
        else if (isTrue)
        {
            Assert.Equal(to, from.TrueSuccessor);
            Assert.Contains(from, to.Predecessors);
        }
        else
        {
            Assert.Equal(to, from.FalseSuccessor);
            Assert.Contains(from, to.Predecessors);
        }
    }
    
    [Fact]
    public void EmptyFunction_HasEntryAndExitBlocks()
    {
        // Arrange
        var function = CreateTestFunction("""{}""");
        
        // Act
        var cfg = CFGBuilder.Build(function);
        
        // Assert
        Assert.NotNull(cfg);
        Assert.Equal(2, cfg.Blocks.Count); // Entry and Exit only
        Assert.True(cfg.Entry.IsEntry);
        Assert.True(cfg.Exit.IsExit);
        Assert.Equal(cfg.Exit, cfg.Entry.UnconditionalSuccessor);
    }
    
    [Fact]
    public void SimpleStatements_CreateSingleBlock()
    {
        // Arrange
        var function = CreateTestFunction("""
            {
                let x = 5;
                let y = 10;
                let z = x + y;
            }
            """);
        
        // Act
        var cfg = CFGBuilder.Build(function);
        
        // Assert
        Assert.Equal(2, cfg.Blocks.Count); // Entry block with statements and Exit
        Assert.Equal(3, cfg.Entry.Statements.Count); // All statements are in entry block
        
        // Verify connections
        VerifyConnection(cfg.Entry, cfg.Exit);
    }
    
    [Fact]
    public void ReturnStatement_TerminatesBlock()
    {
        // Arrange
        var function = CreateTestFunction("""
            {
                let x = 5;
                return x;
                let y = 10;
            }
            """);
        
        // Act
        var cfg = CFGBuilder.Build(function);
        
        // Assert
        Assert.Equal(2, cfg.Blocks.Count); // Entry and Exit
        Assert.Equal(2, cfg.Entry.Statements.Count); // The let x and return statements
        
        // Verify connections and that return connects directly to exit
        VerifyConnection(cfg.Entry, cfg.Exit);
        
        // Verify the unreachable statement is not in any block
        Assert.DoesNotContain(cfg.Blocks, b => b.Statements.Any(s => s is VarDeclNode v && v.Name == "y"));
    }
    
    [Fact]
    public void IfStatement_CreatesBranchingPath()
    {
        // Arrange
        var function = CreateTestFunction("""
            {
                let x = 5;
                if (x > 0) {
                    let y = 10;
                }
                let z = 15;
            }
            """);
        
        // Act
        var cfg = CFGBuilder.Build(function);
        
        // Assert
        // CFG includes: entry block (with x), condition block, true branch, and exit block
        Assert.True(cfg.Blocks.Count >= 3); // At minimum: Entry, condition+true branch, Exit
        
        // Find the condition block - it contains the BinaryOperationNode for x > 0
        var conditionBlock = cfg.Blocks.First(b => b.Statements.Any(s => s is BinaryOperationNode));
        
        // Verify the condition block has two successors
        Assert.NotNull(conditionBlock.TrueSuccessor);
        Assert.NotNull(conditionBlock.FalseSuccessor);
        
        // Find the true branch (contains "let y = 10")
        var trueBlock = conditionBlock.TrueSuccessor;
        Assert.NotNull(trueBlock);
        Assert.Contains(trueBlock.Statements, s => s is VarDeclNode v && v.Name == "y");
        
        // Find the join block (contains "let z = 15")
        var joinBlock = cfg.Blocks.First(b => b.Statements.Any(s => s is VarDeclNode v && v.Name == "z"));
        
        // The false branch can go directly to the join block (no empty join block needed)
        Assert.True(
            (trueBlock.UnconditionalSuccessor == joinBlock) || 
            (conditionBlock.FalseSuccessor == joinBlock)
        );
    }
    
    [Fact]
    public void IfElseStatement_CreatesTwoBranchingPaths()
    {
        // Arrange
        var function = CreateTestFunction("""
            {
                let x = 5;
                if (x > 0) {
                    let y = 10;
                } else {
                    let z = 15;
                }
                let w = 20;
            }
            """);
        
        // Act
        var cfg = CFGBuilder.Build(function);
        
        // Assert - Similar to IfStatement test but with both branches having code
        var conditionBlock = cfg.Blocks.First(b => b.Statements.Any(s => s is BinaryOperationNode));
        
        // Verify the condition block has two successors
        Assert.NotNull(conditionBlock.TrueSuccessor);
        Assert.NotNull(conditionBlock.FalseSuccessor);
        
        // Find the true and false branches
        var trueBlock = conditionBlock.TrueSuccessor;
        var falseBlock = conditionBlock.FalseSuccessor;
        
        Assert.Contains(trueBlock!.Statements, s => s is VarDeclNode v && v.Name == "y");
        Assert.Contains(falseBlock!.Statements, s => s is VarDeclNode v && v.Name == "z");
        
        // Find the join block (contains "let w = 20")
        var joinBlock = cfg.Blocks.First(b => b.Statements.Any(s => s is VarDeclNode v && v.Name == "w"));
        
        // Verify both branches lead to the join block
        // The branches connect directly to the join block
        Assert.True(
            (trueBlock.UnconditionalSuccessor == joinBlock) && 
            (falseBlock.UnconditionalSuccessor == joinBlock)
        );
    }
    
    [Fact]
    public void WhileLoop_CreatesProperLoopStructure()
    {
        // Arrange
        var function = CreateTestFunction("""
            {
                let i = 0;
                while (i < 10) {
                    let j = i * 2;
                    i = i + 1;
                }
                let z = 20;
            }
            """);
        
        // Act
        var cfg = CFGBuilder.Build(function);
        
        // Assert
        // Find the condition block - it contains the BinaryOperationNode for i < 10
        var conditionBlock = cfg.Blocks.First(b => b.Statements.Any(s => s is BinaryOperationNode));
        
        // Verify the condition block has two successors - loop body and exit loop
        Assert.NotNull(conditionBlock.TrueSuccessor);
        Assert.NotNull(conditionBlock.FalseSuccessor);
        
        // Find the loop body block (contains the loop contents)
        var bodyBlock = conditionBlock.TrueSuccessor;
        
        // Find the after-loop block (contains "let z = 20")
        var afterLoopBlock = cfg.Blocks.First(b => b.Statements.Any(s => s is VarDeclNode v && v.Name == "z"));
        
        // Verify loop exit is the false branch of the condition
        // The false branch connects directly to after-loop block (no empty join blocks)
        Assert.True(conditionBlock.FalseSuccessor == afterLoopBlock);
        
        // Verify the loop body connects back to the condition block
        var lastBodyBlock = bodyBlock;
        // Find the last block in the body (may be different if there are more statements)
        while (lastBodyBlock?.UnconditionalSuccessor != null && 
               lastBodyBlock.UnconditionalSuccessor != conditionBlock &&
               lastBodyBlock.UnconditionalSuccessor != afterLoopBlock)
        {
            lastBodyBlock = lastBodyBlock.UnconditionalSuccessor;
        }
        
        // Verify the last block in the loop body connects back to the condition
        Assert.Equal(conditionBlock, lastBodyBlock?.UnconditionalSuccessor);
    }
    
    [Fact]
    public void ComplexNestedStructures_CreateValidCFG()
    {
        // Arrange - Create a function with nested if-else and while loops
        var function = CreateTestFunction("""
            {
                let x = 5;
                if (x > 0) {
                    let i = 0;
                    while (i < 5) {
                        if (i == 2) {
                            return i;
                        }
                        i = i + 1;
                    }
                } else {
                    return 0;
                }
                return x;
            }
            """);
        
        // Act
        var cfg = CFGBuilder.Build(function);
        
        // Assert
        Assert.True(cfg.Blocks.Count > 5); // Complex structure creates many blocks
        
        // Count return statements in the graph
        int returnCount = 0;
        foreach (var block in cfg.Blocks)
        {
            returnCount += block.Statements.Count(s => s is ReturnNode);
        }
        
        // Verify all three return statements are captured in the CFG
        Assert.Equal(3, returnCount);
        
        // Verify all blocks with returns have the exit block as successor
        foreach (var block in cfg.Blocks)
        {
            if (block.Statements.Any(s => s is ReturnNode))
            {
                Assert.Equal(cfg.Exit, block.UnconditionalSuccessor);
            }
        }
    }
    
    [Fact]
    public void BlockIdentifiers_AreUnique()
    {
        // This test ensures that each block has a unique identifier
        
        // Arrange
        var function1 = CreateTestFunction("""
            {
                let x = 5;
                if (x > 0) { let y = 10; }
            }
            """);
            
        var function2 = CreateTestFunction("""
            {
                let a = 15;
                while (a > 0) { a = a - 1; }
            }
            """);
        
        // Act
        var cfg1 = CFGBuilder.Build(function1);
        var cfg2 = CFGBuilder.Build(function2);
        
        // Assert
        // Check for uniqueness within each CFG
        var ids1 = cfg1.Blocks.Select(b => b.Id).ToList();
        var ids2 = cfg2.Blocks.Select(b => b.Id).ToList();
        
        Assert.Equal(ids1.Count, ids1.Distinct().Count());
        Assert.Equal(ids2.Count, ids2.Distinct().Count());
        
        // Note: Due to the static counter in BasicBlock, IDs will be sequential across instances
        // This is a design choice, but means blocks in different CFGs won't have the same IDs
    }
    
    [Fact]
    public void UnreachableBlocks_ArePruned()
    {
        // Arrange
        var function = CreateTestFunction("""
            {
                let x = 5;
                return x;
                let y = 10; // Unreachable
                while (y > 0) { // Unreachable
                    y = y - 1;
                }
            }
            """);
        
        // Act
        var cfg = CFGBuilder.Build(function);
        
        // Assert
        // Check that unreachable blocks are not in the final CFG
        Assert.DoesNotContain(cfg.Blocks, b => b.Statements.Any(s => s is VarDeclNode v && v.Name == "y"));
        
        // Verify the only blocks are entry (with x and return) and exit
        Assert.Equal(2, cfg.Blocks.Count);
    }
}