namespace PipedAgents.MAF.Workflows

open System
open Microsoft.Agents.AI.Workflows

type Node<'TIn, 'TOut>(binding: ExecutorBinding) =
    member _.Binding = binding

[<RequireQualifiedAccess>]
type EdgeType<'T> =
    | Direct of fromNode:Node<obj, 'T> * toNode:Node<'T, obj>
    | Conditional of fromNode:Node<obj, 'T> * toNode:Node<'T, obj> * condition:('T -> bool)
    | Switch of fromNode:Node<obj, 'T> * switch:Action<SwitchBuilder>
    | FanOut of fromNode:Node<obj, 'T> * toNode:Node<'T, obj> seq
    | FanOutCond of fromNode:Node<obj, 'T> * toNode:Node<'T, obj> seq * condition:Func<'T,int,int seq>
    | FanIn of fromNode:Node<obj, 'T> seq * toNode:Node<'T, obj>

[<RequireQualifiedAccess>]
type CaseType<'T> =
    | Case of condition:('T -> bool) * executor:Node<'T, obj>
    | Default of Node<'T, obj>

[<AutoOpen>]
module NodeOperators =
    let inline boxIn (n: Node<'a, 'b>) = Node<obj, 'b>(n.Binding)
    let inline boxOut (n: Node<'a, 'b>) = Node<'a, obj>(n.Binding)
    let inline defaultCase (value : Node<'T1,'T2>) =
        CaseType.Default (boxOut value)
    let inline case (condition: 'T1 -> bool) (value : Node<'T1,'T2>) =
        CaseType.Case (condition, boxOut value)
    let inline private switchInner (cases: CaseType<'a> seq) =
        fun (switchBuilder: SwitchBuilder) ->
            for c in cases do
                match c with
                | CaseType.Case (condition, executor) ->
                    switchBuilder.AddCase(condition, [| executor.Binding |]) |> ignore
                | CaseType.Default executor ->
                    switchBuilder.WithDefault([| executor.Binding  |]) |> ignore
    /// Creates a switch edge from a node to multiple case nodes.
    let inline (=|>) (fromNode : Node<'a, 'b>) (cases: CaseType<'b> seq) =
        EdgeType.Switch(boxIn fromNode, switchInner cases)
    /// Creates a direct edge from one node to another.
    let inline (==>) (fromNode : Node<'a, 'b>) (toNode: Node<'b, 'c>) =
        EdgeType.Direct(Node<obj, 'b>(fromNode.Binding), Node<'b, obj>(toNode.Binding))
    /// Creates a conditional edge from one node to another based on a condition.
    let inline (=?>) (fromNode : Node<'a, 'b>) (toNode: Node<'b, 'c>, condition: 'b -> bool) =
        EdgeType.Conditional(Node<obj, 'b>(fromNode.Binding), Node<'b, obj>(toNode.Binding), condition)
    /// Creates a fan-out edge from one node to multiple nodes.
    let inline (=>>) (fromNode : Node<'a, 'b>) (toNodes: Node<'b, 'c> seq) =
        EdgeType.FanOut(boxIn fromNode, toNodes |> Seq.map boxOut)
    /// Creates a fan-out edge from one node to multiple nodes based on a assigner. (value -> target count -> target indices)
    let inline (=?>>) (fromNode : Node<'a, 'b>) (toNodes: Node<'b, 'c> seq, assigner: 'b -> int -> int seq) =
        EdgeType.FanOutCond(boxIn fromNode, toNodes |> Seq.map boxOut, assigner)
    /// Creates a fan-in edge from multiple nodes to one node.
    let inline (>>=) (fromNodes : Node<'a, 'b> seq) (toNode: Node<'b, 'c>) =
        EdgeType.FanIn(fromNodes |> Seq.map boxIn, boxOut toNode)

[<AutoOpen; AbstractClass; Sealed>]
type NodeOperations =
    static member GetNode (value : Func<'T1,'T2>, id: string, ?options: ExecutorOptions, ?threadsafe: bool) : Node<'T1, 'T2> =
        Node(value.BindAsExecutor(id, (options |> Option.toObj), (threadsafe |> Option.defaultValue false)))
    static member GetNode (value : Executor<'T1,'T2>) : Node<'T1, 'T2> =
        Node(value.BindExecutor())
    static member GetNode (value : RequestPort<'T1,'T2>) : Node<'T1, 'T2> =
        Node(value.BindAsExecutor())


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
        | EdgeType.FanOutCond(from, tos, condition) ->
            workflow.AddFanOutEdge(from.Binding, tos |> Seq.map _.Binding, condition) |> ignore
        | EdgeType.FanIn(from, ``to``) ->
            workflow.AddFanInEdge(from |> Seq.map _.Binding, ``to``.Binding) |> ignore
        | EdgeType.Switch(from, switch) ->
            workflow.AddSwitch(from.Binding, switch) |> ignore
    member inline _.Combine((), ()) = ()
    member _.Return(toNode: Node<_, 'TOut>) =
        workflow.WithOutputFrom(toNode.Binding) |> ignore
    member _.Return(toNodes: Node<_, 'TOut> seq) =
        workflow.WithOutputFrom(toNodes |> Seq.map _.Binding |> Seq.toArray) |> ignore
    member _.Run(()) =
        workflow.Build()

[<AutoOpen>]
module WorkflowTopLevel =
    let Workflow<'TFirstIn, 'TFirstOut, 'TOut> (node: Node<'TFirstIn, 'TFirstOut>) = WorkflowBuilderInner<'TFirstIn, 'TFirstOut, 'TOut>(node)
