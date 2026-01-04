#nowarn "57"

open OpenAI.Chat
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
open PipedAgents.MAF.OpenAI
open Workflows_Step02_EdgeCondition
open FSharp.Control

/// <summary>
/// Constants for shared state scopes.
/// </summary>
module EmailStateConstants =
    [<Literal>]
    let EmailStateScope = "EmailState"

[<AllowNullLiteral>]
type DetectionResult() =
    [<JsonPropertyName("is_spam")>]
    member val IsSpam = false with get, set
    [<JsonPropertyName("reason")>]
    member val Reason = "" with get, set
    [<JsonIgnore>]
    member val EmailId = "" with get, set

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

module BaseLine =

    type SpamDetectionExecutor(spamDetectionAgent: AIAgent) =
        inherit Executor<ChatMessage, DetectionResult>("SpamDetectionExecutor")
        override this.HandleAsync(message: ChatMessage, context: IWorkflowContext, cancellationToken) =
            task {
                let newEmail = Email(EmailId = Guid.NewGuid().ToString("N"), EmailContent = message.Text)
                do! context.QueueStateUpdateAsync(newEmail.EmailId, newEmail, scopeName = EmailStateConstants.EmailStateScope, cancellationToken = cancellationToken).AsTask()
                let! response = spamDetectionAgent.RunAsync(message.Text, cancellationToken = cancellationToken)
                let detectionResult = JsonSerializer.Deserialize<DetectionResult>(response.Text)
                detectionResult.EmailId <- newEmail.EmailId
                return detectionResult
            } |> ValueTask<DetectionResult>

    type EmailAssistantExecutor(emailAssistantAgent: AIAgent) =
        inherit Executor<DetectionResult, EmailResponse>("EmailAssistantExecutor")
        override this.HandleAsync(message: DetectionResult, context: IWorkflowContext, cancellationToken) =
            task {
                if message.IsSpam then failwith "This executor should only handle non-spam messages."
                let! email = context.ReadStateAsync<Email>(message.EmailId, scopeName = EmailStateConstants.EmailStateScope, cancellationToken = cancellationToken)
                if email |> isNull then failwith "Email not found."
                let! response = emailAssistantAgent.RunAsync(email.EmailContent, cancellationToken = cancellationToken)
                return JsonSerializer.Deserialize<EmailResponse>(response.Text)
            } |> ValueTask<EmailResponse>

    type SendEmailExecutor() =
        inherit Executor<EmailResponse>("SendEmailExecutor")
        override this.HandleAsync(message: EmailResponse, context, cancellationToken) =
            context.YieldOutputAsync($"Email sent: {message.Response}", cancellationToken)

    type HandleSpamExecutor() =
        inherit Executor<DetectionResult>("HandleSpamExecutor")
        override this.HandleAsync(message: DetectionResult, context, cancellationToken) =
            task {
                if message.IsSpam then
                    do! context.YieldOutputAsync($"Email marked as spam: {message.Reason}", cancellationToken)
                else
                    failwith "This executor should only handle spam messages."
            } |> ValueTask

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
        let chatClient = client.GetChatClient(Environment.GetEnvironmentVariable "MODEL_ID")

        let spamDetectionAgent = chatClient.CreateAIAgent(ChatClientAgentOptions(ChatOptions = ChatOptions(
                                    Instructions = "You are a spam detection assistant that identifies spam emails.",
                                    ResponseFormat = ChatResponseFormat.ForJsonSchema<DetectionResult>())))
        let emailAssistantAgent = chatClient.CreateAIAgent(ChatClientAgentOptions(ChatOptions = ChatOptions(
                                    Instructions = "You are an email assistant that helps users draft responses to emails with professionalism.",
                                    ResponseFormat = ChatResponseFormat.ForJsonSchema<EmailResponse>())))

        let spamDetectionExecutor = SpamDetectionExecutor(spamDetectionAgent).BindExecutor()
        let emailAssistantExecutor = EmailAssistantExecutor(emailAssistantAgent).BindExecutor()
        let sendEmailExecutor = SendEmailExecutor().BindExecutor()
        let handleSpamExecutor = HandleSpamExecutor().BindExecutor()

        let getCondition expectedResult =
            Func<obj, bool>(fun detectionResult ->
                match detectionResult with
                | :? DetectionResult as result -> result.IsSpam = expectedResult
                | _ -> false)

        let workflow =
            WorkflowBuilder(spamDetectionExecutor)
                .AddEdge(spamDetectionExecutor, emailAssistantExecutor, condition = getCondition false)
                .AddEdge(emailAssistantExecutor, sendEmailExecutor)
                .AddEdge(spamDetectionExecutor, handleSpamExecutor, condition = getCondition true)
                .WithOutputFrom(handleSpamExecutor, sendEmailExecutor)
                .Build()

        +task {
            use! run = InProcessExecution.StreamAsync(workflow, ChatMessage(ChatRole.User, Emails.legitimate))
            let! _ = run.TrySendMessageAsync(TurnToken(emitEvents = true))
            for evt in run.WatchStreamAsync() do
                match evt with
                | :? WorkflowOutputEvent as outputEvent ->
                    Console.WriteLine($"{outputEvent}")
                | _ -> ()
        }

module Target =
    type SpamDetectionExecutor(spamDetectionAgent: AIAgent) =
        inherit Executor<ChatMessage, DetectionResult>("SpamDetectionExecutor")
        override this.HandleAsync(message: ChatMessage, context: IWorkflowContext, cancellationToken) =
            task {
                let newEmail = Email(EmailId = Guid.NewGuid().ToString("N"), EmailContent = message.Text)
                do! context.QueueStateUpdateAsync(newEmail.EmailId, newEmail, scopeName = EmailStateConstants.EmailStateScope, cancellationToken = cancellationToken).AsTask()
                let! response = spamDetectionAgent.RunAsync(message.Text, cancellationToken = cancellationToken)
                let detectionResult = JsonSerializer.Deserialize<DetectionResult>(response.Text)
                detectionResult.EmailId <- newEmail.EmailId
                return detectionResult
            } |> ValueTask<DetectionResult>

    type EmailAssistantExecutor(emailAssistantAgent: AIAgent) =
        inherit Executor<DetectionResult, EmailResponse>("EmailAssistantExecutor")
        override this.HandleAsync(message: DetectionResult, context: IWorkflowContext, cancellationToken) =
            task {
                if message.IsSpam then failwith "This executor should only handle non-spam messages."
                let! email = context.ReadStateAsync<Email>(message.EmailId, scopeName = EmailStateConstants.EmailStateScope, cancellationToken = cancellationToken)
                if email |> isNull then failwith "Email not found."
                let! response = emailAssistantAgent.RunAsync(email.EmailContent, cancellationToken = cancellationToken)
                return JsonSerializer.Deserialize<EmailResponse>(response.Text)
            } |> ValueTask<EmailResponse>

    let sendEmailExecutor (message: EmailResponse) =
        $"Email sent: {message.Response}"

    let handleSpamExecutor (message: DetectionResult) =
        if message.IsSpam then
            $"Email marked as spam: {message.Reason}"
        else
            failwith "This executor should only handle spam messages."

    let run() =
        use client = Client.ForChatCompletionsAPI(Environment.GetEnvironmentVariable "MODEL_ID")
        let spamDetectionAgent = client.CreateAgent(AgentOptions(
            Instructions = "You are a spam detection assistant that identifies spam emails.",
            ResponseFormat = ChatResponseFormat.ForJsonSchema<DetectionResult>()
        ))
        let emailAssistantAgent = client.CreateAgent(AgentOptions(
            Instructions = "You are an email assistant that helps users draft responses to emails with professionalism.",
            ResponseFormat = ChatResponseFormat.ForJsonSchema<EmailResponse>()
        ))
        let spamDetectionNode = GetNode(SpamDetectionExecutor(spamDetectionAgent))
        let emailAssistantNode = GetNode(EmailAssistantExecutor(emailAssistantAgent))
        let sendEmailNode = GetNode(sendEmailExecutor, "SendEmailExecutor")
        let handleSpamNode = GetNode(handleSpamExecutor, "HandleSpamExecutor")
        let spamCondition expectedResult (detectionResult: DetectionResult) =
            detectionResult.IsSpam = expectedResult

        let workflow =
            Workflow(spamDetectionNode) {
                spamDetectionNode =?> (emailAssistantNode, spamCondition false)
                spamDetectionNode =?> (handleSpamNode, spamCondition true)
                emailAssistantNode ==> sendEmailNode
                return handleSpamNode
                return sendEmailNode
            }

        +task {
            use! run = InProcessExecution.StreamAsync(workflow, ChatMessage(ChatRole.User, Emails.spam))
            let! _ = run.TrySendMessageAsync(TurnToken(emitEvents = true))
            for evt in run.WatchStreamAsync() do
                match evt with
                | :? WorkflowOutputEvent as outputEvent ->
                    Console.WriteLine($"{outputEvent}")
                | _ -> ()
        }

Target.run()
