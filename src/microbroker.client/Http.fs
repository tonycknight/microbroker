namespace Microbroker.Client

open System
open System.Diagnostics.CodeAnalysis
open System.Threading
open System.Threading.Tasks
open System.Net
open System.Net.Http
open Microbroker.Client
open Newtonsoft.Json.Linq

type internal HttpResponseHeaders = (string * string) list

[<CLIMutable>]
type internal HttpResponseErrors =
    { errors: string[] }

    static member empty = { errors = [||] }

type internal HttpRequestResponse =
    | HttpOkRequestResponse of
        status: HttpStatusCode *
        body: string *
        contentType: string option *
        headers: HttpResponseHeaders
    | HttpTooManyRequestsResponse of headers: HttpResponseHeaders
    | HttpBadGatewayResponse of headers: HttpResponseHeaders
    | HttpErrorRequestResponse of
        status: HttpStatusCode *
        body: string *
        headers: HttpResponseHeaders *
        errors: HttpResponseErrors
    | HttpExceptionRequestResponse of ex: Exception

    static member status(response: HttpRequestResponse) =
        match response with
        | HttpOkRequestResponse(status, _, _, _) -> status
        | HttpTooManyRequestsResponse(_) -> System.Net.HttpStatusCode.TooManyRequests
        | HttpErrorRequestResponse(status, _, _, _) -> status
        | HttpExceptionRequestResponse _ -> HttpStatusCode.InternalServerError
        | HttpBadGatewayResponse _ -> HttpStatusCode.BadGateway

    static member loggable(response: HttpRequestResponse) =
        let status = HttpRequestResponse.status response
        $"{response.GetType().Name} {status}"

[<ExcludeFromCodeCoverage>]
module internal Http =

    let body (cancellation: System.Threading.CancellationToken) (resp: HttpResponseMessage) =
        task {
            let! body =
                match resp.Content.Headers.ContentEncoding |> Seq.tryHead with
                | Some x when x = "gzip" ->
                    task {
                        use s = resp.Content.ReadAsStream(cancellation)
                        return Strings.fromGzip s
                    }
                | _ -> resp.Content.ReadAsStringAsync()

            return body
        }

    let errors body =
        match body with
        | "" -> HttpResponseErrors.empty
        | json ->
            let jq = JObject.Parse json

            let msgs =
                jq.SelectTokens("errors").Values()
                |> Seq.map (fun t -> t.ToString())
                |> Array.ofSeq

            { HttpResponseErrors.empty with
                errors = msgs }

    let contentHeaders (resp: HttpResponseMessage) =
        resp.Content.Headers
        |> Seq.collect (fun x -> x.Value |> Seq.map (fun v -> Strings.toLower x.Key, v))

    let respHeaders (resp: HttpResponseMessage) =
        resp.Headers
        |> Seq.collect (fun x -> x.Value |> Seq.map (fun v -> (Strings.toLower x.Key, v)))

    let headers (resp: HttpResponseMessage) =
        respHeaders resp
        |> Seq.append (contentHeaders resp)
        |> Seq.sortBy fst
        |> List.ofSeq

    let parse (cancellation: System.Threading.CancellationToken) (resp: HttpResponseMessage) =
        let respHeaders = headers resp

        match resp.IsSuccessStatusCode, resp.StatusCode with
        | true, _ ->
            task {
                let! body = body cancellation resp

                let mediaType =
                    resp.Content.Headers.ContentType
                    |> Option.ofNull<Headers.MediaTypeHeaderValue>
                    |> Option.map _.MediaType

                return HttpOkRequestResponse(resp.StatusCode, body, mediaType, respHeaders)
            }
        | false, HttpStatusCode.TooManyRequests -> HttpTooManyRequestsResponse(respHeaders) |> Tasks.toTaskResult
        | false, HttpStatusCode.BadGateway -> HttpBadGatewayResponse(respHeaders) |> Tasks.toTaskResult
        | false, HttpStatusCode.BadRequest ->
            task {
                let! body = body cancellation resp

                return HttpErrorRequestResponse(resp.StatusCode, body, respHeaders, errors body)
            }
        | false, _ ->
            task {
                let! body = body cancellation resp
                return HttpErrorRequestResponse(resp.StatusCode, body, respHeaders, HttpResponseErrors.empty)
            }

    let send (cancellation: System.Threading.CancellationToken) (client: HttpClient) (msg: HttpRequestMessage) =
        task {
            try
                use! resp = client.SendAsync (msg, cancellation)
                return! parse cancellation resp
            with ex ->
                return HttpExceptionRequestResponse(ex)
        }

    let encodeUrl (value: string) = System.Web.HttpUtility.UrlEncode value

type internal IHttpClient =
    abstract member GetAsync: url: string * ?cancellation: CancellationToken -> Task<HttpRequestResponse>
    abstract member PutAsync: url: string * content: string * ?cancellation: CancellationToken -> Task<HttpRequestResponse>
    abstract member PostAsync: url: string * content: string * ?cancellation: CancellationToken -> Task<HttpRequestResponse>

[<ExcludeFromCodeCoverage>]
type internal InternalHttpClient(httpClient: HttpClient) =
    
    let cancellationToken (token: System.Threading.CancellationToken option) =
        token |> Option.defaultValue System.Threading.CancellationToken.None

    let httpSend canx = Http.send canx httpClient

    let getReq (url: string) =
        new HttpRequestMessage(HttpMethod.Get, url)

    let sendJsonReq (method: HttpMethod) (url: string) (content: string) =
        let result = new HttpRequestMessage(method, url)

        result.Content <-
            new System.Net.Http.StringContent(
                content,
                Text.Encoding.UTF8,
                System.Net.Mime.MediaTypeNames.Application.Json
            )

        result

    let putJsonReq = sendJsonReq HttpMethod.Put

    let postJsonReq = sendJsonReq HttpMethod.Post

    interface IHttpClient with
        member this.GetAsync (url, cancellation) = url |> getReq |> httpSend (cancellationToken cancellation)
        member this.PutAsync (url, content, cancellation) = content |> putJsonReq url |> httpSend (cancellationToken cancellation)
        member this.PostAsync (url, content, cancellation) = content |> postJsonReq url |> httpSend (cancellationToken cancellation)
