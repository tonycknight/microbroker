namespace Microbroker.Client.Tests

open System
open Microbroker.Client
open FsCheck
open FsCheck.Xunit

module StringsTests =
    [<Property(Verbose = true)>]
    let ``toGzip returns byte array`` (value: NonEmptyString) =
        let r = Strings.toGzip value.Get

        r.Length > 0

    [<Property(Verbose = true)>]
    let ``toGzip/fromGzip is symmetric`` (value: NonEmptyString) =
        let toStream (bytes: byte[]) = new System.IO.MemoryStream(bytes)
        let f = Strings.toGzip >> toStream >> Strings.fromGzip

        f value.Get = value.Get

    [<Property(Verbose = true)>]
    let ``toLower returns lower`` (value: NonEmptyString) =
        let r = Strings.toLower value.Get

        r |> Seq.filter Char.IsLetter |> Seq.forall Char.IsLower

    [<Property(Verbose = true)>]
    let ``toUpper returns upper`` (value: NonEmptyString) =
        let r = Strings.toUpper value.Get

        r |> Seq.filter Char.IsLetter |> Seq.forall Char.IsUpper

    [<Property(Verbose = true)>]
    let ``trimSlash returns slashless string`` (value: NonEmptyString) (appendSlash: bool) =

        let s = value.Get.Replace("/", "") + (if appendSlash then "/" else "")

        let r = Strings.trimSlash s

        r.EndsWith("/") = false
