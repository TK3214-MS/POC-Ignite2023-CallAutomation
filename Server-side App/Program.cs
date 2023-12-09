using Azure;
using Azure.AI.OpenAI;
using Azure.Communication;
using Azure.Communication.CallAutomation;
using Azure.Messaging;
using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.SystemEvents;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

var builder = WebApplication.CreateBuilder(args);

//Get ACS Connection String from appsettings.json
var acsConnectionString = builder.Configuration.GetValue<string>("AcsConnectionString");
var weatherApiKey = builder.Configuration.GetValue<string>("WeatherApiKey");
ArgumentNullException.ThrowIfNullOrEmpty(acsConnectionString);

//Call Automation Client
var client = new CallAutomationClient(connectionString: acsConnectionString);

//Grab the Cognitive Services endpoint from appsettings.json
var cognitiveServicesEndpoint = builder.Configuration.GetValue<string>("CognitiveServiceEndpoint");
ArgumentNullException.ThrowIfNullOrEmpty(cognitiveServicesEndpoint);

string answerPromptSystemTemplate = """ 
    You are an assisant designed to answer the customer query and analyze the sentiment score from the customer tone. 
    You also need to determine the intent of the customer query and classify it into categories such as sales, marketing, shopping, etc.
    You also need to determine if the customer wants the real-time information or not. If so, use proper function calling to get that data and respond to the customer.
    You also need to determine if the customer wants the information of specific product or not. If so, use proper function calling and summarize the answer content to respond to the customer.
    Use a scale of 1-10 (10 being highest) to rate the sentiment score. 
    Use the below format, replacing the text in brackets with the result. Do not include the brackets in the output: 
    Content:[Answer the customer query briefly and clearly in two lines and ask if there is anything else you can help with] 
    Score:[Sentiment score of the customer tone] 
    Intent:[Determine the intent of the customer query] 
    Category:[Classify the intent into one of the categories]
    """;

string helloPrompt = "お電話ありがとうございます。こちらはお客様相談センターです。何かお手伝い出来る事はありますか？";
string timeoutSilencePrompt = "申し訳ございません。音声が聞こえないようです。お手伝いをご希望されている内容をおっしゃって下さい。";
string goodbyePrompt = "お電話ありがとうございました。お役に立てましたら幸いです。失礼致します。";
string connectAgentPrompt = "申し訳ございませんが、ご要望にお答えする事が出来ません。サポート担当にお繋ぎしますので少々お待ち下さい。";
string callTransferFailurePrompt = "申し訳ございませんが、現在対応可能なサポート担当がいないようです。空きましたら早急に折り返しさせて頂きますので電話を切って少々お待ち下さい。";
string agentPhoneNumberEmptyPrompt = "申し訳ございませんが、現在対応可能なサポート担当がいないようです。空きましたら早急に折り返しさせて頂きますので電話を切って少々お待ち下さい。";
string EndCallPhraseToConnectAgent = "サポート担当にお繋ぎしますので、お電話を切らずそのままでお待ち下さい。";

string transferFailedContext = "TransferFailed";
string connectAgentContext = "ConnectAgent";
string goodbyeContext = "Goodbye";

string agentPhonenumber = builder.Configuration.GetValue<string>("AgentPhoneNumber");
string chatResponseExtractPattern = @"\s*Content:(.*)\s*Score:(.*\d+)\s*Intent:(.*)\s*Category:(.*)";

var key = builder.Configuration.GetValue<string>("AzureOpenAIServiceKey");
ArgumentNullException.ThrowIfNullOrEmpty(key);

var endpoint = builder.Configuration.GetValue<string>("AzureOpenAIServiceEndpoint");
ArgumentNullException.ThrowIfNullOrEmpty(endpoint);

var ai_client = new OpenAIClient(new Uri(endpoint), new AzureKeyCredential(key));

//Register and make CallAutomationClient accessible via dependency injection
builder.Services.AddSingleton(client);
builder.Services.AddSingleton(ai_client);
var app = builder.Build();

var devTunnelUri = builder.Configuration.GetValue<string>("DevTunnelUri");
ArgumentNullException.ThrowIfNullOrEmpty(devTunnelUri);
var maxTimeout = 2;

app.MapGet("/", () => "Hello ACS CallAutomation!");

app.MapPost("/api/incomingCall", async (
    [FromBody] EventGridEvent[] eventGridEvents,
    ILogger<Program> logger) =>
{
    foreach (var eventGridEvent in eventGridEvents)
    {
        logger.LogInformation($"Incoming Call event received.");

        // Handle system events
        if (eventGridEvent.TryGetSystemEventData(out object eventData))
        {
            // Handle the subscription validation event.
            if (eventData is SubscriptionValidationEventData subscriptionValidationEventData)
            {
                var responseData = new SubscriptionValidationResponse
                {
                    ValidationResponse = subscriptionValidationEventData.ValidationCode
                };
                return Results.Ok(responseData);
            }
        }

        var jsonObject = Helper.GetJsonObject(eventGridEvent.Data);
        var callerId = Helper.GetCallerId(jsonObject);
        var incomingCallContext = Helper.GetIncomingCallContext(jsonObject);
        var callbackUri = new Uri(new Uri(devTunnelUri), $"/api/callbacks/{Guid.NewGuid()}?callerId={callerId}");
        Console.WriteLine($"Callback Url: {callbackUri}");
        var options = new AnswerCallOptions(incomingCallContext, callbackUri)
        {
            CallIntelligenceOptions = new CallIntelligenceOptions() { CognitiveServicesEndpoint = new Uri(cognitiveServicesEndpoint) }
        };

        AnswerCallResult answerCallResult = await client.AnswerCallAsync(options);
        Console.WriteLine($"Answered call for connection id: {answerCallResult.CallConnection.CallConnectionId}");

        //Use EventProcessor to process CallConnected event
        var answer_result = await answerCallResult.WaitForEventProcessorAsync();
        if (answer_result.IsSuccess)
        {
            Console.WriteLine($"Call connected event received for connection id: {answer_result.SuccessResult.CallConnectionId}");
            var callConnectionMedia = answerCallResult.CallConnection.GetCallMedia();
            await HandleRecognizeAsync(callConnectionMedia, callerId, helloPrompt);
        }

        client.GetEventProcessor().AttachOngoingEventProcessor<PlayCompleted>(answerCallResult.CallConnection.CallConnectionId, async (playCompletedEvent) =>
        {
            logger.LogInformation($"Play completed event received for connection id: {playCompletedEvent.CallConnectionId}.");
            if (!string.IsNullOrWhiteSpace(playCompletedEvent.OperationContext) && (playCompletedEvent.OperationContext.Equals(transferFailedContext, StringComparison.OrdinalIgnoreCase) 
            || playCompletedEvent.OperationContext.Equals(goodbyeContext, StringComparison.OrdinalIgnoreCase)))
            {
                logger.LogInformation($"Disconnecting the call...");
                await answerCallResult.CallConnection.HangUpAsync(true);
            }
            else if (!string.IsNullOrWhiteSpace(playCompletedEvent.OperationContext) && playCompletedEvent.OperationContext.Equals(connectAgentContext, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(agentPhonenumber))
                {
                    logger.LogInformation($"Agent phone number is empty");
                    await HandlePlayAsync(agentPhoneNumberEmptyPrompt,
                      transferFailedContext, answerCallResult.CallConnection.GetCallMedia());
                }
                else
                {
                    logger.LogInformation($"Initializing the Call transfer...");
                    CommunicationIdentifier transferDestination = new PhoneNumberIdentifier(agentPhonenumber);
                    TransferCallToParticipantResult result = await answerCallResult.CallConnection.TransferCallToParticipantAsync(transferDestination);
                    logger.LogInformation($"Transfer call initiated: {result.OperationContext}");
                }
            }
        });

        client.GetEventProcessor().AttachOngoingEventProcessor<PlayFailed>(answerCallResult.CallConnection.CallConnectionId, async (playFailedEvent) =>
        {
            logger.LogInformation($"Play failed event received for connection id: {playFailedEvent.CallConnectionId}. Hanging up call...");
            await answerCallResult.CallConnection.HangUpAsync(true);
        });
        client.GetEventProcessor().AttachOngoingEventProcessor<CallTransferAccepted>(answerCallResult.CallConnection.CallConnectionId, async (callTransferAcceptedEvent) =>
        {
            logger.LogInformation($"Call transfer accepted event received for connection id: {callTransferAcceptedEvent.CallConnectionId}.");
        });
        client.GetEventProcessor().AttachOngoingEventProcessor<CallTransferFailed>(answerCallResult.CallConnection.CallConnectionId, async (callTransferFailedEvent) =>
        {
            logger.LogInformation($"Call transfer failed event received for connection id: {callTransferFailedEvent.CallConnectionId}.");
            var resultInformation = callTransferFailedEvent.ResultInformation;
            logger.LogError("Encountered error during call transfer, message={msg}, code={code}, subCode={subCode}", resultInformation?.Message, resultInformation?.Code, resultInformation?.SubCode);

            await HandlePlayAsync(callTransferFailurePrompt,
            transferFailedContext, answerCallResult.CallConnection.GetCallMedia());

        });
        client.GetEventProcessor().AttachOngoingEventProcessor<RecognizeCompleted>(answerCallResult.CallConnection.CallConnectionId, async (recognizeCompletedEvent) =>
        {
            Console.WriteLine($"Recognize completed event received for connection id: {recognizeCompletedEvent.CallConnectionId}");
            var speech_result = recognizeCompletedEvent.RecognizeResult as SpeechResult;
            if (!string.IsNullOrWhiteSpace(speech_result?.Speech))
            {
                Console.WriteLine($"Recognized speech: {speech_result.Speech}");

                if (await DetectEscalateToAgentIntent(speech_result.Speech, logger))
                {
                    await HandlePlayAsync(EndCallPhraseToConnectAgent,
                               connectAgentContext, answerCallResult.CallConnection.GetCallMedia());
                }
                else
                {
                    var chatGPTResponse = await GetChatGPTResponse(speech_result.Speech);
                    logger.LogInformation($"Chat GPT response: {chatGPTResponse}");
                    Regex regex = new Regex(chatResponseExtractPattern);
                    Match match = regex.Match(chatGPTResponse);
                    if (match.Success)
                    {
                        string answer = match.Groups[1].Value;
                        string sentimentScore = match.Groups[2].Value.Trim();
                        string intent = match.Groups[3].Value;
                        string category = match.Groups[4].Value;

                        logger.LogInformation("Chat GPT Answer={ans}, Sentiment Rating={rating}, Intent={Int}, Category={cat}",
                            answer, sentimentScore, intent, category);
                        var score = getSentimentScore(sentimentScore);
                        if (score > -1 && score < 5)
                        {
                            await HandlePlayAsync(connectAgentPrompt,
                                connectAgentContext, answerCallResult.CallConnection.GetCallMedia());
                        }
                        else
                        {
                            await HandleChatResponse(answer, answerCallResult.CallConnection.GetCallMedia(), callerId, logger);
                        }
                    }
                    else
                    {
                        logger.LogInformation("No match found");
                        await HandleChatResponse(chatGPTResponse, answerCallResult.CallConnection.GetCallMedia(), callerId, logger);
                    }
                }
            }
        });

        client.GetEventProcessor().AttachOngoingEventProcessor<RecognizeFailed>(answerCallResult.CallConnection.CallConnectionId, async (recognizeFailedEvent) =>
        {
            var callConnectionMedia = answerCallResult.CallConnection.GetCallMedia();

            if (MediaEventReasonCode.RecognizeInitialSilenceTimedOut.Equals(recognizeFailedEvent.ResultInformation.SubCode.Value.ToString()) && maxTimeout > 0)
            {
                Console.WriteLine($"Recognize failed event received for connection id: {recognizeFailedEvent.CallConnectionId}. Retrying recognize...");
                maxTimeout--;
                await HandleRecognizeAsync(callConnectionMedia, callerId, timeoutSilencePrompt);
            }
            else
            {
                Console.WriteLine($"Recognize failed event received for connection id: {recognizeFailedEvent.CallConnectionId}. Playing goodbye message...");
                await HandlePlayAsync(goodbyePrompt, goodbyeContext, callConnectionMedia);
            }
        });
    }
    return Results.Ok();
});

// api to handle call back events
app.MapPost("/api/callbacks/{contextId}", async (
    [FromBody] CloudEvent[] cloudEvents,
    [FromRoute] string contextId,
    [Required] string callerId,
    CallAutomationClient callAutomationClient,
    ILogger<Program> logger) =>
{
    var eventProcessor = client.GetEventProcessor();
    eventProcessor.ProcessEvents(cloudEvents);
    return Results.Ok();
});

async Task HandleChatResponse(string chatResponse, CallMedia callConnectionMedia, string callerId, ILogger logger, string context = "OpenAISample")
{
    var chatGPTResponseSource = new TextSource(chatResponse)
    {
        VoiceName = "ja-JP-NanamiNeural"
    };

    var recognizeOptions =
        new CallMediaRecognizeSpeechOptions(
            targetParticipant: CommunicationIdentifier.FromRawId(callerId))
        {
            InterruptPrompt = false,
            InitialSilenceTimeout = TimeSpan.FromSeconds(5),
            Prompt = chatGPTResponseSource,
            OperationContext = context,
            EndSilenceTimeout = TimeSpan.FromMilliseconds(500),
            SpeechLanguage = "ja-JP"
        };

    var recognize_result = await callConnectionMedia.StartRecognizingAsync(recognizeOptions);
}

int getSentimentScore(string sentimentScore)
{
    string pattern = @"(\d)+";
    Regex regex = new Regex(pattern);
    Match match = regex.Match(sentimentScore);
    return match.Success ? int.Parse(match.Value) : -1;
}

async Task<bool> DetectEscalateToAgentIntent(string speechText, ILogger logger) =>
           await HasIntentAsync(userQuery: speechText, intentDescription: "talk to agent", logger);

async Task<bool> HasIntentAsync(string userQuery, string intentDescription, ILogger logger)
{
    var systemPrompt = "You are a helpful assistant";
    var baseUserPrompt = "In 1 word: does {0} have similar meaning as {1}?";
    var combinedPrompt = string.Format(baseUserPrompt, userQuery, intentDescription);

    var response = await GetChatCompletionsAsync(systemPrompt, combinedPrompt);

    var isMatch = response.ToLowerInvariant().Contains("yes");
    logger.LogInformation($"OpenAI results: isMatch={isMatch}, customerQuery='{userQuery}', intentDescription='{intentDescription}'");
    return isMatch;
}

async Task<string> GetChatGPTResponse(string speech_input)
{
    return await GetChatCompletionsAsync(answerPromptSystemTemplate, speech_input);
}

async Task<string> GetChatCompletionsAsync(string systemPrompt, string userPrompt)
{
    var chatCompletionsOptions = new ChatCompletionsOptions()
    {
        Messages = {
                    new ChatMessage(ChatRole.System, systemPrompt),
                    new ChatMessage(ChatRole.User, userPrompt),
                    },
        MaxTokens = 1000
    };

    // var response = await ai_client.GetChatCompletionsAsync(
    //     deploymentOrModelName: builder.Configuration.GetValue<string>("AzureOpenAIDeploymentModelName"),
    //     chatCompletionsOptions);

    ChatCompletions response;
    ChatChoice responseChoice;

    // var response_content = response.Value.Choices[0].Message.Content;

    // Definition of Function Calling
    // FunctionDefinition getWeatherFuntionDefinition = GetWeatherFunction.GetFunctionDefinition();
    // FunctionDefinition getCapitalFuntionDefinition = GetCapitalFunction.GetFunctionDefinition();
    // chatCompletionsOptions.Functions.Add(getWeatherFuntionDefinition);
    // chatCompletionsOptions.Functions.Add(getCapitalFuntionDefinition);

    FunctionDefinition getMicrosoftSearchApi = MicrosoftSearchApiFunction.GetFunctionDefinition();
    FunctionDefinition getCurrentTimeFunctionDefinition = GetCurrentTimeFunction.GetFunctionDefinition();
    FunctionDefinition getWeatherFunctionDefinition = GetWeatherFunction.GetFunctionDefinition();
    chatCompletionsOptions.Functions.Add(getMicrosoftSearchApi);
    chatCompletionsOptions.Functions.Add(getCurrentTimeFunctionDefinition);
    chatCompletionsOptions.Functions.Add(getWeatherFunctionDefinition);

    response = await ai_client.GetChatCompletionsAsync(builder.Configuration.GetValue<string>("AzureOpenAIDeploymentModelName"), chatCompletionsOptions);

    responseChoice = response.Choices[0];

    // function_call のうちはループを回す
    while (responseChoice.FinishReason == CompletionsFinishReason.FunctionCall)
    {
        // Add message as a history.
        chatCompletionsOptions.Messages.Add(responseChoice.Message);

        //if (responseChoice.Message.FunctionCall.Name == GetWeatherFunction.Name)
        if (responseChoice.Message.FunctionCall.Name == MicrosoftSearchApiFunction.Name)
        {
            Console.WriteLine($"呼び出す関数: {MicrosoftSearchApiFunction.Name}");
            string unvalidatedArguments = responseChoice.Message.FunctionCall.Arguments;

            Console.WriteLine($"引数: {unvalidatedArguments}");
            SearchInput input = JsonSerializer.Deserialize<SearchInput>(unvalidatedArguments,
                    new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!;
            Console.WriteLine($"インプット: {input}");
            var functionResultData = CallMicrosoftSearchAsync(input);
            var functionResponseMessage = new ChatMessage(
                ChatRole.Function,
                JsonSerializer.Serialize(
                    functionResultData,
                    new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            functionResponseMessage.Name = MicrosoftSearchApiFunction.Name;
            chatCompletionsOptions.Messages.Add(functionResponseMessage);
        } else if (responseChoice.Message.FunctionCall.Name == GetCurrentTimeFunction.Name) // Handle the time function call
        {
            Console.WriteLine($"呼び出す関数: {GetCurrentTimeFunction.Name}");
            string unvalidatedArguments = responseChoice.Message.FunctionCall.Arguments;
            Console.WriteLine($"引数: {unvalidatedArguments}");
            TimeInput input = JsonSerializer.Deserialize<TimeInput>(unvalidatedArguments,
                    new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!;
            Console.WriteLine($"インプット: {input}");
            var functionResultData = GetCurrentTimeFunction.GetTime(input.Location);
            var functionResponseMessage = new ChatMessage(
                ChatRole.Function,
                JsonSerializer.Serialize(
                    functionResultData,
                    new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            functionResponseMessage.Name = GetCurrentTimeFunction.Name;
            chatCompletionsOptions.Messages.Add(functionResponseMessage);
        } else if (responseChoice.Message.FunctionCall.Name == GetWeatherFunction.Name)
        {
            Console.WriteLine($"呼び出す関数: {GetWeatherFunction.Name}");
            string unvalidatedArguments = responseChoice.Message.FunctionCall.Arguments;
            WeatherInput input = JsonSerializer.Deserialize<WeatherInput>(unvalidatedArguments,
                    new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!;
            // var functionResultData = GetWeatherFunction.GetWeather(input.Location, input.Unit);
            Console.WriteLine($"引数: {unvalidatedArguments}");
            var functionResultData = GetWeatherFunction.GetWeather(input.Location, weatherApiKey);
            var functionResponseMessage = new ChatMessage(
                ChatRole.Function,
                JsonSerializer.Serialize(
                    functionResultData,
                    new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            functionResponseMessage.Name = GetWeatherFunction.Name;
            chatCompletionsOptions.Messages.Add(functionResponseMessage);
        }

        Console.WriteLine($"Function call: {chatCompletionsOptions.Messages}");

        response = await ai_client.GetChatCompletionsAsync(
            deploymentOrModelName: builder.Configuration.GetValue<string>("AzureOpenAIDeploymentModelName"),
            chatCompletionsOptions
        );

        responseChoice = response.Choices[0];
    }

    var response_content = responseChoice.Message.Content;

    return response_content;
    }
    async Task<string> CallMicrosoftSearchAsync(SearchInput input)
    {
        // エンドポイントとHttpClientを設定
        var endpoint = builder.Configuration.GetValue<string>("FunctionsEndpoint");
        var httpClient = new HttpClient();
        
        // inputオブジェクトをJSONにシリアル化
        var jsonContent = JsonSerializer.Serialize(input);
        var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
        
        // POSTリクエストを実行
        var httpResponse = await httpClient.PostAsync(endpoint, httpContent);
        
        // 応答をチェックし、応答コンテンツを返す
        httpResponse.EnsureSuccessStatusCode();
        var responseContent = await httpResponse.Content.ReadAsStringAsync();
        return responseContent;
        }

async Task HandleRecognizeAsync(CallMedia callConnectionMedia, string callerId, string message)
{
    // Play greeting message
    var greetingPlaySource = new TextSource(message)
    {
        VoiceName = "ja-JP-NanamiNeural"
    };

    var recognizeOptions =
        new CallMediaRecognizeSpeechOptions(
            targetParticipant: CommunicationIdentifier.FromRawId(callerId))
        {
            InterruptPrompt = false,
            InitialSilenceTimeout = TimeSpan.FromSeconds(5),
            Prompt = greetingPlaySource,
            OperationContext = "GetFreeFormText",
            EndSilenceTimeout = TimeSpan.FromMilliseconds(500),
            SpeechLanguage = "ja-JP"
        };

    var recognize_result = await callConnectionMedia.StartRecognizingAsync(recognizeOptions);
}

async Task HandlePlayAsync(string textToPlay, string context, CallMedia callConnectionMedia)
{
    // Play message
    var playSource = new TextSource(textToPlay)
    {
        VoiceName = "ja-JP-NanamiNeural"
    };

    var playOptions = new PlayToAllOptions(playSource) { OperationContext = context };
    await callConnectionMedia.PlayToAllAsync(playOptions);
}

app.Run();