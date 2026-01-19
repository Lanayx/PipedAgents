namespace PipedAgents.Strands.JS

open Fable.Core.JS
open Fable.Core.JsInterop

/// <summary>
/// High-level F# API for creating and using Strands agents.
/// Provides idiomatic F# functions that wrap the JavaScript SDK with type safety.
/// </summary>
module Agent =
    
    open Types
    open Interop
    
    /// <summary>
    /// Represents a Strands agent instance with type-safe F# wrapper.
    /// This type encapsulates the JavaScript Agent and provides F# methods.
    /// </summary>
    type Agent = private { JSAgent: JSAgent }
    
    /// <summary>
    /// Internal helper to create an Agent from options.
    /// Should primarily be called via Client.CreateAgent.
    /// </summary>
    let internal createInternal (options: AgentOptions) : Agent =
        { JSAgent = JSAgent(options.ToJS()) }
    
    /// <summary>
    /// Invokes the agent with string input and returns a Promise of the result.
    /// This is the primary method for single-turn agent interactions.
    /// </summary>
    /// <param name="input">String input message for the agent</param>
    /// <param name="agent">Agent instance to invoke</param>
    /// <returns>Promise that resolves to AgentResult containing the response</returns>
    /// <exception cref="StrandsSDKException">Thrown when agent invocation fails</exception>
    let invoke (input: string) (agent: Agent) : Promise<obj> =
        agent.JSAgent.invoke(input)
    
    /// <summary>
    /// Invokes the agent with complex input (ContentBlock array or Message array).
    /// Supports multi-modal inputs and structured conversation history.
    /// </summary>
    /// <param name="input">Complex input - can be ContentBlock array or Message array</param>
    /// <param name="agent">Agent instance to invoke</param>
    /// <returns>Promise that resolves to AgentResult containing the response</returns>
    /// <exception cref="StrandsSDKException">Thrown when agent invocation fails</exception>
    let invokeComplex (input: obj) (agent: Agent) : Promise<obj> =
        agent.JSAgent.invoke(input)
    
    /// <summary>
    /// Streams the agent execution with string input, yielding events and returning the final result.
    /// This method provides real-time access to agent processing steps including model streaming,
    /// tool execution, and conversation updates.
    /// </summary>
    /// <param name="input">String input message for the agent</param>
    /// <param name="agent">Agent instance to stream</param>
    /// <returns>JavaScript AsyncGenerator that yields AgentStreamEvent objects and returns AgentResult</returns>
    /// <exception cref="StrandsSDKException">Thrown when agent streaming fails</exception>
    let stream (input: string) (agent: Agent) : obj =
        agent.JSAgent.stream(input)
    
    /// <summary>
    /// Streams the agent execution with complex input (ContentBlock array or Message array).
    /// Supports multi-modal inputs and structured conversation history with real-time streaming.
    /// </summary>
    /// <param name="input">Complex input - can be ContentBlock array or Message array</param>
    /// <param name="agent">Agent instance to stream</param>
    /// <returns>JavaScript AsyncGenerator that yields AgentStreamEvent objects and returns AgentResult</returns>
    /// <exception cref="StrandsSDKException">Thrown when agent streaming fails</exception>
    let streamComplex (input: obj) (agent: Agent) : obj =
        agent.JSAgent.stream(input)

/// <summary>
/// High-level F# API for creating and configuring OpenAI clients.
/// Provides idiomatic F# functions that wrap the JavaScript OpenAI SDK with type safety.
/// </summary>
module Client =
    
    open Types
    open Interop
    
    /// <summary>
    /// Represents an OpenAI client instance with type-safe F# wrapper.
    /// Acts as a factory for Agents.
    /// </summary>
    type OpenAIClient = private { JSModel: JSOpenAIModel }
        with
        /// <summary>
        /// Gets the underlying JavaScript model instance for advanced usage.
        /// </summary>
        member this.GetJSModel() = this.JSModel
        
        /// <summary>
        /// Creates a new Strands Agent using this client as the model.
        /// </summary>
        /// <param name="options">Agent configuration options</param>
        /// <returns>Agent instance initialized with this client</returns>
        member this.CreateAgent(options: AgentOptions) =
            options.Model <- this.JSModel
            Agent.createInternal options
    
    /// <summary>
    /// Creates a new OpenAI client with the provided configuration.
    /// Converts F# OpenAIClientOptions to JavaScript object and initializes the underlying JSOpenAIModel.
    /// </summary>
    /// <param name="modelId">Model identifier string</param>
    /// <param name="options">F# OpenAIClientOptions class</param>
    /// <returns>OpenAIClient instance ready for use</returns>
    /// <exception cref="StrandsSDKException">Thrown when client creation fails</exception>
    let ForChatCompletionsAPI (modelId: string, options: OpenAIClientOptions) : OpenAIClient =
        let jsOptions: obj = options.ToJS()
        jsOptions?modelId <- modelId
        { JSModel = JSOpenAIModel(jsOptions) }