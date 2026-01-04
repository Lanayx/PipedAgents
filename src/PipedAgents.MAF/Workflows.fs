namespace PipedAgents.MAF

open System
open System.Runtime.CompilerServices
open Microsoft.Agents.AI
open Microsoft.Agents.AI.Workflows

type Node<'TIn, 'TOut>(binding: ExecutorBinding) =
    member _.Binding = binding

[<RequireQualifiedAccess>]
type EdgeType<'T> =
    | Direct of fromNode:Node<obj, 'T> * toNode:Node<'T, obj>
    | Conditional of fromNode:Node<obj, 'T> * toNode:Node<'T, obj> * condition:('T -> bool)
    | Switch of fromNode:Node<obj, 'T> * switch:Action<SwitchBuilder>
    | FanOut of fromNode:Node<obj, 'T> * toNode:Node<'T, obj> seq
    | FanIn of fromNode:Node<obj, 'T> seq * toNode:Node<'T, obj>
[<AutoOpen>]
module NodeOperators =
    let inline private boxFromNode (n: Node<'a, 'b>) = Node<obj, 'b>(n.Binding)
    let inline private boxToNode (n: Node<'a, 'b>) = Node<'a, obj>(n.Binding)

    let inline (==>) (fromNode : Node<'a, 'b>) (toNode: Node<'b, 'c>) =
        EdgeType.Direct(Node<obj, 'b>(fromNode.Binding), Node<'b, obj>(toNode.Binding))
    let inline (=?>) (fromNode : Node<'a, 'b>) (toNode: Node<'b, 'c>, condition: 'b -> bool) =
        EdgeType.Conditional(Node<obj, 'b>(fromNode.Binding), Node<'b, obj>(toNode.Binding), condition)
    let inline (=>>) (fromNode : Node<'a, 'b>) (toNodes: Node<'b, 'c> seq) =
        EdgeType.FanOut(boxFromNode fromNode, toNodes |> Seq.map boxToNode)
    let inline (>>=) (fromNodes : Node<'a, 'b> seq) (toNode: Node<'b, 'c>) =
        EdgeType.FanIn(fromNodes |> Seq.map boxFromNode, boxToNode toNode)

[<AutoOpen; AbstractClass; Sealed>]
type NodeOperations =
    static member GetNode (value : Func<'T1,'T2>, id: string, ?options: ExecutorOptions, ?threadsafe: bool) : Node<'T1, 'T2> =
        Node(value.BindAsExecutor(id, (options |> Option.toObj), (threadsafe |> Option.defaultValue false)))
    static member GetNode (value : Executor<'T1,'T2>) : Node<'T1, 'T2> =
        Node(value.BindExecutor())

type WorkflowBuilderInner<'TFirstIn, 'TFirstOut, 'TOut>(node: Node<'TFirstIn, 'TFirstOut>) =

    let workflow = WorkflowBuilder(node.Binding)

    member inline _.Zero() = ()
    member inline _.Delay(f) = f()
    member _.Yield(edge: EdgeType<'T>) =
        match edge with
        | EdgeType.Direct(from, ``to``) ->
            workflow.AddEdge(from.Binding, ``to``.Binding) |> ignore
        | EdgeType.Conditional(from, ``to``, condition) ->
            workflow.AddEdge(from.Binding, ``to``.Binding, condition) |> ignore
        | EdgeType.FanOut(from, tos) ->
            workflow.AddFanOutEdge(from.Binding, tos |> Seq.map _.Binding) |> ignore
        | EdgeType.FanIn(from, ``to``) ->
            workflow.AddFanInEdge(from |> Seq.map _.Binding, ``to``.Binding) |> ignore
        | EdgeType.Switch(from, switch) ->
            workflow.AddSwitch(from.Binding, switch) |> ignore
    member inline _.Combine((), ()) = ()
    member _.Return(toNode: Node<_, 'TOut>) =
        workflow.WithOutputFrom(toNode.Binding) |> ignore
    member _.Run(()) =
        workflow.Build()

[<AutoOpen>]
module WorkflowTopLevel =
    let Workflow<'TFirstIn, 'TFirstOut, 'TOut> (node: Node<'TFirstIn, 'TFirstOut>) = WorkflowBuilderInner<'TFirstIn, 'TFirstOut, 'TOut>(node)
