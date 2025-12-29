#nowarn "57"

open System
open System.ClientModel
open System.ClientModel.Primitives
open System.Linq
open System.Text.Json
open System.Threading
open System.Runtime.InteropServices
open FSharp.Control
open FunAgents.MAF
open FunAgents.MAF.OpenAI
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
    member val ThreadId: string = null with get, set

    [<VectorStoreData>]
    member val Timestamp: Nullable<DateTimeOffset> = Nullable() with get, set

    [<VectorStoreData>]
    member val SerializedMessage: string = null with get, set

    [<VectorStoreData>]
    member val MessageText: string = null with get, set

/// <summary>
/// A sample implementation of <see cref="ChatMessageStore"/> that stores chat messages in a vector store.
/// </summary>
type VectorChatMessageStore(vectorStore: VectorStore, serializedStoreState: JsonElement) =
    inherit ChatMessageStore()

    let mutable threadDbKey: string =
        if serializedStoreState.ValueKind = JsonValueKind.String then
            serializedStoreState.Deserialize<string>()
        else
            null

    member _.ThreadDbKey = threadDbKey

    override _.AddMessagesAsync(messages: seq<ChatMessage>,
                                [<Optional; DefaultParameterValue(CancellationToken())>] cancellationToken: CancellationToken) =
        task {
            if isNull threadDbKey then
                threadDbKey <- Guid.NewGuid().ToString("N")
            let collection = vectorStore.GetCollection<string, ChatHistoryItem>("ChatHistory")
            do! collection.EnsureCollectionExistsAsync(cancellationToken)
            let items =
                messages.Select(fun x ->
                    ChatHistoryItem(
                        Key = threadDbKey + x.MessageId,
                        Timestamp = Nullable DateTimeOffset.UtcNow,
                        ThreadId = threadDbKey,
                        SerializedMessage = JsonSerializer.Serialize(x),
                        MessageText = x.Text
                    ))

            do! collection.UpsertAsync(items, cancellationToken)
        } :> _

    override _.GetMessagesAsync([<Optional; DefaultParameterValue(CancellationToken())>] cancellationToken: CancellationToken) =
        task {
            let collection = vectorStore.GetCollection<string, ChatHistoryItem>("ChatHistory")
            do! collection.EnsureCollectionExistsAsync(cancellationToken)
            let! records: ResizeArray<ChatHistoryItem> =
                collection
                    .GetAsync(
                        (fun x -> x.ThreadId = threadDbKey),
                        10, null, cancellationToken
                    )
                    .ToListAsync(cancellationToken)

            let messages = records.ConvertAll(fun x -> JsonSerializer.Deserialize<ChatMessage>(x.SerializedMessage))
            messages.Reverse()
            return messages :> seq<ChatMessage>
        }

    override _.Serialize([<Optional; DefaultParameterValue(null:JsonSerializerOptions)>] jsonSerializerOptions: JsonSerializerOptions) =
        JsonSerializer.SerializeToElement(threadDbKey, jsonSerializerOptions)

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
                .CreateAIAgent(
                    ChatClientAgentOptions(
                        Name = "Joker",
                        ChatOptions =
                            ChatOptions(
                                Instructions = "You are good at telling jokes.",
                                RawRepresentationFactory = (fun _ -> CreateResponseOptions(StoredOutputEnabled = false))
                            ),
                        ChatMessageStoreFactory =(fun ctx ->
                            // Create a new chat message store for this agent that stores the messages in a vector store.
                            // Each thread must get its own copy of the VectorChatMessageStore, since the store
                            // also contains the id that the thread is stored under.
                            VectorChatMessageStore(vectorStore, ctx.SerializedState))
                    )
                )

        task {
            // Start a new thread for the agent conversation.
            let thread = agent.GetNewThread()

            // Run the agent with the thread that stores conversation history in the vector store.
            let! response1 = agent.RunAsync("Tell me a joke about a pirate.", thread)
            printfn $"{response1}"

            // Serialize the thread state, so it can be stored for later use.
            let serializedThread = thread.Serialize()

            printfn "\n--- Serialized thread ---\n"
            printfn $"{JsonSerializer.Serialize(serializedThread, JsonSerializerOptions(WriteIndented = true))}"

            // Deserialize the thread state after loading from storage.
            let resumedThread = agent.DeserializeThread(serializedThread)

            // Run the agent with the thread that stores conversation history in the vector store a second time.
            let! response2 = agent.RunAsync("Now tell the same joke in the voice of a pirate, and add some emojis to the joke.", resumedThread)
            printfn $"{response2}"

            // We can access the VectorChatMessageStore via the thread's GetService method if we need to read the key under which threads are stored.
            let messageStore = resumedThread.GetService<VectorChatMessageStore>()
            printfn $"\nThread is stored in vector store under key: {messageStore.ThreadDbKey}"
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
                ChatMessageStoreFactory = (fun ctx -> VectorChatMessageStore(vectorStore, ctx.SerializedState))
            ))

        +task {
            let thread = agent |> Thread.New
            let run = agent.GetThreadRun(thread)
            let! response1 = run "Tell me a joke about a pirate."
            printfn $"{response1}"
            let serializedThread = Thread.ToString thread
            printfn $"\n--- Serialized thread ---\n {serializedThread} \n"
            let resumedThread = Thread.FromString(serializedThread, agent)
            let run = agent.GetThreadRun(resumedThread)
            let! response2 = run "Now tell the same joke in the voice of a pirate, and add some emojis to the joke."
            printfn $"{response2}"
            let messageStore = thread.MessageStore :?> VectorChatMessageStore
            printfn $"\nThread is stored in vector store under key: {messageStore.ThreadDbKey}"
        }

Target.run()