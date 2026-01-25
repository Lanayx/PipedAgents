#nowarn "57"

open System
open Microsoft.Agents.AI
open global.Anthropic
open PipedAgents.MAF
open global.Anthropic.Core
open Shared
open PipedAgents.MAF.Anthropic

module BaseLine =

    let run() =
        let httpClient = getLoggingHttpClient()
        let options = ClientOptions(
            APIKey = Environment.GetEnvironmentVariable "ANTHROPIC_API_KEY",
            HttpClient = httpClient,
            BaseUrl = Uri(Environment.GetEnvironmentVariable "ANTHROPIC_BASE_URL"),
            AuthToken = Environment.GetEnvironmentVariable "ANTHROPIC_AUTH_TOKEN"
        )
        let client = AnthropicClient(options)
        let agent = client.AsAIAgent(
            model = Environment.GetEnvironmentVariable "MODEL_ID",
            instructions = "You are good at telling jokes. Write jokes with all uppercase letters.",
            name = "Joker")
        +agent.RunAsync("Tell me a joke about a pirate.")
        |> string
        |> printfn "%s"


module Target =

    let run() =
        let client = Client.ForMessagesAPI(Environment.GetEnvironmentVariable "MODEL_ID")
        let agent = client.CreateAgent(AgentOptions(
            Instructions = "You are good at telling jokes. Write jokes with all uppercase letters.",
            Name = "Joker"))
        +task {
            let! result = agent.RunAsync("Tell me a joke about a pirate.")
            printfn $"{result}"
        }

    let runSteaming() =
        let client = Client.ForMessagesAPI(Environment.GetEnvironmentVariable "MODEL_ID")
        let agent = client.CreateAgent(AgentOptions(
            Instructions = "You are good at telling jokes. Write jokes with all uppercase letters.",
            Name = "Joker"))
        +task {
            use enumerator = agent.RunStreamingAsync("Tell me a joke about a pirate.").GetAsyncEnumerator()
            while! enumerator.MoveNextAsync() do
                enumerator.Current |> string |> printf "%s"
        }

Target.runSteaming()
