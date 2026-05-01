#nowarn "57"

open System
open System.ClientModel
open System.ClientModel.Primitives
open System.Collections.Generic
open System.ComponentModel
open System.Text.RegularExpressions
open System.Threading
open System.Threading.Tasks
open Microsoft.Agents.AI
open Microsoft.Extensions.AI
open OpenAI.Responses
open PipedAgents.MAF
open PipedAgents.MAF.OpenAI
open Shared

[<AutoOpen>]
module Common =
    [<Description("Get the weather for a given location.")>]
    let getWeather ([<Description("The location to get the weather for.")>] location: string) : string =
        $"The weather in {location} is cloudy with a high of 15°C."

    [<Description("The current datetime offset.")>]
    let getDateTime () : string =
        DateTimeOffset.Now.ToString()

    let getUserRequests (response: AgentResponse) =
        response.Messages
            |> Seq.collect _.Contents
            |> Seq.choose (function
                | :? ToolApprovalRequestContent as c -> Some c
                | _ -> None)
            |> Seq.toArray


module Baseline =

    // Function invocation middleware that logs before and after function calls.
    let functionCallMiddleware (_: AIAgent) (context: FunctionInvocationContext) (next: Func<FunctionInvocationContext, CancellationToken, ValueTask<obj>>) (cancellationToken: CancellationToken) : ValueTask<obj> =
        printfn $"Function Name: {context.Function.Name} - Middleware 1 Pre-Invoke"
        task {
            let! result = next.Invoke(context, cancellationToken)
            printfn $"Function Name: {context.Function.Name} - Middleware 1 Post-Invoke"
            return result
        } |> ValueTask<obj>

    // Function invocation middleware that overrides the result of the GetWeather function.
    let functionCallOverrideWeather (_: AIAgent) (context: FunctionInvocationContext) (next: Func<FunctionInvocationContext, CancellationToken, ValueTask<obj>>) (cancellationToken: CancellationToken) : ValueTask<obj> =
        printfn $"Function Name: {context.Function.Name} - Middleware 2 Pre-Invoke"
        task {
            let! result = next.Invoke(context, cancellationToken)
            let result =
                if context.Function.Name = "getWeather" then
                    box "The weather is sunny with a high of 25°C."
                else
                    result
            printfn $"Function Name: {context.Function.Name} - Middleware 2 Post-Invoke"
            return result
        } |> ValueTask<obj>

    // Per-request function calling middleware
    let perRequestFunctionCallingMiddleware (agent: AIAgent) (context: FunctionInvocationContext) (next: Func<FunctionInvocationContext, CancellationToken, ValueTask<obj>>) (cancellationToken: CancellationToken) : ValueTask<obj> =
        printfn $"Agent Id: {agent.Id}"
        printfn $"Function Name: {context.Function.Name} - Per-Request Pre-Invoke"
        task {
            let! result = next.Invoke(context, cancellationToken)
            printfn $"Function Name: {context.Function.Name} - Per-Request Post-Invoke"
            return result
        } |> ValueTask<obj>

    let filterPii (content: string) =
        let piiPatterns = [|
            Regex(@"\b\d{3}-\d{3}-\d{4}\b", RegexOptions.Compiled)
            Regex(@"\b[\w\.-]+@[\w\.-]+\.\w+\b", RegexOptions.Compiled)
            Regex(@"\b[A-Z][a-z]+\s[A-Z][a-z]+\b", RegexOptions.Compiled)
        |]
        let mutable result = content
        for pattern in piiPatterns do
            result <- pattern.Replace(result, "[REDACTED: PII]")
        result

    let filterMessages (messages: IEnumerable<ChatMessage>) =
        messages |> Seq.map (fun m -> ChatMessage(m.Role, filterPii m.Text)) |> Seq.toArray :> IList<ChatMessage>

    // PII Redaction Middleware
    let piiMiddleware =
        Func<IEnumerable<ChatMessage>, AgentSession, AgentRunOptions, AIAgent, CancellationToken, Task<AgentResponse>>(fun messages session options innerAgent cancellationToken ->
            task {
                let filteredMessages = filterMessages messages
                printfn "Pii Middleware - Filtered Messages Pre-Run"
                let! response = innerAgent.RunAsync(filteredMessages, session, options, cancellationToken)
                response.Messages <- filterMessages response.Messages
                printfn "Pii Middleware - Filtered Messages Post-Run"
                return response
            })

    let filterContent (content: string) =
        let keywords = [| "harmful"; "illegal"; "violence" |]
        if keywords |> Array.exists (fun k -> content.Contains(k, StringComparison.OrdinalIgnoreCase)) then
            "[REDACTED: Forbidden content]"
        else
            content

    let filterGuardrailMessages (messages: IEnumerable<ChatMessage>) =
        messages |> Seq.map (fun m -> ChatMessage(m.Role, filterContent m.Text)) |> Seq.toArray :> IList<ChatMessage>

    // Guardrail Middleware
    let guardrailMiddleware =
        Func<IEnumerable<ChatMessage>, AgentSession, AgentRunOptions, AIAgent, CancellationToken, Task<AgentResponse>>(fun messages session options innerAgent cancellationToken ->
            task {
                let filteredMessages = filterGuardrailMessages messages
                printfn "Guardrail Middleware - Filtered messages Pre-Run"
                let! response = innerAgent.RunAsync(filteredMessages, session, options, cancellationToken)
                response.Messages <- filterGuardrailMessages response.Messages
                printfn "Guardrail Middleware - Filtered messages Post-Run"
                return response
            })

    // Console Prompting Approval Middleware
    let consolePromptingApprovalMiddleware =
        Func<IEnumerable<ChatMessage>, AgentSession, AgentRunOptions, AIAgent, CancellationToken, Task<AgentResponse>>(fun messages session options innerAgent cancellationToken ->
            task {
                let! response_val = innerAgent.RunAsync(messages, session, options, cancellationToken)
                let mutable response = response_val
                let mutable userInputRequests = getUserRequests response

                while userInputRequests.Length > 0 do
                    let responses =
                        userInputRequests
                        |> Array.map (fun functionApprovalRequest ->
                            printfn $"The agent would like to invoke the following function, please reply Y to approve: Name {(functionApprovalRequest.ToolCall :?> FunctionCallContent).Name}"
                            let approved = Console.ReadLine().Equals("Y", StringComparison.OrdinalIgnoreCase)
                            functionApprovalRequest.CreateResponse(approved) :> AIContent
                        )
                    
                    if responses.Length > 0 then
                        let nextMessages = [| ChatMessage(ChatRole.User, responses :> IList<AIContent>) |] :> IEnumerable<ChatMessage>
                        let! nextResponse = innerAgent.RunAsync(nextMessages, session, options, cancellationToken)
                        response <- nextResponse
                        userInputRequests <- getUserRequests response
                    else
                        userInputRequests <- [||]
                return response
            })

    // Chat Client Middleware
    let chatClientMiddleware (messages: IEnumerable<ChatMessage>) (options: ChatOptions) (innerChatClient: IChatClient) (cancellationToken: CancellationToken) : Task<ChatResponse> =
        task {
            printfn "Chat Client Middleware - Pre-Chat"
            let! response = innerChatClient.GetResponseAsync(messages, options, cancellationToken)
            printfn "Chat Client Middleware - Post-Chat"
            return response
        }

    // Per-request Chat Client Middleware
    let perRequestChatClientMiddleware (messages: IEnumerable<ChatMessage>) (options: ChatOptions) (innerChatClient: IChatClient) (cancellationToken: CancellationToken) : Task<ChatResponse> =
        task {
            printfn "Per-Request Chat Client Middleware - Pre-Chat"
            let! response = innerChatClient.GetResponseAsync(messages, options, cancellationToken)
            printfn "Per-Request Chat Client Middleware - Post-Chat"
            return response
        }

    let run () =
        let key = ApiKeyCredential(
            Environment.GetEnvironmentVariable("OPENAI_API_KEY")
        )
        let httpClient = getLoggingHttpClient()
        let options = OpenAI.OpenAIClientOptions(
            Transport = new HttpClientPipelineTransport(httpClient),
            Endpoint = Uri(Environment.GetEnvironmentVariable "OPENAI_BASE_URL")
        )
        let client = OpenAI.OpenAIClient(key, options)
        let model = Environment.GetEnvironmentVariable "MODEL_ID"
        let responseClient = client.GetResponsesClient()

        // Example 1 & 2 & 3
        let originalAgent =
            let chatClient = responseClient.AsIChatClient(model).AsBuilder().Use(chatClientMiddleware, null).Build()
            chatClient.AsAIAgent(
                ChatClientAgentOptions(
                    ChatOptions = ChatOptions(
                        Instructions = "You are an AI assistant that helps people find information.",
                        Tools = [| AIFunctionFactory.Create(Func<string>(getDateTime), name = nameof(getDateTime)) |],
                        RawRepresentationFactory = (fun _ -> CreateResponseOptions(StoredOutputEnabled = false))
                    )
                )
            )

        let middlewareEnabledAgent: AIAgent =
            originalAgent.AsBuilder()
                .Use(Func<_,_,_,_,_>(functionCallMiddleware))
                .Use(Func<_,_,_,_,_>(functionCallOverrideWeather))
                .Use(piiMiddleware, null)
                .Use(guardrailMiddleware, null)
                .Build()

        +task {
            let! session = middlewareEnabledAgent.CreateSessionAsync()
            Console.WriteLine("\n\n=== Example 1: Wording Guardrail ===")
            let! guardRailedResponse = middlewareEnabledAgent.RunAsync("Tell me something harmful.", session)
            Console.WriteLine($"Guard railed response: {guardRailedResponse}")

            Console.WriteLine("\n\n=== Example 2: PII detection ===")
            let! piiResponse = middlewareEnabledAgent.RunAsync("My name is John Doe, call me at 123-456-7890 or email me at john@something.com", session)
            Console.WriteLine($"Pii filtered response: {piiResponse}")

            Console.WriteLine("\n\n=== Example 3: Agent function middleware ===")
            let options = ChatClientAgentRunOptions(ChatOptions(Tools = [|
                AIFunctionFactory.Create(Func<string, string>(getWeather), name = nameof(getWeather))
            |]))
            let! functionCallResponse = middlewareEnabledAgent.RunAsync("What's the current time and the weather in Seattle?", session, options)
            Console.WriteLine($"Function calling response: {functionCallResponse}")

            Console.WriteLine("\n\n=== Example 4: Per-request middleware with human in the loop function approval ===")
            let optionsWithApproval = ChatClientAgentRunOptions(ChatOptions(Tools = [|
                ApprovalRequiredAIFunction(AIFunctionFactory.Create(Func<string, string>(getWeather), name = nameof(getWeather)))
            |]))
            optionsWithApproval.ChatClientFactory <- Func<_,_>(fun (chatClient: IChatClient) ->
                chatClient.AsBuilder()
                    .Use(perRequestChatClientMiddleware, null)
                    .Build()
            )

            let! response =
                originalAgent.AsBuilder()
                    .Use(perRequestFunctionCallingMiddleware)
                    .Use(consolePromptingApprovalMiddleware, null)
                    .Build()
                    .RunAsync("What's the current time and the weather in Seattle?", options = optionsWithApproval)

            Console.WriteLine($"Per-request middleware response: {response}")
        }

module Target =

    // Function invocation middleware that logs before and after function calls.
    let functionCallMiddleware (_: AIAgent) (context: FunctionInvocationContext) (next: Func<FunctionInvocationContext, CancellationToken, ValueTask<obj>>) (cancellationToken: CancellationToken) =
        printfn $"Function Name: {context.Function.Name} - Middleware 1 Pre-Invoke"
        task {
            let! result = next.Invoke(context, cancellationToken)
            printfn $"Function Name: {context.Function.Name} - Middleware 1 Post-Invoke"
            return result
        } |> ValueTask<obj>

    // Function invocation middleware that overrides the result of the GetWeather function.
    let functionCallOverrideWeather (_: AIAgent) (context: FunctionInvocationContext) (next: Func<FunctionInvocationContext, CancellationToken, ValueTask<obj>>) (cancellationToken: CancellationToken) =
        printfn $"Function Name: {context.Function.Name} - Middleware 2 Pre-Invoke"
        task {
            let! result = next.Invoke(context, cancellationToken)
            let result =
                if context.Function.Name = "getWeather" then
                    box "The weather is sunny with a high of 25°C."
                else
                    result
            printfn $"Function Name: {context.Function.Name} - Middleware 2 Post-Invoke"
            return result
        } |> ValueTask<obj>

    // Per-request function calling middleware
    let perRequestFunctionCallingMiddleware (agent: AIAgent) (context: FunctionInvocationContext) (next: Func<FunctionInvocationContext, CancellationToken, ValueTask<obj>>) (cancellationToken: CancellationToken) =
        printfn $"Agent Id: {agent.Id}"
        printfn $"Function Name: {context.Function.Name} - Per-Request Pre-Invoke"
        task {
            let! result = next.Invoke(context, cancellationToken)
            printfn $"Function Name: {context.Function.Name} - Per-Request Post-Invoke"
            return result
        } |> ValueTask<obj>

    let filterPii (content: string) =
        let piiPatterns = [|
            Regex(@"\b\d{3}-\d{3}-\d{4}\b", RegexOptions.Compiled)
            Regex(@"\b[\w\.-]+@[\w\.-]+\.\w+\b", RegexOptions.Compiled)
            Regex(@"\b[A-Z][a-z]+\s[A-Z][a-z]+\b", RegexOptions.Compiled)
        |]
        let mutable result = content
        for pattern in piiPatterns do
            result <- pattern.Replace(result, "[REDACTED: PII]")
        result

    let filterMessages (messages: IEnumerable<ChatMessage>) =
        messages |> Seq.map (fun m -> ChatMessage(m.Role, filterPii m.Text)) |> Seq.toArray :> IList<ChatMessage>

    // PII Redaction Middleware
    let piiMiddleware messages session options (innerAgent: AIAgent) cancellationToken =
        task {
            let filteredMessages = filterMessages messages
            printfn "Pii Middleware - Filtered Messages Pre-Run"
            let! response = innerAgent.RunAsync(filteredMessages, session, options, cancellationToken)
            response.Messages <- filterMessages response.Messages
            printfn "Pii Middleware - Filtered Messages Post-Run"
            return response
        }

    let filterContent (content: string) =
        let keywords = [| "harmful"; "illegal"; "violence" |]
        if keywords |> Array.exists (fun k -> content.Contains(k, StringComparison.OrdinalIgnoreCase)) then
            "[REDACTED: Forbidden content]"
        else
            content

    let filterGuardrailMessages (messages: IEnumerable<ChatMessage>) =
        messages |> Seq.map (fun m -> ChatMessage(m.Role, filterContent m.Text)) |> Seq.toArray

    // Guardrail Middleware
    // Func<IEnumerable<ChatMessage>, AgentSession, AgentRunOptions, AIAgent, CancellationToken, Task<AgentResponse>>
    let guardrailMiddleware messages session options (innerAgent: AIAgent) cancellationToken =
        task {
            let filteredMessages = filterGuardrailMessages messages
            printfn "Guardrail Middleware - Filtered messages Pre-Run"
            let! response = innerAgent.RunAsync(filteredMessages, session, options, cancellationToken)
            response.Messages <- filterGuardrailMessages response.Messages
            printfn "Guardrail Middleware - Filtered messages Post-Run"
            return response
        }

    // Console Prompting Approval Middleware
    let consolePromptingApprovalMiddleware (messages: ChatMessage seq) session options (innerAgent: AIAgent) cancellationToken =
        task {
            let! response_val = innerAgent.RunAsync(messages, session, options, cancellationToken)
            let mutable response = response_val
            let mutable userInputRequests = getUserRequests response

            while userInputRequests.Length > 0 do
                let responses =
                    userInputRequests
                    |> Array.map (fun functionApprovalRequest ->
                        printfn $"The agent would like to invoke the following function, please reply Y to approve: Name {(functionApprovalRequest.ToolCall :?> FunctionCallContent).Name}"
                        let approved = Console.ReadLine() = "Y"
                        functionApprovalRequest.CreateResponse(approved) :> AIContent
                    )

                if responses.Length > 0 then
                    let nextMessages = [| ChatMessage(ChatRole.User, responses) |]
                    let! nextResponse = innerAgent.RunAsync(nextMessages, session, options, cancellationToken)
                    response <- nextResponse
                    userInputRequests <- getUserRequests response
                else
                    userInputRequests <- [||]
            return response
        }

    // Chat Client Middleware
    let chatClientMiddleware (messages: IEnumerable<ChatMessage>) (options: ChatOptions) (innerChatClient: IChatClient) (cancellationToken: CancellationToken) : Task<ChatResponse> =
        task {
            printfn "Chat Client Middleware - Pre-Chat"
            let! response = innerChatClient.GetResponseAsync(messages, options, cancellationToken)
            printfn "Chat Client Middleware - Post-Chat"
            return response
        }

    // Per-request Chat Client Middleware
    let perRequestChatClientMiddleware (messages: IEnumerable<ChatMessage>) (options: ChatOptions) (innerChatClient: IChatClient) (cancellationToken: CancellationToken) : Task<ChatResponse> =
        task {
            printfn "Per-Request Chat Client Middleware - Pre-Chat"
            let! response = innerChatClient.GetResponseAsync(messages, options, cancellationToken)
            printfn "Per-Request Chat Client Middleware - Post-Chat"
            return response
        }

    let run () =
        use client = Client.ForResponsesAPI(Environment.GetEnvironmentVariable "MODEL_ID")

        // Example 1 & 2 & 3
        let originalAgent =
            let chatClient = client.AddMiddleware(chatClientMiddleware)
            chatClient.CreateAgent(AgentOptions(
                Instructions = "You are an AI assistant that helps people find information.",
                Tools = [|
                    Tool.Get(<@ getDateTime @>, AIFunctionFactoryOptions(Name = nameof getDateTime))
                |],
                CreateRawOptions = (fun _ -> CreateResponseOptions(StoredOutputEnabled = false))
            ))

        let middlewareEnabledAgent: AIAgent =
            originalAgent
                .AddFunctionCallMiddleware(functionCallMiddleware)
                .AddFunctionCallMiddleware(functionCallOverrideWeather)
                .AddRunMiddleware(piiMiddleware)
                .AddRunMiddleware(guardrailMiddleware)

        +task {
            let! session = middlewareEnabledAgent.CreateSessionAsync()
            let run = middlewareEnabledAgent.GetSessionRun(session)
            Console.WriteLine("\n\n=== Example 1: Wording Guardrail ===")
            let! guardRailedResponse = run "Tell me something harmful."
            Console.WriteLine($"Guard railed response: {guardRailedResponse}")

            Console.WriteLine("\n\n=== Example 2: PII detection ===")
            let! piiResponse = run "My name is John Doe, call me at 123-456-7890 or email me at john@something.com"
            Console.WriteLine($"Pii filtered response: {piiResponse}")

            Console.WriteLine("\n\n=== Example 3: Agent function middleware ===")
            let options = ChatClientAgentRunOptions(ChatOptions(Tools = [|
                Tool.Get(<@ getWeather @>, AIFunctionFactoryOptions(Name = nameof getWeather))
            |]))
            let! functionCallResponse = middlewareEnabledAgent.RunAsync("What's the current time and the weather in Seattle?", session, options)
            Console.WriteLine($"Function calling response: {functionCallResponse}")

            Console.WriteLine("\n\n=== Example 4: Per-request middleware with human in the loop function approval ===")
            let optionsWithApproval = ChatClientAgentRunOptions(
                ChatOptions(Tools = [|
                    Tool.Get(<@ getWeather @>, AIFunctionFactoryOptions(Name = nameof getWeather)) |> ApprovalRequiredAIFunction
                |]),
                ChatClientFactory = _.AddMiddleware(perRequestChatClientMiddleware)
            )
            let! response =
                originalAgent
                    .AddFunctionCallMiddleware(perRequestFunctionCallingMiddleware)
                    .AddRunMiddleware(consolePromptingApprovalMiddleware)
                    .RunAsync("What's the current time and the weather in Seattle?", options = optionsWithApproval)
            Console.WriteLine($"Per-request middleware response: {response}")
        }

Target.run()

