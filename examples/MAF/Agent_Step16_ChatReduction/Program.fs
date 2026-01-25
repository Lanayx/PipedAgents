#nowarn "57"

open System
open System.ClientModel
open System.ClientModel.Primitives
open System.Collections.Generic
open System.Text.Json
open System.Runtime.InteropServices
open System.Threading.Tasks
open PipedAgents.MAF
open Microsoft.Agents.AI
open Microsoft.Extensions.AI
open Shared
open OpenAI.Responses
open PipedAgents.MAF.OpenAI
open FSharp.Control

/// <summary>
/// A sample implementation of <see cref="ChatMessageStore"/> that reduces the chat history using a <see cref="ChatReducer"/>.
/// </summary>
type ReducedChatMessageStore(reducer: IChatReducer, serializedState: JsonElement) =
    inherit ChatMessageStore()
    
    let messages = List<ChatMessage>()

    override _.InvokingAsync(context, cancellationToken) =
        task {
            let! reduced = reducer.ReduceAsync(messages, cancellationToken)
            return reduced :> IEnumerable<ChatMessage>
        } |> ValueTask<IEnumerable<ChatMessage>>

    override _.InvokedAsync(context, cancellationToken) =
        task {
            if context.InvokeException |> isNull |> not then return ()
            messages.AddRange(context.RequestMessages)
            if context.ResponseMessages |> isNull |> not then
                messages.AddRange(context.ResponseMessages)
        } |> ValueTask

    override _.Serialize([<Optional; DefaultParameterValue(null:JsonSerializerOptions)>] jsonSerializerOptions: JsonSerializerOptions) =
        JsonSerializer.SerializeToElement("", jsonSerializerOptions)

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
        let agent = responseClient.AsAIAgent(ChatClientAgentOptions(
            Name = "Joker",
            ChatOptions = ChatOptions(
                Instructions = "You are good at telling jokes.",
                RawRepresentationFactory = fun _ -> CreateResponseOptions(StoredOutputEnabled = false)
            ),
            ChatMessageStoreFactory = (fun ctx ct -> ValueTask<ChatMessageStore>(ReducedChatMessageStore(MessageCountingChatReducer(2),
                ctx.SerializedState))))
        )
        +task {
            let! thread = agent.GetNewThreadAsync()
            // Invoke the agent and output the text result.
            let! result1 = agent.RunAsync("Tell me a joke about a pirate.", thread)
            Console.WriteLine $"{result1}"

            // Get the chat history to see how many messages are stored.
            let chatHistory = thread.GetService<IList<ChatMessage>>()
            Console.WriteLine($"\nChat history has {chatHistory.Count} messages.\n")

            // Invoke the agent a few more times.
            let! result2 = agent.RunAsync("Tell me a joke about a robot.", thread)
            Console.WriteLine $"{result2}"
            Console.WriteLine($"\nChat history has {chatHistory.Count} messages.\n")
            let! result3 = agent.RunAsync("Tell me a joke about a lemur.", thread)
            Console.WriteLine $"{result3}"
            Console.WriteLine($"\nChat history has {chatHistory.Count} messages.\n")

            // At this point, the chat history has exceeded the limit and the original message will not exist anymore,
            // so asking a follow up question about it will not work as expected.
            let! result4 = agent.RunAsync("Tell me the joke about the pirate again, but add emojis and use the voice of a parrot.", thread)
            Console.WriteLine $"{result4}"
            Console.WriteLine($"\nChat history has {chatHistory.Count} messages.\n")
        }


module Target =

    let run () =
        use client = Client.ForResponsesAPI(Environment.GetEnvironmentVariable "MODEL_ID")
        let agent = client.CreateAgent(AgentOptions(
            Name = "Joker",
            Instructions = "You are good at telling jokes.",
            CreateRawOptions = (fun _ -> CreateResponseOptions(StoredOutputEnabled = false)),
            ChatMessageStoreFactory = (fun ctx ct -> ValueTask<ChatMessageStore>(ReducedChatMessageStore(MessageCountingChatReducer(2),
                ctx.SerializedState))))
        )
        +task {
            let! thread = agent |> Thread.New
            let run = agent.GetThreadRun(thread)
            let chatHistory = thread |> Thread.GetChatHistory
            let! result1 = run "Tell me a joke about a pirate."
            printfn $"{result1}"
            Console.WriteLine($"\nChat history has {chatHistory.Count} messages.\n")
            let! result2 = run "Tell me a joke about a robot."
            printfn $"{result2}"
            Console.WriteLine($"\nChat history has {chatHistory.Count} messages.\n")
            let! result3 = run "Tell me a joke about a lemur."
            printfn $"{result3}"
            Console.WriteLine($"\nChat history has {chatHistory.Count} messages.\n")
            let! result4 = run "Tell me the joke about the pirate again, but add emojis and use the voice of a parrot."
            printfn $"{result4}"
            Console.WriteLine($"\nChat history has {chatHistory.Count} messages.\n")
        }

    let runStreaming() =
        use client = Client.ForResponsesAPI(Environment.GetEnvironmentVariable "MODEL_ID")
        let agent = client.CreateAgent(AgentOptions(
            Name = "Joker",
            Instructions = "You are good at telling jokes.",
            CreateRawOptions = (fun _ -> CreateResponseOptions(StoredOutputEnabled = false)),
            ChatMessageStoreFactory = (fun ctx ct -> ValueTask<ChatMessageStore>(ReducedChatMessageStore(MessageCountingChatReducer(2),
                ctx.SerializedState))))
        )
        +task {
            let! thread = agent |> Thread.New
            let run = agent.GetStreamingThreadRun(thread)
            let chatHistory = thread |> Thread.GetChatHistory
            do! "Tell me a joke about a pirate." |> run |> TaskSeq.iter (printf "%O")
            Console.WriteLine($"\nChat history has {chatHistory.Count} messages.\n")
            do! "Tell me a joke about a robot." |> run |> TaskSeq.iter (printf "%O")
            Console.WriteLine($"\nChat history has {chatHistory.Count} messages.\n")
            do! "Tell me a joke about a lemur." |> run |> TaskSeq.iter (printf "%O")
            Console.WriteLine($"\nChat history has {chatHistory.Count} messages.\n")
            do! "Tell me the joke about the pirate again, but add emojis and use the voice of a parrot." |> run |> TaskSeq.iter (printf "%O")
            Console.WriteLine($"\nChat history has {chatHistory.Count} messages.\n")
        }

Target.runStreaming()
