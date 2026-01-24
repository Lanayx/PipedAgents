#nowarn 46

module App

open Fetch
open Node
open Fable.Core
open Fable.Core.JS
open Fable.Core.JsInterop
open PipedAgents.Strands

let tool: obj -> Tool = import "tool" "@strands-agents/sdk"

[<Emit("fetch($0, $1)")>]
let globalFetch (url: string) (options: RequestInit) : JS.Promise<Response> = jsNative

// Custom fetch implementation for logging
let loggingFetch (url: string) (options: RequestInit) : JS.Promise<Response> =
    promise {
        printfn $"\n[LoggingFetch] Request: {options.method} %s{url}"

        if Option.isSome options.body then
            printfn $"[LoggingFetch] Body: {options.body}"

        try
            let! response = globalFetch url options
            printfn $"[LoggingFetch] Response Status: {response.Status}"

            let clone = response.clone()
            clone.text().``then``(fun (text: string) ->
                printfn $"[LoggingFetch] Response Body: {text}"
            ).catch(fun err ->
                printfn $"[LoggingFetch] Error reading response body: {err}"
            ) |> ignore

            return response
        with ex ->
            printfn $"[LoggingFetch] Error: {ex}"
            return raise ex
    }

let run () =
    // Initialize the OpenAI model
    let modelId = process.env?MODEL_ID
    
    // Create the OpenAI client with environment configuration and custom fetch
    let clientOptions = OpenAIClientOptions(
        ApiKey = process.env?OPENAI_API_KEY,
        BaseURL = process.env?OPENAI_API_BASE_URL,
        Fetch = System.Func<_, _, _>(loggingFetch)
    )

    let client = Client.ForChatCompletionsAPI(modelId, clientOptions)

    // Define the weather tool
    let weatherTool = tool({|
        name = "weather_forecast"
        description = "Get weather forecast for a city"
        inputSchema = z.object({|
            city = z.string().describe("The name of the city")
            days = z.number().``default``(3).describe("Number of days for the forecast")
        |})
        callback = fun (input: {| city: string; days: int |}) ->
            $"Weather forecast for {input.city} for the next {input.days} days is cloudy with a high of 15°C"
    |})

    // Create the agent
    let agentOptions = AgentOptions(
        SystemPrompt = "You are a helpful assistant",
        Printer = false,
        Tools = [| weatherTool |]
    )

    let agent = client.CreateAgent(agentOptions)

    // Run the agent
    promise {
        console.log("Asking for weather forecast...")
        let! result = agent.invoke "Give me a weather forecast Amsterdam for the next five days"
        console.log(string result)
    }

let stream () =
    // Initialize the OpenAI model
    let modelId = process.env?MODEL_ID
    
    // Create the OpenAI client with environment configuration and custom fetch
    let clientOptions = OpenAIClientOptions(
        ApiKey = process.env?OPENAI_API_KEY,
        BaseURL = process.env?OPENAI_API_BASE_URL,
        Fetch = System.Func<_, _, _>(loggingFetch)
    )

    let client = Client.ForChatCompletionsAPI(modelId, clientOptions)

    // Define the weather tool
    let weatherTool = tool({|
        name = "weather_forecast"
        description = "Get weather forecast for a city"
        inputSchema = z.object({|
            city = z.string().describe("The name of the city")
            days = z.number().``default``(3).describe("Number of days for the forecast")
        |})
        callback = fun (input: {| city: string; days: int |}) ->
            $"Weather forecast for {input.city} for the next {input.days} days is cloudy with a high of 15°C"
    |})

    // Create the agent
    let agentOptions = AgentOptions(
        SystemPrompt = "You are a helpful assistant",
        Printer = false,
        Tools = [| weatherTool |]
    )

    let agent = client.CreateAgent(agentOptions)

    // Run the agent
    promise {
        console.log("Asking for weather forecast...\n")
        let enumerator = agent.stream "Give me a weather forecast Amsterdam for the next five days"
        return! enumerator |> AsyncIterable.iter (function
            | ModelContentBlockDeltaEvent (TextDelta txt) ->
                process.stdout.write txt |> ignore
            | _ ->
                ()
        )
    }

[<EntryPoint>]
let entryPoint _ =
    run().catch(fun ex ->
        console.error(ex)
        process.exitCode <- 1
    ) |> ignore
    0 // Return success exit code
