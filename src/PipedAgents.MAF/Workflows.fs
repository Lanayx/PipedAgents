namespace PipedAgents.MAF.Workflows

open System
open Microsoft.Agents.AI.Workflows

type Node<'TIn, 'TOut>(binding: ExecutorBinding) =
    member _.Value = binding
type TypedWorkflow<'TIn, 'TOut>(binding: Workflow) =
    member _.Value = binding

[<RequireQualifiedAccess>]
type CaseType<'T> =
    | Case of condition:('T -> bool) * executor:Node<'T, obj>
    | Default of Node<'T, obj>

[<RequireQualifiedAccess>]
type EdgeType<'T> =
    | Direct of fromNode:Node<obj, 'T> * toNode:Node<'T, obj>
    | Conditional of fromNode:Node<obj, 'T> * toNode:Node<'T, obj> * condition:('T -> bool)
    | Switch of fromNode:Node<obj, 'T> * cases:CaseType<'T> seq
    | FanOut of fromNode:Node<obj, 'T> * toNode:Node<'T, obj> seq
    | FanOutCond of fromNode:Node<obj, 'T> * toNode:Node<'T, obj> seq * condition:Func<'T,int,int seq>
    | FanIn of fromNode:Node<obj, 'T> seq * toNode:Node<'T, obj>

[<AutoOpen>]
module NodeOperators =
    let inline boxIn (n: Node<'a, 'b>) = Node<obj, 'b>(n.Value)
    let inline boxOut (n: Node<'a, 'b>) = Node<'a, obj>(n.Value)
    let inline defaultCase (value : Node<'T1,'T2>) =
        CaseType.Default (boxOut value)
    let inline case (condition: 'T1 -> bool) (value : Node<'T1,'T2>) =
        CaseType.Case (condition, boxOut value)
    /// Creates a switch edge from a node to multiple case nodes.
    let inline (=|>) (fromNode : Node<'a, 'b>) (cases: CaseType<'b> seq) =
        EdgeType.Switch(boxIn fromNode, cases)
    /// Creates a direct edge from one node to another.
    let inline (==>) (fromNode : Node<'a, 'b>) (toNode: Node<'b, 'c>) =
        EdgeType.Direct(Node<obj, 'b>(fromNode.Value), Node<'b, obj>(toNode.Value))
    /// Creates a conditional edge from one node to another based on a condition.
    let inline (=?>) (fromNode : Node<'a, 'b>) (toNode: Node<'b, 'c>, condition: 'b -> bool) =
        EdgeType.Conditional(Node<obj, 'b>(fromNode.Value), Node<'b, obj>(toNode.Value), condition)
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
    static member GetNode (value : 'T1 -> 'T2, id: string, ?options: ExecutorOptions, ?threadsafe: bool) : Node<'T1, 'T2> =
        Node(Func<'T1, 'T2>(value).BindAsExecutor(id, (options |> Option.toObj), (threadsafe |> Option.defaultValue false)))
    static member GetNode (value : Executor<'T1,'T2>) : Node<'T1, 'T2> =
        Node(value.BindExecutor())
    static member GetNode (value : RequestPort<'T1,'T2>) : Node<'T1, 'T2> =
        Node(value.BindAsExecutor())
    static member GetNode (value : TypedWorkflow<'T1,'T2>, id: string, ?options: ExecutorOptions) : Node<'T1, 'T2> =
        Node(value.Value.BindAsExecutor(id, (options |> Option.toObj)))

type workflow<'TIn, 'TFirstOut, 'TOut>(node: Node<'TIn, 'TFirstOut>) =

    let workflow = WorkflowBuilder(node.Value)

    member internal this.ReduceToFanOut(source: ExecutorBinding,
                                        cases:CaseType<'T> seq) =

        let allExecutors = ResizeArray()
        let regularCases = ResizeArray()
        let defaultCases = ResizeArray(1)
        let mutable i = 0
        for case in cases do
            match case with
            | CaseType.Case (condition, executor) ->
                allExecutors.Add(executor.Value)
                regularCases.Add(condition, i)
            | CaseType.Default executor ->
                allExecutors.Add(executor.Value)
                defaultCases.Add(i)
            i <- i + 1
        if defaultCases.Count = 0 then
            failwith "Switch edge must have a default case."

        let casePartitioner (input: 'T) (targetCount: int) =
            regularCases
            |> Seq.tryPick (fun (condition, index) -> if condition input then Some index else None)
            |> Option.map Seq.singleton
            |> Option.defaultValue defaultCases

        workflow.AddFanOutEdge(source,  allExecutors, casePartitioner)

    member inline _.Zero() = ()
    member inline _.Delay(f) = f()
    member this.Yield(edge: EdgeType<'T>) =
        match edge with
        | EdgeType.Direct(from, ``to``) ->
            workflow.AddEdge(from.Value, ``to``.Value) |> ignore
        | EdgeType.Conditional(from, ``to``, condition) ->
            workflow.AddEdge(from.Value, ``to``.Value, condition) |> ignore
        | EdgeType.FanOut(from, tos) ->
            workflow.AddFanOutEdge(from.Value, tos |> Seq.map _.Value) |> ignore
        | EdgeType.FanOutCond(from, tos, condition) ->
            workflow.AddFanOutEdge(from.Value, tos |> Seq.map _.Value, condition) |> ignore
        | EdgeType.FanIn(from, ``to``) ->
            workflow.AddFanInEdge(from |> Seq.map _.Value, ``to``.Value) |> ignore
        | EdgeType.Switch(from, switch) ->
            this.ReduceToFanOut(from.Value, switch) |> ignore
    member inline _.Combine((), ()) = ()
    member _.Return(toNode: Node<_, 'TOut>) =
        workflow.WithOutputFrom(toNode.Value) |> ignore
    member _.Return(toNodes: Node<_, 'TOut> seq) =
        workflow.WithOutputFrom(toNodes |> Seq.map _.Value |> Seq.toArray) |> ignore
    member _.Run(()) =
        workflow.Build() |> TypedWorkflow<'TIn, 'TOut>

type Workflow =
    static member Run<'TIn, 'TOut>(workflow: TypedWorkflow<'TIn, 'TOut>, input: 'TIn, ?runId: string, ?ct: Threading.CancellationToken) =
        InProcessExecution.RunAsync(workflow.Value, input, runId = (runId |> Option.toObj), ?cancellationToken = ct)
    static member Stream<'TIn, 'TOut>(workflow: TypedWorkflow<'TIn, 'TOut>, input: 'TIn, ?runId: string, ?ct: Threading.CancellationToken) =
        InProcessExecution.StreamAsync(workflow.Value, input, runId = (runId |> Option.toObj), ?cancellationToken = ct)