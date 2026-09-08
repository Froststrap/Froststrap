def main [] {
    let redist_dir = "redist"
    mkdir $redist_dir

    let files = [
        {
            url: "https://aka.ms/vs/17/release/vc_redist.x64.exe"
            path: $"($redist_dir)/vc_redist.x64.exe"
        }
        {
            url: "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/10.0.11/windowsdesktop-runtime-10.0.11-win-x64.exe"
            path: $"($redist_dir)/windowsdesktop-runtime-10-win-x64.exe"
        }
    ]

    for f in $files {
        if not ($f.path | path exists) {
            print $"Downloading ($f.url)"
            http get $f.url | save $f.path
        } else {
            print $"Already have ($f.path), skipping"
        }
    }
}
