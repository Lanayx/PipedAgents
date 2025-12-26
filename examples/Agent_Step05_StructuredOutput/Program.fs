#nowarn "57"

open System
open System.ClientModel
open System.ClientModel.Primitives
open System.ComponentModel
open System.Text.Json
open System.Text.Json.Serialization
open FSharp.Control
open FunAgents.MAF
open FunAgents.MAF.OpenAI
open Microsoft.Agents.AI
open Microsoft.Extensions.AI
open OpenAI.Responses
open Shared

/// <summary>
/// Represents information about a person, including their name, age, and occupation, matched to the JSON schema used in the agent.
/// </summary>
[<Description("Information about a person including their name, age, and occupation")>]
type PersonInfo() =
    [<JsonPropertyName("name")>]
    member val Name: string = null with get, set

    [<JsonPropertyName("age")>]
    member val Age: Nullable<int> = Nullable() with get, set

    [<JsonPropertyName("occupation")>]
    member val Occupation: string = null with get, set

module Baseline =

    let run() =
        let key = ApiKeyCredential(Environment.GetEnvironmentVariable("OPENAI_API_KEY"))
        let httpClient = getLoggingHttpClient()
        let options = OpenAI.OpenAIClientOptions(
            Transport = new HttpClientPipelineTransport(httpClient),
            Endpoint = Uri(Environment.GetEnvironmentVariable "OPENAI_BASE_URL")
        )
        let client = OpenAI.OpenAIClient(key, options)
        let responseClient = client.GetResponsesClient(Environment.GetEnvironmentVariable "MODEL_ID")
        
        // Create the ChatClientAgent with the specified name and instructions.
        let agent = responseClient.CreateAIAgent(name = "HelpfulAssistant", instructions = "You are a helpful assistant.")

        // Set PersonInfo as the type parameter of RunAsync method to specify the expected structured output from the agent and invoke the agent with some unstructured input.
        let response = agent.RunAsync<PersonInfo>("Please provide information about fictional character John Smith, who is a 35-year-old software engineer.")
                       |> _.GetAwaiter().GetResult()

        // Access the structured output via the Result property of the agent response.
        printfn "Assistant Output:"
        printfn $"Name: {response.Result.Name}"
        printfn $"Age: {response.Result.Age}"
        printfn $"Occupation: {response.Result.Occupation}"

module Target =

    let run() =
        // Create the responses client and agent, and provide the instructions and response format.
        let client = Client.ForResponsesAPI(Environment.GetEnvironmentVariable "MODEL_ID")
        let agent = client.CreateAIAgent(name = "HelpfulAssistant", instructions = "You are a helpful assistant.")
        +task {
            // Non-streaming agent interaction with structured output.
            let! response = agent.RunAsync<PersonInfo> "Please provide information about fictional character John Smith, who is a 35-year-old software engineer."

            printfn "Assistant Output:"
            printfn $"Name: {response.Result.Name}"
            printfn $"Age: {response.Result.Age}"
            printfn $"Occupation: {response.Result.Occupation}"
        }

    let runStreaming () =
        // Create the responses client and agent, and provide the instructions and response format.
        let client = Client.ForResponsesAPI(Environment.GetEnvironmentVariable "MODEL_ID")
        let agent =
            client.CreateChatAgent(
                AgentOptions(
                    Name = "HelpfulAssistant",
                    Instructions = "You are a helpful assistant",
                    ResponseFormat = ChatResponseFormat.ForJsonSchema<PersonInfo>(),
                    CreateResponseOptions = (fun _ -> CreateResponseOptions(
                        ReasoningOptions = ResponseReasoningOptions(
                            ReasoningEffortLevel = ResponseReasoningEffortLevel.Minimal)
                    ))
                )
            )
            
        +task {
            // Streaming agent interaction with structured output.
            let updates = agent.RunStreamingAsync("Please provide information about fictional character John Smith, who is a 35-year-old software engineer.")
            
            let! responseStreaming = updates.ToAgentRunResponseAsync()
            let personInfo = responseStreaming.Deserialize<PersonInfo>(JsonSerializerOptions.Web)

            printfn "Assistant Output (Target Streaming):"
            printfn $"Name: {personInfo.Name}"
            printfn $"Age: {personInfo.Age}"
            printfn $"Occupation: {personInfo.Occupation}"
        }

Target.runStreaming()
