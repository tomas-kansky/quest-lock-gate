using System.Text.Json.Serialization;

namespace QuestLock.Core.Models;

public class MathProblem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("operation")]
    public string Operation { get; set; } = "add";

    [JsonPropertyName("operandA")]
    public int OperandA { get; set; }

    [JsonPropertyName("operandB")]
    public int OperandB { get; set; }

    [JsonPropertyName("operatorSymbol")]
    public string OperatorSymbol { get; set; } = "+";

    [JsonPropertyName("expectedResult")]
    public int ExpectedResult { get; set; }

    [JsonPropertyName("displayExpression")]
    public string DisplayExpression { get; set; } = string.Empty;

    public bool Verify(int answer)
    {
        return answer == ExpectedResult;
    }
}
