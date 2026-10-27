using System;
using System.Collections.Generic;
using QuestLock.Core.Math;
using QuestLock.Core.Models;
using Xunit;

namespace QuestLock.Core.Tests;

public class MathGeneratorTests
{
    [Fact]
    public void Addition_ProducesSumUnderOrEqualToMax_AndCorrectExpectedResult()
    {
        var generator = new MathGenerator(12345);
        var config = new MathConfig
        {
            AllowedOperations = new List<string> { "add" },
            MaxNumber = 100
        };

        for (int i = 0; i < 100; i++)
        {
            var problem = generator.GenerateProblem(config);
            Assert.Equal("add", problem.Operation);
            Assert.Equal("+", problem.OperatorSymbol);
            Assert.True(problem.OperandA >= 1);
            Assert.True(problem.OperandB >= 1);
            Assert.True(problem.ExpectedResult <= 100);
            Assert.Equal(problem.OperandA + problem.OperandB, problem.ExpectedResult);
            Assert.True(problem.Verify(problem.ExpectedResult));
            Assert.False(problem.Verify(problem.ExpectedResult + 1));
        }
    }

    [Fact]
    public void Subtraction_ProducesNonNegativeResult_AndMinuendUnderMax()
    {
        var generator = new MathGenerator(12345);
        var config = new MathConfig
        {
            AllowedOperations = new List<string> { "subtract" },
            MaxNumber = 100
        };

        for (int i = 0; i < 100; i++)
        {
            var problem = generator.GenerateProblem(config);
            Assert.Equal("subtract", problem.Operation);
            Assert.Equal("-", problem.OperatorSymbol);
            Assert.True(problem.OperandA >= 2 && problem.OperandA <= 100);
            Assert.True(problem.OperandB >= 1 && problem.OperandB <= problem.OperandA);
            Assert.True(problem.ExpectedResult >= 0);
            Assert.Equal(problem.OperandA - problem.OperandB, problem.ExpectedResult);
            Assert.True(problem.Verify(problem.ExpectedResult));
        }
    }

    [Fact]
    public void Multiplication_ProducesOperandsUpTo10_AndResultUpTo100()
    {
        var generator = new MathGenerator(12345);
        var config = new MathConfig
        {
            AllowedOperations = new List<string> { "multiply" },
            MaxNumber = 100
        };

        for (int i = 0; i < 100; i++)
        {
            var problem = generator.GenerateProblem(config);
            Assert.Equal("multiply", problem.Operation);
            Assert.Equal("×", problem.OperatorSymbol);
            Assert.InRange(problem.OperandA, 1, 10);
            Assert.InRange(problem.OperandB, 1, 10);
            Assert.True(problem.ExpectedResult <= 100);
            Assert.Equal(problem.OperandA * problem.OperandB, problem.ExpectedResult);
            Assert.True(problem.Verify(problem.ExpectedResult));
        }
    }

    [Fact]
    public void Division_ProducesValidIntegerDivision_WithDividendUpTo100()
    {
        var generator = new MathGenerator(12345);
        var config = new MathConfig
        {
            AllowedOperations = new List<string> { "divide" },
            MaxNumber = 100
        };

        for (int i = 0; i < 100; i++)
        {
            var problem = generator.GenerateProblem(config);
            Assert.Equal("divide", problem.Operation);
            Assert.Equal(":", problem.OperatorSymbol);
            Assert.InRange(problem.OperandB, 2, 10); // dělitel 2 až 10 (nikdy nula)
            Assert.InRange(problem.ExpectedResult, 1, 10); // podíl 1 až 10
            Assert.True(problem.OperandA <= 100); // dělenec do 100
            Assert.Equal(0, problem.OperandA % problem.OperandB); // beze zbytku
            Assert.Equal(problem.ExpectedResult, problem.OperandA / problem.OperandB);
            Assert.True(problem.Verify(problem.ExpectedResult));
        }
    }

    [Fact]
    public void GenerateSet_ReturnsExactRequestedCount()
    {
        var generator = new MathGenerator();
        var config = new MathConfig { TotalQuestions = 5 };
        var set = generator.GenerateSet(config, 5);

        Assert.Equal(5, set.Count);
    }
}
