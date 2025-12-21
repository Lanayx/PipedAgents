#nowarn "57"

open System
open System.ClientModel
open System.ClientModel.Primitives
open Microsoft.Agents.AI
open Microsoft.Extensions.AI
open Shared
open OpenAI.Responses
open FunAgents.MAF.OpenAI

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
        let agent = client.CreateAIAgent(
            instructions = "You are good at telling jokes.",
            name = "Joker")
        let enumerator = agent.RunStreamingAsync("Tell me a joke about a pirate.").GetAsyncEnumerator()
        task {
            while! enumerator.MoveNextAsync() do
                enumerator.Current |> string |> printf "%s"
        }
        |> _.GetAwaiter().GetResult()

BaseLine.run()