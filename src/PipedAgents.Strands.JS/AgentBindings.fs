namespace PipedAgents.Strands.JS

open System
open Fable.Core
open Fable.Core.JS

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
        | ToolResultBlock of toolUseId: string * status: string * content: obj list * error: exn option
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
        Content: ContentBlock list
    }

    /// <summary>
    /// Agent configuration record with F# Option types for optional parameters.
    /// Provides type-safe configuration for creating Strands agents.
    /// </summary>
    type AgentConfig = {
        /// The model instance or string model ID to use
        Model: obj option
        /// Initial conversation history
        Messages: Message list option
        /// Tools available to the agent (nested arrays are flattened)
        Tools: obj list option
        /// System prompt to guide agent behavior
        SystemPrompt: string option
        /// Initial state values for the agent
        State: Map<string, obj> option
        /// Enable automatic console output printing
        Printer: bool option
        /// Conversation manager for handling message history
        ConversationManager: obj option
        /// Hook providers for extending agent behavior
        Hooks: obj list option
    }

    /// <summary>
    /// OpenAI model configuration with type-safe optional parameters.
    /// </summary>
    type OpenAIModelConfig = {
        /// Required model identifier (e.g., "gpt-4")
        ModelId: string
        /// API key (falls back to environment variable if not provided)
        ApiKey: string option
        /// Sampling temperature (0.0 to 2.0)
        Temperature: float option
        /// Maximum tokens to generate
        MaxTokens: int option
        /// Top-p sampling parameter
        TopP: float option
        /// Frequency penalty (-2.0 to 2.0)
        FrequencyPenalty: float option
        /// Presence penalty (-2.0 to 2.0)
        PresencePenalty: float option
        /// Additional client configuration options
        ClientConfig: obj option
    }

    /// <summary>
    /// Client configuration for OpenAI API connections.
    /// </summary>
    type ClientConfig = {
        /// Custom base URL for API requests
        BaseURL: string option
        /// Request timeout in milliseconds
        Timeout: int option
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
        Messages: Message list
        /// Final agent state
        State: Map<string, obj>
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
        member _.invoke(args: obj): Promise<obj> = jsNative
        
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
        
        /// <summary>
        /// Streams a conversation with the OpenAI model.
        /// </summary>
        /// <param name="messages">Array of conversation messages</param>
        /// <param name="options">Optional streaming configuration</param>
        /// <returns>AsyncIterable of streaming events</returns>
        member _.stream(messages: obj, options: obj): obj = jsNative

/// <summary>
/// Configuration conversion functions for transforming F# types to JavaScript objects.
/// These functions handle the conversion between F# records with Option types and
/// JavaScript objects with optional properties.
/// </summary>
module ConfigConversion =
    
    open Types
    open Fable.Core.JsInterop
    
    // Create an alias for the Map module to avoid conflicts with JavaScript Map
    module FSharpMap = Microsoft.FSharp.Collections.Map
    
    /// <summary>
    /// Converts an F# Map to a JavaScript object.
    /// Note: This is a simplified implementation that returns an empty object.
    /// The Map iteration issue will be resolved in a future iteration.
    /// </summary>
    /// <param name="map">F# Map to convert</param>
    /// <returns>JavaScript object with the map's key-value pairs</returns>
    let private mapToJS (map: Map<string, obj>): obj =
        // Simplified implementation - returns empty object for now
        // TODO: Implement proper Map to JS conversion
        !!{| |}
    
    /// <summary>
    /// Converts F# MessageRole to JavaScript string representation.
    /// </summary>
    /// <param name="role">F# MessageRole discriminated union</param>
    /// <returns>JavaScript string representation of the role</returns>
    let private messageRoleToJS (role: MessageRole): string =
        match role with
        | User -> "user"
        | Assistant -> "assistant"
    
    /// <summary>
    /// Converts F# ContentBlock to JavaScript object representation.
    /// Handles all content block types and their specific properties.
    /// </summary>
    /// <param name="contentBlock">F# ContentBlock discriminated union</param>
    /// <returns>JavaScript object representation of the content block</returns>
    let private contentBlockToJS (contentBlock: ContentBlock): obj =
        match contentBlock with
        | TextBlock text -> 
            !!{|
                ``type`` = "text"
                text = text
            |}
        | ToolUseBlock (id, name, input) ->
            !!{|
                ``type`` = "toolUse"
                id = id
                name = name
                input = input
            |}
        | ToolResultBlock (toolUseId, status, content, error) ->
            let baseObj = {|
                ``type`` = "toolResult"
                toolUseId = toolUseId
                status = status
                content = content
            |}
            match error with
            | Some err -> 
                !!{| baseObj with error = err |}
            | None -> !!baseObj
        | ReasoningBlock (text, signature, redactedContent) ->
            let mutable jsObj = !!{| ``type`` = "reasoning" |}
            match text with Some t -> jsObj?text <- t | None -> ()
            match signature with Some s -> jsObj?signature <- s | None -> ()
            match redactedContent with Some rc -> jsObj?redactedContent <- rc | None -> ()
            jsObj
        | CachePointBlock cacheType ->
            !!{|
                ``type`` = "cachePoint"
                cacheType = cacheType
            |}
        | GuardContentBlock (text, image) ->
            let mutable jsObj = !!{| ``type`` = "guardContent" |}
            match text with Some t -> jsObj?text <- t | None -> ()
            match image with Some i -> jsObj?image <- i | None -> ()
            jsObj
        | ImageBlock (format, source) ->
            !!{|
                ``type`` = "image"
                format = format
                source = source
            |}
        | VideoBlock (format, source) ->
            !!{|
                ``type`` = "video"
                format = format
                source = source
            |}
        | DocumentBlock (format, source) ->
            !!{|
                ``type`` = "document"
                format = format
                source = source
            |}
        | JsonBlock json ->
            !!{|
                ``type`` = "json"
                json = json
            |}
    
    /// <summary>
    /// Converts F# Message to JavaScript object representation.
    /// </summary>
    /// <param name="message">F# Message record</param>
    /// <returns>JavaScript object representation of the message</returns>
    let private messageToJS (message: Message): obj =
        !!{|
            role = messageRoleToJS message.Role
            content = (message.Content |> List.map contentBlockToJS |> List.toArray)
        |}
    
    /// <summary>
    /// Flattens nested tool arrays recursively.
    /// The Strands SDK accepts nested arrays of tools and flattens them automatically.
    /// This function replicates that behavior for type safety.
    /// </summary>
    /// <param name="tools">List of tool objects that may contain nested arrays</param>
    /// <returns>Flattened array of tool objects</returns>
    let private flattenTools (tools: obj list): obj array =
        let rec flatten (item: obj): obj list =
            match item with
            | :? (obj array) as arr -> arr |> List.ofArray |> List.collect flatten
            | :? (obj list) as lst -> lst |> List.collect flatten
            | tool -> [tool]
        
        tools |> List.collect flatten |> List.toArray
    

    
    /// <summary>
    /// Converts F# AgentConfig record to JavaScript object for the Strands SDK.
    /// Handles optional fields properly by only including them if they have values.
    /// Supports model polymorphism (string or object) and tool array flattening.
    /// </summary>
    /// <param name="config">F# AgentConfig record with Option types</param>
    /// <returns>JavaScript object compatible with Strands SDK Agent constructor</returns>
    let agentConfigToJS (config: AgentConfig): obj =
        let mutable jsObj = !!{| |}
        
        // Handle model polymorphism - can be string or Model instance
        match config.Model with
        | Some model -> jsObj?model <- model
        | None -> ()
        
        // Convert messages array if provided
        match config.Messages with
        | Some messages -> 
            jsObj?messages <- (messages |> List.map messageToJS |> List.toArray)
        | None -> ()
        
        // Flatten and convert tools array if provided
        match config.Tools with
        | Some tools -> 
            jsObj?tools <- flattenTools tools
        | None -> ()
        
        // Handle system prompt - can be string or SystemPrompt object
        match config.SystemPrompt with
        | Some systemPrompt -> jsObj?systemPrompt <- systemPrompt
        | None -> ()
        
        // Convert state map to JavaScript object if provided
        match config.State with
        | Some state -> 
            jsObj?state <- mapToJS state
        | None -> ()
        
        // Set printer boolean if provided
        match config.Printer with
        | Some printer -> jsObj?printer <- printer
        | None -> ()
        
        // Set conversation manager if provided
        match config.ConversationManager with
        | Some conversationManager -> jsObj?conversationManager <- conversationManager
        | None -> ()
        
        // Set hooks array if provided
        match config.Hooks with
        | Some hooks -> jsObj?hooks <- (hooks |> List.toArray)
        | None -> ()
        
        jsObj

    /// <summary>
    /// Converts F# OpenAIModelConfig record to JavaScript object for the OpenAI SDK.
    /// Handles optional model parameters and client configuration pass-through.
    /// The modelId is required, while all other parameters are optional.
    /// </summary>
    /// <param name="config">F# OpenAIModelConfig record with Option types</param>
    /// <returns>JavaScript object compatible with OpenAI SDK constructor</returns>
    let openAIModelConfigToJS (config: OpenAIModelConfig): obj =
        let mutable jsObj = !!{| modelId = config.ModelId |}
        
        // Set API key if provided (falls back to environment variable if not)
        match config.ApiKey with
        | Some apiKey -> jsObj?apiKey <- apiKey
        | None -> ()
        
        // Set optional model parameters
        match config.Temperature with
        | Some temperature -> jsObj?temperature <- temperature
        | None -> ()
        
        match config.MaxTokens with
        | Some maxTokens -> jsObj?maxTokens <- maxTokens
        | None -> ()
        
        match config.TopP with
        | Some topP -> jsObj?topP <- topP
        | None -> ()
        
        match config.FrequencyPenalty with
        | Some frequencyPenalty -> jsObj?frequencyPenalty <- frequencyPenalty
        | None -> ()
        
        match config.PresencePenalty with
        | Some presencePenalty -> jsObj?presencePenalty <- presencePenalty
        | None -> ()
        
        // Pass through client configuration if provided
        match config.ClientConfig with
        | Some clientConfig -> jsObj?clientConfig <- clientConfig
        | None -> ()
        
        jsObj

