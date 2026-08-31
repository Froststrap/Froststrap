open System
open System.IO
open System.Text.RegularExpressions

printf "Path of folder of exported Crowdin files: "
let exports = Console.ReadLine()

printf "Destination resources folder: "
let dest = Console.ReadLine()

let files = Directory.EnumerateFiles(exports, "*.*", SearchOption.AllDirectories)

for filename in files do
    let m = Regex.Match(filename, @"\\([a-zA-Z-]+)\\Strings\.")
    
    if m.Success then
        printfn "Copying %s" filename
        let localeCode = m.Groups.[1].Value
        let targetPath = Path.Combine(dest, sprintf "Strings.%s.resx" localeCode)
        
        File.Copy(filename, targetPath, overwrite = true)
