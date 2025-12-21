#nowarn "57"

open System
open System.ClientModel
open System.ClientModel.Primitives
open HelloWorld
open OpenAI
open OpenAI.Responses

let key = ApiKeyCredential(
    Environment.GetEnvironmentVariable("OPENAI_API_KEY")
)
let httpClient = getLoggingHttpClient()
let options = OpenAIClientOptions(
    Transport = new HttpClientPipelineTransport(httpClient),
    Endpoint = Uri(Environment.GetEnvironmentVariable "OPENAI_BASE_URL")
)
// httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + Environment.GetEnvironmentVariable "OPENAI_API_KEY")
// let models = httpClient.GetStringAsync(Uri((Environment.GetEnvironmentVariable "OPENAI_BASE_URL") + "/models")).GetAwaiter().GetResult()

let client = OpenAIClient(key, options)
let responseClient = client.GetResponsesClient(Environment.GetEnvironmentVariable "MODEL_ID")
let agent = responseClient.CreateAIAgent(
    instructions = "You are good at telling jokes.",
    name = "Joker")
agent.RunAsync("Tell me a joke about a pirate.")
|> _.GetAwaiter().GetResult()
|> string
|> printfn "%s"