#nowarn "57"

namespace PipedAgents.MAF.OpenAI

open System
open System.ClientModel
open System.Runtime.CompilerServices
open PipedAgents.MAF
open Microsoft.Agents.AI
open Microsoft.Extensions.AI
open OpenAI.Chat
open OpenAI.Responses

type ResponsesChatClient(chatClient: IChatClient) =
    interface IChatClient with
        member this.GetResponseAsync(messages, options, ct) = chatClient.GetResponseAsync(messages, options, ct)
        member this.GetStreamingResponseAsync(messages, options, ct) = chatClient.GetStreamingResponseAsync(messages, options, ct)
        member this.GetService(serviceType, serviceKey) = chatClient.GetService(serviceType, serviceKey)
        member this.Dispose() = chatClient.Dispose()

type ChatCompletionsChatClient(chatClient: IChatClient) =
    interface IChatClient with
        member this.GetResponseAsync(messages, options, ct) = chatClient.GetResponseAsync(messages, options, ct)
        member this.GetStreamingResponseAsync(messages, options, ct) = chatClient.GetStreamingResponseAsync(messages, options, ct)
        member this.GetService(serviceType, serviceKey) = chatClient.GetService(serviceType, serviceKey)
        member this.Dispose() = chatClient.Dispose()

type ChatClientExtensions =
    [<Extension>]
    static member CreateAgent(chatClient: ResponsesChatClient, chatOptions: AgentOptions<CreateResponseOptions>) =
        chatClient.AsAIAgent(chatOptions.ToAgentOptions())
    [<Extension>]
    static member CreateAgent(chatClient: ChatCompletionsChatClient, chatOptions: AgentOptions<ChatCompletionOptions>) =
        chatClient.AsAIAgent(chatOptions.ToAgentOptions())
    [<Extension>]
    static member AddMiddleware(chatClient: ResponsesChatClient, runMiddleware, ?streamingRunMiddleware) =
        chatClient.AsBuilder().Use(runMiddleware, (streamingRunMiddleware |> Option.toObj)).Build()
        |> fun chatClient -> new ResponsesChatClient(chatClient)
    [<Extension>]
    static member AddMiddleware(chatClient: ChatCompletionsChatClient, runMiddleware, ?streamingRunMiddleware) =
        chatClient.AsBuilder().Use(runMiddleware, (streamingRunMiddleware |> Option.toObj)).Build()
        |> fun chatClient -> new ChatCompletionsChatClient(chatClient)

type Client =
    static member ForResponsesAPI(model, ?key, ?options) =
        let key =
            key |> Option.defaultWith (fun () ->
                Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                |> nullArgCheck "OPENAI_API_KEY"
                |> ApiKeyCredential
            )
        let options =
            options |> Option.defaultWith (fun () ->
                match Environment.GetEnvironmentVariable("OPENAI_BASE_URL") with
                | null ->
                    OpenAI.OpenAIClientOptions()
                | baseUrl ->
                    OpenAI.OpenAIClientOptions(Endpoint = Uri(baseUrl))
            )
        let client = OpenAI.OpenAIClient(key, options)
        client.GetResponsesClient().AsIChatClient(model)
            |> fun chatClient -> new ResponsesChatClient(chatClient)

    static member ForChatCompletionsAPI(model, ?key, ?options) =
        let key =
            key |> Option.defaultWith (fun () ->
                Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                |> nullArgCheck "OPENAI_API_KEY"
                |> ApiKeyCredential
            )
        let options =
            options |> Option.defaultWith (fun () ->
                match Environment.GetEnvironmentVariable("OPENAI_BASE_URL") with
                | null ->
                    OpenAI.OpenAIClientOptions()
                | baseUrl ->
                    OpenAI.OpenAIClientOptions(Endpoint = Uri(baseUrl))
            )
        OpenAI.OpenAIClient(key, options).GetChatClient(model).AsIChatClient()
            |> fun chatClient -> new ChatCompletionsChatClient(chatClient)

