#nowarn "57"

open System
open System.ClientModel
open System.ClientModel.Primitives
open OpenAI.Chat
open Shared
open OpenAI
open FunAgents.MAF.OpenAI

module BaseLine =

    let run() =
        let key = ApiKeyCredential(
            Environment.GetEnvironmentVariable("OPENAI_API_KEY")
        )
        let httpClient = getLoggingHttpClient()
        let options = OpenAIClientOptions(
            Transport = new HttpClientPipelineTransport(httpClient),
            Endpoint = Uri(Environment.GetEnvironmentVariable "OPENAI_BASE_URL")
        )
        // httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + Environment.GetEnvironmentVariable "OPENAI_API_KEY")
        // let models = httpClient.GetStringAsync(Uri((Environment.GetEnvironmentVariable "OPENAI_BASE_URL") + "/models")).GetAwaiter().GetResult()

        let client = OpenAIClient(key, options)
        let responseClient = client.GetChatClient(Environment.GetEnvironmentVariable "MODEL_ID")
        let agent = responseClient.CreateAIAgent(
            instructions = "You are good at telling jokes. Write jokes with all uppercase letters.",
            name = "Joker")
        agent.RunAsync("Tell me a joke about a pirate.")
        |> _.GetAwaiter().GetResult()
        |> string
        |> printfn "%s"


module Target =

    let run() =
        let client = Client.ForChatCompletionsAPI(Environment.GetEnvironmentVariable "MODEL_ID")
        let agent = client.CreateAgent(AgentOptions(
            Instructions = "You are good at telling jokes. Write jokes with all uppercase letters.",
            Name = "Joker"))
        +task {
            let! result = agent.RunAsync("Tell me a joke about a pirate.")
            printfn $"{result}"
        }

    let runSteaming() =
        let client = Client.ForChatCompletionsAPI(Environment.GetEnvironmentVariable "MODEL_ID")
        let agent = client.CreateAgent(AgentOptions(
            Instructions = "You are good at telling jokes. Write jokes with all uppercase letters.",
            Name = "Joker"))
        let enumerator = agent.RunStreamingAsync("Tell me a joke about a pirate.").GetAsyncEnumerator()
        +task {
            while! enumerator.MoveNextAsync() do
                enumerator.Current |> string |> printf "%s"
        }

Target.run()