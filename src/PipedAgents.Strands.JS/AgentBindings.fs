namespace PipedAgents.Strands.JS

open System
open Fable.Core
open Fable.Core.JsInterop

type [<AllowNullLiteral>] AsyncIterable<'T> =
    inherit JS.AsyncIterable
    [<Emit("$0[Symbol.asyncIterator]()")>]
    abstract asyncIterator: unit -> AsyncIterator<'T>

and [<AllowNullLiteral>] IteratorResult<'T> =
    abstract value: 'T with get, set
    abstract ``done``: bool with get, set

and [<AllowNullLiteral>] AsyncIterator<'T> =
    abstract next: unit -> JS.Promise<IteratorResult<'T>>
    abstract ``return``: value: 'T -> JS.Promise<IteratorResult<'T>>
    abstract throw: e: obj -> JS.Promise<IteratorResult<'T>>

and [<AllowNullLiteral>] AsyncGenerator<'T> =
    inherit AsyncIterator<'T>
    inherit AsyncIterable<'T>

/// <summary>
/// Core type definitions for F# Fable bindings to the Strands SDK.
/// These types mirror the TypeScript interfaces while following F# conventions.
/// </summary>
module Types =

    /// <summary>
    /// Message role in a conversation between user and assistant.
    /// </summary>
    [<StringEnum>]
    type Role =
        /// Human input message
        | User
        /// AI assistant response message
        | Assistant

    /// <summary>
    /// Content block types that can appear in messages.
    /// Represents different types of content including text, tool usage, media, and metadata.
    /// </summary>
    [<TypeScriptTaggedUnion("type")>]
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

    [<TypeScriptTaggedUnion("type")>]
    type ContentBlockDelta =
        | TextDelta of text: string
        | ToolUseDelta of input: string
        | ReasoningDelta of text: string option * signature: string option * redactedContent: byte array option

    /// <summary>
    /// A message in the conversation history.
    /// Contains the sender role and an array of content blocks.
    /// </summary>
    type Message = {
        /// The role of the message sender (user or assistant)
        Role: Role
        /// Array of content blocks that make up this message
        Content: ContentBlock array
    }

    type Usage =
        /// Number of input tokens processed
        abstract inputTokens: int
        /// Number of output tokens generated
        abstract outputTokens: int
        /// Total number of tokens (input + output)
        abstract totalTokens: int
        /// Number of cache read input tokens (optional)
        abstract cacheReadInputTokens: int option
        /// Number of cache write input tokens (optional)
        abstract cacheWriteInputTokens: int option

    type Metrics =
        /// Latency in milliseconds.
        abstract latencyMs: int

    type AgentData =
        /// Agent state storage accessible to tools and application logic.
        abstract state: obj
        /// The conversation history of messages between user and assistant.
        abstract messages: Message array

    /// <summary>
    /// Stream events that can be emitted during agent execution.
    /// Provides real-time updates on agent processing steps.
    /// </summary>
    [<TypeScriptTaggedUnion("type")>]
    type AgentStreamEvent =
        /// Model streaming event
        | ModelMessageStartEvent of role: Role
        | ModelContentBlockStartEvent of start: obj
        | ModelContentBlockDeltaEvent of delta: ContentBlockDelta
        | ModelContentBlockStopEvent
        | ModelMessageStopEvent of stopReason: string * additionalModelResponseFields: obj
        | ModelMetadataEvent of usage: Usage * metrics: Metrics * trace : obj
        /// Content block
        | TextBlock of text: string
        | ToolUseBlock of id: string * name: string * input: obj
        | ToolResultBlock of toolUseId: string * status: string * content: obj array * error: exn option
        | ReasoningBlock of text: string option * signature: string option * redactedContent: byte array option
        | CachePointBlock of cacheType: string
        | GuardContentBlock of text: obj option * image: obj option
        | ImageBlock of format: string * source: obj
        | VideoBlock of format: string * source: obj
        | DocumentBlock of format: string * source: obj
        | JsonBlock of json: obj
        /// Tool stream event
        | ToolStreamEvent of obj
        /// BeforeInvocation event
        | BeforeInvocationEvent of agent: AgentData
        /// AfterInvocation event
        | AfterInvocationEvent of agent: AgentData
        /// BeforeModelCall event
        | BeforeModelCallEvent of agent: AgentData
        /// AfterModelCall event
        | AfterModelCallEvent of agent: AgentData
        /// BeforeToolsExecution event
        | BeforeToolsEvent of agent: AgentData * message: Message
        /// AfterToolsExecution event
        | AfterToolsEvent of agent: AgentData * message: Message
        /// BeforeToolCall event
        | BeforeToolCallEvent of agent: AgentData * toolUse: {| name: string; toolUseId: string; input: obj|} * tool: obj
        /// AfterToolCall event
        | AfterToolCallEvent of agent: AgentData * toolUse: {| name: string; toolUseId: string; input: obj|} * tool: obj * result: obj * error: obj option
        /// Message added event
        | MessageAddedEvent of agent: AgentData * message: Message
        /// ModelStreamEventHook
        | ModelStreamEventHook of agent: AgentData * event: obj
        /// Final agent result event
        | AgentResult of stopReason: string * lastMessage: Message


open Types

/// <summary>
/// Agent configuration options.
/// Directly wraps a JavaScript object for efficient interop with the Strands SDK.
/// </summary>
type AgentOptions() =
    let jsObj = !!{| printer = false |}
    
    /// The model instance or string model ID to use
    member this.Model with set (value: obj) = jsObj?model <- value
    /// Initial conversation history
    member this.Messages with set (value: Message array) = jsObj?messages <- value
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
        member _.stream(args: obj): AsyncGenerator<'T> = jsNative

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
