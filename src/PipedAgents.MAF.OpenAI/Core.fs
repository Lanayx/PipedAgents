#nowarn "57"

namespace PipedAgents.MAF.OpenAI

open System
open System.ClientModel
open PipedAgents.MAF
open Microsoft.Agents.AI
open Microsoft.Extensions.AI
open OpenAI.Chat
open OpenAI.Responses

type IOpenAiChatClient<'T> = interface end
type ResponsesChatClient(chatClient: IChatClient) =
    interface IOpenAiChatClient<CreateResponseOptions>
    interface IChatClient with
        member this.GetResponseAsync(messages, options, ct) = chatClient.GetResponseAsync(messages, options, ct)
        member this.GetStreamingResponseAsync(messages, options, ct) = chatClient.GetStreamingResponseAsync(messages, options, ct)
        member this.GetService(serviceType, serviceKey) = chatClient.GetService(serviceType, serviceKey)
        member this.Dispose() = chatClient.Dispose()

type ChatCompletionsChatClient(chatClient: IChatClient) =
    interface IOpenAiChatClient<ChatCompletionOptions>
    interface IChatClient with
        member this.GetResponseAsync(messages, options, ct) = chatClient.GetResponseAsync(messages, options, ct)
        member this.GetStreamingResponseAsync(messages, options, ct) = chatClient.GetStreamingResponseAsync(messages, options, ct)
        member this.GetService(serviceType, serviceKey) = chatClient.GetService(serviceType, serviceKey)
        member this.Dispose() = chatClient.Dispose()

type OpenAiChatClient =
    | ResponsesClient of ResponsesChatClient
    | ChatCompletionsClient of ChatCompletionsChatClient
    member this.CreateAgent(chatOptions: AgentOptions<'T>) =
        match this with
        | ResponsesClient agent ->
            agent.CreateAIAgent(chatOptions.ToAgentOptions())
        | ChatCompletionsClient agent ->
            agent.CreateAIAgent(chatOptions.ToAgentOptions())
    member this.AddMiddleware(runMiddleware, ?streamingRunMiddleware) =
        match this with
        | ResponsesClient client ->
            client.AsBuilder().Use(runMiddleware, (streamingRunMiddleware |> Option.toObj))
                .Build() |> fun chatClient -> new ResponsesChatClient(chatClient) |> OpenAiChatClient.ResponsesClient
        | ChatCompletionsClient client ->
            client.AsBuilder().Use(runMiddleware, (streamingRunMiddleware |> Option.toObj))
                .Build() |> fun chatClient -> new ChatCompletionsChatClient(chatClient) |> OpenAiChatClient.ChatCompletionsClient
    interface IDisposable with
        member this.Dispose() =
            match this with
            | ResponsesClient client -> (client :> IDisposable).Dispose()
            | ChatCompletionsClient client -> (client :> IDisposable).Dispose()

type Client =
    static member ForResponsesAPI(model, ?key, ?options) =
        let key =
            key |> Option.defaultValue (
                ApiKeyCredential(Environment.GetEnvironmentVariable("OPENAI_API_KEY"))
            )
        let options =
            options |> Option.defaultValue (
                OpenAI.OpenAIClientOptions(Endpoint = Uri(Environment.GetEnvironmentVariable "OPENAI_BASE_URL"))
            )
        OpenAI.OpenAIClient(key, options).GetResponsesClient(model).AsIChatClient()
            |> fun chatClient -> new ChatCompletionsChatClient(chatClient) |> ChatCompletionsClient

    static member ForChatCompletionsAPI(model, ?key, ?options) =
        let key =
            key |> Option.defaultValue (
                ApiKeyCredential(Environment.GetEnvironmentVariable("OPENAI_API_KEY"))
            )
        let options =
            options |> Option.defaultValue (
                OpenAI.OpenAIClientOptions(Endpoint = Uri(Environment.GetEnvironmentVariable "OPENAI_BASE_URL"))
            )
        OpenAI.OpenAIClient(key, options).GetChatClient(model).AsIChatClient()
            |> fun chatClient -> new ResponsesChatClient(chatClient) |> ResponsesClient

