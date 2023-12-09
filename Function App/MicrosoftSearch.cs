using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Azure.Core;
using Azure.Identity;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.AI.OpenAI;

namespace graph_search_functions
{
    public class MicrosoftSearch
    {
        private readonly ILogger _logger;

        public MicrosoftSearch(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<MicrosoftSearch>();
        }

        [Function("MicrosoftSearch")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            // _logger.LogInformation("C# HTTP trigger function processed a request.");
            new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            string requestBody = await req.ReadAsStringAsync();
            Console.WriteLine(requestBody);
            var input = JsonSerializer.Deserialize<SearchInput>(requestBody);

            Console.WriteLine(input);
            string searchResponse = await ExecuteSearchAsync(input);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            response.WriteString(searchResponse);

            return response;
        }

        public class SearchInput
        {
            public List<RequestObject> Requests { get; set; }

            public class RequestObject
            {
                public List<string> EntityTypes { get; set; }
                public QueryObject Query { get; set; }

                public class QueryObject
                {
                    public string QueryString { get; set; }
                }
            }
        }

        private async Task<string> ExecuteSearchAsync(SearchInput input)
        {
            // ここでの認証情報の定義は、セキュリティの観点から推奨されません。
            // 代わりに、Azure Key Vault や他の安全な方法を使用して認証情報を管理してください。
            var ClientId = System.Environment.GetEnvironmentVariable("ClientId");
            var TenantId = System.Environment.GetEnvironmentVariable("TenantId");
            var ClientSecret = System.Environment.GetEnvironmentVariable("ClientSecret");


            var clientSecretCredential = new ClientSecretCredential(
                TenantId, ClientId, ClientSecret);

            var tokenRequestContext = new TokenRequestContext(new[] { "https://graph.microsoft.com/.default" });
            var accessToken = await clientSecretCredential.GetTokenAsync(tokenRequestContext);

            var searchRequest = new
            {
                requests = new[]
                {
                    new
                    {
                        entityTypes = input.Requests[0].EntityTypes.ToArray(),
                        query = new { queryString = input.Requests[0].Query.QueryString },
                        region = "NAM"
                    }
                }
            };

            var httpMessage = new HttpRequestMessage(HttpMethod.Post, "https://graph.microsoft.com/v1.0/search/query")
            {
                Content = new StringContent(JsonSerializer.Serialize(searchRequest), System.Text.Encoding.UTF8, "application/json")
            };

            httpMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);
            httpMessage.Headers.Add("Prefer", "search.appsearch.microsoft.com;region=US");

            var httpClient = new HttpClient();
            var httpResponseMessage = await httpClient.SendAsync(httpMessage);

            return await httpResponseMessage.Content.ReadAsStringAsync();
        }
    }
}
