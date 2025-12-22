#nowarn "57"

open System
open System.ClientModel
open System.ClientModel.Primitives
open FunAgents.MAF
open Microsoft.Agents.AI
open Microsoft.Extensions.AI
open Shared
open OpenAI.Responses
open FunAgents.MAF.OpenAI
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
        let agent = responseClient.CreateAIAgent(ChatClientAgentOptions(
            Name = "Joker",
            ChatOptions = ChatOptions(
                Instructions = "You are good at telling jokes.",
                RawRepresentationFactory = fun _ -> CreateResponseOptions(StoredOutputEnabled = false)
            )
        ))
        let thread = agent.GetNewThread()
        agent.RunAsync("Tell me a joke about a pirate.", thread)
        |> _.GetAwaiter().GetResult()
        |> string
        |> printfn "%s"
        agent.RunAsync("Now tell the same joke for a kid", thread)
        |> _.GetAwaiter().GetResult()
        |> string
        |> printfn "%s"


module Target =

    let run() =
        let client = Client.ForResponsesAPI(Environment.GetEnvironmentVariable "MODEL_ID")
        let agent = client.CreateAIAgent(ChatClientAgentOptions(
            Name = "Joker",
            ChatOptions = ChatOptions(
                Instructions = "You are good at telling jokes.",
                RawRepresentationFactory = fun _ -> CreateResponseOptions(StoredOutputEnabled = false)
            )
        ))
        let run = agent.GetStreamingThreadRun()
        task {
            do! "Tell me a joke about a pirate." |> run |> TaskSeq.iter (printf "%O")
            printfn ""
            do! "Now tell the same joke for a kid" |> run |> TaskSeq.iter (printf "%O")
        }
        |> _.Wait()

Target.run()