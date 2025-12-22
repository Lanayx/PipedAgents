#nowarn "57"

namespace FunAgents.MAF.OpenAI

open System
open System.ClientModel
open OpenAI

type Client =
    static member ForResponsesAPI(model, ?key, ?options) =
        let key =
            key |> Option.defaultValue (
                ApiKeyCredential(Environment.GetEnvironmentVariable("OPENAI_API_KEY"))
            )
        let options =
            options |> Option.defaultValue (
                OpenAIClientOptions(Endpoint = Uri(Environment.GetEnvironmentVariable "OPENAI_BASE_URL"))
            )
        OpenAIClient(key, options).GetResponsesClient(model)