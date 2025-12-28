[<AutoOpen>]
module Shared.Logging

open System
open System.Net.Http
open System.Threading.Tasks

type OutputStream =
    | Regular
    | Error

[<AutoOpen>]
type LoggingHandler(innerHandler: HttpMessageHandler, ?outputStream: OutputStream) =
    inherit DelegatingHandler(innerHandler)
    let log (str: string) =
        match outputStream with
        | Some Error -> Console.Error.WriteLine(str)
        | _ -> Console.WriteLine(str)
    member this.BaseSendAsync(request, token) =
        base.SendAsync(request, token)
    override this.SendAsync(request, cancellationToken) =
        task {
            if request.Content |> isNull |> not then
                let! req = request.Content.ReadAsStringAsync(cancellationToken)
                log $"Request: {req}"
            let! response = this.BaseSendAsync(request, cancellationToken)
            if response.Content |> isNull |> not then
                let! res = response.Content.ReadAsStringAsync(cancellationToken)
                log $"Response: {res}"
            return response
        }

    static member getLoggingHttpClient(?logStream: OutputStream) =
        let socketHandler =
            new SocketsHttpHandler(
                PooledConnectionLifetime = TimeSpan.FromMinutes 15.0,
                ConnectTimeout = TimeSpan.FromSeconds 15.0
            )
        let loggingHandler = new LoggingHandler(socketHandler, ?outputStream = logStream)
        new HttpClient(loggingHandler)

let inline (~+) (t: Task<'a>) : 'a = t.GetAwaiter().GetResult()