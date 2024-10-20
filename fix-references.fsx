open System.IO

let lines = File.ReadAllLines "references.fsx"

let fixedLines = lines |> Array.map (fun line ->
    if line.StartsWith("#r") then
        if line.Contains("/usr/share/dotnet/packs/") then
            line.Replace("/packs/", "/shared/")
                .Replace(".Ref/","/")
                .Replace("/ref/net8.0/", "/")
        else if line.Contains("/obj/") || line.Contains("/bin") then
            line.Replace("/ref/", "/")
        else
            line.Replace("/ref/", "/lib/")
        
    else
        line
)

File.WriteAllLines("references-fixed.fsx", fixedLines)
 