using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Azure.AI.OpenAI;
public class GetCapitalFunction
{
    static public string Name = "get_capital";

    // Return the function metadata
    static public FunctionDefinition GetFunctionDefinition()
    {
        return new FunctionDefinition()
        {
            Name = Name,
            Description = "Get the capital of the location",
            Parameters = BinaryData.FromObjectAsJson(
            new
            {
                Type = "object",
                Properties = new
                {
                    Location = new
                    {
                        Type = "string",
                        Description = "The city, state or country, e.g. San Francisco, CA",
                    }
                },
                Required = new[] { "location" },
            },
            new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
        };
    }

    // The function implementation. It always return Tokyo for now.
    static public string GetCapital(string location)
    {
        return "Tokyo";
    }
}

// Argument for the function
public class CapitalInput
{
    public string Location { get; set; } = string.Empty;
}