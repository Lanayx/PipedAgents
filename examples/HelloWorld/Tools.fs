[<AutoOpen>]
module HelloWorld.Tools

open System
open System.Net.Http

type LoggingHandler(innerHandler: HttpMessageHandler) =
    inherit DelegatingHandler(innerHandler)
    member this.BaseSendAsync(request, token) =
        base.SendAsync(request, token)
    override this.SendAsync(request, cancellationToken) =
        task {
            if request.Content |> isNull |> not then
                let! req = request.Content.ReadAsStringAsync(cancellationToken)
                Console.WriteLine($"Request: {req}")
            let! response = this.BaseSendAsync(request, cancellationToken)
            if response.Content |> isNull |> not then
                let! res = response.Content.ReadAsStringAsync(cancellationToken)
                Console.WriteLine($"Response: {res}")
            return response
        }

let getLoggingHttpClient() =
    let socketHandler =
        new SocketsHttpHandler(
            PooledConnectionLifetime = TimeSpan.FromMinutes 15.0,
            ConnectTimeout = TimeSpan.FromSeconds 15.0
        )
    let loggingHandler = new LoggingHandler(socketHandler)
    new HttpClient(loggingHandler)