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
        let! result1 = agent.invoke "Tell me a joke about a pirate."
        printfn $"Normal version: \n{string result1}"
        let! result2 = agent.invoke "Now tell the same joke for a kid"
        printfn $"Child version: \n{string result2}"
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

    promise {
        printfn "Normal version:"
        let enumerator1 = agent.stream "Tell me a joke about a pirate."
        do! enumerator1 |> AsyncIterable.iter (function
            | ModelContentBlockDeltaEvent (TextDelta txt) ->
                process.stdout.write txt |> ignore
            | _ ->
                ()
        )
        printfn "\nChild version:"
        let enumerator1 = agent.stream "Now tell the same joke for a kid."
        do! enumerator1 |> AsyncIterable.iter (function
            | ModelContentBlockDeltaEvent (TextDelta txt) ->
                process.stdout.write txt |> ignore
            | _ ->
                ()
        )
        printfn "\nAgent call completed"
    }

[<EntryPoint>]
let entryPoint _ =
    stream().catch(fun ex ->
        console.error(ex)
        process.exitCode <- 1
    ) |> ignore
    0 // Return success exit code