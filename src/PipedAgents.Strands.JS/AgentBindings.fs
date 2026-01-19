namespace PipedAgents.Strands.JS

open System
open Fable.Core
open Fable.Core.JsInterop

/// <summary>
/// Core type definitions for F# Fable bindings to the Strands SDK.
/// These types mirror the TypeScript interfaces while following F# conventions.
/// </summary>
module Types =

    /// <summary>
    /// Message role in a conversation between user and assistant.
    /// </summary>
    type MessageRole =
        /// Human input message
        | User
        /// AI assistant response message
        | Assistant

    /// <summary>
    /// Content block types that can appear in messages.
    /// Represents different types of content including text, tool usage, media, and metadata.
    /// </summary>
    type ContentBlock =
        /// Plain text content
        | TextBlock of text: string
        /// Tool usage request with parameters
        | ToolUseBlock of id: string * name: string * input: obj
        /// Result from tool execution
        | ToolResultBlock of toolUseId: string * status: string * content: obj array * error: exn option
        /// AI reasoning process content (may be redacted)
        | ReasoningBlock of text: string option * signature: string option * redactedContent: byte array option
        /// Cache point marker for prompt caching
        | CachePointBlock of cacheType: string
        /// Content for guardrail evaluation
        | GuardContentBlock of text: obj option * image: obj option
        /// Image content with format and source
        | ImageBlock of format: string * source: obj
        /// Video content with format and source
        | VideoBlock of format: string * source: obj
        /// Document content with format and source
        | DocumentBlock of format: string * source: obj
        /// Structured JSON data
        | JsonBlock of json: obj

    /// <summary>
    /// A message in the conversation history.
    /// Contains the sender role and an array of content blocks.
    /// </summary>
    type Message = {
        /// The role of the message sender (user or assistant)
        Role: MessageRole
        /// Array of content blocks that make up this message
        Content: ContentBlock array
    }

    /// <summary>
    /// Result returned by agent execution containing the final state.
    /// </summary>
    type AgentResult = {
        /// Reason why the model stopped generating (e.g., "endTurn", "toolUse")
        StopReason: string
        /// The last message added to the conversation
        LastMessage: Message
        /// Complete conversation history
        Messages: Message array
        /// Final agent state
        State: obj
    }

    /// <summary>
    /// Stream events that can be emitted during agent execution.
    /// Provides real-time updates on agent processing steps.
    /// </summary>
    type AgentStreamEvent =
        /// A new message was added to the conversation
        | MessageAdded of Message
        /// Incremental content update during streaming
        | ContentBlockDelta of delta: obj
        /// Tool execution started
        | ToolExecutionStarted of toolName: string
        /// Tool execution completed
        | ToolExecutionCompleted of result: obj
        /// Model streaming event
        | ModelStreamEvent of event: obj
        /// Before agent invocation hook event
        | BeforeInvocation of event: obj
        /// After agent invocation hook event
        | AfterInvocation of event: obj
        /// Before model call hook event
        | BeforeModelCall of event: obj
        /// After model call hook event
        | AfterModelCall of event: obj
        /// Before tools execution hook event
        | BeforeToolsExecution of event: obj
        /// After tools execution hook event
        | AfterToolsExecution of event: obj
        /// Before individual tool call hook event
        | BeforeToolCall of event: obj
        /// After individual tool call hook event
        | AfterToolCall of event: obj
        /// Final agent result event
        | AgentResultEvent of AgentResult

    /// <summary>
    /// Custom exception for Strands SDK errors with context information.
    /// </summary>
    exception StrandsSDKException of message: string * innerException: exn option

/// <summary>
/// Internal configuration conversion utilities.
/// </summary>
module internal InternalConversion =
    open Types

    let messageRoleToJS (role: MessageRole): string =
        match role with
        | User -> "user"
        | Assistant -> "assistant"

    let contentBlockToJS (contentBlock: ContentBlock): obj =
        match contentBlock with
        | TextBlock text -> !!{| ``type`` = "text"; text = text |}
        | ToolUseBlock (id, name, input) -> !!{| ``type`` = "toolUse"; id = id; name = name; input = input |}
        | ToolResultBlock (toolUseId, status, content, error) ->
            let baseObj = {| ``type`` = "toolResult"; toolUseId = toolUseId; status = status; content = content |}
            match error with
            | Some err -> !!{| baseObj with error = err |}
            | None -> !!baseObj
        | ReasoningBlock (text, signature, redactedContent) ->
            let mutable jsObj = !!{| ``type`` = "reasoning" |}
            match text with Some t -> jsObj?text <- t | None -> ()
            match signature with Some s -> jsObj?signature <- s | None -> ()
            match redactedContent with Some rc -> jsObj?redactedContent <- rc | None -> ()
            jsObj
        | CachePointBlock cacheType -> !!{| ``type`` = "cachePoint"; cacheType = cacheType |}
        | GuardContentBlock (text, image) ->
            let mutable jsObj = !!{| ``type`` = "guardContent" |}
            match text with Some t -> jsObj?text <- t | None -> ()
            match image with Some i -> jsObj?image <- i | None -> ()
            jsObj
        | ImageBlock (format, source) -> !!{| ``type`` = "image"; format = format; source = source |}
        | VideoBlock (format, source) -> !!{| ``type`` = "video"; format = format; source = source |}
        | DocumentBlock (format, source) -> !!{| ``type`` = "document"; format = format; source = source |}
        | JsonBlock json -> !!{| ``type`` = "json"; json = json |}

    let messageToJS (message: Message): obj =
        !!{|
            role = messageRoleToJS message.Role
            content = (message.Content |> Array.map contentBlockToJS)
        |}

open Types
open InternalConversion

/// <summary>
/// Agent configuration options.
/// Directly wraps a JavaScript object for efficient interop with the Strands SDK.
/// </summary>
type AgentOptions() =
    let jsObj = !!{| printer = false |}
    
    /// The model instance or string model ID to use
    member this.Model with set (value: obj) = jsObj?model <- value
    /// Initial conversation history
    member this.Messages with set (value: Message array) = jsObj?messages <- (value |> Array.map messageToJS)
    /// Tools available to the agent (nested arrays are flattened)
    member this.Tools with set (value: obj array) = jsObj?tools <- value
    /// System prompt to guide agent behavior
    member this.SystemPrompt with set (value: string) = jsObj?systemPrompt <- value
    /// Initial state values for the agent
    member this.State with set (value: obj) = jsObj?state <- obj
    /// Enable automatic console output printing
    member this.Printer with set (value: bool) = jsObj?printer <- value
    /// Conversation manager for handling message history
    member this.ConversationManager with set (value: obj) = jsObj?conversationManager <- value
    /// Hook providers for extending agent behavior
    member this.Hooks with set (value: obj array) = jsObj?hooks <- value

    member internal this.ToJS() = jsObj

/// <summary>
/// OpenAI client configuration options.
/// Directly wraps a JavaScript object for efficient interop with the OpenAI SDK.
/// </summary>
type OpenAIClientOptions() =
    let jsObj = !!{| |}
    let getClientConfig () =
        if isNull jsObj?clientConfig then
            jsObj?clientConfig <- !!{| |}
        jsObj?clientConfig
    /// API key (falls back to environment variable if not provided)
    member this.ApiKey with set (value: string) = jsObj?apiKey <- value
    /// Sampling temperature (0.0 to 2.0)
    member this.Temperature with set (value: Nullable<float>) = if value.HasValue then jsObj?temperature <- value.Value
    /// Maximum tokens to generate
    member this.MaxTokens with set (value: Nullable<int>) = if value.HasValue then jsObj?maxTokens <- value.Value
    /// Top-p sampling parameter
    member this.TopP with set (value: Nullable<float>) = if value.HasValue then jsObj?topP <- value.Value
    /// Frequency penalty (-2.0 to 2.0)
    member this.FrequencyPenalty with set (value: Nullable<float>) = if value.HasValue then jsObj?frequencyPenalty <- value.Value
    /// Presence penalty (-2.0 to 2.0)
    member this.PresencePenalty with set (value: Nullable<float>) = if value.HasValue then jsObj?presencePenalty <- value.Value
    /// Custom base URL for API requests
    member this.BaseURL with set (value: string) = (getClientConfig())?baseURL <- value
    /// Request timeout in milliseconds
    member this.Timeout with set (value: Nullable<int>) = if value.HasValue then (getClientConfig())?timeout <- value.Value

    member internal this.ToJS() = jsObj

/// <summary>
/// Low-level JavaScript interop bindings for the Strands SDK.
/// These bindings use Fable's [<Import>] attributes to access the JavaScript SDK directly.
/// </summary>
module Interop =

    /// <summary>
    /// Low-level JavaScript binding for the Agent class from @strands-agents/sdk.
    /// This type provides direct access to the JavaScript Agent constructor and methods.
    /// </summary>
    [<Import("Agent", "@strands-agents/sdk")>]
    type JSAgent(config: obj) =
        /// <summary>
        /// Invokes the agent with the provided input and returns a Promise of the result.
        /// </summary>
        /// <param name="args">Input arguments - can be string, ContentBlock[], or Message[]</param>
        /// <returns>Promise that resolves to AgentResult</returns>
        member _.invoke(args: obj): JS.Promise<obj> = jsNative
        
        /// <summary>
        /// Streams the agent execution, yielding events and returning the final result.
        /// </summary>
        /// <param name="args">Input arguments - can be string, ContentBlock[], or Message[]</param>
        /// <returns>AsyncGenerator that yields AgentStreamEvent objects</returns>
        member _.stream(args: obj): obj = jsNative

    /// <summary>
    /// Low-level JavaScript binding for the OpenAIModel class from @strands-agents/sdk/openai.
    /// This type provides direct access to the JavaScript OpenAIModel constructor and methods.
    /// </summary>
    [<Import("OpenAIModel", "@strands-agents/sdk/openai")>]
    type JSOpenAIModel(config: obj) =
        /// <summary>
        /// Updates the model configuration by merging with existing settings.
        /// </summary>
        /// <param name="modelConfig">Configuration object with model-specific settings to update</param>
        member _.updateConfig(modelConfig: obj): unit = jsNative
        
        /// <summary>
        /// Retrieves the current model configuration.
        /// </summary>
        /// <returns>The current configuration object</returns>
        member _.getConfig(): obj = jsNative
