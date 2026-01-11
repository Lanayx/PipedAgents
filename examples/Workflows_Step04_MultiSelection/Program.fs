#nowarn "57"

open OpenAI.Responses
open System
open System.ClientModel
open System.ClientModel.Primitives
open System.Text.Json
open System.Text.Json.Serialization
open System.Threading.Tasks
open Microsoft.Agents.AI
open Microsoft.Agents.AI.Workflows
open Microsoft.Extensions.AI
open Shared.Logging
open PipedAgents.MAF
open PipedAgents.MAF.Workflows
open PipedAgents.MAF.OpenAI
open Workflows_Step02_EdgeCondition
open FSharp.Control

/// <summary>
/// Constants for shared state scopes.
/// </summary>
module EmailStateConstants =
    [<Literal>]
    let EmailStateScope = "EmailState"

type SpamDecision =
    | Spam = 0
    | NotSpam = 1
    | Uncertain = 2

[<AllowNullLiteral>]
type AnalysisResult() =
    [<JsonPropertyName("spam_decision")>]
    [<JsonConverter(typeof<JsonStringEnumConverter>)>]
    member val spamDecision = SpamDecision.Uncertain with get, set
    [<JsonPropertyName("reason")>]
    member val Reason = "" with get, set
    [<JsonIgnore>]
    member val EmailId = "" with get, set
    [<JsonIgnore>]
    member val EmailLength = 0 with get, set
    [<JsonIgnore>]
    member val EmailSummary = "" with get, set

[<AllowNullLiteral>]
type Email() =
    [<JsonPropertyName("email_id")>]
    member val EmailId = "" with get, set
    [<JsonPropertyName("email_content")>]
    member val EmailContent = "" with get, set

[<AllowNullLiteral>]
type EmailResponse() =
    [<JsonPropertyName("response")>]
    member val Response = "" with get, set

[<AllowNullLiteral>]
type EmailSummary() =
    [<JsonPropertyName("summary")>]
    member val Summary = "" with get, set

type EmailAnalysisExecutor(emailAnalysisAgent: AIAgent) =
    inherit Executor<ChatMessage, AnalysisResult>("EmailAnalysisExecutor")
    override this.HandleAsync(message: ChatMessage, context: IWorkflowContext, cancellationToken) =
        task {
            let newEmail = Email(EmailId = Guid.NewGuid().ToString("N"), EmailContent = message.Text)
            do! context.QueueStateUpdateAsync(newEmail.EmailId, newEmail,
                scopeName = EmailStateConstants.EmailStateScope, cancellationToken = cancellationToken)
            let! response = emailAnalysisAgent.RunAsync(message.Text, cancellationToken = cancellationToken)
            let AnalysisResult = JsonSerializer.Deserialize<AnalysisResult>(response.Text)
            AnalysisResult.EmailId <- newEmail.EmailId
            AnalysisResult.EmailLength <- newEmail.EmailContent.Length;
            return AnalysisResult
        } |> ValueTask<AnalysisResult>

type EmailAssistantExecutor(emailAssistantAgent: AIAgent) =
    inherit Executor<AnalysisResult, EmailResponse>("EmailAssistantExecutor")
    override this.HandleAsync(message: AnalysisResult, context: IWorkflowContext, cancellationToken) =
        task {
            match message.spamDecision with
            | SpamDecision.NotSpam ->
                let! email = context.ReadStateAsync<Email>(message.EmailId, scopeName = EmailStateConstants.EmailStateScope, cancellationToken = cancellationToken)
                let! response = emailAssistantAgent.RunAsync(email.EmailContent, cancellationToken = cancellationToken)
                return JsonSerializer.Deserialize<EmailResponse>(response.Text)
            | _ ->
                return failwith "This executor should only handle non-spam messages."
        } |> ValueTask<EmailResponse>

type EmailSummaryExecutor(emailSummaryAgent: AIAgent) =
    inherit Executor<AnalysisResult, AnalysisResult>("EmailSummaryExecutor")
    override this.HandleAsync(message: AnalysisResult, context: IWorkflowContext, cancellationToken) =
        task {
            // Read the email content from the shared states
            let! email = context.ReadStateAsync<Email>(message.EmailId, EmailStateConstants.EmailStateScope, cancellationToken)
            // Invoke the agent
            let! response = emailSummaryAgent.RunAsync(email.EmailContent, cancellationToken = cancellationToken);
            let emailSummary = JsonSerializer.Deserialize<EmailSummary>(response.Text)
            message.EmailSummary <- emailSummary.Summary

            return message
        } |> ValueTask<AnalysisResult>

type SendEmailExecutor() =
    inherit Executor<EmailResponse, string>("SendEmailExecutor")
    override this.HandleAsync(message: EmailResponse, context, cancellationToken) =
        $"Email sent: {message.Response}" |> ValueTask<string>

type HandleSpamExecutor() =
    inherit Executor<AnalysisResult, string>("HandleSpamExecutor")
    override this.HandleAsync(message: AnalysisResult, context, cancellationToken) =
        task {
            match message.spamDecision with
            | SpamDecision.Spam ->
                return $"Email marked as spam: {message.Reason}"
            | _ ->
                return failwith "This executor should only handle spam messages."
        } |> ValueTask<string>

type HandleUncertainExecutor() =
    inherit Executor<AnalysisResult, string>("HandleUncertainExecutor")
    override this.HandleAsync(message: AnalysisResult, context, cancellationToken) =
        task {
            if message.spamDecision = SpamDecision.Uncertain then
                let! email = context.ReadStateAsync<Email>(message.EmailId, EmailStateConstants.EmailStateScope, cancellationToken)
                return $"Email marked as uncertain: {message.Reason}. Email content: {email.EmailContent}"
            else
                return failwith "This executor should only handle uncertain messages."
        } |> ValueTask<string>

/// <summary>
/// A custom workflow event for database operations.
/// </summary>
/// <param name="message">The message associated with the event</param>
type DatabaseEvent(message: string) =
    inherit WorkflowEvent(message)

type DatabaseAccessExecutor() =
    inherit Executor<AnalysisResult, unit>("DatabaseAccessExecutor")
    override this.HandleAsync(message: AnalysisResult, context, cancellationToken) =
        task {
            // 1. Save the email content
            let! _  = context.ReadStateAsync<Email>(message.EmailId, EmailStateConstants.EmailStateScope, cancellationToken);
            do! Task.Delay(100, cancellationToken) // Simulate database access delay

            // 2. Save analysis result
            do! Task.Delay(100, cancellationToken) // Simulate processing delay

            // Not using the `WorkflowCompletedEvent` because this is not the end of the workflow.
            // The end of the workflow is signaled by the `SendEmailExecutor` or the `HandleUnknownExecutor`.
            return! context.AddEventAsync(DatabaseEvent($"Email {message.EmailId} saved to database."), cancellationToken)
        } |> ValueTask<unit>

let LongEmailThreshold = 100

module BaseLine =

    /// <summary>
    /// Creates a partitioner for routing messages based on the analysis result.
    /// </summary>
    /// <returns>A function that takes an analysis result and returns the target partitions.</returns>
    let getTargetAssigner () : Func<AnalysisResult, int, int seq> =
        Func<AnalysisResult, int, int seq>(fun analysisResult targetCount ->
            if isNull (box analysisResult) then
                raise (InvalidOperationException("Invalid analysis result."))
            seq {
                match analysisResult.spamDecision with
                | SpamDecision.Spam ->
                    yield 0
                | SpamDecision.NotSpam ->
                    yield 1 // Route to the email assistant
                    if analysisResult.EmailLength > LongEmailThreshold then
                        yield 2 // Route to the email summarizer too
                | _ ->
                    yield 3
            }
        )

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
        let chatClient = client.GetResponsesClient(Environment.GetEnvironmentVariable "MODEL_ID")

        let spamDetectionAgent = chatClient.CreateAIAgent(ChatClientAgentOptions(ChatOptions = ChatOptions(
                                    Instructions = "You are a spam detection assistant that identifies spam emails.",
                                    ResponseFormat = ChatResponseFormat.ForJsonSchema<AnalysisResult>())))
        let emailAssistantAgent = chatClient.CreateAIAgent(ChatClientAgentOptions(ChatOptions = ChatOptions(
                                    Instructions = "You are an email assistant that helps users draft responses to emails with professionalism.",
                                    ResponseFormat = ChatResponseFormat.ForJsonSchema<EmailResponse>())))
        let emailSummaryAgent = chatClient.CreateAIAgent(ChatClientAgentOptions(ChatOptions = ChatOptions(
                                    Instructions = "You are an assistant that helps users summarize emails.",
                                    ResponseFormat = ChatResponseFormat.ForJsonSchema<EmailSummary>())))

        let emailAnalysisExecutor = EmailAnalysisExecutor(spamDetectionAgent).BindExecutor()
        let emailAssistantExecutor = EmailAssistantExecutor(emailAssistantAgent).BindExecutor()
        let emailSummaryExecutor = EmailSummaryExecutor(emailSummaryAgent).BindExecutor()
        let sendEmailExecutor = SendEmailExecutor().BindExecutor()
        let handleSpamExecutor = HandleSpamExecutor().BindExecutor()
        let handleUncertainExecutor = HandleUncertainExecutor().BindExecutor()
        let databaseAccessExecutor = DatabaseAccessExecutor().BindExecutor()

        let workflow =
            WorkflowBuilder(emailAnalysisExecutor)
                .AddFanOutEdge(emailAnalysisExecutor, [|
                    handleSpamExecutor
                    emailAssistantExecutor
                    emailSummaryExecutor
                    handleUncertainExecutor
                |], getTargetAssigner())
                .AddEdge(emailAssistantExecutor, sendEmailExecutor)
                .AddEdge(emailAnalysisExecutor, databaseAccessExecutor,
                    fun (analysisResult: AnalysisResult) -> analysisResult.EmailLength <= LongEmailThreshold)
                // Save the analysis result to the database with summary
                .AddEdge(emailSummaryExecutor, databaseAccessExecutor)
                .WithOutputFrom(handleUncertainExecutor, handleSpamExecutor, sendEmailExecutor)
                .Build()

        +task {
            use! run = InProcessExecution.StreamAsync(workflow, ChatMessage(ChatRole.User, Emails.legitimate))
            let! _ = run.TrySendMessageAsync(TurnToken(emitEvents = true))
            for evt in run.WatchStreamAsync() do
                match evt with
                | :? WorkflowOutputEvent as outputEvent ->
                    Console.WriteLine($"{outputEvent}")
                | :? DatabaseEvent as databaseEvent ->
                    Console.WriteLine($"{databaseEvent}")
                | _ -> ()
        }

module Target =

    let run() =
        use client = Client.ForChatCompletionsAPI(Environment.GetEnvironmentVariable "MODEL_ID")
        let emailAnalysisAgent = client.CreateAgent(AgentOptions(
            Instructions = "You are a spam detection assistant that identifies spam emails.",
            ResponseFormat = ChatResponseFormat.ForJsonSchema<AnalysisResult>()
        ))
        let emailAssistantAgent = client.CreateAgent(AgentOptions(
            Instructions = "You are an email assistant that helps users draft responses to emails with professionalism.",
            ResponseFormat = ChatResponseFormat.ForJsonSchema<EmailResponse>()
        ))
        let summarizerAgent = client.CreateAgent(AgentOptions(
            Instructions = "You are an assistant that helps users summarize emails.",
            ResponseFormat = ChatResponseFormat.ForJsonSchema<EmailSummary>()
        ))
        let emailAnalysisNode = GetNode(EmailAnalysisExecutor(emailAnalysisAgent))
        let emailAssistantNode = GetNode(EmailAssistantExecutor(emailAssistantAgent))
        let emailSummaryNode = GetNode(EmailSummaryExecutor(summarizerAgent))
        let sendEmailNode = GetNode(SendEmailExecutor())
        let handleSpamNode = GetNode(HandleSpamExecutor())
        let handleUncertainNode = GetNode(HandleUncertainExecutor())
        let databaseAccessNode = GetNode(DatabaseAccessExecutor())

        let getTargetAssigner (analysisResult: AnalysisResult) _ =
            if isNull (box analysisResult) then
                raise (InvalidOperationException("Invalid analysis result."))
            seq {
                match analysisResult.spamDecision with
                | SpamDecision.Spam ->
                    yield 0
                | SpamDecision.NotSpam ->
                    yield 1 // Route to the email assistant
                    if analysisResult.EmailLength > LongEmailThreshold then
                        yield 2 // Route to the email summarizer too
                | _ ->
                    yield 3
            }

        let mainWorkflow =
            workflow(emailAnalysisNode) {
                emailAnalysisNode =?>> ([
                    handleSpamNode |> boxOut
                    emailAssistantNode |> boxOut
                    emailSummaryNode |> boxOut
                    handleUncertainNode |> boxOut
                ], getTargetAssigner)
                emailAssistantNode ==> sendEmailNode
                emailAnalysisNode =?> (databaseAccessNode, fun analysisResult ->
                    analysisResult.EmailLength <= LongEmailThreshold)
                emailSummaryNode ==> databaseAccessNode
                return [
                    handleSpamNode |> boxIn
                    sendEmailNode |> boxIn
                    handleUncertainNode |> boxIn
                ]
            }

        +task {
            use! stream = Workflow.Stream(mainWorkflow, ChatMessage(ChatRole.User, Emails.legitimate))
            let! _ = stream.TrySendMessageAsync(TurnToken(emitEvents = true))
            for evt in stream.WatchStreamAsync() do
                match evt with
                | :? WorkflowOutputEvent as outputEvent ->
                    Console.WriteLine outputEvent
                | :? DatabaseEvent as databaseEvent ->
                    Console.WriteLine databaseEvent
                | _ -> ()
        }


Target.run()
