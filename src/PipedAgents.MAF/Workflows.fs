namespace PipedAgents.MAF

open System
open System.Runtime.CompilerServices
open Microsoft.Agents.AI
open Microsoft.Agents.AI.Workflows

type Node<'TIn, 'TOut>(binding: ExecutorBinding) =
    member _.Binding = binding

[<RequireQualifiedAccess>]
type EdgeType =
    | Direct of fromNode:ExecutorBinding * toNode:ExecutorBinding
    | Conditional of fromNode:ExecutorBinding * toNode:ExecutorBinding * condition:(obj -> bool)
    | Switch of fromNode:ExecutorBinding * switch:Action<SwitchBuilder>
    | FanOut of fromNode:ExecutorBinding * toNode:ExecutorBinding seq
    | FanIn of fromNode:ExecutorBinding seq * toNode:ExecutorBinding

[<AutoOpen>]
module NodeOperators =
    let inline (==>) (fromNode : Node<'a, 'b>) (toNode: Node<'b, 'c>) =
        EdgeType.Direct(fromNode.Binding, toNode.Binding)
    let inline (=?>) (fromNode : Node<'a, 'b>) (toNode: Node<'b, 'c>, condition: 'b -> bool) =
        EdgeType.Conditional(fromNode.Binding, toNode.Binding, (fun (o: obj) -> condition (o :?> 'b)))
    let inline (=>>) (fromNode : Node<'a, 'b>) (toNodes: Node<'b, 'c> seq) =
        EdgeType.FanOut(fromNode.Binding, toNodes |> Seq.map (fun n -> n.Binding))
    let inline (>>=) (fromNodes : Node<'a, 'b> seq) (toNode: Node<'b, 'c>) =
        EdgeType.FanIn(fromNodes |> Seq.map (fun n -> n.Binding), toNode.Binding)

[<AutoOpen; AbstractClass; Sealed>]
type NodeOperations =
    static member GetNode (value : Func<'T1,'T2>, id: string, ?options: ExecutorOptions, ?threadsafe: bool) : Node<'T1, 'T2> =
        Node(value.BindAsExecutor(id, (options |> Option.toObj), (threadsafe |> Option.defaultValue false)))
    static member GetNode (value : Executor<'T1,'T2>, ?options: ExecutorOptions, ?threadsafe: bool) : Node<'T1, 'T2> =
        Node(value.BindExecutor())
    static member GetNode (value : Executor, ?options: ExecutorOptions, ?threadsafe: bool) : Node<obj, obj> =
        Node(value.BindExecutor())


type Workflow<'TIn, 'TNodeOut>(node: Node<'TIn, 'TNodeOut>) =

    let workflow = WorkflowBuilder(node.Binding)

    member _.Zero() = ()
    member _.Delay(f) = f()
    member _.Yield(edge: EdgeType) =
        match edge with
        | EdgeType.Direct(from, ``to``) ->
            workflow.AddEdge(from, ``to``) |> ignore
        | EdgeType.Conditional(from, ``to``, condition) ->
            workflow.AddEdge(from, ``to``, Func<obj, bool>(condition)) |> ignore
        | EdgeType.FanOut(from, tos) ->
            workflow.AddFanOutEdge(from, tos) |> ignore
        | EdgeType.FanIn(from, ``to``) ->
            workflow.AddFanInEdge(from, ``to``) |> ignore
        | EdgeType.Switch(from, switch) ->
            workflow.AddSwitch(from, switch) |> ignore
    member _.Combine(a: unit, b: unit) = ()
    member _.Return(toNode: Node<_, 'TOut>) =
        workflow.WithOutputFrom(toNode.Binding) |> ignore
    member _.Run(()) =
        workflow.Build()
