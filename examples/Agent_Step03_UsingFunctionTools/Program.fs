#nowarn "57"

open System
open System.ClientModel
open System.ClientModel.Primitives
open System.ComponentModel
open FSharp.Control
open FunAgents.MAF
open FunAgents.MAF.OpenAI
open Microsoft.Agents.AI
open Microsoft.Extensions.AI
open OpenAI.Responses
open Shared

module Baseline =

    [<Description("Get the weather for a given location.")>]
    let getWeather ([<Description("The location to get the weather for.")>] location: string) : string =
        $"The weather in {location} is cloudy with a high of 15°C."

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
        let agent =
            responseClient.CreateAIAgent(
                ChatClientAgentOptions(
                    ChatOptions =
                        ChatOptions(
                            Instructions = "You are a helpful assistant",
                            Tools = [| AIFunctionFactory.Create(Func<string,string>(getWeather)) |],
                            RawRepresentationFactory = (fun _ -> CreateResponseOptions(StoredOutputEnabled = false))
                        )
                )
            )
        // Non-streaming agent interaction with function tools.
        agent.RunAsync("What is the weather like in Amsterdam?")
        |> _.GetAwaiter().GetResult()
        |> string
        |> printfn "%s"


module Target =

    [<Description("Get the weather for a given location.")>]
    let getWeather ([<Description("The location to get the weather for.")>] location: string) : string =
        $"The weather in {location} is cloudy with a high of 15°C."

    let run () =
        // Create the responses client and agent, and provide the function tool to the agent.
        let client = Client.ForResponsesAPI(Environment.GetEnvironmentVariable "MODEL_ID")
        let agent =
            client.CreateChatAgent(
                AgentOptions(
                    Instructions = "You are a helpful assistant",
                    Tools = [| Tool.Get <@ getWeather @> |],
                    CreateResponseOptions = (fun _ -> CreateResponseOptions(StoredOutputEnabled = false))
                )
            )
        // Non-streaming agent interaction with function tools.
        +task {
            let! result = agent.RunAsync("What is the weather like in Amsterdam?")
            printfn $"{result}"
        }

    let runStreaming () =
        // Create the responses client and agent, and provide the function tool to the agent.
        let client = Client.ForResponsesAPI(Environment.GetEnvironmentVariable "MODEL_ID")
        let agent =
            client.CreateChatAgent(
                AgentOptions(
                    Instructions = "You are a helpful assistant",
                    Tools = [| Tool.Get <@ getWeather @> |],
                    CreateResponseOptions = (fun _ -> CreateResponseOptions(StoredOutputEnabled = false))
                )
            )
        // Streaming agent interaction with function tools.
        +task {
            for item in agent.RunStreamingAsync("What is the weather like in Amsterdam?") do
                printf $"{item}"
        }

Target.run()