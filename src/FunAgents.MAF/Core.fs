namespace FunAgents.MAF

open System.Runtime.CompilerServices
open System.Threading
open Microsoft.Agents.AI

type Extensions =
    [<Extension>]
    static member GetThreadRun(agent: ChatClientAgent, ?thread: AgentThread, ?options: AgentRunOptions, ?ct: CancellationToken) =
        let thread: AgentThread = thread |> Option.defaultWith agent.GetNewThread
        match options, ct with
        | None, None ->  fun (message: string) -> agent.RunAsync(message, thread)
        | Some options, None ->  fun (message: string) -> agent.RunAsync(message, thread, options)
        | None, Some ct ->  fun (message: string) -> agent.RunAsync(message, thread, cancellationToken = ct)
        | Some options, Some ct ->  fun (message: string) -> agent.RunAsync(message, thread, options, ct)

    [<Extension>]
    static member GetStreamingThreadRun(agent: ChatClientAgent, ?thread: AgentThread, ?options: AgentRunOptions, ?ct: CancellationToken) =
        let thread: AgentThread = thread |> Option.defaultWith agent.GetNewThread
        match options, ct with
        | None, None ->  fun (message: string) -> agent.RunStreamingAsync(message, thread)
        | Some options, None ->  fun (message: string) -> agent.RunStreamingAsync(message, thread, options)
        | None, Some ct ->  fun (message: string) -> agent.RunStreamingAsync(message, thread, cancellationToken = ct)
        | Some options, Some ct ->  fun (message: string) -> agent.RunStreamingAsync(message, thread, options, ct)