#nowarn "57"


open System
open System.Threading
open System.Threading.Tasks
open Microsoft.Agents.AI.Workflows
open Shared.Logging
open PipedAgents.MAF.Workflows
open FSharp.Control


module BaseLine =

    /// <summary>
    /// Signals used for communication between GuessNumberExecutor and JudgeExecutor.
    /// </summary>
    type NumberSignal =
        | Init = 0
        | Above = 1
        | Below = 2

    /// <summary>
    /// Executor that makes a guess based on the current bounds.
    /// </summary>
    type GuessNumberExecutor(lowerBound: int, upperBound: int) =
        inherit Executor<NumberSignal>("Guess")

        let mutable lowerBound = lowerBound
        let mutable upperBound = upperBound
        let stateKey = "GuessNumberExecutorState"

        member private this.NextGuess = (lowerBound + upperBound) / 2

        override this.HandleAsync(message: NumberSignal, context: IWorkflowContext, cancellationToken: CancellationToken) =
            task {
                match message with
                | NumberSignal.Init ->
                    do! context.SendMessageAsync(this.NextGuess, cancellationToken = cancellationToken)
                | NumberSignal.Above ->
                    upperBound <- this.NextGuess - 1
                    do! context.SendMessageAsync(this.NextGuess, cancellationToken = cancellationToken)
                | NumberSignal.Below ->
                    lowerBound <- this.NextGuess + 1
                    do! context.SendMessageAsync(this.NextGuess, cancellationToken = cancellationToken)
                | _ -> ()
            } |> ValueTask

        /// <summary>
        /// Checkpoint the current state of the executor.
        /// </summary>
        override this.OnCheckpointingAsync(context: IWorkflowContext, cancellationToken: CancellationToken) =
            context.QueueStateUpdateAsync(stateKey, (lowerBound, upperBound), cancellationToken = cancellationToken)

        /// <summary>
        /// Restore the state of the executor from a checkpoint.
        /// </summary>
        override this.OnCheckpointRestoredAsync(context: IWorkflowContext, cancellationToken: CancellationToken) =
            task {
                let! lb, ub = context.ReadStateAsync<int * int>(stateKey, cancellationToken = cancellationToken)
                lowerBound <- lb
                upperBound <- ub
            } |> ValueTask

    /// <summary>
    /// Executor that judges the guess and provides feedback.
    /// </summary>
    type JudgeExecutor(targetNumber: int) =
        inherit Executor<int>("Judge")

        let mutable tries = 0
        let stateKey = "JudgeExecutorState"

        override this.HandleAsync(message: int, context: IWorkflowContext, cancellationToken: CancellationToken) =
            task {
                tries <- tries + 1
                if message = targetNumber then
                    do! context.YieldOutputAsync($"{targetNumber} found in {tries} tries!", cancellationToken)
                elif message < targetNumber then
                    do! context.SendMessageAsync(NumberSignal.Below, cancellationToken = cancellationToken)
                else
                    do! context.SendMessageAsync(NumberSignal.Above, cancellationToken = cancellationToken)
            } |> ValueTask

        /// <summary>
        /// Checkpoint the current state of the executor.
        /// </summary>
        override this.OnCheckpointingAsync(context: IWorkflowContext, cancellationToken: CancellationToken) =
            context.QueueStateUpdateAsync(stateKey, tries, cancellationToken = cancellationToken)

        /// <summary>
        /// Restore the state of the executor from a checkpoint.
        /// </summary>
        override this.OnCheckpointRestoredAsync(context: IWorkflowContext, cancellationToken: CancellationToken) =
            task {
                let! t = context.ReadStateAsync<int>(stateKey, cancellationToken = cancellationToken)
                tries <- t
            } |> ValueTask

    module WorkflowFactory =
        let buildWorkflow () =
            let guessNumberExecutor = GuessNumberExecutor(1, 100).BindExecutor()
            let judgeExecutor = JudgeExecutor(42).BindExecutor()

            WorkflowBuilder(guessNumberExecutor)
                .AddEdge(guessNumberExecutor, judgeExecutor)
                .AddEdge(judgeExecutor, guessNumberExecutor)
                .WithOutputFrom(judgeExecutor)
                .Build()

    let run () =
        task {
            // Create the workflow
            let workflow = WorkflowFactory.buildWorkflow()

            // Create checkpoint manager
            let checkpointManager = CheckpointManager.Default
            let checkpoints = System.Collections.Generic.List<CheckpointInfo>()

            // Execute the workflow and save checkpoints
            use! checkpointedRun = InProcessExecution.StreamAsync(workflow, NumberSignal.Init, checkpointManager)
            
            do! checkpointedRun.Run.WatchStreamAsync()
                |> TaskSeq.iter (fun evt ->
                    match evt with
                    | :? ExecutorCompletedEvent as executorCompletedEvt ->
                        Console.WriteLine($"* Executor {executorCompletedEvt.ExecutorId} completed.")
                    | :? SuperStepCompletedEvent as superStepCompletedEvt ->
                        let checkpoint = superStepCompletedEvt.CompletionInfo.Checkpoint
                        if not (isNull checkpoint) then
                            checkpoints.Add(checkpoint)
                            Console.WriteLine($"** Checkpoint created at step {checkpoints.Count}.")
                    | :? WorkflowOutputEvent as workflowOutputEvt ->
                        Console.WriteLine($"Workflow completed with result: {workflowOutputEvt.Data}")
                    | _ -> ())

            if checkpoints.Count = 0 then
                failwith "No checkpoints were created during the workflow execution."
            
            Console.WriteLine($"Number of checkpoints created: {checkpoints.Count}")

            // Rehydrate a new workflow instance from a saved checkpoint and continue execution
            let newWorkflow = WorkflowFactory.buildWorkflow()
            let checkpointIndex = 5
            Console.WriteLine($"\n\nHydrating a new workflow instance from the {checkpointIndex + 1}th checkpoint.")
            let savedCheckpoint = checkpoints[checkpointIndex]
            
            use! newCheckpointedRun = InProcessExecution.ResumeStreamAsync(newWorkflow, savedCheckpoint, checkpointManager, checkpointedRun.Run.RunId)
            
            do! newCheckpointedRun.Run.WatchStreamAsync()
                |> TaskSeq.iter (fun evt ->
                    match evt with
                    | :? ExecutorCompletedEvent as executorCompletedEvt ->
                        Console.WriteLine($"* Executor {executorCompletedEvt.ExecutorId} completed.")
                    | :? WorkflowOutputEvent as workflowOutputEvt ->
                        Console.WriteLine($"Workflow completed with result: {workflowOutputEvt.Data}")
                    | _ -> ())
        } |> _.GetAwaiter().GetResult()

module Target =

    type NumberSignal =
        | Init
        | Above
        | Below

    type ExecutorResponse =
        | Hint of NumberSignal
        | Found of string
        member this.Wrap() =
            { Response = this }
    and ExecutorResponseWrap = {
        Response: ExecutorResponse
    }

    type JudgeExecutor(targetNumber: int) =
        inherit Executor<int, ExecutorResponseWrap>("Judge")
        let mutable tries = 0
        let stateKey = "JudgeExecutorState"

        override this.HandleAsync(message: int, context: IWorkflowContext, cancellationToken: CancellationToken) =
            task {
                tries <- tries + 1
                if message = targetNumber then
                    return Found($"{targetNumber} found in {tries} tries!").Wrap()
                elif message < targetNumber then
                    return Hint(NumberSignal.Below).Wrap()
                else
                    return Hint(NumberSignal.Above).Wrap()
            } |> ValueTask<ExecutorResponseWrap>
        override this.OnCheckpointingAsync(context: IWorkflowContext, cancellationToken: CancellationToken) =
            context.QueueStateUpdateAsync(stateKey, tries, cancellationToken = cancellationToken)
        override this.OnCheckpointRestoredAsync(context: IWorkflowContext, cancellationToken: CancellationToken) =
            task {
                let! t = context.ReadStateAsync<int>(stateKey, cancellationToken = cancellationToken)
                tries <- t
            } |> ValueTask

    type GuessNumberExecutor(lowerBound: int, upperBound: int) =
        inherit Executor<ExecutorResponseWrap, int>("Guess")

        let mutable lowerBound = lowerBound
        let mutable upperBound = upperBound
        let stateKey = "GuessNumberExecutorState"

        member private this.NextGuess = (lowerBound + upperBound) / 2

        override this.HandleAsync(message: ExecutorResponseWrap, context: IWorkflowContext, cancellationToken: CancellationToken) =
            task {
                match message.Response with
                | Hint NumberSignal.Init ->
                    return this.NextGuess
                | Hint NumberSignal.Above ->
                    upperBound <- this.NextGuess - 1
                    return this.NextGuess
                | Hint NumberSignal.Below ->
                    lowerBound <- this.NextGuess + 1
                    return this.NextGuess
                | _ -> return failwith "Unexpected response"
            } |> ValueTask<int>

        override this.OnCheckpointingAsync(context: IWorkflowContext, cancellationToken: CancellationToken) =
            context.QueueStateUpdateAsync(stateKey, (lowerBound, upperBound), cancellationToken = cancellationToken)
        override this.OnCheckpointRestoredAsync(context: IWorkflowContext, cancellationToken: CancellationToken) =
            task {
                let! lb, ub = context.ReadStateAsync<int * int>(stateKey, cancellationToken = cancellationToken)
                lowerBound <- lb
                upperBound <- ub
            } |> ValueTask

    let run () =

        let getWorkflow() =
            let guessNumberNode = GuessNumberExecutor(1, 100) |> GetNode
            let judgeNode = JudgeExecutor(42) |> GetNode
            let outputNode = GetNode((function
                | { Response = Found result } -> result
                | _ -> failwith "Unexpected response"), "Output")

            workflow(guessNumberNode) {
                guessNumberNode ==> judgeNode
                judgeNode =|> [
                    case _.Response.IsHint guessNumberNode
                    defaultCase outputNode
                ]
                return outputNode
            }

        +task {
            use! stream = Workflow.CheckpointStream(getWorkflow(), Hint(Init).Wrap(), CheckpointManager.Default)
            let checkpoints = ResizeArray<CheckpointInfo>()
            for event in stream.Run.WatchStreamAsync() do
                match event with
                | :? ExecutorCompletedEvent as executorCompletedEvt ->
                    Console.WriteLine($"* Executor {executorCompletedEvt.ExecutorId} completed.")
                | :? SuperStepCompletedEvent as superStepCompletedEvt ->
                    let checkpoint = superStepCompletedEvt.CompletionInfo.Checkpoint
                    if not (isNull checkpoint) then
                        checkpoints.Add(checkpoint)
                        Console.WriteLine($"** Checkpoint created at step {checkpoints.Count}.")
                | :? WorkflowOutputEvent as workflowOutputEvt ->
                    Console.WriteLine($"Workflow completed with result: {workflowOutputEvt.Data}")
                | _ ->
                    ()

            let checkpointIndex = 5
            Console.WriteLine($"\n\nHydrating a new workflow instance from the {checkpointIndex + 1}th checkpoint.")
            let savedCheckpoint = checkpoints[checkpointIndex]
            use! newStream = Workflow.ResumeStream(getWorkflow(), savedCheckpoint, CheckpointManager.Default, stream.Run.RunId)

            do! newStream.Run.WatchStreamAsync()
                |> TaskSeq.iter (function
                    | :? ExecutorCompletedEvent as executorCompletedEvt ->
                        Console.WriteLine($"* Executor {executorCompletedEvt.ExecutorId} completed.")
                    | :? WorkflowOutputEvent as workflowOutputEvt ->
                        Console.WriteLine($"Workflow completed with result: {workflowOutputEvt.Data}")
                    | _ -> ())
        }

Target.run()
