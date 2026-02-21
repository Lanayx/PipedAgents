#nowarn "57"


open System
open System.Threading
open System.Threading.Tasks
open Microsoft.Agents.AI.Workflows
open Shared.Logging
open PipedAgents.MAF.Workflows
open FSharp.Control


let rec private readIntegerFromConsole (prompt: string) =
    Console.Write(prompt)
    let input = Console.ReadLine()
    match Int32.TryParse(input) with
    | true, value ->
        value
    | _ ->
        Console.WriteLine("Invalid input. Please enter a valid integer.")
        readIntegerFromConsole prompt

module BaseLine =

    /// <summary>
    /// Signals indicating if the guess was too high, too low, or an initial guess.
    /// </summary>
    type NumberSignal =
        | Init = 0
        | Above = 1
        | Below = 2

    /// <summary>
    /// Signals used for communication between guesses and the JudgeExecutor.
    /// </summary>
    type SignalWithNumber(signal: NumberSignal, ?number: int) =
        member val Signal = signal
        member val Number = number

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
                    do! context.SendMessageAsync(SignalWithNumber(NumberSignal.Below, message), cancellationToken = cancellationToken)
                else
                    do! context.SendMessageAsync(SignalWithNumber(NumberSignal.Above, message), cancellationToken = cancellationToken)
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
            let numberRequest = RequestPort.Create<SignalWithNumber, int>("GuessNumber").BindAsExecutor()
            let judgeExecutor = JudgeExecutor(42).BindExecutor()

            WorkflowBuilder(numberRequest)
                .AddEdge(numberRequest, judgeExecutor)
                .AddEdge(judgeExecutor, numberRequest)
                .WithOutputFrom(judgeExecutor)
                .Build()

    let handleExternalRequest (request: ExternalRequest) =
        let signal = request.Data.As<SignalWithNumber>()
        if box signal <> null then
            match signal.Signal with
            | NumberSignal.Init ->
                let initialGuess = readIntegerFromConsole "Please provide your initial guess: "
                request.CreateResponse(initialGuess)
            | NumberSignal.Above ->
                let numberStr = signal.Number |> Option.map string |> Option.defaultValue "unknown"
                let lowerGuess = readIntegerFromConsole $"You previously guessed {numberStr} too large. Please provide a new guess: "
                request.CreateResponse(lowerGuess)
            | NumberSignal.Below ->
                let numberStr = signal.Number |> Option.map string |> Option.defaultValue "unknown"
                let higherGuess = readIntegerFromConsole $"You previously guessed {numberStr} too small. Please provide a new guess: "
                request.CreateResponse(higherGuess)
            | _ -> failwith $"Signal {signal.Signal} is not supported"
        else
            failwith $"Request {request.PortInfo.RequestType} is not supported"

    let run () =
        task {
            // Create the workflow
            let workflow = WorkflowFactory.buildWorkflow()

            // Create checkpoint manager
            let checkpointManager = CheckpointManager.Default
            let checkpoints = System.Collections.Generic.List<CheckpointInfo>()

            // Execute the workflow and save checkpoints
            use! checkpointedRun = InProcessExecution.RunStreamingAsync(workflow, SignalWithNumber(NumberSignal.Init), checkpointManager)

            for evt in checkpointedRun.WatchStreamAsync() do
                match evt with
                | :? RequestInfoEvent as requestInputEvt ->
                    let response = handleExternalRequest requestInputEvt.Request
                    do! checkpointedRun.SendResponseAsync(response)
                | :? ExecutorCompletedEvent as executorCompletedEvt ->
                    Console.WriteLine($"* Executor {executorCompletedEvt.ExecutorId} completed.")
                | :? SuperStepCompletedEvent as superStepCompletedEvt ->
                    let checkpoint = superStepCompletedEvt.CompletionInfo.Checkpoint
                    if not (isNull checkpoint) then
                        checkpoints.Add(checkpoint)
                        Console.WriteLine($"** Checkpoint created at step {checkpoints.Count}.")
                | :? WorkflowOutputEvent as workflowOutputEvt ->
                    Console.WriteLine($"Workflow completed with result: {workflowOutputEvt.Data}")
                | _ -> ()


            if checkpoints.Count = 0 then
                failwith "No checkpoints were created during the workflow execution."
            Console.WriteLine($"Number of checkpoints created: {checkpoints.Count}")

            // Restoring from a checkpoint and resuming execution
            let checkpointIndex = 1
            Console.WriteLine($"\n\nRestoring from the {checkpointIndex + 1}th checkpoint.")
            let savedCheckpoint = checkpoints[checkpointIndex]
            // Note that we are restoring the state directly to the same run instance.
            do! checkpointedRun.RestoreCheckpointAsync(savedCheckpoint, CancellationToken.None)

            for evt in checkpointedRun.WatchStreamAsync() do
                match evt with
                | :? RequestInfoEvent as requestInputEvt ->
                    // Handle `RequestInfoEvent` from the workflow
                    let response = handleExternalRequest requestInputEvt.Request
                    do! checkpointedRun.SendResponseAsync(response)
                | :? ExecutorCompletedEvent as executorCompletedEvt ->
                    Console.WriteLine($"* Executor {executorCompletedEvt.ExecutorId} completed.")
                | :? WorkflowOutputEvent as workflowOutputEvt ->
                    Console.WriteLine($"Workflow completed with result: {workflowOutputEvt.Data}")
                | _ -> ()

        } |> _.GetAwaiter().GetResult()

BaseLine.run()


module Target =

    type NumberSignal =
        | Above of int
        | Below of int

    type ExecutorResponse =
        | Init
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
                    return Hint(NumberSignal.Below message).Wrap()
                else
                    return Hint(NumberSignal.Above message).Wrap()
            } |> ValueTask<ExecutorResponseWrap>
        override this.OnCheckpointingAsync(context: IWorkflowContext, cancellationToken: CancellationToken) =
            context.QueueStateUpdateAsync(stateKey, tries, cancellationToken = cancellationToken)
        override this.OnCheckpointRestoredAsync(context: IWorkflowContext, cancellationToken: CancellationToken) =
            task {
                let! t = context.ReadStateAsync<int>(stateKey, cancellationToken = cancellationToken)
                tries <- t
            } |> ValueTask

    let private handleExternalRequest (request: ExternalRequest) =
        if request.Data.Is<ExecutorResponseWrap>() then
            match request.Data.As<ExecutorResponseWrap>().Response with
            | ExecutorResponse.Init ->
                "Please provide your initial guess: "
            | ExecutorResponse.Hint (NumberSignal.Above number) ->
                $"You previously guessed {number} too large. Please provide a new guess: "
            | ExecutorResponse.Hint (NumberSignal.Below number) ->
                $"You previously guessed {number} too small. Please provide a new guess: "
            | ExecutorResponse.Found _ ->
                raise <| NotSupportedException $"Request {request.PortInfo.RequestType} is not supported"
            |> readIntegerFromConsole
            |> request.CreateResponse
        else
            raise <| NotSupportedException $"Request {request.PortInfo.RequestType} is not supported"

    let run () =
        let guessNumberNode = RequestPort.Create<ExecutorResponseWrap, int>("GuessNumber") |> GetNode
        let judgeNode = JudgeExecutor(42) |> GetNode
        let outputNode = GetNode((function
            | { Response = Found result } -> result
            | _ -> failwith "Unexpected response"), "Output")

        let mainWorkflow =
            workflow(guessNumberNode) {
                guessNumberNode ==> judgeNode
                judgeNode =|> [
                    case _.Response.IsHint guessNumberNode
                    defaultCase outputNode
                ]
                return outputNode
            }

        +task {
            use! checkpointedRun = Workflow.CheckpointStream(mainWorkflow, Init.Wrap(), CheckpointManager.Default)
            let checkpoints = ResizeArray<CheckpointInfo>()

            for evt in checkpointedRun.WatchStreamAsync() do
                match evt with
                | :? RequestInfoEvent as requestInputEvt ->
                    let response = handleExternalRequest requestInputEvt.Request
                    do! checkpointedRun.SendResponseAsync(response)
                | :? ExecutorCompletedEvent as executorCompletedEvt ->
                    Console.WriteLine($"* Executor {executorCompletedEvt.ExecutorId} completed.")
                | :? SuperStepCompletedEvent as superStepCompletedEvt ->
                    let checkpoint = superStepCompletedEvt.CompletionInfo.Checkpoint
                    if not (isNull checkpoint) then
                        checkpoints.Add(checkpoint)
                        Console.WriteLine($"** Checkpoint created at step {checkpoints.Count}.")
                | :? WorkflowOutputEvent as workflowOutputEvt ->
                    Console.WriteLine($"Workflow completed with result: {workflowOutputEvt.Data}")
                | _ -> ()

            if checkpoints.Count = 0 then
                failwith "No checkpoints were created during the workflow execution."
            Console.WriteLine($"Number of checkpoints created: {checkpoints.Count}")

            // Restoring from a checkpoint and resuming execution
            let checkpointIndex = 1
            Console.WriteLine($"\n\nRestoring from the {checkpointIndex + 1}th checkpoint.")
            let savedCheckpoint = checkpoints[checkpointIndex]
            // Note that we are restoring the state directly to the same run instance.
            do! checkpointedRun.RestoreCheckpointAsync(savedCheckpoint, CancellationToken.None)

            for evt in checkpointedRun.WatchStreamAsync() do
                match evt with
                | :? RequestInfoEvent as requestInputEvt ->
                    // Handle `RequestInfoEvent` from the workflow
                    let response = handleExternalRequest requestInputEvt.Request
                    do! checkpointedRun.SendResponseAsync(response)
                | :? ExecutorCompletedEvent as executorCompletedEvt ->
                    Console.WriteLine($"* Executor {executorCompletedEvt.ExecutorId} completed.")
                | :? WorkflowOutputEvent as workflowOutputEvt ->
                    Console.WriteLine($"Workflow completed with result: {workflowOutputEvt.Data}")
                | _ -> ()
        }

BaseLine.run()