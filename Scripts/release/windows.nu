def main [
  project: string = "Froststrap/Froststrap.csproj"
  build_dir: string = "build"
  config: string = "Release"
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

  let publish_dir_abs = ($temp_publish | path expand)
  let wix_project = "packaging/winInstaller/Froststrap.wixproj"

  dotnet build $wix_project -c $config -p:PublishDir=$publish_dir_abs -p:AppVersion=$version
  if $env.LAST_EXIT_CODE != 0 {
    print -e $"WiX build failed with exit code ($env.LAST_EXIT_CODE)"
    exit $env.LAST_EXIT_CODE
  }

  let wix_output = "packaging/winInstaller/bin/Release/Froststrap-Setup.msi"
  cp $wix_output $"($build_dir)/Froststrap-Setup.msi"

  rm -r -f $temp_publish
  print $"(ansi green)Windows installer complete: ($build_dir)/Froststrap-Setup.msi(ansi reset)"
}
