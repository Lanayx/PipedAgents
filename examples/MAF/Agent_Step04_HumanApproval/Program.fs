#nowarn "57"

open System
open System.ClientModel
open System.ClientModel.Primitives
open System.ComponentModel
open Microsoft.Agents.AI
open Microsoft.Extensions.AI
open OpenAI.Responses
open Shared
open PipedAgents.MAF
open PipedAgents.MAF.OpenAI


[<Description("Get the weather for a given location.")>]
let getWeather ([<Description("The location to get the weather for.")>] location: string) : string =
    $"The weather in {location} is cloudy with a high of 15°C."

let getUserRequests (response: AgentResponse) =
    response.Messages
        |> Seq.collect _.Contents
        |> Seq.choose (function
            | :? FunctionApprovalRequestContent as c -> Some c
            | _ -> None)
        |> Seq.toArray

module Baseline =

    let run() =
        let key = ApiKeyCredential(
            Environment.GetEnvironmentVariable("OPENAI_API_KEY")
        )
        let httpClient = getLoggingHttpClient()
        let options = OpenAI.OpenAIClientOptions(
            Transport = new HttpClientPipelineTransport(httpClient),
            Endpoint = Uri(Environment.GetEnvironmentVariable "OPENAI_BASE_URL")
        )
        let client = OpenAI.OpenAIClient(key, options)
        let responseClient = client.GetResponsesClient(Environment.GetEnvironmentVariable "MODEL_ID")
        let agent =
            responseClient.AsAIAgent(
                ChatClientAgentOptions(
                    ChatOptions =
                        ChatOptions(
                            Instructions = "You are a helpful assistant",
                            Tools = [| ApprovalRequiredAIFunction(AIFunctionFactory.Create(Func<string,string>(getWeather))) |],
                            RawRepresentationFactory = (fun _ -> CreateResponseOptions(StoredOutputEnabled = false))
                        )
                )
            )

        task {
            // Call the agent and check if there are any user input requests to handle.
            let! session = agent.CreateSessionAsync()
            let mutable response = +agent.RunAsync("What is the weather like in Amsterdam?", session)
            let mutable userInputRequests = getUserRequests response

            while userInputRequests.Length > 0 do
                // Ask the user to approve each function call request.
                let userInputResponses =
                    userInputRequests
                    |> Array.map (fun functionApprovalRequest ->
                        printfn "The agent would like to invoke the following function, please reply Y to approve: Name %s" functionApprovalRequest.FunctionCall.Name
                        let approved = String.Equals(Console.ReadLine(), "Y", StringComparison.OrdinalIgnoreCase)
                        let response = functionApprovalRequest.CreateResponse(approved)
                        ChatMessage(ChatRole.User, [| response :> AIContent |] :> System.Collections.Generic.IList<AIContent>)
                    )

                // Pass the user input responses back to the agent for further processing.
                response <- +agent.RunAsync(userInputResponses, session)
                userInputRequests <- getUserRequests response

            printfn "\nAgent: %O" response
        } |> _.GetAwaiter().GetResult()


module Target =
    let run () =
        use client = Client.ForResponsesAPI(Environment.GetEnvironmentVariable "MODEL_ID")
        let agent = client.CreateAgent(
            AgentOptions(
                Instructions = "You are a helpful assistant",
                Tools = [| <@ getWeather @> |> Tool.Get |> ApprovalRequiredAIFunction |],
                CreateRawOptions = (fun _ -> CreateResponseOptions(StoredOutputEnabled = false))
            )
        )
        let rec handleResponse (response: AgentResponse) runMsg =
            task {
                let userInputRequests = getUserRequests response
                if userInputRequests.Length > 0 then
                    let! response =
                        userInputRequests
                        |> Array.map (fun functionApprovalRequest ->
                            Console.WriteLine(
                                "The agent would like to invoke the following function, please reply Y to approve: Name {0}",
                                functionApprovalRequest.FunctionCall.Name)
                            let approved = Console.ReadLine() = "Y"
                            functionApprovalRequest.CreateResponse(approved) :> AIContent)
                        |> Message.GetUserMessage
                        |> runMsg
                    return! handleResponse response runMsg
                else
                    printfn $"\nAgent: {response}"
            }
        +task {
            let! session = agent.CreateSessionAsync()
            let run = agent.GetSessionRun(session)
            let runMsg = agent.GetSessionMessageRun(session)
            let! response = run "What is the Amsterdam weather like?"
            return! handleResponse response runMsg
        }


Target.run()

