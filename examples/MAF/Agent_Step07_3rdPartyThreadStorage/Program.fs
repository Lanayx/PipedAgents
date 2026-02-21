#nowarn "57"

open System
open System.Collections.Generic
open System.ClientModel
open System.ClientModel.Primitives
open System.Linq
open System.Text.Json
open System.Threading.Tasks
open FSharp.Control
open PipedAgents.MAF
open PipedAgents.MAF.OpenAI
open Microsoft.Agents.AI
open Microsoft.Extensions.AI
open Microsoft.Extensions.VectorData
open Microsoft.SemanticKernel.Connectors.InMemory
open OpenAI.Responses
open Shared

/// <summary>
/// The data structure used to store chat history items in the vector store.
/// </summary>
type ChatHistoryItem() =
    [<VectorStoreKey>]
    member val Key: string = null with get, set

    [<VectorStoreData>]
    member val SessionId: string = null with get, set

    [<VectorStoreData>]
    member val Timestamp: Nullable<DateTimeOffset> = Nullable() with get, set

    [<VectorStoreData>]
    member val SerializedMessage: string = null with get, set

    [<VectorStoreData>]
    member val MessageText: string = null with get, set

type State = {
    SessionDbKey: string
}

/// <summary>
/// A sample implementation of <see cref="ChatHistoryProvider"/> that stores chat messages in a vector store.
/// </summary>
type VectorChatHistoryProvider(vectorStore: VectorStore,
                               ?stateInitializer: AgentSession -> State) as this =
    inherit ChatHistoryProvider()

    let initializer = stateInitializer |> Option.defaultValue (fun _ -> { SessionDbKey = Guid.NewGuid().ToString("N") })
    let sessionState = ProviderSessionState(initializer, this.GetType().Name)

    override _.StateKey = sessionState.StateKey

    override _.ProvideChatHistoryAsync(context, cancellationToken) =
        task {
            let state = sessionState.GetOrInitializeState(context.Session)
            let collection = vectorStore.GetCollection<string, ChatHistoryItem>("ChatHistory")
            do! collection.EnsureCollectionExistsAsync(cancellationToken)
            let! (records: ResizeArray<ChatHistoryItem>) =
                collection
                    .GetAsync(
                        (fun x -> x.SessionId = state.SessionDbKey), 10,
                        FilteredRecordRetrievalOptions(OrderBy = _.Descending(_.Timestamp)),
                        cancellationToken
                    )
                    .ToListAsync(cancellationToken)

            let messages = records.ConvertAll(fun x -> JsonSerializer.Deserialize<ChatMessage>(x.SerializedMessage))
            messages.Reverse()
            return messages :> IEnumerable<ChatMessage>
        } |> ValueTask<IEnumerable<ChatMessage>>

    override _.StoreChatHistoryAsync(context, cancellationToken) =
        task {
            let state = sessionState.GetOrInitializeState(context.Session)

            let collection = vectorStore.GetCollection<string, ChatHistoryItem>("ChatHistory")
            do! collection.EnsureCollectionExistsAsync(cancellationToken)

            let allNewMessages =
                seq {
                    yield! context.RequestMessages
                    if context.ResponseMessages |> isNull |> not then
                        yield! context.ResponseMessages
                }

            let items =
                allNewMessages.Select(fun x ->
                    ChatHistoryItem(
                        Key = state.SessionDbKey + x.MessageId,
                        Timestamp = Nullable DateTimeOffset.UtcNow,
                        SessionId = state.SessionDbKey,
                        SerializedMessage = JsonSerializer.Serialize(x),
                        MessageText = x.Text
                    ))

            do! collection.UpsertAsync(items, cancellationToken)
        } |> ValueTask

    member _.GetSessionDbKey(session: AgentSession) =
        sessionState.GetOrInitializeState(session).SessionDbKey

module Baseline =

    let run () =
        let key = ApiKeyCredential(Environment.GetEnvironmentVariable("OPENAI_API_KEY"))
        let httpClient = getLoggingHttpClient()
        let options = OpenAI.OpenAIClientOptions(
            Transport = new HttpClientPipelineTransport(httpClient),
            Endpoint = Uri(Environment.GetEnvironmentVariable "OPENAI_BASE_URL")
        )
        let vectorStore = new InMemoryVectorStore()

        // Create the agent
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
                            ),
                        ChatHistoryProvider = VectorChatHistoryProvider(vectorStore)
                    )
                )

        task {
            // Start a new thread for the agent conversation.
            let! session = agent.CreateSessionAsync()

            // Run the agent with the thread that stores conversation history in the vector store.
            let! response1 = agent.RunAsync("Tell me a joke about a pirate.", session)
            printfn $"{response1}"

            // Serialize the thread state, so it can be stored for later use.
            let! serializedSession = agent.SerializeSessionAsync(session)

            printfn "\n--- Serialized thread ---\n"
            printfn $"{JsonSerializer.Serialize(serializedSession, JsonSerializerOptions(WriteIndented = true))}"

            // Deserialize the thread state after loading from storage.
            let! resumedSession = agent.DeserializeSessionAsync(serializedSession)

            // Run the agent with the thread that stores conversation history in the vector store a second time.
            let! response2 = agent.RunAsync("Now tell the same joke in the voice of a pirate, and add some emojis to the joke.", resumedSession)
            printfn $"{response2}"

            // We can access the VectorChatMessageStore via the thread's GetService method if we need to read the key under which threads are stored.
            let chatHistoryProvider = resumedSession.GetService<VectorChatHistoryProvider>()
            printfn $"\nThread is stored in vector store under key: {chatHistoryProvider.GetSessionDbKey(resumedSession)}"
        }
        |> _.GetAwaiter().GetResult()


module Target =

    let run () =
        use client = Client.ForResponsesAPI(Environment.GetEnvironmentVariable "MODEL_ID")
        let vectorStore = new InMemoryVectorStore()
        let agent =
            client.CreateAgent(AgentOptions(
                Name = "Joker",
                Instructions = "You are good at telling jokes.",
                CreateRawOptions = (fun _ -> CreateResponseOptions(StoredOutputEnabled = false)),
                ChatHistoryProvider = VectorChatHistoryProvider(vectorStore)
            ))

        +task {
            let! session = agent |> Session.New
            let run = agent.GetSessionRun(session)
            let! response1 = run "Tell me a joke about a pirate."
            printfn $"{response1}"
            let! serializedSession = Session.Serialize(session, agent)
            printfn $"\n--- Serialized thread ---\n {serializedSession} \n"
            let! resumedSession = Session.Deserialize(serializedSession, agent)
            let run = agent.GetSessionRun(resumedSession)
            let! response2 = run "Now tell the same joke in the voice of a pirate, and add some emojis to the joke."
            printfn $"{response2}"
            let chatHistoryProvider = session.GetService<VectorChatHistoryProvider>()
            printfn $"\nThread is stored in vector store under key: {chatHistoryProvider.GetSessionDbKey(resumedSession)}"
        }

Target.run()
