namespace PipedAgents.Strands

open Fable.Core.JS
open Fable.Core.JsInterop


/// <summary>
/// High-level F# API for creating and configuring OpenAI clients.
/// Provides idiomatic F# functions that wrap the JavaScript OpenAI SDK with type safety.
/// </summary>
module Client =

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
            Agent(options.ToJS())
    
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