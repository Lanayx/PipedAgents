namespace FunAgents.MAF

open System
open System.Runtime.CompilerServices

open System.Threading
open Microsoft.Agents.AI
open Microsoft.Extensions.AI

type Extensions =
    [<Extension>]
    static member GetSendThreadMessage(agent: ChatClientAgent, ?thread: AgentThread, ?options: AgentRunOptions, ?ct: CancellationToken) =
        let thread = thread |> Option.defaultWith agent.GetNewThread
        fun (message: string) -> agent.RunAsync(message, thread, options = (options |> Option.toObj), ?cancellationToken = ct)

    [<Extension>]
    static member GetSendThreadContents(agent: ChatClientAgent, ?thread: AgentThread, ?options: AgentRunOptions, ?ct: CancellationToken) =
        let thread = thread |> Option.defaultWith agent.GetNewThread
        fun (messages: AIContent[]) -> agent.RunAsync(ChatMessage(ChatRole.User, messages), thread, options = (options |> Option.toObj), ?cancellationToken = ct)

    [<Extension>]
    static member GetStreamingSendThreadMessage(agent: ChatClientAgent, ?thread: AgentThread, ?options: AgentRunOptions, ?ct: CancellationToken) =
        let thread = thread |> Option.defaultWith agent.GetNewThread
        fun (message: string) -> agent.RunStreamingAsync(message, thread, options = (options |> Option.toObj), ?cancellationToken = ct)

type AiTool =
    static member Get(handler: Func<_, _, _, _, _>, ?options) =
        AIFunctionFactory.Create(handler, options = (options |> Option.toObj))
    static member Get(handler: Func<_, _, _, _>, ?options) =
        AIFunctionFactory.Create(handler, options = (options |> Option.toObj))
    static member Get(handler: Func<_, _, _>, ?options) =
        AIFunctionFactory.Create(handler, options = (options |> Option.toObj))
    static member Get(handler: Func<_, _>, ?options) =
        AIFunctionFactory.Create(handler, options = (options |> Option.toObj))
    static member Get(handler: Action<_>, ?options) =
        AIFunctionFactory.Create(handler, options = (options |> Option.toObj))
