namespace Microbroker.Client.Tests.Unit

open System
open System.Net
open Microsoft.Extensions.Logging
open Microbroker.Client
open NSubstitute

module internal TestUtils =

    let toJson values =
        Newtonsoft.Json.JsonConvert.SerializeObject values

    let testConfig =
        { MicrobrokerConfiguration.brokerBaseUrl = "a"
          throttleMaxTime = TimeSpan.FromSeconds(1.) }

    let logger = Substitute.For<ILoggerFactory>()

    let ok json =
        HttpOkRequestResponse(HttpStatusCode.OK, json, None, [])

    let notfound json =
        HttpErrorRequestResponse(HttpStatusCode.NotFound, json, [], HttpResponseErrors.empty)

    let badRequest json =
        HttpErrorRequestResponse(HttpStatusCode.BadRequest, json, [], HttpResponseErrors.empty)

    let httpClient (response: HttpRequestResponse) =
        let http = Substitute.For<IHttpClient>()

        let param = Arg.Any<string>()

        http.GetAsync(param).Returns(Tasks.toTaskResult response) |> ignore

        http

    let httpClientPost (response: HttpRequestResponse) =
        let http = Substitute.For<IHttpClient>()

        (http.PostAsync (Arg.Any<string>()) (Arg.Any<string>())).Returns(Tasks.toTaskResult response)
        |> ignore

        http

    let httpClientPostException () =
        let http = Substitute.For<IHttpClient>()

        (http.PostAsync (Arg.Any<string>()) (Arg.Any<string>()))
            .Returns(System.Threading.Tasks.Task.FromException<HttpRequestResponse>(new InvalidOperationException()))
        |> ignore

        http

    let proxy config logger client =
        new MicrobrokerProxy(config, client, logger) :> IMicrobrokerProxy

    let defaultProxy client = proxy testConfig logger client
