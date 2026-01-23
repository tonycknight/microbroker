namespace microbroker

open System

[<AutoOpen>]
module Combinators =

    let (&&>>) x y = (fun (v: 'a) -> x (v) && y (v))

    let (||>>) x y = (fun (v: 'a) -> x (v) || y (v))

    let (=>>) x y = (fun (v: 'a) -> x (v) = y (v))

module Char =
    let toLowerInvariant (value: char) = Char.ToLowerInvariant(value)

    let isAlphaNumeric (value: char) = Char.IsLetterOrDigit value

    let isIn (allowedChars: seq<char>) (value: char) =
        let value = toLowerInvariant value
        allowedChars |> Seq.map toLowerInvariant |> Seq.contains value

module Strings =
    let str<'a> (value: 'a) = value.ToString()

    let appendIfMissing (suffix: string) (value: string) =
        if value.EndsWith(suffix) |> not then
            $"{value}{suffix}"
        else
            value

    let (|NullOrWhitespace|_|) value =
        if String.IsNullOrWhiteSpace value then Some value else None

    let defaultIf (comparand: string) (defaultValue: string) (value: string) =
        if value = comparand then defaultValue else value

    let join (delim: string) (values: seq<string>) = System.String.Join(delim, values)

    let lower (value: string) = value.ToLower()

    let inline (==~) (left: string) (right: string) =
        StringComparer.InvariantCultureIgnoreCase.Equals(left, right)

module Args =

    let getValue prefix (args: seq<string>) =
        let arg =
            args
            |> Seq.filter (fun a -> a.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase))
            |> Seq.tryHead

        match arg with
        | Some arg -> arg.Substring(prefix.Length).Trim() |> Some
        | None -> None

module Io =
    open System.IO

    let toFullPath (path: string) =
        if not <| Path.IsPathRooted(path) then
            let wd = Environment.CurrentDirectory

            Path.Combine(wd, path)
        else
            path

    let normalise (path: string) = Path.GetFullPath(path)

    let fileExists (path: string) = File.Exists(path)
