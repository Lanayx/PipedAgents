#nowarn "57"

namespace PipedAgents.MAF.Anthropic

open System
open System.Runtime.CompilerServices
open Anthropic.Core
open Anthropic.Models.Beta.Messages
open PipedAgents.MAF
open Microsoft.Extensions.AI

type MessagesChatClient(chatClient: IChatClient) =
    interface IChatClient with
        member this.GetResponseAsync(messages, options, ct) = chatClient.GetResponseAsync(messages, options, ct)
        member this.GetStreamingResponseAsync(messages, options, ct) = chatClient.GetStreamingResponseAsync(messages, options, ct)
        member this.GetService(serviceType, serviceKey) = chatClient.GetService(serviceType, serviceKey)
        member this.Dispose() = chatClient.Dispose()

type ChatClientExtensions =
    [<Extension>]
    static member CreateAgent(chatClient: MessagesChatClient, chatOptions: AgentOptions<MessageCreateParams>) =
        chatClient.AsAIAgent(chatOptions.ToAgentOptions())
    [<Extension>]
    static member AddMiddleware(chatClient: MessagesChatClient, runMiddleware, ?streamingRunMiddleware) =
        chatClient.AsBuilder().Use(runMiddleware, (streamingRunMiddleware |> Option.toObj)).Build()
        |> fun chatClient -> new MessagesChatClient(chatClient)

type Client =
    static member ForMessagesAPI(model, ?options) =
        let options =
            options |> Option.defaultValue (
                let mutable options =
                    ClientOptions(
                        APIKey = Environment.GetEnvironmentVariable "ANTHROPIC_API_KEY"
                    )
                let baseUrl = Environment.GetEnvironmentVariable "ANTHROPIC_BASE_URL"
                let authToken = Environment.GetEnvironmentVariable "ANTHROPIC_AUTH_TOKEN"
                if baseUrl |> isNull |> not then
                    options.BaseUrl <- Uri(baseUrl)
                if authToken |> isNull |> not then
                    options.AuthToken <- authToken
                options
            )
        Anthropic.AnthropicClient(options).AsIChatClient(model)
            |> fun chatClient -> new MessagesChatClient(chatClient)