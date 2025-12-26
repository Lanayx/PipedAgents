#nowarn "57"

open System
open System.ClientModel
open System.ClientModel.Primitives
open System.IO
open System.Text.Json
open FSharp.Control
open FunAgents.MAF
open FunAgents.MAF.OpenAI
open Microsoft.Agents.AI
open Microsoft.Extensions.AI
open OpenAI.Responses
open Shared

module Baseline =

    let run() =
        let key = ApiKeyCredential(Environment.GetEnvironmentVariable("OPENAI_API_KEY"))
        let httpClient = getLoggingHttpClient()
        let options = OpenAI.OpenAIClientOptions(
            Transport = new HttpClientPipelineTransport(httpClient),
            Endpoint = Uri(Environment.GetEnvironmentVariable "OPENAI_BASE_URL")
        )
        let agent =
            OpenAI.OpenAIClient(key, options)
                .GetResponsesClient(Environment.GetEnvironmentVariable "MODEL_ID")
                .CreateAIAgent(
                    ChatClientAgentOptions(
                        Name = "Joker",
                        ChatOptions =
                            ChatOptions(
                                Instructions = "You are good at telling jokes.",
                                RawRepresentationFactory = (fun _ -> CreateResponseOptions(StoredOutputEnabled = false))
                            )
                    )
                )

        task {
            // Start a new thread for the agent conversation.
            let thread = agent.GetNewThread()

            // Run the agent with a new thread.
            let! response1 = agent.RunAsync("Tell me a joke about a pirate.", thread)
            printfn $"{response1}"

            // Serialize the thread state to a JsonElement, so it can be stored for later use.
            let serializedThread = thread.Serialize()

            // Save the serialized thread to a temporary file (for demonstration purposes).
            let tempFilePath = Path.GetTempFileName()
            do! File.WriteAllTextAsync(tempFilePath, JsonSerializer.Serialize(serializedThread))
            printfn $"Thread state saved to {tempFilePath}"

            // Load the serialized thread from the temporary file (for demonstration purposes).
            let! reloadedContent = File.ReadAllTextAsync(tempFilePath)
            let reloadedSerializedThread = JsonDocument.Parse(reloadedContent).RootElement

            // Deserialize the thread state after loading from storage.
            let resumedThread = agent.DeserializeThread(reloadedSerializedThread)

            // Run the agent again with the resumed thread.
            let! response2 = agent.RunAsync("Now tell the same joke in the voice of a pirate, and add some emojis to the joke.", resumedThread)
            printfn $"{response2}"

            // Clean up the temporary file.
            File.Delete(tempFilePath)
        }
        |> _.GetAwaiter().GetResult()

module Target =

    let run() =
        let agent =
            Environment.GetEnvironmentVariable "MODEL_ID"
            |> Client.ForResponsesAPI
            |> _.CreateChatAgent(AgentOptions(
                Name = "Joker",
                Instructions = "You are good at telling jokes.",
                CreateResponseOptions = fun _ -> CreateResponseOptions(StoredOutputEnabled = false)))
        
        +task {
            // Start a new thread for the agent conversation.
            let thread = agent.GetNewThread()
            let run = agent.GetThreadRun(thread)

            // Run the agent with a new thread.
            let! response1 = run "Tell me a joke about a pirate."
            printfn $"{response1}"

            // Serialize the thread state to a JsonElement, so it can be stored for later use.
            let serializedThread = Thread.ToString thread

            // Save the serialized thread to a temporary file (for demonstration purposes).
            let tempFilePath = Path.GetTempFileName()
            do! File.WriteAllTextAsync(tempFilePath, serializedThread)
            printfn $"Thread state saved to {tempFilePath}"

            // Load the serialized thread from the temporary file (for demonstration purposes).
            let! reloadedContent = File.ReadAllTextAsync(tempFilePath)

            // Deserialize the thread state after loading from storage.
            let resumedThread = Thread.FromString(reloadedContent, agent)
            let run = agent.GetThreadRun(resumedThread)

            // Run the agent again with the resumed thread.
            let! response2 = run "Now tell the same joke in the voice of a pirate, and add some emojis to the joke."
            printfn $"{response2}"
            
            // Clean up the temporary file.
            File.Delete(tempFilePath)
        }

Target.run()
