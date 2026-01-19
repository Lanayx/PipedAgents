#nowarn 46

module App

open Node
open Fable.Core.JS
open Fable.Core.JsInterop
open PipedAgents.Strands.JS.Types
open PipedAgents.Strands.JS

let run () =

    // Create the OpenAI client with environment configuration
    let clientOptions = OpenAIClientOptions()
    clientOptions.ModelId <- process.env?MODEL_ID
    clientOptions.ApiKey <- process.env?OPENAI_API_KEY
    clientOptions.BaseURL <- process.env?OPENAI_API_BASE_URL

    let client = Client.forChatCompletionsAPI clientOptions

    // Create the agent with joke-telling system prompt
    let agentOptions = AgentOptions()
    agentOptions.SystemPrompt <- "You are good at telling jokes. Write jokes with all uppercase letters."
    agentOptions.Printer <- false

    let agent = client.CreateAgent(agentOptions)

    // Invoke the agent with pirate joke request
    promise {
        let! result = Agent.invoke "Tell me a joke about a pirate." agent
        console.log(result.ToString())
        console.log("Agent call completed.")
    }

let stream () =

    // Create the OpenAI client with environment configuration
    let clientOptions = OpenAIClientOptions()
    clientOptions.ModelId <- process.env?MODEL_ID
    clientOptions.ApiKey <- process.env?OPENAI_API_KEY
    clientOptions.BaseURL <- process.env?OPENAI_API_BASE_URL

    let client = Client.forChatCompletionsAPI clientOptions

    // Create the agent with joke-telling system prompt
    let agentOptions = AgentOptions()
    agentOptions.SystemPrompt <- "You are good at telling jokes. Write jokes with all uppercase letters."
    agentOptions.Printer <- false

    let agent = client.CreateAgent(agentOptions)

    // Stream the agent response using promise CE
    promise {
        console.log("Agent response stream:")
        
        let streamGenerator = Agent.stream "Tell me a joke about a pirate." agent
        
        // Process the async generator
        let mutable isDone = false
        while not isDone do
            let! result = streamGenerator?next()
            isDone <- result?``done``
            if not isDone then
                let event = result?value
                // Check if this is a modelContentBlockDeltaEvent with textDelta
                let eventType = event?``type``
                if eventType = "modelContentBlockDeltaEvent" then
                    let delta = event?delta
                    let deltaType = delta?``type``
                    if deltaType = "textDelta" then
                        process.stdout.write delta?text |> ignore
        
        console.log("\nAgent call completed.")
    }

[<EntryPoint>]
let entryPoint _ =
    run().catch(fun ex ->
        console.error(ex)
        process.exitCode <- 1
    ) |> ignore
    0 // Return success exit code