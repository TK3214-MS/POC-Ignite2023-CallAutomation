using Azure.Core;
// using Azure.Identity;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.AI.OpenAI;

public class MicrosoftSearchApiFunction
{
    static public string Name = "microsoft_search_api";

    // Return the function metadata
    static public FunctionDefinition GetFunctionDefinition()
    {
        return new FunctionDefinition()
        {
            Name = Name,
            Description = "手順やルール、ドキュメント内容に関する質問をされるとMicrosoft Graph Search APIを用いて検索を行い、ドキュメントの内容から回答を日本語で応答します。また、応答内容にはContent、Score、Intent、Categoryに関する情報は含めないで下さい。",
            Parameters = BinaryData.FromObjectAsJson(
            new
            {
                Type = "object",
                Properties = new
                {
                    Requests = new
                    {
                        Type = "array",
                        Items = new
                        {
                            Type = "object",
                            Properties = new
                            {
                                EntityTypes = new
                                {
                                    Type = "array", // entityTypesを配列として定義
                                    Items = new
                                    {
                                        Type = "string", // 配列の各要素は文字列型
                                        // Enum = new[] { "site", "list", "listItem", "drive", "driveItem", "message", "event" }
                                        Description = "{site, list, listItem, drive, driveItem, message, event} のいずれかを必ず入力として。driveItemを勝手にDriveItemと大文字小文字を変えたりしないで。"
                                    },
                                    Description = "検索の対象にするリソースの種類。{site, list, listItem, drive, driveItem, message, event} のいずれかを必ず入力として。driveItemを勝手にDriveItemと大文字小文字を変えたりしないで。"
                                },
                                Query = new
                                {
                                    Type = "object",
                                    Properties = new
                                    {
                                        QueryString = new
                                        {
                                            Type = "string",
                                            Description = "検索キーワード。半角スペース区切り。"
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                Required = new[] { "requests" },
            },
            new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
        };
    }
}

// Arguments for the function
public class SearchInput
{
    public List<RequestObject> Requests { get; set; }  // Change this line to match the JSON structure

    public class RequestObject
    {
        // public List<string> EntityTypes { get; set; }
        public List<string> EntityTypes { get; set; }
        public QueryObject Query { get; set; }

        public class QueryObject
        {
            public string QueryString { get; set; }
        }
    }
}