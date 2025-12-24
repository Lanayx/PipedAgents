namespace FunAgents.MAF

open System
open System.Runtime.CompilerServices
open System.Threading
open Microsoft.Agents.AI
open Microsoft.Extensions.AI

type Extensions =
    [<Extension>]
    static member GetSendThreadMessage(agent: ChatClientAgent, ?thread: AgentThread, ?options: AgentRunOptions, ?ct: CancellationToken) =
        let thread: AgentThread = thread |> Option.defaultWith agent.GetNewThread
        match options, ct with
        | None, None -> fun (message: string) -> agent.RunAsync(message, thread)
        | Some options, None -> fun (message: string) -> agent.RunAsync(message, thread, options)
        | None, Some ct -> fun (message: string) -> agent.RunAsync(message, thread, cancellationToken = ct)
        | Some options, Some ct -> fun (message: string) -> agent.RunAsync(message, thread, options, ct)

    [<Extension>]
    static member GetSendThreadContents(agent: ChatClientAgent, ?thread: AgentThread, ?options: AgentRunOptions, ?ct: CancellationToken) =
        let thread: AgentThread = thread |> Option.defaultWith agent.GetNewThread
        match options, ct with
        | None, None -> fun (messages: AIContent[]) -> agent.RunAsync(ChatMessage(ChatRole.User, messages), thread)
        | Some options, None -> fun (messages: AIContent[]) -> agent.RunAsync(ChatMessage(ChatRole.User, messages), thread, options)
        | None, Some ct -> fun (messages: AIContent[]) -> agent.RunAsync(ChatMessage(ChatRole.User, messages), thread, cancellationToken = ct)
        | Some options, Some ct -> fun (messages: AIContent[]) -> agent.RunAsync(ChatMessage(ChatRole.User, messages), thread, options, ct)

    [<Extension>]
    static member GetStreamingSendThreadMessage(agent: ChatClientAgent, ?thread: AgentThread, ?options: AgentRunOptions, ?ct: CancellationToken) =
        let thread: AgentThread = thread |> Option.defaultWith agent.GetNewThread
        match options, ct with
        | None, None -> fun (message: string) -> agent.RunStreamingAsync(message, thread)
        | Some options, None -> fun (message: string) -> agent.RunStreamingAsync(message, thread, options)
        | None, Some ct -> fun (message: string) -> agent.RunStreamingAsync(message, thread, cancellationToken = ct)
        | Some options, Some ct -> fun (message: string) -> agent.RunStreamingAsync(message, thread, options, ct)

type AiTool =
    static member Get(handler: Func<_, _, _, _, _>, ?options) =
        match options with
        | Some options -> AIFunctionFactory.Create(handler, options)
        | None -> AIFunctionFactory.Create(handler)
    static member Get(handler: Func<_, _, _, _>, ?options) =
        match options with
        | Some options -> AIFunctionFactory.Create(handler, options)
        | None -> AIFunctionFactory.Create(handler)
    static member Get(handler: Func<_, _, _>, ?options) =
        match options with
        | Some options -> AIFunctionFactory.Create(handler, options)
        | None -> AIFunctionFactory.Create(handler)
    static member Get(handler: Func<_, _>, ?options) =
        match options with
        | Some options -> AIFunctionFactory.Create(handler, options)
        | None -> AIFunctionFactory.Create(handler)
    static member Get(handler: Action<_>, ?options) =
        match options with
        | Some options -> AIFunctionFactory.Create(handler, options)
        | None -> AIFunctionFactory.Create(handler)
