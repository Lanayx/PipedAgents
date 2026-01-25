#nowarn "57"

open System
open System.ClientModel
open System.ClientModel.Primitives
open PipedAgents.MAF
open Microsoft.Agents.AI
open Microsoft.Extensions.AI
open Shared
open OpenAI.Responses
open PipedAgents.MAF.OpenAI
open FSharp.Control

module BaseLine =

    let run() =
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
        let agent = responseClient.AsAIAgent(ChatClientAgentOptions(
            Name = "Joker",
            ChatOptions = ChatOptions(
                Instructions = "You are good at telling jokes.",
                RawRepresentationFactory = fun _ -> CreateResponseOptions(StoredOutputEnabled = false)
            )
        ))
        +task{
            let! thread = agent.GetNewThreadAsync()
            let! joke = agent.RunAsync("Tell me a joke about a pirate.", thread)
            joke |> string |> printfn "%s"
            let! kidJoke = agent.RunAsync("Now tell the same joke for a kid", thread)
            kidJoke |> string |> printfn "%s"
        }


module Target =

    let run () =
        use client = Client.ForResponsesAPI(Environment.GetEnvironmentVariable "MODEL_ID")
        let agent = client.CreateAgent(AgentOptions(
            Name = "Joker",
            Instructions = "You are good at telling jokes.",
            CreateRawOptions = fun _ -> CreateResponseOptions(StoredOutputEnabled = false)
        ))
        let run = agent.GetThreadRun()
        task {
            let! result1 = run "Tell me a joke about a pirate."
            printfn $"{result1}"
            let! result2 = run "Now tell the same joke for a kid"
            printfn $"{result2}"
        }
        |> _.Wait()

    let runStreaming() =
        use client = Client.ForResponsesAPI(Environment.GetEnvironmentVariable "MODEL_ID")
        let agent = client.CreateAgent(AgentOptions(
            Name = "Joker",
            Instructions = "You are good at telling jokes.",
            CreateRawOptions = fun _ -> CreateResponseOptions(StoredOutputEnabled = false)
        ))
        let run = agent.GetStreamingThreadRun()
        +task {
            do! "Tell me a joke about a pirate." |> run |> TaskSeq.iter (printf "%O")
            printfn ""
            do! "Now tell the same joke for a kid" |> run |> TaskSeq.iter (printf "%O")
        }

Target.run()
