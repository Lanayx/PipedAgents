#nowarn "57"

namespace FunAgents.MAF.OpenAI

open System
open System.ClientModel
open System.Runtime.CompilerServices

open Microsoft.Agents.AI
open Microsoft.Extensions.AI
open OpenAI.Responses

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
        OpenAI.OpenAIClient(key, options).GetResponsesClient(model)

type AgentOptions() =
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
    member this.CreateResponseOptions
        with set (value: IChatClient -> CreateResponseOptions) =
            agentOptions.ChatOptions.RawRepresentationFactory <- (fun x -> value x)
    member this.AdditionalProperties
        with set value = agentOptions.ChatOptions.AdditionalProperties <- value
    member this.ToAgentOptions() = agentOptions

type Extensions =
    [<Extension>]
    static member CreateAgent(agent: ResponsesClient, chatOptions: AgentOptions) =
        agent.CreateAIAgent(chatOptions.ToAgentOptions())
