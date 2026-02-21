#nowarn "57"

namespace PipedAgents.MAF

open System
open System.Collections.Generic
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open FSharp.Control
open Microsoft.Agents.AI
open Microsoft.Extensions.AI
open FSharp.Quotations
open FSharp.Quotations.Patterns

type AgentOptions<'T>() =
    let agentOptions = ChatClientAgentOptions(ChatOptions = ChatOptions())
    member this.Id
        with set value = agentOptions.Id <- value
    member this.Name
        with set value = agentOptions.Name <- value
    member this.Description
        with set value = agentOptions.Description <- value
    member this.ChatHistoryProvider
        with set value = agentOptions.ChatHistoryProvider <- value
    member this.AIContextProviders
        with set value = agentOptions.AIContextProviders <- value
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

type ThreadExtensions =
    [<Extension>]
    static member GetSessionRun(agent: AIAgent, ?session: AgentSession, ?options: AgentRunOptions, ?ct: CancellationToken) =
        let sessionTask =
            task {
                match session with
                | Some t -> return t
                | None -> return! agent.CreateSessionAsync()
            }
        fun (message: string) ->
            task {
                let! session = sessionTask
                return! agent.RunAsync(message, session, options = (options |> Option.toObj), ?cancellationToken = ct)
            }

    [<Extension>]
    static member GetSessionRun<'T>(agent: AIAgent, ?session: AgentSession, ?options: AgentRunOptions, ?ct: CancellationToken) =
        let sessionTask =
            task {
                match session with
                | Some t -> return t
                | None -> return! agent.CreateSessionAsync()
            }
        fun (message: string) ->
            task {
                let! session = sessionTask
                return! agent.RunAsync<'T>(message, session, options = (options |> Option.toObj), ?cancellationToken = ct)
            }

    [<Extension>]
    static member GetSessionMessageRun(agent: AIAgent, ?session: AgentSession, ?options: AgentRunOptions, ?ct: CancellationToken) =
        let sessionTask =
            task {
                match session with
                | Some t -> return t
                | None -> return! agent.CreateSessionAsync()
            }
        fun (message: ChatMessage) ->
            task {
                let! session = sessionTask
                return! agent.RunAsync(message, session, options = (options |> Option.toObj), ?cancellationToken = ct)
            }

    [<Extension>]
    static member GetSessionMessageRun<'T>(agent: AIAgent, ?session: AgentSession, ?options: AgentRunOptions, ?ct: CancellationToken) =
        let sessionTask =
            task {
                match session with
                | Some t -> return t
                | None -> return! agent.CreateSessionAsync()
            }
        fun (message: ChatMessage) ->
            task {
                let! session = sessionTask
                return! agent.RunAsync<'T>(message, session, options = (options |> Option.toObj), ?cancellationToken = ct)
            }

    [<Extension>]
    static member GetStreamingSessionRun(agent: AIAgent, ?session: AgentSession, ?options: AgentRunOptions, ?ct: CancellationToken) =
        let sessionTask =
            task {
                match session with
                | Some t -> return t
                | None -> return! agent.CreateSessionAsync()
            }
        fun (message: string) ->
            taskSeq {
                let! session = sessionTask
                yield! agent.RunStreamingAsync(message, session, options = (options |> Option.toObj), ?cancellationToken = ct)
            }

    [<Extension>]
    static member GetStreamingThreadMessageRun(agent: AIAgent, ?session: AgentSession, ?options: AgentRunOptions, ?ct: CancellationToken) =
        let sessionTask =
            task {
                match session with
                | Some t -> return t
                | None -> return! agent.CreateSessionAsync()
            }
        fun (message: ChatMessage) ->
            taskSeq {
                let! session = sessionTask
                yield! agent.RunStreamingAsync(message, session, options = (options |> Option.toObj), ?cancellationToken = ct)
            }

type OpenTelemetryOptions() =
    member val EnableSensitiveData: bool = false with get, set
type AgentExtensions =
    [<Extension>]
    static member AddOpenTelemetry(agent: AIAgent, ?sourceName: string, ?options: OpenTelemetryOptions) =
        match options with
        | None ->
            new OpenTelemetryAgent(agent, (sourceName |> Option.toObj))
        | Some options ->
            new OpenTelemetryAgent(agent, (sourceName |> Option.toObj), EnableSensitiveData = options.EnableSensitiveData)
    [<Extension>]
    static member AddRunMiddleware(agent: AIAgent,
            runMiddleware: ChatMessage seq -> AgentSession -> AgentRunOptions -> AIAgent -> CancellationToken -> Task<AgentResponse>,
            ?streamRunMiddleware: ChatMessage seq -> AgentSession -> AgentRunOptions -> AIAgent -> CancellationToken -> IAsyncEnumerable<AgentResponseUpdate>
            ) =
        match streamRunMiddleware with
        | Some f ->
            agent.AsBuilder()
                .Use(runMiddleware, f)
                .Build()
        | None ->
            agent.AsBuilder()
                .Use(runMiddleware, null)
                .Build()

    [<Extension>]
    static member AddFunctionCallMiddleware(agent: AIAgent,
            functionCallMiddleware: AIAgent -> FunctionInvocationContext -> Func<FunctionInvocationContext, CancellationToken, ValueTask<obj>> -> CancellationToken -> ValueTask<obj>
            ) =
        agent.AsBuilder()
            .Use(functionCallMiddleware)
            .Build()
    [<Extension>]
    static member AddMiddleware(agent: IChatClient,
                                runMiddleware: ChatMessage seq -> ChatOptions -> IChatClient -> CancellationToken -> Task<ChatResponse>,
                                ?streamRunMiddleware: ChatMessage seq -> ChatOptions -> IChatClient -> CancellationToken -> IAsyncEnumerable<ChatResponseUpdate>
        ) =
        match streamRunMiddleware with
        | Some f ->
            agent.AsBuilder()
                .Use(runMiddleware, f)
                .Build()
        | None ->
            agent.AsBuilder()
                .Use(runMiddleware, null)
                .Build()

type Tool =
    static member Get(handler: Expr, ?options) =
        let rec extract = function
            | Call(_, mi, _) -> mi
            | Lambda(_, body) -> extract body
            | _ -> failwith "Unsupported expression. Please provide a call to a function."
        let mi = extract handler
        AIFunctionFactory.Create(mi, target= null, options = (options |> Option.toObj))

type Session =
    static member New(agent: ChatClientAgent) =
        task {
            let! session = agent.CreateSessionAsync()
            return session :?> ChatClientAgentSession
        }
    static member GetChatHistory(session: ChatClientAgentSession) =
        session.GetService<IList<ChatMessage>>()
    [<Extension>]
    static member Serialize(session: AgentSession, agent: AIAgent,
                            [<Optional; DefaultParameterValue(null:JsonSerializerOptions)>]options: JsonSerializerOptions) =
        task {
            let! jsonElement = agent.SerializeSessionAsync(session, options)
            return jsonElement.ToString()
        }
    static member Deserialize(session: string, agent: AIAgent,
                              [<Optional; DefaultParameterValue(null:JsonSerializerOptions)>]options: JsonSerializerOptions) =
        let element = JsonElement.Parse(session)
        agent.DeserializeSessionAsync(element, options).AsTask()


type Message =
    static member GetTextContent(text: string) =
        TextContent text :> AIContent
    static member GetUrlContent(uri: string, mediaType: string) =
        UriContent(uri, mediaType) :> AIContent
    static member GetUserMessage(contents: IList<AIContent>) =
        ChatMessage(ChatRole.User, contents)

