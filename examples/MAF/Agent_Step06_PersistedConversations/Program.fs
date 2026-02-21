#nowarn "57"

open System
open System.ClientModel
open System.ClientModel.Primitives
open System.IO
open System.Text.Json
open FSharp.Control
open PipedAgents.MAF
open PipedAgents.MAF.OpenAI
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
                .AsAIAgent(
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
            // Start a new session for the agent conversation.
            let! session = agent.CreateSessionAsync()

            // Run the agent with a new session.
            let! response1 = agent.RunAsync("Tell me a joke about a pirate.", session)
            printfn $"{response1}"

            // Serialize the session state to a JsonElement, so it can be stored for later use.
            let! serializedSession = agent.SerializeSessionAsync(session)

            // Save the serialized session to a temporary file (for demonstration purposes).
            let tempFilePath = Path.GetTempFileName()
            do! File.WriteAllTextAsync(tempFilePath, JsonSerializer.Serialize(serializedSession))
            printfn $"Session state saved to {tempFilePath}"

            // Load the serialized session from the temporary file (for demonstration purposes).
            let! reloadedContent = File.ReadAllTextAsync(tempFilePath)
            let reloadedSerializedSession = JsonDocument.Parse(reloadedContent).RootElement

            // Deserialize the session state after loading from storage.
            let! resumedSession = agent.DeserializeSessionAsync(reloadedSerializedSession)

            // Run the agent again with the resumed session.
            let! response2 = agent.RunAsync("Now tell the same joke in the voice of a pirate, and add some emojis to the joke.", resumedSession)
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
            |> _.CreateAgent(AgentOptions(
                Name = "Joker",
                Instructions = "You are good at telling jokes.",
                CreateRawOptions = fun _ -> CreateResponseOptions(StoredOutputEnabled = false)
            ))
        
        +task {
            // Start a new session for the agent conversation.
            let! session = agent.CreateSessionAsync()
            let run = agent.GetSessionRun(session)

            // Run the agent with a new session.
            let! response1 = run "Tell me a joke about a pirate."
            printfn $"{response1}"

            // Serialize the session state to a JsonElement, so it can be stored for later use.
            let! serializedSession = Session.Serialize(session, agent)

            // Save the serialized session to a temporary file (for demonstration purposes).
            let tempFilePath = Path.GetTempFileName()
            do! File.WriteAllTextAsync(tempFilePath, serializedSession)
            printfn $"Session state saved to {tempFilePath}"

            // Load the serialized session from the temporary file (for demonstration purposes).
            let! reloadedContent = File.ReadAllTextAsync(tempFilePath)

            // Deserialize the session state after loading from storage.
            let! resumedSession = Session.Deserialize(reloadedContent, agent)
            let run = agent.GetSessionRun(resumedSession)

            // Run the agent again with the resumed session.
            let! response2 = run "Now tell the same joke in the voice of a pirate, and add some emojis to the joke."
            printfn $"{response2}"
            
            // Clean up the temporary file.
            File.Delete(tempFilePath)
        }

Target.run()

