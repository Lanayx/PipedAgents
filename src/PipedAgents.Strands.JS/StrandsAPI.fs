namespace PipedAgents.Strands.JS

open Fable.Core.JS

/// <summary>
/// High-level F# API for creating and using Strands agents.
/// Provides idiomatic F# functions that wrap the JavaScript SDK with type safety.
/// </summary>
module Agent =
    
    open Types
    open Interop
    open ConfigConversion
    
    /// <summary>
    /// Represents a Strands agent instance with type-safe F# wrapper.
    /// This type encapsulates the JavaScript Agent and provides F# methods.
    /// </summary>
    type Agent = private { JSAgent: JSAgent }
    
    /// <summary>
    /// Creates a new Strands agent with the provided configuration.
    /// Converts F# AgentConfig to JavaScript object and initializes the underlying JSAgent.
    /// </summary>
    /// <param name="config">F# AgentConfig record with type-safe optional parameters</param>
    /// <returns>Agent instance ready for invocation</returns>
    /// <exception cref="StrandsSDKException">Thrown when agent creation fails</exception>
    let create (config: AgentConfig) : Agent =
        try
            let jsConfig = agentConfigToJS config
            { JSAgent = JSAgent(jsConfig) }
        with
        | ex -> raise (StrandsSDKException($"Failed to create agent: {ex.Message}", Some ex))
    
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
    /// <param name="input">Complex input - can be ContentBlock list or Message list</param>
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
        try
            agent.JSAgent.stream(input)
        with
        | ex -> raise (StrandsSDKException($"Failed to stream agent: {ex.Message}", Some ex))
    
    /// <summary>
    /// Streams the agent execution with complex input (ContentBlock array or Message array).
    /// Supports multi-modal inputs and structured conversation history with real-time streaming.
    /// </summary>
    /// <param name="input">Complex input - can be ContentBlock list or Message list</param>
    /// <param name="agent">Agent instance to stream</param>
    /// <returns>JavaScript AsyncGenerator that yields AgentStreamEvent objects and returns AgentResult</returns>
    /// <exception cref="StrandsSDKException">Thrown when agent streaming fails</exception>
    let streamComplex (input: obj) (agent: Agent) : obj =
        try
            agent.JSAgent.stream(input)
        with
        | ex -> raise (StrandsSDKException($"Failed to stream agent: {ex.Message}", Some ex))

/// <summary>
/// High-level F# API for creating and configuring OpenAI models.
/// Provides idiomatic F# functions that wrap the JavaScript OpenAI SDK with type safety.
/// </summary>
module OpenAIModel =
    
    open Types
    open Interop
    open ConfigConversion
    
    /// <summary>
    /// Represents an OpenAI model instance with type-safe F# wrapper.
    /// This type encapsulates the JavaScript OpenAIModel and provides F# methods.
    /// </summary>
    type OpenAIModel = private { JSModel: JSOpenAIModel }
    
    /// <summary>
    /// Creates a new OpenAI model with the provided configuration.
    /// Converts F# OpenAIModelConfig to JavaScript object and initializes the underlying JSOpenAIModel.
    /// The modelId parameter is required, while all other parameters are optional.
    /// API key falls back to OPENAI_API_KEY environment variable if not provided.
    /// </summary>
    /// <param name="config">F# OpenAIModelConfig record with type-safe optional parameters</param>
    /// <returns>OpenAIModel instance ready for use with agents</returns>
    /// <exception cref="StrandsSDKException">Thrown when model creation fails</exception>
    let create (config: OpenAIModelConfig) : OpenAIModel =
        try
            let jsConfig = openAIModelConfigToJS config
            { JSModel = JSOpenAIModel(jsConfig) }
        with
        | ex -> raise (StrandsSDKException($"Failed to create OpenAI model: {ex.Message}", Some ex))
    
    /// <summary>
    /// Gets the underlying JavaScript model instance for advanced usage.
    /// This allows access to the raw JavaScript API when needed.
    /// </summary>
    /// <param name="model">OpenAIModel instance</param>
    /// <returns>The underlying JSOpenAIModel instance</returns>
    let getJSModel (model: OpenAIModel) : JSOpenAIModel =
        model.JSModel