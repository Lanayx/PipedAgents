#nowarn "57"

open System
open System.ClientModel
open System.ClientModel.Primitives
open System.ComponentModel
open Microsoft.Agents.AI
open Microsoft.Extensions.AI
open OpenAI.Responses
open Shared

module Baseline =

    [<Description("Get the weather for a given location.")>]
    let getWeather ([<Description("The location to get the weather for.")>] location: string) : string =
        $"The weather in {location} is cloudy with a high of 15°C."

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
            responseClient.CreateAIAgent(
                ChatClientAgentOptions(
                    Name = "Helper",
                    ChatOptions =
                        ChatOptions(
                            Instructions = "You are a helpful assistant",
                            Tools = [| ApprovalRequiredAIFunction(AIFunctionFactory.Create(Func<string,string>(getWeather))) |],
                            RawRepresentationFactory = (fun _ -> CreateResponseOptions(StoredOutputEnabled = false))
                        )
                )
            )

        // Call the agent and check if there are any user input requests to handle.
        let thread = agent.GetNewThread()
        let mutable response = agent.RunAsync("What is the weather like in Amsterdam?", thread).GetAwaiter().GetResult()
        let mutable userInputRequests = response.UserInputRequests |> Seq.toList

        while userInputRequests.Length > 0 do
            // Ask the user to approve each function call request.
            let userInputResponses =
                userInputRequests
                |> List.choose (function
                    | :? FunctionApprovalRequestContent as functionApprovalRequest ->
                        printfn "The agent would like to invoke the following function, please reply Y to approve: Name %s" functionApprovalRequest.FunctionCall.Name
                        let approved = String.Equals(Console.ReadLine(), "Y", StringComparison.OrdinalIgnoreCase)
                        let response = functionApprovalRequest.CreateResponse(approved)
                        Some(ChatMessage(ChatRole.User, [| response :> AIContent |] :> System.Collections.Generic.IList<AIContent>))
                    | _ -> None
                )

            // Pass the user input responses back to the agent for further processing.
            response <- agent.RunAsync(userInputResponses, thread).GetAwaiter().GetResult()
            userInputRequests <- response.UserInputRequests |> Seq.toList

        printfn "\nAgent: %O" response


module Target =

    let run () =
        ()

Baseline.run()
