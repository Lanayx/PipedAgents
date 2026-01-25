#nowarn "57"

open System
open System.ClientModel
open System.ClientModel.Primitives
open System.Net.Mime
open Microsoft.Agents.AI
open PipedAgents.MAF
open OpenAI.Chat
open Microsoft.Extensions.AI
open Shared
open PipedAgents.MAF.OpenAI
open FSharp.Control

module BaseLine =

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
        let responseClient = client.GetChatClient(Environment.GetEnvironmentVariable "MODEL_ID")
        let agent = responseClient.AsAIAgent(name = "VisionAgent")
        let thread = +(agent.GetNewThreadAsync().AsTask())
        let message =
            ChatMessage(ChatRole.User, [|
                TextContent("What do you see in this image?") :> AIContent
                UriContent("https://upload.wikimedia.org/wikipedia/commons/thumb/d/dd/Gfp-wisconsin-madison-the-nature-boardwalk.jpg/2560px-Gfp-wisconsin-madison-the-nature-boardwalk.jpg", "image/jpeg")
            |])
        +agent.RunAsync(message, thread)
        |> string
        |> printfn "%s"

module Target =

    let runStreaming() =
        let client = Client.ForChatCompletionsAPI(Environment.GetEnvironmentVariable "MODEL_ID")
        let agent = client.CreateAgent(AgentOptions(Name = "VisionAgent"))
        let message =
            [|
                Message.GetTextContent("What do you see in this image?")
                Message.GetUrlContent("https://upload.wikimedia.org/wikipedia/commons/thumb/d/dd/Gfp-wisconsin-madison-the-nature-boardwalk.jpg/2560px-Gfp-wisconsin-madison-the-nature-boardwalk.jpg", MediaTypeNames.Image.Jpeg)
            |]
            |> Message.GetUserMessage
        let run = agent.GetStreamingThreadMessageRun()
        +task {
            for chunk in run message do
                printf $"{chunk}"
        }

Target.runStreaming()
