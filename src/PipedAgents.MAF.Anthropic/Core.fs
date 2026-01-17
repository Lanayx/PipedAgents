#nowarn "57"

namespace PipedAgents.MAF.OpenAI

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
        chatClient.CreateAIAgent(chatOptions.ToAgentOptions())
    [<Extension>]
    static member AddMiddleware(chatClient: MessagesChatClient, runMiddleware, ?streamingRunMiddleware) =
        chatClient.AsBuilder().Use(runMiddleware, (streamingRunMiddleware |> Option.toObj)).Build()
        |> fun chatClient -> new MessagesChatClient(chatClient)

type Client =
    static member ForMessagesAPI(model, ?options) =
        let options =
            options |> Option.defaultValue (
                ClientOptions(
                    BaseUrl = Uri(Environment.GetEnvironmentVariable "ANTHROPIC_BASE_URL"),
                    APIKey = Environment.GetEnvironmentVariable "ANTHROPIC_API_KEY"
                )
            )
        Anthropic.AnthropicClient(options).AsIChatClient(model)
            |> fun chatClient -> new MessagesChatClient(chatClient)