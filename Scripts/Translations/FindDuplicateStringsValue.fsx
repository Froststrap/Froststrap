open System
open System.IO
open System.Xml.Linq

module ResxDuplicateFinder =
    let getResourcesPath () =
        let scriptDir = __SOURCE_DIRECTORY__
        Path.Combine(scriptDir, "..", "..", "Froststrap", "Resources", "Strings.resx")
        |> Path.GetFullPath

    let parseResx (resxPath: string) =
        if not (File.Exists resxPath) then
            printfn "Error: Strings.resx not found at %s" resxPath
            []
        else
            try
                let doc = XDocument.Load(resxPath)
                doc.Descendants(XName.Get "data")
                |> Seq.choose (fun data ->
                    let name = data.Attribute(XName.Get "name")
                    let valueElem = data.Element(XName.Get "value")

                    match name, valueElem with
                    | null, _ | _, null -> None
                    | n, v when String.IsNullOrWhiteSpace v.Value -> None
                    | n, v ->
                        let key = n.Value
                        let value = v.Value.Trim()

                        if key.Contains("Enum") || value.Length < 2 || Seq.forall Char.IsDigit value then
                            None
                        else
                            Some(value, key))
                |> Seq.toList
            with (ex: System.Exception) ->
                printfn "Error parsing %s: %s" resxPath ex.Message
                []

    let findDuplicates (resxPath: string) =
        parseResx resxPath
        |> Seq.groupBy fst
        |> Seq.map (fun (value, group) -> value, group |> Seq.map snd |> Seq.sort |> Seq.toList)
        |> Seq.filter (fun (_, keys) -> keys.Length > 1)
        |> Seq.sortByDescending (fun (_, keys) -> keys.Length)
        |> Seq.toList

    let printDuplicates (duplicates: (string * string list) list) =
        if List.isEmpty duplicates then
            printfn "\nNo duplicate string values found in Strings.resx"
        else
            printfn "\nFile: Strings.resx"

            let totalGroups = duplicates.Length
            let totalEntries = duplicates |> List.sumBy (fun (_, keys) -> List.length keys)

            for value, keys in duplicates do
                printfn "\n  Value: \"%s\"" value
                printfn "      Used in %d keys:" keys.Length
                for key in keys do
                    printfn "        - %s" key

            printfn "\nSummary:"
            printfn "  - %d duplicate value groups found" totalGroups
            printfn "  - %d total duplicate entries" totalEntries
            printfn "  - %d unique duplicate values" totalGroups

let baseFile = ResxDuplicateFinder.getResourcesPath()

printfn "RESX Duplicate Value Finder\n"
printfn "Looking for: %s\n" baseFile

let duplicates = ResxDuplicateFinder.findDuplicates baseFile
ResxDuplicateFinder.printDuplicates duplicates
