#nowarn "57"

namespace FunAgents.MAF.OpenAI

open System
open System.ClientModel
open System.Runtime.CompilerServices

open Microsoft.Agents.AI
open Microsoft.Extensions.AI
open OpenAI.Chat
open OpenAI.Responses

type AgentOptions<'T>() =
    let agentOptions = ChatClientAgentOptions(ChatOptions = ChatOptions())
    member this.Id
        with set value = agentOptions.Id <- value
    member this.Name
        with set value = agentOptions.Name <- value
    member this.Description
        with set value = agentOptions.Description <- value
    member this.ChatMessageStoreFactory
        with set value = agentOptions.ChatMessageStoreFactory <- value
    member this.AIContextProviderFactory
        with set value = agentOptions.AIContextProviderFactory <- value
    member this.UseProvidedChatClientAsIs
        with set value = agentOptions.UseProvidedChatClientAsIs <- value
    member this.ConversationId
        with set value = agentOptions.ChatOptions.ConversationId <- value
    member this.Instructions
        with set value = agentOptions.ChatOptions.Instructions <- value
    member this.Temperature
        with set value = agentOptions.ChatOptions.Temperature <- value
    member this.MaxOutputTokens
        with set value = agentOptions.ChatOptions.MaxOutputTokens <- value
    member this.TopP
        with set value = agentOptions.ChatOptions.TopP <- value
    member this.TopK
        with set value = agentOptions.ChatOptions.TopK <- value
    member this.FrequencyPenalty
        with set value = agentOptions.ChatOptions.FrequencyPenalty <- value
    member this.PresencePenalty
        with set value = agentOptions.ChatOptions.PresencePenalty <- value
    member this.Seed
        with set value = agentOptions.ChatOptions.Seed <- value
    member this.ResponseFormat
        with set value = agentOptions.ChatOptions.ResponseFormat <- value
    member this.ModelId
        with set value = agentOptions.ChatOptions.ModelId <- value
    member this.StopSequences
        with set value = agentOptions.ChatOptions.StopSequences <- value
    member this.AllowMultipleToolCalls
        with set value = agentOptions.ChatOptions.AllowMultipleToolCalls <- value
    member this.ToolMode
        with set value = agentOptions.ChatOptions.ToolMode <- value
    member this.Tools
        with set value = agentOptions.ChatOptions.Tools <- value
    member this.AllowBackgroundResponses
        with set value = agentOptions.ChatOptions.AllowBackgroundResponses <- value
    member this.ContinuationToken
        with set value = agentOptions.ChatOptions.ContinuationToken <- value
    member this.CreateRawOptions
        with set (value: IChatClient -> 'T) =
            agentOptions.ChatOptions.RawRepresentationFactory <- (fun x -> value x |> box)
    member this.AdditionalProperties
        with set value = agentOptions.ChatOptions.AdditionalProperties <- value
    member this.ToAgentOptions() = agentOptions

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
