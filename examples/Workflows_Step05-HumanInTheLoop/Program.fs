#nowarn "57"


open System
open System.Threading.Tasks
open Microsoft.Agents.AI.Workflows
open Shared.Logging
open PipedAgents.MAF
open PipedAgents.MAF.Workflows
open FSharp.Control

type NumberSignal =
    | Init
    | Above
    | Below

module BaseLine =

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

    let rec private readIntegerFromConsole (prompt: string) =
        Console.Write(prompt)
        let input = Console.ReadLine()
        match Int32.TryParse(input) with
        | true, value ->
            value
        | _ ->
            Console.WriteLine("Invalid input. Please enter a valid integer.")
            readIntegerFromConsole prompt

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
    let run () =
        ()

BaseLine.run()
