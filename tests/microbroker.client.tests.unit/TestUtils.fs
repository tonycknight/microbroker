namespace Microbroker.Client.Tests.Unit

open System
open System.Net
open Microbroker.Client
open NSubstitute

module internal TestUtils =

    let toJson values =
        Newtonsoft.Json.JsonConvert.SerializeObject values

    let testConfig =
        { MicrobrokerConfiguration.brokerBaseUrl = "a"
          throttleMaxTime = TimeSpan.FromSeconds(1.) }

    let ok json =
        HttpOkRequestResponse(HttpStatusCode.OK, json, None, [])

    let notfound json =
        HttpErrorRequestResponse(HttpStatusCode.NotFound, json, [], HttpResponseErrors.empty)

    let badRequest json =
        HttpErrorRequestResponse(HttpStatusCode.BadRequest, json, [], HttpResponseErrors.empty)

    let badRequestErrors json errors =
        HttpErrorRequestResponse(
            HttpStatusCode.BadRequest,
            json,
            [],
            { HttpResponseErrors.empty with
                errors = errors }
        )

    let exceptionResponse ex = HttpExceptionRequestResponse ex

    let badGatewayResponse () = HttpBadGatewayResponse []

    let tooManyRequestsResponse () = HttpTooManyRequestsResponse []

    let httpClient (response: HttpRequestResponse) =
        let http = Substitute.For<IHttpClient>()

        http.GetAsync(Arg.Any<string>(), System.Threading.CancellationToken.None).Returns(Tasks.toTaskResult response)
        |> ignore

        http

    let httpClientPost (response: HttpRequestResponse) =
        let http = Substitute.For<IHttpClient>()

        (http.PostAsync(Arg.Any<string>(), Arg.Any<string>(), System.Threading.CancellationToken.None))
            .Returns(Tasks.toTaskResult response)
        |> ignore

        http

    let httpClientPostException () =
        let http = Substitute.For<IHttpClient>()

        (http.PostAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<System.Threading.CancellationToken>()))
            .Returns(System.Threading.Tasks.Task.FromException<HttpRequestResponse>(new InvalidOperationException()))
        |> ignore

        http

    let proxy config client =
        new MicrobrokerProxy(config, client) :> IMicrobrokerProxy

    let defaultProxy client = proxy testConfig client
