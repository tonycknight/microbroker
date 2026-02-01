namespace Microbroker.Client

open System
open System.Diagnostics.CodeAnalysis

[<AutoOpen>]
[<ExcludeFromCodeCoverage>]
module internal Combinators =

    let (&&>>) x y = (fun (v: 'a) -> x (v) && y (v))

    let (||>>) x y = (fun (v: 'a) -> x (v) || y (v))

module internal Strings =

    let toLower (value: string) = value.ToLower()

    let toUpper (value: string) = value.ToUpper()

    let fromGzip (value: System.IO.Stream) =
        let bufferSize = 512
        let buffer = Array.create<byte> bufferSize 0uy
        use outStream = new System.IO.MemoryStream()

        use decomp =
            new System.IO.Compression.GZipStream(value, System.IO.Compression.CompressionMode.Decompress)

        let mutable len = -1

        while len <> 0 do
            len <- decomp.Read(buffer)

            if len > 0 then
                outStream.Write(buffer, 0, len)

        outStream.Seek(0, System.IO.SeekOrigin.Begin) |> ignore
        use reader = new System.IO.StreamReader(outStream)
        reader.ReadToEnd()

    let toGzip (value: string) =
        let bytes = System.Text.Encoding.UTF8.GetBytes(value)
        use outStream = new System.IO.MemoryStream()

        use comp =
            new System.IO.Compression.GZipStream(outStream, System.IO.Compression.CompressionMode.Compress)

        comp.Write(bytes)
        comp.Flush()
        outStream.Seek(0, System.IO.SeekOrigin.Begin) |> ignore
        outStream.ToArray()

    let trimSlash (uri: string) =
        if uri.EndsWith("/") then
            uri.Substring(0, uri.Length - 1)
        else
            uri

    let join (delim: string) (values: seq<string>) = System.String.Join(delim, values)

[<ExcludeFromCodeCoverage>]
module internal Option =

    let ofNull<'a> (value: 'a) =
        if Object.ReferenceEquals(value, null) then
            None
        else
            Some value

[<ExcludeFromCodeCoverage>]
module internal Tasks =
    let toTaskResult (value) =
        System.Threading.Tasks.Task.FromResult(value)

[<ExcludeFromCodeCoverage>]
module internal Time =
    let toDateTimeOffset (value: DateTime) = new DateTimeOffset(value)

    let now () = DateTimeOffset.UtcNow

    let add (span: TimeSpan) (value: DateTimeOffset) = value.Add(span)
