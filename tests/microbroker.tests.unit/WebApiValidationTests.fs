namespace microbroker.tests.unit

open System
open FsCheck.FSharp
open FsCheck.Xunit
open microbroker

module WebApiValidationTests =
    
    let strings = ArbMap.defaults |> ArbMap.arbitrary<string>

    let nonEmptyAlphanumerics = strings |> Arb.filter (Seq.forall Char.IsLetterOrDigit) |> Arb.filter ((<>) "")

    let validName (value: string) =
        let validNameCharacters = [ 'a' .. 'z' ] @ [ '0' .. '9' ] @ [ '-'; '_' ] |> Array.ofSeq

        value.Length > 0
        && Strings.lower value |> Seq.forall (fun c -> Array.contains c validNameCharacters)
    
    [<Property>]
    let ``isValidQueueName empty strings return false`` () =  
        Prop.forAll strings
            ( (String.IsNullOrEmpty >> not) ||>> (WebApiValidation.isValidQueueName >> not) )
            
    [<Property>]
    let ``isValidQueueName whitespace strings return false`` () =  
        Prop.forAll strings
            ( ( String.IsNullOrWhiteSpace >> not) ||>> (WebApiValidation.isValidQueueName >> not) )

    [<Property>]
    let ``isValidQueueName alphanumeric strings return true`` () =  
        Prop.forAll nonEmptyAlphanumerics WebApiValidation.isValidQueueName

    [<Property>]
    let ``isValidQueueName case insensitive alphanumeric strings return true`` () =  
        Prop.forAll nonEmptyAlphanumerics
            ( (Strings.lower >> WebApiValidation.isValidQueueName) =>> (Strings.lower >> WebApiValidation.isValidQueueName))

    [<Property>]
    let ``IsValidQueueName finds valid names`` () =
        Prop.forAll (strings |> Arb.filter validName) WebApiValidation.isValidQueueName

    [<Property>]
    let ``IsValidQueueName finds invalid names`` () =
        Prop.forAll (strings |> Arb.filter (validName >> not) ) (WebApiValidation.isValidQueueName >> not)

    [<Property>]
    let ``validateQueueName finds invalid names`` () =
        
        let names = strings |> Arb.filter (WebApiValidation.isValidQueueName >> not)
        
        Prop.forAll names 
            (fun n -> match WebApiValidation.validateQueueName n with
                        | Choice2Of2 _ -> false
                        | Choice1Of2 _ -> true)

    [<Property>]
    let ``validateQueueName finds valid names`` () =
        
        let names = strings |> Arb.filter WebApiValidation.isValidQueueName
        
        Prop.forAll names 
                    (fun n -> match WebApiValidation.validateQueueName n with
                                | Choice2Of2 _ -> true
                                | Choice1Of2 _ -> false)
