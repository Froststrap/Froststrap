open System
open System.IO
open System.Net.Http
open System.Text
open System.Text.Json
open System.Text.RegularExpressions
open System.Xml.Linq
open System.Security.Cryptography
open System.Diagnostics

module DeepLXTranslator =
    type TranslationRequest = {
        [<System.Text.Json.Serialization.JsonPropertyName("text")>] Text: string
        [<System.Text.Json.Serialization.JsonPropertyName("target_lang")>] TargetLang: string
    }

    type DeepLXResponse = {
        [<System.Text.Json.Serialization.JsonPropertyName("code")>] Code: int
        [<System.Text.Json.Serialization.JsonPropertyName("data")>] Data: string
    }

    type Config = {
        DeepLxUrl: string
        ProjectRoot: string
        ResourcesDir: string
        BaseFile: string
        CacheFile: string
        Languages: string list
        SkipLanguages: string list
        DeepLLangMap: Map<string, string>
        SkipPatterns: string list
    }

    let createConfig () =
        let scriptDir = __SOURCE_DIRECTORY__
        let projectRoot = Path.GetFullPath(Path.Combine(scriptDir, "..", ".."))
        let resourcesDir = Path.Combine(projectRoot, "Froststrap", "Resources")
        let baseDir = AppContext.BaseDirectory
        
        {
            DeepLxUrl = "http://localhost:1188/translate"
            ProjectRoot = projectRoot
            ResourcesDir = resourcesDir
            BaseFile = Path.Combine(resourcesDir, "Strings.resx")
            CacheFile = Path.Combine(baseDir, "deeplx_cache.json")
            Languages = [
                "ar"; "bg"; "cs"; "da"; "de"; "el"; "es-ES"; "et";
                "fi"; "fr"; "hu"; "id"; "it"; "ja"; "ko"; "lt"; 
                "lv"; "nl"; "pl"; "pt-BR"; "pt-PT"; "ro"; "ru"; 
                "sk"; "sl"; "sv-SE"; "tr"; "uk"; "vi"; "zh-CN"; "zh-TW"
            ]
            SkipLanguages = ["en-US"; "en"]
            DeepLLangMap = Map [
                ("pt-BR", "PT-BR"); ("pt-PT", "PT-PT"); ("sv-SE", "SV");
                ("zh-CN", "ZH"); ("zh-TW", "ZH-HANT"); ("es-ES", "ES");
                ("ar", "AR"); ("bg", "BG"); ("cs", "CS"); ("da", "DA");
                ("de", "DE"); ("el", "EL"); ("et", "ET"); ("fi", "FI");
                ("fr", "FR"); ("hu", "HU"); ("id", "ID"); ("it", "IT");
                ("ja", "JA"); ("ko", "KO"); ("lt", "LT"); ("lv", "LV");
                ("nl", "NL"); ("pl", "PL"); ("ro", "RO"); ("ru", "RU");
                ("sk", "SK"); ("sl", "SL"); ("sv", "SV"); ("tr", "TR");
                ("uk", "UK"); ("vi", "VI")
            ]
            SkipPatterns = [
                @"^\{[^}]+\}$"
                @"^https?://"
                @"^\[.*\]\(.*\)$"
                @"^#.*$"
                @"^[A-Z_]+$"
            ]
        }

    let httpClient = new HttpClient(Timeout = TimeSpan.FromSeconds(30.0))

    let computeMd5 (text: string) =
        use md5 = MD5.Create()
        let bytes = Encoding.UTF8.GetBytes(text)
        let hash = md5.ComputeHash(bytes)
        BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant()

    let getCacheKey (text: string) (lang: string) =
        sprintf "%s|%s" lang (computeMd5 text)

    let loadCache (cachePath: string) : Map<string, string> =
        if File.Exists(cachePath) then
            try
                let json = File.ReadAllText(cachePath)
                let result = JsonSerializer.Deserialize<Map<string, string>>(json)
                if box result <> null then result else Map.empty
            with _ -> Map.empty
        else Map.empty

    let saveCache (cachePath: string) (cache: Map<string, string>) =
        try
            let options = JsonSerializerOptions(WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping)
            let json = JsonSerializer.Serialize(cache, options)
            File.WriteAllText(cachePath, json)
        with ex -> printfn "Failed to save cache: %s" ex.Message

    let shouldSkip (patterns: string list) (text: string) =
        if String.IsNullOrWhiteSpace(text) || text.Trim().Length < 2 then true
        else patterns |> List.exists (fun pattern -> Regex.IsMatch(text, pattern))

    let getDeepLCode (config: Config) (lang: string) =
        match config.DeepLLangMap.TryFind(lang) with
        | Some code -> code
        | None -> lang.ToUpperInvariant()

    let translateWithRetry (config: Config) (cache: Map<string, string>) (text: string) (targetLang: string) (maxRetries: int) =
        if shouldSkip config.SkipPatterns text then (text, cache)
        else
            let cacheKey = getCacheKey text targetLang
            match cache.TryFind(cacheKey) with
            | Some cached -> (cached, cache)
            | None ->
                let rec attemptLoop retryCount waitTime =
                    if retryCount >= maxRetries then (text, cache)
                    else
                        try
                            let target = getDeepLCode config targetLang
                            let payload = { Text = text; TargetLang = target }
                            let jsonPayload = JsonSerializer.Serialize(payload)
                            use content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                            
                            let response = httpClient.PostAsync(config.DeepLxUrl, content).GetAwaiter().GetResult()

                            match int response.StatusCode with
                            | 400 ->
                                if retryCount < maxRetries - 1 then System.Threading.Thread.Sleep(1500); attemptLoop (retryCount + 1) waitTime
                                else (text, cache)
                            | 429 ->
                                printfn "    Rate limited (429). Backing off for %d seconds..." waitTime
                                System.Threading.Thread.Sleep(waitTime * 1000)
                                let nextWait = Math.Min(waitTime + 3, 15)
                                attemptLoop (retryCount + 1) nextWait
                            | status when status >= 500 ->
                                if retryCount < maxRetries - 1 then System.Threading.Thread.Sleep(3000); attemptLoop (retryCount + 1) waitTime
                                else (text, cache)
                            | 200 ->
                                let respBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                                let result = JsonSerializer.Deserialize<DeepLXResponse>(respBody)
                                
                                if box result = null || result.Code <> 200 then
                                    if retryCount < maxRetries - 1 then System.Threading.Thread.Sleep(2000); attemptLoop (retryCount + 1) waitTime
                                    else (text, cache)
                                else
                                    let translated = if String.IsNullOrEmpty(result.Data) then text else result.Data
                                    if translated = text && text.Length > 3 then
                                        if retryCount = 0 then
                                            printfn "    Retrying once (translation failed)..."
                                            System.Threading.Thread.Sleep(3000)
                                            attemptLoop (retryCount + 1) waitTime
                                        else (text, cache)
                                    else
                                        let updatedCache = cache.Add(cacheKey, translated)
                                        saveCache config.CacheFile updatedCache
                                        (translated, updatedCache)
                            | _ -> (text, cache)
                        with _ ->
                            if retryCount = maxRetries - 1 then (text, cache)
                            else
                                System.Threading.Thread.Sleep(3000)
                                attemptLoop (retryCount + 1) waitTime

                attemptLoop 0 5

    let translateBatch (config: Config) (cache: Map<string, string>) (texts: string list) (targetLang: string) =
        if List.isEmpty texts then (Map.empty, cache)
        else
            let mutable currentCache = cache
            let mutable results = Map.empty
            let mutable uncached = []

            for text in texts do
                if shouldSkip config.SkipPatterns text then
                    results <- results.Add(text, text)
                else
                    let cacheKey = getCacheKey text targetLang
                    match currentCache.TryFind(cacheKey) with
                    | Some cached -> results <- results.Add(text, cached)
                    | None -> uncached <- text :: uncached

            let uncachedList = List.rev uncached
            if not (List.isEmpty uncachedList) then
                printfn "    Translating %d strings..." uncachedList.Length
                
                for i = 0 to uncachedList.Length - 1 do
                    let text = uncachedList.[i]
                    if i > 0 && i % 10 = 0 then
                        printfn "    Progress: %d/%d (Cooling down 3s...)" i uncachedList.Length
                        System.Threading.Thread.Sleep(3000)

                    let (translated, newCache) = translateWithRetry config currentCache text targetLang 4
                    currentCache <- newCache
                    results <- results.Add(text, translated)

                    if i < uncachedList.Length - 1 then
                        System.Threading.Thread.Sleep(1800)

                saveCache config.CacheFile currentCache

            (results, currentCache)

    let getBaseHeader (baseFile: string) =
        if not (File.Exists baseFile) then ""
        else
            let baseContent = File.ReadAllText(baseFile, Encoding.UTF8)
            let schemaEnd = baseContent.IndexOf("</xsd:schema>")
            if schemaEnd <> -1 then
                let dataStart = baseContent.IndexOf("<data name=", schemaEnd)
                if dataStart <> -1 then baseContent.Substring(0, dataStart)
                else baseContent
            else
                let resheaderEnd = baseContent.LastIndexOf("</resheader>")
                if resheaderEnd <> -1 then
                    let dataStart = baseContent.IndexOf("<data name=", resheaderEnd)
                    if dataStart <> -1 then baseContent.Substring(0, dataStart)
                    else baseContent
                else
                    let dataStart = baseContent.IndexOf("<data name=")
                    if dataStart = -1 then baseContent else baseContent.Substring(0, dataStart)

    let getStrings (baseFile: string) =
        if not (File.Exists baseFile) then
            printfn "Base file not found: %s" baseFile
            Map.empty
        else
            let doc = XDocument.Load(baseFile)
            doc.Descendants(XName.Get "data")
            |> Seq.choose (fun data ->
                let nameAttr = Option.ofObj (data.Attribute(XName.Get "name"))
                let valueElem = Option.ofObj (data.Element(XName.Get "value"))
                let commentElem = Option.ofObj (data.Element(XName.Get "comment"))

                match nameAttr, valueElem with
                | Some n, Some v when not (String.IsNullOrEmpty v.Value) ->
                    let isSkippedComment = 
                        match commentElem with
                        | Some c -> c.Value = "Boolean" || c.Value = "Int32" || c.Value = "StringArray"
                        | None -> false
                    if isSkippedComment then None else Some (n.Value, v.Value)
                | _ -> None)
            |> Map.ofSeq

    let getExistingTranslations (langFile: string) =
        if not (File.Exists langFile) then Map.empty
        else
            try
                let doc = XDocument.Load(langFile)
                doc.Descendants(XName.Get "data")
                |> Seq.choose (fun data ->
                    let nameAttr = Option.ofObj (data.Attribute(XName.Get "name"))
                    let valueElem = Option.ofObj (data.Element(XName.Get "value"))
                    match nameAttr, valueElem with
                    | Some n, Some v when not (String.IsNullOrEmpty v.Value) -> Some (n.Value, v.Value)
                    | _ -> None)
                |> Map.ofSeq
            with _ -> Map.empty

    let formatResxFile (dataElements: XElement list) (baseHeader: string) =
        let sb = StringBuilder()
        sb.Append(baseHeader.TrimEnd()).Append("\n") |> ignore

        let sortedElements = 
            dataElements 
            |> List.sortBy (fun elem -> 
                match Option.ofObj (elem.Attribute(XName.Get "name")) with
                | Some a -> a.Value
                | None -> "")

        for data in sortedElements do
            let nameAttr = Option.ofObj (data.Attribute(XName.Get "name"))
            let valueElem = Option.ofObj (data.Element(XName.Get "value"))
            let commentElem = Option.ofObj (data.Element(XName.Get "comment"))

            match nameAttr, valueElem with
            | Some name, Some value ->
                let valueText = if value.Value <> null then value.Value else ""
                let escapedValue = System.Security.SecurityElement.Escape(valueText)

                sb.Append(sprintf "  <data name=\"%s\" xml:space=\"preserve\">\n" name.Value) |> ignore
                sb.Append(sprintf "    <value>%s</value>\n" escapedValue) |> ignore

                match commentElem with
                | Some comment when not (String.IsNullOrEmpty comment.Value) ->
                    let escapedComment = System.Security.SecurityElement.Escape(comment.Value)
                    sb.Append(sprintf "    <comment>%s</comment>\n" escapedComment) |> ignore
                | _ -> ()

                sb.Append("  </data>\n") |> ignore
            | _ -> ()

        sb.Append("</root>").ToString()

    let translateLanguage (config: Config) (cache: Map<string, string>) (lang: string) (baseStrings: Map<string, string>) =
        if List.contains lang config.SkipLanguages then
            printfn "Skipping %s (base English file)" lang
            (0, cache)
        else
            let langFile = Path.Combine(config.ResourcesDir, sprintf "Strings.%s.resx" lang)
            printfn "Processing %s..." lang

            let existingTranslations = getExistingTranslations langFile
            let existingKeys = existingTranslations |> Map.keys |> Set.ofSeq
            let baseKeys = baseStrings |> Map.keys |> Set.ofSeq

            let addedKeys = Set.difference baseKeys existingKeys
            let removedKeys = Set.difference existingKeys baseKeys
            let commonKeys = Set.intersect baseKeys existingKeys

            let changedKeys = 
                commonKeys 
                |> Set.filter (fun key -> baseStrings.[key] <> existingTranslations.[key])

            if Set.isEmpty addedKeys && Set.isEmpty removedKeys && Set.isEmpty changedKeys then
                printfn "  No changes"
                (0, cache)
            else
                if not (Set.isEmpty addedKeys) then printfn "  Added: %d new strings" addedKeys.Count
                if not (Set.isEmpty changedKeys) then printfn "  Changed: %d strings updated" changedKeys.Count
                if not (Set.isEmpty removedKeys) then printfn "  Removed: %d strings" removedKeys.Count

                let needTranslationKeys = Set.union addedKeys changedKeys
                let needTranslationMap = 
                    needTranslationKeys 
                    |> Seq.map (fun k -> k, baseStrings.[k]) 
                    |> Map.ofSeq

                let mutable translatedMap = Map.empty
                let mutable currentCache = cache

                if not (Map.isEmpty needTranslationMap) then
                    printfn "  Translating %d strings..." needTranslationMap.Count
                    let textList = needTranslationMap |> Map.values |> Seq.toList
                    let (resMap, updatedCache) = translateBatch config currentCache textList lang
                    translatedMap <- resMap
                    currentCache <- updatedCache

                let baseHeader = getBaseHeader config.BaseFile
                if String.IsNullOrEmpty baseHeader then
                    printfn "  Failed to read base header!"
                    (0, currentCache)
                else
                    let baseDoc = XDocument.Load(config.BaseFile)
                    let newDoc = XDocument(baseDoc)
                    
                    newDoc.Descendants(XName.Get "data") |> Seq.toList |> List.iter (fun elem -> elem.Remove())

                    let root = Option.ofObj newDoc.Root
                    match root with
                    | None -> (0, currentCache)
                    | Some rootNode ->
                        for KeyValue(key, original) in needTranslationMap do
                            let translated = match translatedMap.TryFind(original) with Some t -> t | None -> original
                            if translated <> original then
                                let dataElem = XElement(XName.Get "data", XAttribute(XName.Get "name", key), XAttribute(XNamespace.Xml + "space", "preserve"))
                                let valueElem = XElement(XName.Get "value", translated)
                                dataElem.Add(valueElem)

                                let baseData = 
                                    baseDoc.Descendants(XName.Get "data") 
                                    |> Seq.tryFind (fun e -> 
                                        match Option.ofObj (e.Attribute(XName.Get "name")) with
                                        | Some a -> a.Value = key
                                        | None -> false)

                                match baseData with
                                | Some bd ->
                                    let comment = Option.ofObj (bd.Element(XName.Get "comment"))
                                    match comment with
                                    | Some c when not (String.IsNullOrEmpty c.Value) ->
                                        dataElem.Add(XElement(XName.Get "comment", c.Value))
                                    | _ -> ()
                                | None -> ()

                                rootNode.Add(dataElem)

                        let dataElements = newDoc.Descendants(XName.Get "data") |> Seq.toList
                        let formattedContent = formatResxFile dataElements baseHeader

                        File.WriteAllText(langFile, formattedContent, Encoding.UTF8)
                        let totalChanges = addedKeys.Count + changedKeys.Count + removedKeys.Count
                        printfn "  Added %d new, updated %d, removed %d" addedKeys.Count changedKeys.Count removedKeys.Count
                        (totalChanges, currentCache)

    let isDeepLxRunning (url: string) =
        try
            let payload = { Text = "Hello"; TargetLang = "ES" }
            let json = JsonSerializer.Serialize(payload)
            use content = new StringContent(json, Encoding.UTF8, "application/json")
            let resp = httpClient.PostAsync(url, content).GetAwaiter().GetResult()
            resp.IsSuccessStatusCode
        with _ -> false

    let startDeepLx () =
        try
            printfn "Starting DeepLX container..."
            
            let runProc (cmd: string) (args: string) =
                let psi = ProcessStartInfo(cmd, args)
                psi.RedirectStandardOutput <- true
                psi.RedirectStandardError <- true
                psi.UseShellExecute <- false
                use p = Process.Start(psi)
                if p <> null then
                    p.WaitForExit()
                    p.ExitCode, p.StandardError.ReadToEnd()
                else
                    -1, "Failed to start process"

            runProc "docker" "rm -f deeplx" |> ignore

            let code1, err1 = runProc "docker" "run -d --name deeplx -p 1188:1188 --restart unless-stopped ghcr.io/owo-network/deeplx:latest"
            if code1 <> 0 then
                printfn "Failed to start DeepLX: %s" err1
                printfn "Trying alternative DeepLX image..."
                let code2, err2 = runProc "docker" "run -d --name deeplx -p 1188:1188 --restart unless-stopped missuo/deeplx:latest"
                if code2 <> 0 then
                    printfn "Failed to start with alternative image: %s" err2
                    false
                else
                    printfn "Waiting for DeepLX to initialize..."
                    System.Threading.Thread.Sleep(10000)
                    true
            else
                printfn "Waiting for DeepLX to initialize..."
                System.Threading.Thread.Sleep(10000)
                true
        with ex ->
            printfn "Failed to start DeepLX: %s" ex.Message
            false

    let translateAll () =
        let config = createConfig ()
        printfn "\nStarting translation with DeepLX..."

        if not (isDeepLxRunning config.DeepLxUrl) then
            printfn "DeepLX is not running. Attempting to start it..."
            if not (startDeepLx ()) then
                printfn "Failed to start DeepLX!"
            else ()
        else
            printfn "DeepLX is running.\n"

        let baseStrings = getStrings config.BaseFile
        if Map.isEmpty baseStrings then
            printfn "No strings found!"
        else
            printfn "Found %d strings in base file" baseStrings.Count
            printfn "Translating %d languages\n" config.Languages.Length

            let mutable cache = loadCache config.CacheFile
            let mutable totalStats = 0

            config.Languages |> List.iteri (fun i lang ->
                printf "[%d/%d] " (i + 1) config.Languages.Length
                let (changes, updatedCache) = translateLanguage config cache lang baseStrings
                cache <- updatedCache
                totalStats <- totalStats + changes

                if i < config.Languages.Length - 1 then
                    printfn "  Language complete. Cooling down 5 seconds before next language..."
                    System.Threading.Thread.Sleep(5000)
            )

            printfn "\nComplete. Processed %d changes across %d languages" totalStats config.Languages.Length

[<EntryPoint>]
let main argv =
    DeepLXTranslator.translateAll()
    0
