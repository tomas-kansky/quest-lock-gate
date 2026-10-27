using QuestLock.Core.Models;

namespace QuestLock.Core.Math;

public class MathGenerator
{
    private readonly Random _random;

    public MathGenerator(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public MathProblem GenerateProblem(MathConfig config)
    {
        var allowed = config.AllowedOperations != null && config.AllowedOperations.Count > 0
            ? config.AllowedOperations
            : new List<string> { "add", "subtract", "multiply", "divide" };

        var operation = allowed[_random.Next(allowed.Count)].ToLowerInvariant();
        var max = System.Math.Max(10, config.MaxNumber);

        return operation switch
        {
            "add" => GenerateAddition(max),
            "subtract" => GenerateSubtraction(max),
            "multiply" => GenerateMultiplication(),
            "divide" => GenerateDivision(),
            _ => GenerateAddition(max)
        };
    }

    public List<MathProblem> GenerateSet(MathConfig config, int count)
    {
        var problems = new List<MathProblem>();
        for (int i = 0; i < count; i++)
        {
            problems.Add(GenerateProblem(config));
        }
        return problems;
    }

    private MathProblem GenerateAddition(int max)
    {
        // Výsledek a + b je v rozmezí 2 až max (výchozí max = 100)
        var sum = _random.Next(2, max + 1);
        var a = _random.Next(1, sum);
        var b = sum - a;

        return new MathProblem
        {
            Operation = "add",
            OperandA = a,
            OperandB = b,
            OperatorSymbol = "+",
            ExpectedResult = sum,
            DisplayExpression = $"{a} + {b} ="
        };
    }

    private MathProblem GenerateSubtraction(int max)
    {
        // Menšenec a je v rozmezí 2 až max (až 100)
        var a = _random.Next(2, max + 1);
        // Menšitel b je v rozmezí 1 až a (výsledek nezáporný >= 0)
        var b = _random.Next(1, a + 1);

        return new MathProblem
        {
            Operation = "subtract",
            OperandA = a,
            OperandB = b,
            OperatorSymbol = "-",
            ExpectedResult = a - b,
            DisplayExpression = $"{a} - {b} ="
        };
    }

    private MathProblem GenerateMultiplication()
    {
        // Malá násobilka pro 10letého žáka: a, b v rozmezí 1 až 10 (výsledek 1 až 100)
        var a = _random.Next(1, 11);
        var b = _random.Next(1, 11);

        return new MathProblem
        {
            Operation = "multiply",
            OperandA = a,
            OperandB = b,
            OperatorSymbol = "×",
            ExpectedResult = a * b,
            DisplayExpression = $"{a} × {b} ="
        };
    }

    private MathProblem GenerateDivision()
    {
        // Dělení z malé násobilky (výsledek celé číslo, dělitel 2 až 10, dělenec do 100)
        var b = _random.Next(2, 11); // dělitel
        var q = _random.Next(1, 11); // očekávaný podíl
        var a = b * q;              // dělenec (např. 81 při b=9, q=9)

        return new MathProblem
        {
            Operation = "divide",
            OperandA = a,
            OperandB = b,
            OperatorSymbol = ":",
            ExpectedResult = q,
            DisplayExpression = $"{a} : {b} ="
        };
    }
}
