#nowarn "57"


open System
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

    type NumberSignal =
        | Init
        | Above
        | Below

    type JudgeExecutor(targetNumber: int) =
        inherit Executor<int>("Judge")
        let mutable tries = 0
        let targetNumber = targetNumber

        override this.HandleAsync(message: int, context: IWorkflowContext, cancellationToken: System.Threading.CancellationToken) =
            task {
                tries <- tries + 1
                if message = targetNumber then
                    do! context.YieldOutputAsync($"{targetNumber} found in {tries} tries!", cancellationToken)
                elif message < targetNumber then
                    do! context.SendMessageAsync(NumberSignal.Below, cancellationToken = cancellationToken)
                else
                    do! context.SendMessageAsync(NumberSignal.Above, cancellationToken = cancellationToken)
            } |> ValueTask

    let private handleExternalRequest (request: ExternalRequest) =
        if request.DataIs<NumberSignal>() then
            match request.DataAs<NumberSignal>() with
            | NumberSignal.Init ->
                let initialGuess = readIntegerFromConsole "Please provide your initial guess: "
                request.CreateResponse(initialGuess)
            | NumberSignal.Above ->
                let lowerGuess = readIntegerFromConsole "You previously guessed too large. Please provide a new guess: "
                request.CreateResponse(lowerGuess)
            | NumberSignal.Below ->
                let higherGuess = readIntegerFromConsole "You previously guessed too small. Please provide a new guess: "
                request.CreateResponse(higherGuess)
        else
            raise (NotSupportedException $"Request {request.PortInfo.RequestType} is not supported")

    let run () =
        let numberRequestPort = RequestPort.Create<NumberSignal, int>("GuessNumber").BindAsExecutor()
        let judgeExecutor = JudgeExecutor(42).BindExecutor()

        let workflow =
            WorkflowBuilder(numberRequestPort)
                .AddEdge(numberRequestPort, judgeExecutor)
                .AddEdge(judgeExecutor, numberRequestPort)
                .WithOutputFrom(judgeExecutor)
                .Build()

        +task {
            use! handle = InProcessExecution.StreamAsync(workflow, NumberSignal.Init)
            use enumerator = handle.WatchStreamAsync().GetAsyncEnumerator()
            while! enumerator.MoveNextAsync() do
                match enumerator.Current with
                | :? RequestInfoEvent as requestInputEvt ->
                    let response = handleExternalRequest requestInputEvt.Request
                    do! handle.SendResponseAsync(response)
                | :? WorkflowOutputEvent as outputEvt ->
                    Console.WriteLine($"Workflow completed with result: {outputEvt.Data}")
                | _ ->
                    ()
        }

module Target =

    type NumberSignal =
        | Above
        | Below

    type ExecutorResponse =
        | Init
        | Hint of NumberSignal
        | Found of string

    let judgeExecutor targetNumber =
        let mutable tries = 0
        fun message ->
            tries <- tries + 1
            if message = targetNumber then
                Found($"{targetNumber} found in {tries} tries!")
            elif message < targetNumber then
                Hint(NumberSignal.Below)
            else
                Hint(NumberSignal.Above)

    let private handleExternalRequest (request: ExternalRequest) =
        if request.DataIs<ExecutorResponse>() then
            match request.DataAs<ExecutorResponse>() with
            | ExecutorResponse.Init -> "Please provide your initial guess: "
            | ExecutorResponse.Hint NumberSignal.Above -> "You previously guessed too large. Please provide a new guess: "
            | ExecutorResponse.Hint NumberSignal.Below -> "You previously guessed too small. Please provide a new guess: "
            | ExecutorResponse.Found _ -> raise <| NotSupportedException $"Request {request.PortInfo.RequestType} is not supported"
            |> readIntegerFromConsole
            |> request.CreateResponse
        else
            raise <| NotSupportedException $"Request {request.PortInfo.RequestType} is not supported"

    let run () =
        let numberRequestPort = GetNode(RequestPort.Create<ExecutorResponse, int>("GuessNumber"))
        let judgeExecutor = GetNode(judgeExecutor 42, "Judge")
        let outputNode = GetNode((function
            | Found result -> result
            | _ -> failwith "Unexpected response"), "Output")

        let workflow =
            Workflow(numberRequestPort) {
                numberRequestPort ==> judgeExecutor
                judgeExecutor =|> [
                    case _.IsHint numberRequestPort
                    defaultCase outputNode
                ]
                return outputNode
            }

        +task {
            use! handle = InProcessExecution.StreamAsync(workflow, ExecutorResponse.Init)
            use enumerator = handle.WatchStreamAsync().GetAsyncEnumerator()
            while! enumerator.MoveNextAsync() do
                match enumerator.Current with
                | :? RequestInfoEvent as requestInputEvt ->
                    let response = handleExternalRequest requestInputEvt.Request
                    do! handle.SendResponseAsync(response)
                | :? WorkflowOutputEvent as outputEvt ->
                    Console.WriteLine($"Workflow completed with result: {outputEvt.Data}")
                | _ ->
                    ()
        }

Target.run()
