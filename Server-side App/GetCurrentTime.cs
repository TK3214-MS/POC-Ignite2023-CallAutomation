using System;
using System.Text.Json;
using Azure.AI.OpenAI;

public class GetCurrentTimeFunction
{
    static public string Name = "get_current_time";

    // Return the function metadata
    static public FunctionDefinition GetFunctionDefinition()
    {
        return new FunctionDefinition()
        {
            Name = Name,
            Description = "Get the current time in a given location's timezone",
            Parameters = BinaryData.FromObjectAsJson(
            new
            {
                Type = "object",
                Properties = new
                {
                    Location = new
                    {
                        Type = "string",
                        Description = "The Windows timezone ID to get the current time for, e.g., 'Tokyo Standard Time'",
                    },
                },
                Required = new[] { "location" },
            },
            new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
        };
    }

    // The function implementation
    static public CurrentTime GetTime(string location)
    {
        try
        {
            // Parse the location to a TimeZoneInfo object
            TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(location);

            // Get the current UTC time and convert it to the target timezone
            DateTime currentTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);

            // Format the time in 12-hour format with AM/PM
            string timeString = currentTime.ToString("hh:mm:ss tt");

            return new CurrentTime() { Time = timeString, Location = location };
        }
        catch (TimeZoneNotFoundException)
        {
            return new CurrentTime() { Time = "The timezone was not found.", Location = location };
        }
        catch (InvalidTimeZoneException)
        {
            return new CurrentTime() { Time = "The timezone data is invalid.", Location = location };
        }
        catch (Exception)
        {
            return new CurrentTime() { Time = "An unknown error occurred.", Location = location };
        }
    }
}

// Argument for the function
public class TimeInput
{
    public string Location { get; set; } = string.Empty;
}

// Return type
public class CurrentTime
{
    public string Time { get; set; }
    public string Location { get; set; } = string.Empty;
}