def main [
  --project: string = "Froststrap/Froststrap.csproj"
  --build-dir: string = "build"
  --config: string = "Release"
] {
  mkdir $build_dir
  let temp_publish = $"($build_dir)/temp-contained"
  let version = (git describe --tags --abbrev=0 | str trim | str replace -r '^v' '')
  let nuget_config = $"($env.CURRENT_FILE | path dirname)/../../nuget.config"

  dotnet publish $project /p:PublishProfile=Publish-windows-x64 -c $config -o $temp_publish --configfile $nuget_config

  if $env.LAST_EXIT_CODE != 0 {
    print -e $"dotnet publish failed with exit code ($env.LAST_EXIT_CODE)"
    exit $env.LAST_EXIT_CODE
  }

  makensis $"/DPUBLISH_DIR=..\\($temp_publish)" $"/DAPP_VERSION=($version)" "/DSELFCONTAINED=1" Scripts/Installer.nsi

  rm -r -f $temp_publish

  print $"(ansi green)Self-contained Windows installer complete: ($build_dir)/Froststrap-Setup.exe(ansi reset)"
}
