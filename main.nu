const project_file: string = "Froststrap/Froststrap.csproj"
const build_dir: string = "build"
const release_config: string = "Release"
let script_dir = ($env.CURRENT_FILE | path dirname)

let commands = [
  {
    name: "publish"
    description: "Makes a published executable"
  }
  {
    name: "build"
    description: "Runs the dotnet build command"
  }
  {
    name: "debug"
    description: "Publishes with a debug type"
  }
]

def publish [] {
  print "Running publish"
  match (uname | get operating-system) {
    "Darwin" => {
      nu ($script_dir | path join "Scripts/release/macos.nu")
    }
    "Linux" => {
      nu ($script_dir | path join "Scripts/release/linux.nu") $project_file $build_dir "Publish-linux-x64"
    }
    $s if ($s | str contains "Windows") => {
      nu ($script_dir | path join "Scripts/release/windows.nu") $project_file $build_dir
    }
    _ => {}
  }
}

def build [] {
  print "Running build"
  dotnet build -c $release_config --no-restore
}

def debug [] {
  print "Running debug"
  match (uname | get operating-system) {
    "Darwin" => {
      dotnet publish $project_file -r osx-arm64 -c Debug --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --configfile nuget.config
    }
    "Linux" => {
      dotnet publish $project_file -r linux-x64 -c Debug --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --configfile nuget.config
    }
    $s if ($s | str contains "Windows") => {
      dotnet publish $project_file -r win-x64 -c Debug --self-contained true -p:PublishSingleFile=true --configfile nuget.config
    }
    _ => {}
  }
}

def sayHelp [] {
  print $commands
}

def main [
  command?: string
] {
  if $command == null or --help == true {
    sayHelp
    return
  }

  match $command {
    "publish" => { publish }
    "build"   => { build }
    "debug"   => { debug }
    _ => {
      print $"Unknown command: ($command)"
      print ""
      sayHelp
    }
  }
}