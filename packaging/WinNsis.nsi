!include "MUI2.nsh"
!include "nsDialogs.nsh"
!include "LogicLib.nsh"

SetCompressor /SOLID /FINAL lzma

Var DesktopShortcutCheckbox
Var StartMenuShortcutCheckbox
Var CreateDesktopShortcut
Var CreateStartMenuShortcut
Var KeepUserDataCheckbox
Var KeepUserData
Var ExistingInstallChoice ; 0=Upgrade, 1=Clean
Var IsExistingInstall     ; 1 if Froststrap is already installed

Name "Froststrap"

!define MUI_ICON "..\Froststrap\Froststrap.ico"
!define MUI_UNICON "..\Froststrap\Froststrap.ico"

!ifndef PUBLISH_DIR
  !define PUBLISH_DIR "..\build"
!endif

!ifndef APP_VERSION
  !define APP_VERSION "Unknown"
!endif

!ifdef SELFCONTAINED
  OutFile "..\build\Froststrap-Setup.exe"
!else
  OutFile "${PUBLISH_DIR}\Froststrap-Setup.exe"
!endif

Icon "..\Froststrap\Froststrap.ico"
UninstallIcon "..\Froststrap\Froststrap.ico"
InstallDir "$LOCALAPPDATA\Froststrap"
InstallDirRegKey HKCU "Software\Froststrap" "InstallLocation"
RequestExecutionLevel user

!define APP_NAME "Froststrap"
!define APP_EXE "Froststrap.exe"
!define APP_UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\Froststrap"
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "Launch Froststrap"

!insertmacro MUI_PAGE_DIRECTORY
Page Custom ExistingInstallPageCreate ExistingInstallPageLeave
Page Custom OptionsPageCreate OptionsPageLeave
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
UninstPage Custom un.OptionsPageCreate un.OptionsPageLeave
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

; ---------------------------------------------------------------------------
; Existing installation options page
; ---------------------------------------------------------------------------

Function ExistingInstallPageCreate
    IfSilent silent notsilent
silent:
    Abort
notsilent:

    IfFileExists "$INSTDIR\${APP_EXE}" exists notexists
notexists:
    Abort
exists:
    StrCpy $IsExistingInstall 1

    nsDialogs::Create 1018
    Pop $0
    ${If} $0 == error
        Abort
    ${EndIf}

    ${NSD_CreateLabel} 0 0 100% 24u "Froststrap is already installed in:$\n$INSTDIR$\n$\nWhat would you like to do?"

    ${NSD_CreateRadioButton} 0 60u 100% 12u "Upgrade (keep your settings and data)"
    Pop $0
    ${NSD_Check} $0
    StrCpy $ExistingInstallChoice 0

    ${NSD_CreateRadioButton} 0 80u 100% 12u "Clean install (remove all existing data before installing)"
    Pop $1

    nsDialogs::Show
FunctionEnd

Function ExistingInstallPageLeave
    ${NSD_GetState} $0 $2
    ${If} $2 == ${BST_CHECKED}
        StrCpy $ExistingInstallChoice 0
    ${Else}
        StrCpy $ExistingInstallChoice 1
    ${EndIf}
FunctionEnd

; ---------------------------------------------------------------------------
; Shortcut selection page
; ---------------------------------------------------------------------------

Function OptionsPageCreate
    ${If} $IsExistingInstall == 1
        Abort
    ${EndIf}

    nsDialogs::Create 1018
    Pop $0
    ${If} $0 == error
        Abort
    ${EndIf}
    ${NSD_CreateLabel} 0 0 100% 24u "Choose which shortcuts to create:"
    ${NSD_CreateCheckBox} 0 30u 100% 12u "Desktop shortcut"
    Pop $DesktopShortcutCheckbox
    ${NSD_Check} $DesktopShortcutCheckbox
    ${NSD_CreateCheckBox} 0 48u 100% 12u "Start Menu shortcut"
    Pop $StartMenuShortcutCheckbox
    ${NSD_Check} $StartMenuShortcutCheckbox
    nsDialogs::Show
FunctionEnd

Function OptionsPageLeave
    ${NSD_GetState} $DesktopShortcutCheckbox $CreateDesktopShortcut
    ${NSD_GetState} $StartMenuShortcutCheckbox $CreateStartMenuShortcut
FunctionEnd

; ---------------------------------------------------------------------------
; Uninstall pages
; ---------------------------------------------------------------------------

Function un.OptionsPageCreate
    nsDialogs::Create 1018
    Pop $0
    ${If} $0 == error
        Abort
    ${EndIf}
    ${NSD_CreateLabel} 0 0 100% 24u "Uninstall options:"
    ${NSD_CreateCheckBox} 0 30u 100% 12u "Keep user data (saves, settings, logs)"
    Pop $KeepUserDataCheckbox
    ${NSD_Check} $KeepUserDataCheckbox
    nsDialogs::Show
FunctionEnd

Function un.OptionsPageLeave
    ${NSD_GetState} $KeepUserDataCheckbox $KeepUserData
FunctionEnd

; ---------------------------------------------------------------------------
; Install section
; ---------------------------------------------------------------------------

Section "Froststrap"
    ${If} $ExistingInstallChoice == 1
        RMDir /r "$INSTDIR"
    ${EndIf}

    SetOutPath "$INSTDIR"
    File /r "${PUBLISH_DIR}\Froststrap.exe"
    File /r "${PUBLISH_DIR}\FluentAvalonia.xml"
    File /r "..\Froststrap\Froststrap.ico"

    ; Froststrap app registry keys (used by the app to locate itself)
    WriteRegStr HKCU "Software\Froststrap" "InstallLocation" "$INSTDIR"
    WriteRegStr HKCU "Software\Froststrap" "AppPath" "$INSTDIR\${APP_EXE}"

    ; Programs & Features / winget uninstall entry
    WriteRegStr HKCU "${APP_UNINSTALL_KEY}" "DisplayName"      "${APP_NAME}"
    WriteRegStr HKCU "${APP_UNINSTALL_KEY}" "DisplayVersion"   "${APP_VERSION}"
    WriteRegStr HKCU "${APP_UNINSTALL_KEY}" "InstallLocation"  "$INSTDIR"
    WriteRegStr HKCU "${APP_UNINSTALL_KEY}" "DisplayIcon"      "$INSTDIR\${APP_EXE},0"
    WriteRegStr HKCU "${APP_UNINSTALL_KEY}" "Publisher"        "Froststrap"
    WriteRegStr HKCU "${APP_UNINSTALL_KEY}" "UninstallString"  '"$INSTDIR\Uninstall.exe"'
    WriteRegStr HKCU "${APP_UNINSTALL_KEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
    WriteRegStr HKCU "${APP_UNINSTALL_KEY}" "ModifyPath"       '"$INSTDIR\${APP_EXE}" -settings'
    WriteRegStr HKCU "${APP_UNINSTALL_KEY}" "HelpLink"         "https://github.com/Froststrap/Froststrap/wiki"
    WriteRegStr HKCU "${APP_UNINSTALL_KEY}" "URLInfoAbout"     "https://github.com/Froststrap/Froststrap/issues/new"
    WriteRegStr HKCU "${APP_UNINSTALL_KEY}" "URLUpdateInfo"    "https://github.com/Froststrap/Froststrap/releases"
    WriteRegDWORD HKCU "${APP_UNINSTALL_KEY}" "NoRepair"       1

    ${If} $CreateStartMenuShortcut == ${BST_CHECKED}
        CreateDirectory "$SMPROGRAMS\Froststrap"
        CreateShortCut "$SMPROGRAMS\Froststrap\Froststrap.lnk" "$INSTDIR\${APP_EXE}"
    ${EndIf}
    ${If} $CreateDesktopShortcut == ${BST_CHECKED}
        CreateShortCut "$DESKTOP\Froststrap.lnk" "$INSTDIR\${APP_EXE}"
    ${EndIf}

    WriteUninstaller "$INSTDIR\Uninstall.exe"

    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\App Paths\${APP_EXE}" "" "$INSTDIR\${APP_EXE}"
SectionEnd

; ---------------------------------------------------------------------------
; Uninstall section
; ---------------------------------------------------------------------------

Section "Uninstall"
    ; For silent uninstalls (/S), the options page never runs so $KeepUserData
    ; is unset. Default it to checked (keep data) to be safe, silent uninstalls
    ; are triggered by the auto-updater which should never wipe user data.
    ${If} $KeepUserData == ""
        StrCpy $KeepUserData ${BST_CHECKED}
    ${EndIf}

    ; Step 1: let the app kill Roblox and restore protocol handlers.
    ExecWait '"$INSTDIR\${APP_EXE}" -uninstall -quiet -nsis' $0

    ; Step 2: NSIS cleans up what it owns

    ; Shortcuts
    Delete "$DESKTOP\Froststrap.lnk"
    Delete "$SMPROGRAMS\Froststrap\Froststrap.lnk"
    RMDir  "$SMPROGRAMS\Froststrap"

    ; Registry keys written by NSIS
    DeleteRegKey   HKCU "${APP_UNINSTALL_KEY}"
    DeleteRegValue HKCU "Software\Froststrap" "InstallLocation"
    DeleteRegValue HKCU "Software\Froststrap" "AppPath"
    DeleteRegKey /IfEmpty HKCU "Software\Froststrap"

    ; Step 3: remove the install directory.
    ${If} $KeepUserData == ${BST_CHECKED}
        Delete "$INSTDIR\${APP_EXE}"
        Delete "$INSTDIR\Uninstall.exe"
    ${Else}
        RMDir /r "$INSTDIR"
    ${EndIf}
SectionEnd
