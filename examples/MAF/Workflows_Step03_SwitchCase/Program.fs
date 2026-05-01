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
type DetectionResult() =
    [<JsonPropertyName("spam_decision")>]
    [<JsonConverter(typeof<JsonStringEnumConverter>)>]
    member val spamDecision = SpamDecision.Uncertain with get, set
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
                match message.spamDecision with
                | SpamDecision.NotSpam ->
                    let! email = context.ReadStateAsync<Email>(message.EmailId, scopeName = EmailStateConstants.EmailStateScope, cancellationToken = cancellationToken)
                    let! response = emailAssistantAgent.RunAsync(email.EmailContent, cancellationToken = cancellationToken)
                    return JsonSerializer.Deserialize<EmailResponse>(response.Text)
                | _ ->
                    return failwith "This executor should only handle non-spam messages."
            } |> ValueTask<EmailResponse>

    type SendEmailExecutor() =
        inherit Executor<EmailResponse>("SendEmailExecutor")
        override this.HandleAsync(message: EmailResponse, context, cancellationToken) =
            context.YieldOutputAsync($"Email sent: {message.Response}", cancellationToken)

    type HandleSpamExecutor() =
        inherit Executor<DetectionResult>("HandleSpamExecutor")
        override this.HandleAsync(message: DetectionResult, context, cancellationToken) =
            task {
                match message.spamDecision with
                | SpamDecision.Spam ->
                    do! context.YieldOutputAsync($"Email marked as spam: {message.Reason}", cancellationToken)
                | _ ->
                    failwith "This executor should only handle spam messages."
            } |> ValueTask

    type HandleUncertainExecutor() =
        inherit Executor<DetectionResult>("HandleUncertainExecutor")
        override this.HandleAsync(message: DetectionResult, context, cancellationToken) =
            task {
                if message.spamDecision = SpamDecision.Uncertain then
                    let! email = context.ReadStateAsync<Email>(message.EmailId, EmailStateConstants.EmailStateScope, cancellationToken)
                    do! context.YieldOutputAsync($"Email marked as uncertain: {message.Reason}. Email content: {email.EmailContent}", cancellationToken)
                else
                    failwith "This executor should only handle uncertain messages."
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
        let model = Environment.GetEnvironmentVariable "MODEL_ID"
        let chatClient = client.GetResponsesClient()

        let spamDetectionAgent = chatClient.AsAIAgent(ChatClientAgentOptions(ChatOptions = ChatOptions(
                                    Instructions = "You are a spam detection assistant that identifies spam emails.",
                                    ResponseFormat = ChatResponseFormat.ForJsonSchema<DetectionResult>())), model = model)
        let emailAssistantAgent = chatClient.AsAIAgent(ChatClientAgentOptions(ChatOptions = ChatOptions(
                                    Instructions = "You are an email assistant that helps users draft responses to emails with professionalism.",
                                    ResponseFormat = ChatResponseFormat.ForJsonSchema<EmailResponse>())), model = model)

        let spamDetectionExecutor = SpamDetectionExecutor(spamDetectionAgent).BindExecutor()
        let emailAssistantExecutor = EmailAssistantExecutor(emailAssistantAgent).BindExecutor()
        let sendEmailExecutor = SendEmailExecutor().BindExecutor()
        let handleSpamExecutor = HandleSpamExecutor().BindExecutor()
        let handleUncertainExecutor = HandleUncertainExecutor().BindExecutor()

        let getCondition expectedResult =
            Func<obj, bool>(fun detectionResult ->
                match detectionResult with
                | :? DetectionResult as result -> result.spamDecision = expectedResult
                | _ -> false)

        let workflow =
            WorkflowBuilder(spamDetectionExecutor)
                .AddSwitch(spamDetectionExecutor, (fun switchBuilder ->
                    switchBuilder
                        .AddCase(getCondition SpamDecision.NotSpam, [ emailAssistantExecutor ])
                        .AddCase(getCondition SpamDecision.Spam, [ handleSpamExecutor ])
                        .WithDefault([ handleUncertainExecutor ])
                        |> ignore
                ))
                .AddEdge(emailAssistantExecutor, sendEmailExecutor)
                .WithOutputFrom(handleSpamExecutor, sendEmailExecutor, handleUncertainExecutor)
                .Build()

        +task {
            use! run = InProcessExecution.RunStreamingAsync(workflow, ChatMessage(ChatRole.User, Emails.ambigious))
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
                if message.spamDecision <> SpamDecision.NotSpam then
                    failwith "This executor should only handle non-spam messages."
                let! email = context.ReadStateAsync<Email>(message.EmailId, scopeName = EmailStateConstants.EmailStateScope, cancellationToken = cancellationToken)
                if email |> isNull then failwith "Email not found."
                let! response = emailAssistantAgent.RunAsync(email.EmailContent, cancellationToken = cancellationToken)
                return JsonSerializer.Deserialize<EmailResponse>(response.Text)
            } |> ValueTask<EmailResponse>

    type HandleUncertainExecutor() =
        inherit Executor<DetectionResult, string>("HandleUncertainExecutor")
        override this.HandleAsync(message: DetectionResult, context, cancellationToken) =
            task {
                if message.spamDecision = SpamDecision.Uncertain then
                    let! email = context.ReadStateAsync<Email>(message.EmailId, EmailStateConstants.EmailStateScope, cancellationToken)
                    return $"Email marked as uncertain: {message.Reason}. Email content: {email.EmailContent}"
                else
                    return failwith "This executor should only handle uncertain messages."
            } |> ValueTask<string>

    let sendEmailExecutor (message: EmailResponse) =
        $"Email sent: {message.Response}"

    let handleSpamExecutor (message: DetectionResult) =
        if message.spamDecision = SpamDecision.Spam then
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
        let handleUncertainNode = GetNode(HandleUncertainExecutor())
        let getCondition expectedResult (detectionResult: DetectionResult) =
            detectionResult.spamDecision = expectedResult

        let mainWorkflow =
            workflow(spamDetectionNode) {
                spamDetectionNode =|> [
                    case (getCondition SpamDecision.NotSpam) emailAssistantNode
                    case (getCondition SpamDecision.Spam) handleSpamNode
                    defaultCase handleUncertainNode
                ]
                emailAssistantNode ==> sendEmailNode
                return handleSpamNode
                return sendEmailNode
                return handleUncertainNode
            }

        +task {
            use! run = Workflow.Stream(mainWorkflow, ChatMessage(ChatRole.User, Emails.spam))
            let! _ = run.TrySendMessageAsync(TurnToken(emitEvents = true))
            for evt in run.WatchStreamAsync() do
                match evt with
                | :? WorkflowOutputEvent as outputEvent ->
                    Console.WriteLine($"{outputEvent}")
                | _ -> ()
        }


Target.run()
