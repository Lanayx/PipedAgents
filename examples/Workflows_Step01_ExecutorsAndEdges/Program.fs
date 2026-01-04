#nowarn "57"


open System
open System.Threading.Tasks
open Microsoft.Agents.AI.Workflows
open Shared
open PipedAgents.MAF

module BaseLine =

    type ReverseTextExecutor() =
        inherit Executor<string, string>("ReverseTextExecutor")
        override this.HandleAsync(message: string, context, cancellationToken) =
            ValueTask.FromResult(String.Concat(message |> Seq.rev))

    let run() =
        let uppercaseFunc = Func<string, string>(fun (s: string) -> s.ToUpperInvariant())
        let uppercaseExecutor = uppercaseFunc.BindAsExecutor("UppercaseExecutor")
        let reverseExecutor = ReverseTextExecutor().BindExecutor()
        let workflow =
            WorkflowBuilder(uppercaseExecutor)
                .AddEdge(uppercaseExecutor, reverseExecutor)
                .WithOutputFrom(reverseExecutor)
                .Build()
        +task {
            use! run = InProcessExecution.RunAsync(workflow, "Hello, World!")
            for evt in run.NewEvents do
                match evt with
                | :? ExecutorCompletedEvent as executorComplete ->
                    Console.WriteLine($"{executorComplete.ExecutorId}: {executorComplete.Data}")
                | _ -> ()
        }


module Target =
    type ReverseTextNode() =
        inherit Executor<string, string>("ReverseTextNode")
        override this.HandleAsync(message: string, context, cancellationToken) =
            ValueTask.FromResult(String.Concat(message |> Seq.rev))

    let run() =
        let uppercaseFunc (s: string) = s.ToUpperInvariant()
        let uppercaseNode = GetNode(uppercaseFunc, "UppercaseNode")
        let reverseTextNode = GetNode(ReverseTextNode())
        let workflow =
            Workflow(uppercaseNode) {
                uppercaseNode ==> reverseTextNode
                return reverseTextNode
            }
        +task {
            use! run = InProcessExecution.RunAsync(workflow, "Hello, World!")
            for evt in run.NewEvents do
                match evt with
                | :? ExecutorCompletedEvent as executorComplete ->
                    Console.WriteLine($"{executorComplete.ExecutorId}: {executorComplete.Data}")
                | _ -> ()
        }


Target.run()
