using System.Text.Json;
using BdslValidator;

try
{
    var input = await Console.In.ReadToEndAsync();
    var analyzer = new GrammarAnalyzer(input);
    var error = analyzer.Analyze();

    object result = error is null
        ? new { hasError = false }
        : (object)new { hasError = true, error.Value.Message, error.Value.ErrorSpans };

    Console.WriteLine(JsonSerializer.Serialize(result,
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
}
catch (Exception ex)
{
    var result = new
    {
        hasError = true,
        message = $"Internal error: {ex.Message}",
        spans = new[] { new { line = 0, startColumn = 0, endColumn = 0 } }
    };
    Console.WriteLine(JsonSerializer.Serialize(result,
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    Environment.Exit(1);
}

