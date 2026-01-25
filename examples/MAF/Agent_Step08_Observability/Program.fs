#nowarn "57"

open System
open System.ClientModel
open System.ClientModel.Primitives
open PipedAgents.MAF
open PipedAgents.MAF.OpenAI
open Microsoft.Agents.AI
open FSharp.Control
open OpenAI.Responses
open OpenTelemetry
open OpenTelemetry.Trace
open Shared

module Baseline =

    let run() =
        // Create TracerProvider with console exporter
        // This will output the telemetry data to the console.
        let sourceName = Guid.NewGuid().ToString("N")
        let tracerProviderBuilder =
            Sdk.CreateTracerProviderBuilder()
                .AddSource(sourceName)
                .AddConsoleExporter()
        let key = ApiKeyCredential(
            Environment.GetEnvironmentVariable("OPENAI_API_KEY")
        )
        let httpClient = getLoggingHttpClient()
        let options = OpenAI.OpenAIClientOptions(
            Transport = new HttpClientPipelineTransport(httpClient),
            Endpoint = Uri(Environment.GetEnvironmentVariable "OPENAI_BASE_URL")
        )
        let client = OpenAI.OpenAIClient(key, options)
        let responseClient = client.GetResponsesClient(Environment.GetEnvironmentVariable "MODEL_ID")

        task {
            use _ = tracerProviderBuilder.Build()
            // Create the agent, and enable OpenTelemetry instrumentation.
            let agent =
                responseClient
                    .AsAIAgent(instructions = "You are good at telling jokes.", name = "Joker")
                    .AsBuilder()
                    .UseOpenTelemetry(sourceName = sourceName).Build()
            // Invoke the agent and output the text result.
            let! response = agent.RunAsync("Tell me a joke about a pirate.")
            printfn $"{response}"
        } |> _.GetAwaiter().GetResult()

module Target =

    let run() =
        let sourceName = Guid.NewGuid().ToString("N")
        let tracerProviderBuilder =
            Sdk.CreateTracerProviderBuilder()
                .AddSource(sourceName)
                .AddConsoleExporter()
        use client = Client.ForResponsesAPI(Environment.GetEnvironmentVariable "MODEL_ID")
        let agent =
            client.CreateAgent(
                AgentOptions(
                    Name = "Joker",
                    Instructions = "You are good at telling jokes.",
                    CreateRawOptions = (fun _ -> CreateResponseOptions(StoredOutputEnabled = false))
                )
            ).AddOpenTelemetry(sourceName)
        +task {
            use _ = tracerProviderBuilder.Build()
            let! response = agent.RunAsync "Tell me a joke about a pirate."
            printfn $"{response}"
        }

    let runStreaming() =
        let sourceName = Guid.NewGuid().ToString("N")
        let tracerProviderBuilder =
            Sdk.CreateTracerProviderBuilder()
                .AddSource(sourceName)
                .AddConsoleExporter()
        use client = Client.ForResponsesAPI(Environment.GetEnvironmentVariable "MODEL_ID")
        let agent =
            client.CreateAgent(
                AgentOptions(
                    Name = "Joker",
                    Instructions = "You are good at telling jokes.",
                    CreateRawOptions = (fun _ -> CreateResponseOptions(StoredOutputEnabled = false))
                )
            ).AddOpenTelemetry(sourceName)
        +task {
            use _ = tracerProviderBuilder.Build()
            do! agent.RunStreamingAsync "Tell me a joke about a pirate." |> TaskSeq.iter (printf "%O")
        }

Target.runStreaming()

