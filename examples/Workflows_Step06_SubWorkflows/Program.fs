#nowarn "57"


open System
open System.Threading.Tasks
open Microsoft.Agents.AI.Workflows
open Shared.Logging
open PipedAgents.MAF.Workflows
open FSharp.Control


module BaseLine =

    /// <summary>
    /// Adds a prefix to the input text.
    /// </summary>
    type PrefixExecutor(prefix: string) =
        inherit Executor<string, string>("PrefixExecutor")
        override _.HandleAsync(message, context, cancellationToken) =
            let result = prefix + message
            Console.WriteLine($"[Prefix] '{message}' → '{result}'")
            ValueTask.FromResult(result)

    /// <summary>
    /// Converts input text to uppercase.
    /// </summary>
    type UppercaseExecutor() =
        inherit Executor<string, string>("UppercaseExecutor")
        override _.HandleAsync(message, context, cancellationToken) =
            let result = message.ToUpperInvariant()
            Console.WriteLine($"[Uppercase] '{message}' → '{result}'")
            ValueTask.FromResult(result)

    /// <summary>
    /// Reverses the input text.
    /// </summary>
    type ReverseExecutor() =
        inherit Executor<string, string>("ReverseExecutor")
        override _.HandleAsync(message, context, cancellationToken) =
            let result = message.ToCharArray() |> Array.rev |> String
            Console.WriteLine($"[Reverse] '{message}' → '{result}'")
            ValueTask.FromResult(result)

    /// <summary>
    /// Appends a suffix to the input text.
    /// </summary>
    type AppendSuffixExecutor(suffix: string) =
        inherit Executor<string, string>("AppendSuffixExecutor")
        override _.HandleAsync(message, context, cancellationToken) =
            let result = message + suffix
            Console.WriteLine($"[AppendSuffix] '{message}' → '{result}'")
            ValueTask.FromResult(result)

    /// <summary>
    /// Performs final post-processing by wrapping the text.
    /// </summary>
    type PostProcessExecutor() =
        inherit Executor<string, string>("PostProcessExecutor")
        override _.HandleAsync(message, context, cancellationToken) =
            let result = $"[FINAL] {message} [END]"
            Console.WriteLine($"[PostProcess] '{message}' → '{result}'")
            ValueTask.FromResult(result)

    let run () =
        task {
            Console.WriteLine("\n=== Sub-Workflow Demonstration ===\n")

            // Step 1: Build a simple text processing sub-workflow
            Console.WriteLine("Building sub-workflow: Uppercase → Reverse → Append Suffix...\n")

            let uppercase = UppercaseExecutor().BindExecutor()
            let reverse = ReverseExecutor().BindExecutor()
            let append = AppendSuffixExecutor(" [PROCESSED]").BindExecutor()

            let subWorkflow =
                WorkflowBuilder(uppercase)
                    .AddEdge(uppercase, reverse)
                    .AddEdge(reverse, append)
                    .WithOutputFrom(append)
                    .Build()

            // Step 2: Configure the sub-workflow as an executor for use in the parent workflow
            let subWorkflowExecutor = subWorkflow.BindAsExecutor("TextProcessingSubWorkflow")

            // Step 3: Build a main workflow that uses the sub-workflow as an executor
            Console.WriteLine("Building main workflow that uses the sub-workflow as an executor...\n")

            let prefix = PrefixExecutor("INPUT: ").BindExecutor()
            let postProcess = PostProcessExecutor().BindExecutor()

            let mainWorkflow =
                WorkflowBuilder(prefix)
                    .AddEdge(prefix, subWorkflowExecutor)
                    .AddEdge(subWorkflowExecutor, postProcess)
                    .WithOutputFrom(postProcess)
                    .Build()

            // Step 4: Execute the main workflow
            Console.WriteLine("Executing main workflow with input: 'hello'\n")
            use! run = InProcessExecution.RunAsync(mainWorkflow, "hello")

            // Display results
            for evt in run.NewEvents do
                match evt with
                | :? ExecutorCompletedEvent as executorComplete when executorComplete.Data <> null ->
                    Console.ForegroundColor <- ConsoleColor.Green
                    Console.WriteLine($"[{executorComplete.ExecutorId}] {executorComplete.Data}")
                    Console.ResetColor()
                | :? WorkflowOutputEvent as output ->
                    Console.ForegroundColor <- ConsoleColor.Cyan
                    Console.WriteLine("\n=== Main Workflow Completed ===")
                    Console.WriteLine($"Final Output: {output.Data}")
                    Console.ResetColor()
                | _ -> ()

            // Optional: Visualize the workflow structure - Note that sub-workflows are not rendered
            Console.ForegroundColor <- ConsoleColor.DarkGray
            Console.WriteLine("\n=== Workflow Visualization ===\n")
            Console.WriteLine(mainWorkflow.ToMermaidString())
            Console.ResetColor()

            Console.WriteLine("\n✅ Sample Complete: Workflows can be composed hierarchically using sub-workflows\n")
        } |> _.GetAwaiter().GetResult()

module Target =

    let run () =
        +task {
            Console.WriteLine("\n=== Sub-Workflow Demonstration (Target DSL) ===\n")

            let uppercase = GetNode((fun (m: string) -> m.ToUpperInvariant()), "Uppercase")
            let reverse = GetNode((fun (m: string) -> m.ToCharArray() |> Array.rev |> String), "Reverse")
            let append = GetNode((fun (m: string) -> m + " [PROCESSED]"), "AppendSuffix")

            let subWorkflow =
                workflow(uppercase) {
                    uppercase ==> reverse
                    reverse ==> append
                    return append
                }

            let subWorkflowNode = GetNode(subWorkflow, "TextProcessingSubWorkflow")
            let prefix = GetNode((fun (m: string) -> "INPUT: " + m), "Prefix")
            let postProcess = GetNode((fun (m: string) -> $"[FINAL] {m} [END]"), "PostProcess")

            let mainWorkflow =
                workflow(prefix) {
                    prefix ==> subWorkflowNode
                    subWorkflowNode ==> postProcess
                    return postProcess
                }

            Console.WriteLine("Executing main workflow with input: 'hello'\n")
            use! run = Workflow.Run(mainWorkflow, "hello")

            for evt in run.NewEvents do
                match evt with
                | :? ExecutorCompletedEvent as executorComplete when executorComplete.Data <> null ->
                    Console.ForegroundColor <- ConsoleColor.Green
                    Console.WriteLine($"[{executorComplete.ExecutorId}] {executorComplete.Data}")
                    Console.ResetColor()
                | :? WorkflowOutputEvent as output ->
                    Console.ForegroundColor <- ConsoleColor.Cyan
                    Console.WriteLine("\n=== Main Workflow Completed ===")
                    Console.WriteLine($"Final Output: {output.Data}")
                    Console.ResetColor()
                | _ -> ()
        }

Target.run()
