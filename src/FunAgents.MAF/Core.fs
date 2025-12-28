namespace FunAgents.MAF

open System
open System.Collections.Generic
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Text.Json
open System.Threading
open Microsoft.Agents.AI
open Microsoft.Extensions.AI
open FSharp.Quotations
open FSharp.Quotations.Patterns


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
    static member AddOpenTelemetry(agent: ChatClientAgent, ?sourceName: string, ?options: OpenTelemetryOptions) =
        match options with
        | None ->
            new OpenTelemetryAgent(agent, (sourceName |> Option.toObj))
        | Some options ->
            new OpenTelemetryAgent(agent, (sourceName |> Option.toObj), EnableSensitiveData = options.EnableSensitiveData)

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
