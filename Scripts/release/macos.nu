def main [
  --sign
] {
  let xcode_project_dir = "packaging/macApp"
  let xcode_project = "macApp.xcodeproj"
  let xcode_scheme = "Froststrap"
  let entitlements_path = $"($xcode_project_dir)/Froststrap.entitlements"
  let config = "Release"
  let build_dir = "build"

  rm -rf $build_dir
  mkdir $build_dir

  print $"Building via xcodebuild \(($xcode_scheme), ($config)\)..."
  let derived_data_path = $"(pwd)/DerivedData"

  xcodebuild -project $"($xcode_project_dir)/($xcode_project)" -scheme $xcode_scheme -configuration $config -derivedDataPath $derived_data_path CODE_SIGNING_ALLOWED=NO build
  if $env.LAST_EXIT_CODE != 0 {
    print -e "xcodebuild failed"
    exit 1
  }

  let app_path = $"($derived_data_path)/Build/Products/($config)/Froststrap.app"
  if not ($app_path | path exists) {
    print -e $"ERROR: expected .app not found at ($app_path)"
    print -e "xcodebuild reported success but the .app isn't where -derivedDataPath should have put it -- check for a custom SYMROOT/CONFIGURATION_BUILD_DIR build setting overriding it in the project."
    exit 1
  }

  cp -r $app_path $"($build_dir)/Froststrap.app"

  if $sign {
    let developer_id_app = $env.DEVELOPER_ID_APP
    let developer_id_installer = $env.DEVELOPER_ID_INSTALLER
    let apple_key_id = $env.APPLE_KEY_ID
    let apple_issuer_id = $env.APPLE_ISSUER_ID

    print $"Signing .app with ($developer_id_app)"
    codesign --force --deep --options runtime --entitlements $entitlements_path --sign $developer_id_app $"($build_dir)/Froststrap.app"
    codesign --verify --verbose=4 $"($build_dir)/Froststrap.app"

    mkdir $"($build_dir)/payload/Applications"
    cp -r $"($build_dir)/Froststrap.app" $"($build_dir)/payload/Applications/Froststrap.app"
    pkgbuild --root $"($build_dir)/payload" --install-location / --identifier xyz.froststrap.desktop $"($build_dir)/Froststrap-unsigned.pkg"

    print $"Signing PKG with ($developer_id_installer)"
    productsign --sign $developer_id_installer $"($build_dir)/Froststrap-unsigned.pkg" $"($build_dir)/Froststrap.pkg"

    print "Submitting for notarization..."
    mkdir ~/.private_keys
    let key_path = $"~/.private_keys/AuthKey_($apple_key_id).p8" | path expand
    $env.APP_STORE_CONNECT_P8_CONTENT | save -f $key_path
    wc -l $key_path

    let result = (xcrun notarytool submit $"($build_dir)/Froststrap.pkg" --key-id $apple_key_id --issuer $apple_issuer_id --key $key_path --wait | complete)
    let submission_output = $"($result.stdout)($result.stderr)"
    print $submission_output

    if $result.exit_code != 0 {
      print -e $"notarytool exited with status ($result.exit_code) \(see output above\)"
      exit 1
    }

    let submission_id = ($submission_output | parse -r 'id: (?<id>[a-f0-9-]+)' | get id.0?)

    if ($submission_output | str contains "status: Invalid") {
      print "Notarization failed. Fetching detailed log..."
      xcrun notarytool log $submission_id --key-id $apple_key_id --issuer $apple_issuer_id --key $key_path
      exit 1
    }

    xcrun stapler staple $"($build_dir)/Froststrap.pkg"
    rm -f $key_path
    rm -rf $"($build_dir)/payload" $"($build_dir)/Froststrap-unsigned.pkg"
  } else {
    print "Building unsigned PKG (skipping signing)"
    mkdir $"($build_dir)/payload/Applications"
    cp -r $"($build_dir)/Froststrap.app" $"($build_dir)/payload/Applications/Froststrap.app"
    pkgbuild --root $"($build_dir)/payload" --install-location / --identifier xyz.froststrap.desktop $"($build_dir)/Froststrap.pkg"
    rm -rf $"($build_dir)/payload"
  }

  print $"macOS build complete: ($build_dir)/Froststrap.pkg"
}
