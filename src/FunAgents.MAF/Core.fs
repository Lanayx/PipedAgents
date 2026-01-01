#nowarn "57"

namespace FunAgents.MAF

open System
open System.Collections.Generic
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Text.Json
open System.Threading
open System.Threading.Tasks
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

type ThreadExtensions =
    [<Extension>]
    static member GetThreadRun(agent: AIAgent, ?thread: AgentThread, ?options: AgentRunOptions, ?ct: CancellationToken) =
        let thread = thread |> Option.defaultWith agent.GetNewThread
        fun (message: string) -> agent.RunAsync(message, thread, options = (options |> Option.toObj), ?cancellationToken = ct)

    [<Extension>]
    static member GetThreadRun<'T>(agent: ChatClientAgent, ?thread: AgentThread, ?options: AgentRunOptions, ?ct: CancellationToken) =
        let thread = thread |> Option.defaultWith agent.GetNewThread
        fun (message: string) -> agent.RunAsync<'T>(message, thread, options = (options |> Option.toObj), ?cancellationToken = ct)

    [<Extension>]
    static member GetThreadMessageRun(agent: AIAgent, ?thread: AgentThread, ?options: AgentRunOptions, ?ct: CancellationToken) =
        let thread = thread |> Option.defaultWith agent.GetNewThread
        fun (message: ChatMessage) -> agent.RunAsync(message, thread, options = (options |> Option.toObj), ?cancellationToken = ct)

    [<Extension>]
    static member GetThreadMessageRun<'T>(agent: ChatClientAgent, ?thread: AgentThread, ?options: AgentRunOptions, ?ct: CancellationToken) =
        let thread = thread |> Option.defaultWith agent.GetNewThread
        fun (message: ChatMessage) -> agent.RunAsync<'T>(message, thread, options = (options |> Option.toObj), ?cancellationToken = ct)

    [<Extension>]
    static member GetStreamingThreadRun(agent: AIAgent, ?thread: AgentThread, ?options: AgentRunOptions, ?ct: CancellationToken) =
        let thread = thread |> Option.defaultWith agent.GetNewThread
        fun (message: string) -> agent.RunStreamingAsync(message, thread, options = (options |> Option.toObj), ?cancellationToken = ct)

    [<Extension>]
    static member GetStreamingThreadMessageRun(agent: AIAgent, ?thread: AgentThread, ?options: AgentRunOptions, ?ct: CancellationToken) =
        let thread = thread |> Option.defaultWith agent.GetNewThread
        fun (message: ChatMessage) -> agent.RunStreamingAsync(message, thread, options = (options |> Option.toObj), ?cancellationToken = ct)

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
            runMiddleware: ChatMessage seq -> AgentThread -> AgentRunOptions -> AIAgent -> CancellationToken -> Task<AgentRunResponse>,
            ?streamRunMiddleware: ChatMessage seq -> AgentThread -> AgentRunOptions -> AIAgent -> CancellationToken -> IAsyncEnumerable<AgentRunResponseUpdate>
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

type Thread =
    static member New(agent: ChatClientAgent) =
        agent.GetNewThread() :?> ChatClientAgentThread
    static member GetChatHistory(thread: ChatClientAgentThread) =
        thread.GetService<IList<ChatMessage>>()
    [<Extension>]
    static member ToString(thread: AgentThread,
                           [<Optional; DefaultParameterValue(null:JsonSerializerOptions)>]options: JsonSerializerOptions) =
        thread.Serialize(options)
        |> string
    static member FromString(thread: string, agent: AIAgent,
                           [<Optional; DefaultParameterValue(null:JsonSerializerOptions)>]options: JsonSerializerOptions) =
        let element = JsonElement.Parse(thread)
        agent.DeserializeThread(element, options)


type Message =
    static member GetTextContent(text: string) =
        TextContent text :> AIContent
    static member GetUrlContent(uri: string, mediaType: string) =
        UriContent(uri, mediaType) :> AIContent
    static member GetUserMessage(contents: IList<AIContent>) =
        ChatMessage(ChatRole.User, contents)
