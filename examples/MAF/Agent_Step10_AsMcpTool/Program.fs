#nowarn "57"

open System
open System.ClientModel
open System.ClientModel.Primitives
open PipedAgents.MAF
open PipedAgents.MAF.OpenAI
open Microsoft.Agents.AI
open FSharp.Control
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open ModelContextProtocol.Server
open OpenAI.Responses
open Shared

module Baseline =

    let run() =
        let key = ApiKeyCredential(
            Environment.GetEnvironmentVariable("OPENAI_API_KEY")
        )
        let httpClient = getLoggingHttpClient(OutputStream.Error)
        let options = OpenAI.OpenAIClientOptions(
            Transport = new HttpClientPipelineTransport(httpClient),
            Endpoint = Uri(Environment.GetEnvironmentVariable "OPENAI_BASE_URL")
        )
        let client = OpenAI.OpenAIClient(key, options)
        let responseClient = client.GetResponsesClient(Environment.GetEnvironmentVariable "MODEL_ID")
        let agent =
            responseClient
                .AsAIAgent(
                    instructions = "You are good at telling jokes, and you always start each joke with 'Aye aye, captain!'.",
                    name = "Joker")

        task {
            // Convert the agent to an AIFunction and then to an MCP tool.
            // The agent name and description will be used as the mcp tool name and description.
            let tool = McpServerTool.Create(agent.AsAIFunction());

            // Register the MCP server with StdIO transport and expose the tool via the server.
            let builder = Host.CreateEmptyApplicationBuilder(null);
            builder.Services
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithTools([tool]) |> ignore

            do! builder.Build().RunAsync()
        } |> _.GetAwaiter().GetResult()

module Target =
    let run() =
        use client = Client.ForResponsesAPI(Environment.GetEnvironmentVariable "MODEL_ID")
        let agent =
            client.CreateAgent(
                AgentOptions(
                    Name = "Joker",
                    Instructions = "You are good at telling jokes, and you always start each joke with 'Aye aye, captain!'."
                )
            )
        +task {
            let tool = McpServerTool.Create(agent.AsAIFunction())
            let builder = Host.CreateEmptyApplicationBuilder(null);
            builder.Services
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithTools([tool]) |> ignore
            do! builder.Build().RunAsync()
        }

Target.run()

