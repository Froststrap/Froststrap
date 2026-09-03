def main [
  project_file: string = "Froststrap/Froststrap.csproj"
  build_dir: string = "build"
  publish_profile: string = "Publish-linux-x64"
] {
  let config = "Release"
  let app_dir = $"($build_dir)/AppDir"
  let script_dir = ($env.CURRENT_FILE | path dirname)
  let repo_root = ($script_dir | path join ".." ".." | path expand)

  # Clean and Publish .NET
  rm -rf $build_dir
  mkdir $build_dir
  dotnet publish $project_file -c $config -p:PublishProfile=$publish_profile -o $"($build_dir)/linux-temp" --configfile $"($repo_root)/nuget.config"
  if $env.LAST_EXIT_CODE != 0 {
    print -e "dotnet publish failed"
    exit 1
  }

  # Setup Filesystem
  mkdir $"($app_dir)/usr/bin"
  mkdir $"($app_dir)/usr/share/applications"
  mkdir $"($app_dir)/usr/share/icons/hicolor/512x512/apps"

  cp $"($build_dir)/linux-temp/Froststrap" $"($app_dir)/usr/bin/Froststrap"
  cp "./Froststrap/Froststrap.png" $"($app_dir)/froststrap.png"
  cp "./Froststrap/Froststrap.png" $"($app_dir)/usr/share/icons/hicolor/512x512/apps/froststrap.png"
  chmod +x $"($app_dir)/usr/bin/Froststrap"
  rm -rf $"($build_dir)/linux-temp"

  # Version
  let raw_version = (do -i { git describe --tags --always --dirty } | complete | get stdout | str trim)
  let raw_version = if ($raw_version | is-empty) { "1.0.0" } else { $raw_version }
  let version = ($raw_version | str replace -r '^v' '' | str replace -a '-' '~')
  let rpm_version = ($version | str replace -a '+' '_')

  # Create Desktop Entry
  let desktop_entry = $"[Desktop Entry]
Type=Application
Name=Froststrap
Comment=A fork of Fishstrap, focused on performance and customization
Exec=Froststrap %u
TryExec=Froststrap
Icon=froststrap
Terminal=false
Categories=Game;
MimeType=x-scheme-handler/roblox;x-scheme-handler/roblox-player;
X-AppImage-Version=($version)
"
  $desktop_entry | save -f $"($app_dir)/Froststrap.desktop"
  cp $"($app_dir)/Froststrap.desktop" $"($app_dir)/usr/share/applications/Froststrap.desktop"

  # Build AppImage
  let apprun = "#!/bin/sh
HERE=\"$(dirname \"$(readlink -f \"$0\")\")\"
exec \"$HERE/usr/bin/Froststrap\" \"$@\"
"
  $apprun | save -f $"($app_dir)/AppRun"
  chmod +x $"($app_dir)/AppRun"

  let appimage_tool = if (which appimagetool | is-not-empty) {
    "appimagetool"
  } else {
    let tool_path = $"($build_dir)/appimagetool.AppImage"
    curl -L --fail -o $tool_path https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-x86_64.AppImage
    chmod +x $tool_path
    $tool_path
  }

  with-env {SOURCE_DATE_EPOCH: null, ARCH: "x86_64"} {
    ^$appimage_tool --appimage-extract-and-run $app_dir $"($build_dir)/Froststrap-linux-x64.AppImage"
  }

  let rpm_topdir = $"($repo_root)/($build_dir)/rpmbuild"
  mkdir $"($rpm_topdir)/BUILD" $"($rpm_topdir)/BUILDROOT" $"($rpm_topdir)/RPMS" $"($rpm_topdir)/SOURCES" $"($rpm_topdir)/SPECS" $"($rpm_topdir)/SRPMS"

  rpmbuild -bb $"($repo_root)/Scripts/fedora/froststrap-rpm.spec" --define $"_topdir ($rpm_topdir)" --define $"_froststrap_appdir ($repo_root)/($app_dir)" --define $"froststrap_version ($rpm_version)"

  let rpm_output = (ls -a ($rpm_topdir | path join "RPMS" "**" "*.rpm") | sort-by modified | get name | first)
  cp $rpm_output $"($build_dir)/Froststrap-linux-x64.rpm"

  # Build Debian Package
  mkdir $"($app_dir)/DEBIAN"
  $"Package: froststrap
Version: ($version)
Architecture: amd64
Maintainer: Froststrap-Dev
Depends: libicu-dev
Description: Roblox bootstrapper and mod manager
" | save -f $"($app_dir)/DEBIAN/control"

  cp Scripts/debian/postinst $"($app_dir)/DEBIAN/postinst"
  chmod 755 $"($app_dir)/DEBIAN/postinst"

  dpkg-deb --build $app_dir $"($build_dir)/Froststrap-linux-x64.deb"

  # Cleanup
  rm -rf $app_dir
  rm -rf $"($build_dir)/appimagetool.AppImage"
  rm -rf $rpm_topdir

  print "Linux builds complete"
}
