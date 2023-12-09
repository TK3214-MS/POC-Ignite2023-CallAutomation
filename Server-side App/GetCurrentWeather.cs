using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.AI.OpenAI;

public class GetWeatherFunction
{
    static public string Name = "get_current_weather";

    // Return the function metadata
    static public FunctionDefinition GetFunctionDefinition()
    {
        return new FunctionDefinition()
        {
            Name = Name,
            Description = "与えられた場所の天気予報と現在の気温を取得します。",
            Parameters = BinaryData.FromObjectAsJson(
            new
            {
                Type = "object",
                Properties = new
                {
                    Location = new
                    {
                        Type = "string",
                        Description = "天気予報と気温を取得したい場所です. 場所の引数の形式は、東京都や京都府、新宿区のように正式名所である必要があります。東京や京都のように省略しないでください。",
                    }
                },
                Required = new[] { "location" },
            },
            new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
        };
    }

    // The function implementation
    static public async Task<WeatherInfo> GetWeather(string location, string apiKey)
    {
        var httpClient = new HttpClient();
        var baseUrl = $"http://api.openweathermap.org/data/2.5/weather?q={location}&appid={apiKey}&lang=ja&units=metric";

        Console.WriteLine($"Calling weather API with URL: {baseUrl}");

        try
        {
            var response = await httpClient.GetAsync(baseUrl);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Response content: {content}");
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var weatherData = JsonSerializer.Deserialize<WeatherResponse>(content, options);
                Console.WriteLine($"Response data: {weatherData}");

                var city = weatherData.Name;
                var temperature = weatherData.Main.Temp;
                var weatherDescription = weatherData.Weather[0].Description;

                return new WeatherInfo { Description = $"{city}の天気は{weatherDescription}です。気温は{temperature}度です。", Location = location };
            }
            else
            {
                Console.WriteLine($"Failed to retrieve weather data: {response.StatusCode}");
                return new WeatherInfo { Description = "天気の取得に失敗しました", Location = location };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception occurred: {ex.Message}");
            return new WeatherInfo { Description = "天気の取得に失敗しました", Location = location };
        }
    }
}

// Argument for the function
public class WeatherInput
{
    public string Location { get; set; } = string.Empty;
}

// Return type
public class WeatherInfo
{
    public string Description { get; set; }
    public string Location { get; set; } = string.Empty;
}

// The data model that represents the response from the weather API
public class WeatherResponse
{
    public string Name { get; set; }
    public Main Main { get; set; }
    public Weather[] Weather { get; set; }
}

public class Main
{
    public double Temp { get; set; }
}

public class Weather
{
    public string Description { get; set; }
}