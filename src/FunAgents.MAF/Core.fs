namespace FunAgents.MAF

open System.Runtime.CompilerServices
open System.Threading
open Microsoft.Agents.AI
open Microsoft.Extensions.AI
open FSharp.Quotations
open FSharp.Quotations.Patterns

type Extensions =
    [<Extension>]
    static member GetThreadRun(agent: ChatClientAgent, ?thread: AgentThread, ?options: AgentRunOptions, ?ct: CancellationToken) =
        let thread = thread |> Option.defaultWith agent.GetNewThread
        fun (message: string) -> agent.RunAsync(message, thread, options = (options |> Option.toObj), ?cancellationToken = ct)

    [<Extension>]
    static member GetThreadRun<'T>(agent: ChatClientAgent, ?thread: AgentThread, ?options: AgentRunOptions, ?ct: CancellationToken) =
        let thread = thread |> Option.defaultWith agent.GetNewThread
        fun (message: string) -> agent.RunAsync<'T>(message, thread, options = (options |> Option.toObj), ?cancellationToken = ct)

    [<Extension>]
    static member GetThreadContentsRun(agent: ChatClientAgent, ?thread: AgentThread, ?options: AgentRunOptions, ?ct: CancellationToken) =
        let thread = thread |> Option.defaultWith agent.GetNewThread
        fun (messages: AIContent[]) -> agent.RunAsync(ChatMessage(ChatRole.User, messages), thread, options = (options |> Option.toObj), ?cancellationToken = ct)

    [<Extension>]
    static member GetThreadContentsRun<'T>(agent: ChatClientAgent, ?thread: AgentThread, ?options: AgentRunOptions, ?ct: CancellationToken) =
        let thread = thread |> Option.defaultWith agent.GetNewThread
        fun (messages: AIContent[]) -> agent.RunAsync<'T>(ChatMessage(ChatRole.User, messages), thread, options = (options |> Option.toObj), ?cancellationToken = ct)

    [<Extension>]
    static member GetStreamingThreadRun(agent: ChatClientAgent, ?thread: AgentThread, ?options: AgentRunOptions, ?ct: CancellationToken) =
        let thread = thread |> Option.defaultWith agent.GetNewThread
        fun (message: string) -> agent.RunStreamingAsync(message, thread, options = (options |> Option.toObj), ?cancellationToken = ct)

    [<Extension>]
    static member GetStreamingThreadContentsRun(agent: ChatClientAgent, ?thread: AgentThread, ?options: AgentRunOptions, ?ct: CancellationToken) =
        let thread = thread |> Option.defaultWith agent.GetNewThread
        fun (messages: AIContent[]) -> agent.RunStreamingAsync(ChatMessage(ChatRole.User, messages), thread, options = (options |> Option.toObj), ?cancellationToken = ct)

type AiTool =
    static member Get(handler: Expr, ?options) =
        let rec extract = function
            | Call(_, mi, _) -> mi
            | Lambda(_, body) -> extract body
            | _ -> failwith "Unsupported expression. Please provide a call to a function."
        let mi = extract handler
        AIFunctionFactory.Create(mi, target= null, options = (options |> Option.toObj))
