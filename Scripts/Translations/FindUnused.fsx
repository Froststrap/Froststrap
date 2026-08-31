open System
open System.IO
open System.Text.RegularExpressions
open System.Xml.Linq

module ProjectDetector =
    let detect () =
        let scriptDir = __SOURCE_DIRECTORY__
        let projectRoot = Path.GetFullPath(Path.Combine(scriptDir, "..", ".."))
        let csprojDir = Path.Combine(projectRoot, "Froststrap")

        printfn "Select project root:"
        printfn "1. Auto-detect (Froststrap.csproj)"
        printfn "2. Manual selection"
        printf "Enter choice (1 or 2): "

        let choice = Console.ReadLine().Trim()

        if choice = "1" then
            let csprojPath = Path.Combine(csprojDir, "Froststrap.csproj")
            if Directory.Exists(csprojDir) && File.Exists(csprojPath) then
                csprojDir
            else
                printfn "Auto-detection failed. Falling back to manual selection."
                printf "Enter project path (the one containing Froststrap.csproj): "
                Console.ReadLine().Trim()
        else
            printf "Enter project path (the one containing Froststrap.csproj): "
            Console.ReadLine().Trim()

module ResxCleaner =
    let removeUnusedStrings (resxPath: string) (unusedStrings: string list) =
        if List.isEmpty unusedStrings then
            printfn "No unused strings to remove."
            false
        else
            printf "\nDo you want to remove %d unused strings from Strings.resx? (yes/no): " unusedStrings.Length
            let response = Console.ReadLine().Trim().ToLower()

            if response <> "yes" && response <> "y" then
                false
            else
                try
                    let unusedSet = Set.ofList unusedStrings
                    let doc = XDocument.Load(resxPath)

                    let mutable removedCount = 0
                    let dataElements = doc.Descendants(XName.Get "data") |> Seq.toList

                    for elem in dataElements do
                        let nameAttr = elem.Attribute(XName.Get "name")
                        if nameAttr <> null && Set.contains nameAttr.Value unusedSet then
                            elem.Remove()
                            removedCount <- removedCount + 1

                    doc.Save(resxPath)
                    printfn "Successfully removed %d unused strings from Strings.resx" removedCount
                    true
                with (ex: System.Exception) ->
                    printfn "Error removing unused strings: %s" ex.Message
                    false

    let run () =
        let directory = ProjectDetector.detect()
        let resxPath = Path.Combine(directory, "Resources", "Strings.resx")

        if not (File.Exists resxPath) then
            printfn "Strings.resx not found at %s" resxPath
        else
            let resxContent = File.ReadAllText(resxPath)
            let existingMatches = Regex.Matches(resxContent, @"name=""([a-zA-Z0-9.]+)"" xml:space=""preserve""")
            let existing = [ for m in existingMatches -> m.Groups.[1].Value ]

            let allFiles = Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
            
            let found =
                allFiles
                |> Seq.filter (fun file ->
                    let normalized = file.Replace('\\', '/')
                    not (normalized.Contains("/bin/") || normalized.Contains("/obj/") || normalized.Contains("/Resources/")))
                |> Seq.collect (fun file ->
                    try
                        let contents = File.ReadAllText(file)

                        let stringsMatches = 
                            Regex.Matches(contents, @"Strings\.([a-zA-Z0-9_]+)")
                            |> Seq.cast<Match>
                            |> Seq.choose (fun m ->
                                let value = m.Groups.[1].Value
                                if value.Contains("_") then Some (value.Replace("_", ".")) else None)

                        let translationMatches = 
                            Regex.Matches(contents, @"FromTranslation\s*=\s*""([a-zA-Z0-9.]+)""")
                            |> Seq.cast<Match>
                            |> Seq.map (fun m -> m.Groups.[1].Value)

                        Seq.append stringsMatches translationMatches
                    with _ -> Seq.empty)
                |> Set.ofSeq

            let unused =
                existing
                |> List.filter (fun entry ->
                    not (Set.contains entry found) &&
                    not (entry.Contains("Enums.")) &&
                    entry <> "CustomTheme.Error")

            for entry in unused do
                printfn "%s" entry

            if not (List.isEmpty unused) then
                removeUnusedStrings resxPath unused |> ignore
            else
                printfn "No unused strings found."

ResxCleaner.run()
