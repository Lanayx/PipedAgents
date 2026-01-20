#nowarn 46

module App

open Node
open Fable.Core.JS
open Fable.Core.JsInterop
open PipedAgents.Strands

let run () =

    let modelId = process.env?MODEL_ID
    // Create the OpenAI client with environment configuration
    let clientOptions = OpenAIClientOptions(
        ApiKey = process.env?OPENAI_API_KEY,
        BaseURL = process.env?OPENAI_API_BASE_URL
    )
    let client = Client.ForChatCompletionsAPI(modelId, clientOptions)

    // Create the agent with joke-telling system prompt
    let agentOptions = AgentOptions(
        SystemPrompt = "You are good at telling jokes. Write jokes with all uppercase letters."
    )

    let agent = client.CreateAgent(agentOptions)

    // Invoke the agent with pirate joke request
    promise {
        let! result = agent.invoke "Tell me a joke about a pirate."
        console.log(string result)
        console.log("Agent call completed.")
    }

let stream () =

    let modelId = process.env?MODEL_ID
    // Create the OpenAI client with environment configuration
    let clientOptions = OpenAIClientOptions(
        ApiKey = process.env?OPENAI_API_KEY,
        BaseURL = process.env?OPENAI_API_BASE_URL
    )
    let client = Client.ForChatCompletionsAPI(modelId, clientOptions)

    // Create the agent with joke-telling system prompt
    let agentOptions = AgentOptions(
        SystemPrompt = "You are good at telling jokes. Write jokes with all uppercase letters."
    )

    let agent = client.CreateAgent(agentOptions)

    // Stream the agent response using promise CE
    promise {
        console.log("Agent response stream:")
        
        let enumerator = agent.stream "Tell me a joke about a pirate."
        
        // Process the async generator
        let mutable isDone = false
        while not isDone do
            let! result = enumerator.next()
            isDone <- result.``done``
            match result.value with
            | ModelContentBlockDeltaEvent (TextDelta txt) ->
                process.stdout.write txt |> ignore
            | _ ->
                ()
        console.log("\nAgent call completed.")
    }

[<EntryPoint>]
let entryPoint _ =
    run().catch(fun ex ->
        console.error(ex)
        process.exitCode <- 1
    ) |> ignore
    0 // Return success exit code