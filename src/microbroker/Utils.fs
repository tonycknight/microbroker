namespace microbroker

open System

module Strings =
    let appendIfMissing (suffix: string) (value: string) =
        if value.EndsWith(suffix) |> not then
            $"{value}{suffix}"
        else
            value

    let (|NullOrWhitespace|_|) value =
        if String.IsNullOrWhiteSpace value then Some value else None

    let defaultIf (comparand: string) (defaultValue: string) (value: string) =
        if value = comparand then defaultValue else value

module Option =
    let nullToOption (value: obj) =
        if value = null then None else Some value

    let ofNull<'a> (value: 'a) =
        if Object.ReferenceEquals(value, null) then
            None
        else
            Some value

    let reduceMany (values: seq<'a option>) =
        values |> Seq.filter Option.isSome |> Seq.map Option.get

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
