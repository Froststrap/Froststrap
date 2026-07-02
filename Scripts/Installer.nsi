!include "MUI2.nsh"
!include "nsDialogs.nsh"
!include "LogicLib.nsh"

Var DesktopShortcutCheckbox
Var StartMenuShortcutCheckbox
Var CreateDesktopShortcut
Var CreateStartMenuShortcut
Var KeepUserDataCheckbox
Var KeepUserData

Var LanguageComboBox
Var LanguageValue

Name "Froststrap"

!ifndef PUBLISH_DIR
  !define PUBLISH_DIR "..\build"
!endif

!ifndef APP_VERSION
  !define APP_VERSION "Unknown"
!endif

!ifdef SELFCONTAINED
  OutFile "..\build\Froststrap-SelfContained-Setup.exe"
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
Page Custom LanguagePageCreate LanguagePageLeave
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
UninstPage Custom un.OptionsPageCreate un.OptionsPageLeave
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

; ---------------------------------------------------------------------------
; Language Selection Page
; ---------------------------------------------------------------------------

Function LanguagePageCreate
    nsDialogs::Create 1018
    Pop $0
    ${If} $0 == error
        Abort
    ${EndIf}

    ${NSD_CreateLabel} 0 0 100% 24u "Select your preferred language:"
    Pop $0

    ${NSD_CreateComboBox} 0 30u 100% 12u ""
    Pop $LanguageComboBox

    ${NSD_CB_AddString} $LanguageComboBox "System Default (nil)"
    ${NSD_CB_AddString} $LanguageComboBox "English (en)"
    ${NSD_CB_AddString} $LanguageComboBox "Arabic(ar)"
    ${NSD_CB_AddString} $LanguageComboBox "Bulgarian (bg)"
    ${NSD_CB_AddString} $LanguageComboBox "Czech (cs)"
    ${NSD_CB_AddString} $LanguageComboBox "Danish (da)"
    ${NSD_CB_AddString} $LanguageComboBox "German (de)"
    ${NSD_CB_AddString} $LanguageComboBox "Greek (el)"
    ${NSD_CB_AddString} $LanguageComboBox "Spanish (es-ES)"
    ${NSD_CB_AddString} $LanguageComboBox "Estonian (et)"
    ${NSD_CB_AddString} $LanguageComboBox "Finnish (fi)"
    ${NSD_CB_AddString} $LanguageComboBox "French (fr)"
    ${NSD_CB_AddString} $LanguageComboBox "Hungarian (hu)"
    ${NSD_CB_AddString} $LanguageComboBox "Bahasa Indonesia (id)"
    ${NSD_CB_AddString} $LanguageComboBox "Italian (it)"
    ${NSD_CB_AddString} $LanguageComboBox "Japanese (ja)"
    ${NSD_CB_AddString} $LanguageComboBox "Korean (ko)"
    ${NSD_CB_AddString} $LanguageComboBox "Lithuanian (lt)"
    ${NSD_CB_AddString} $LanguageComboBox "Latvian (lv)"
    ${NSD_CB_AddString} $LanguageComboBox "Dutch (nl)"
    ${NSD_CB_AddString} $LanguageComboBox "Polish (pl)"
    ${NSD_CB_AddString} $LanguageComboBox "Portuguese Brasil (pt-BR)"
    ${NSD_CB_AddString} $LanguageComboBox "Portuguese Portugal (pt-PT)"
    ${NSD_CB_AddString} $LanguageComboBox "Romanian (ro)"
    ${NSD_CB_AddString} $LanguageComboBox "Russian (ru)"
    ${NSD_CB_AddString} $LanguageComboBox "Slovak (sk)"
    ${NSD_CB_AddString} $LanguageComboBox "Slovanian (sl)"
    ${NSD_CB_AddString} $LanguageComboBox "Swedish (sv-SE)"
    ${NSD_CB_AddString} $LanguageComboBox "Turkish (tr)"
    ${NSD_CB_AddString} $LanguageComboBox "Ukranian (uk)"
    ${NSD_CB_AddString} $LanguageComboBox "Vietnamese (vi)"
    ${NSD_CB_AddString} $LanguageComboBox "Chinese Simplified (zh-CN)"
    ${NSD_CB_AddString} $LanguageComboBox "Chinese Traditional (zh-TW)"

    ${NSD_CB_SelectString} $LanguageComboBox "System Default (nil)"

    nsDialogs::Show
FunctionEnd

Function LanguagePageLeave
    ${NSD_GetText} $LanguageComboBox $0
    Push $0
    Call ExtractLanguageIdentifier
    Pop $LanguageValue

    ${If} $LanguageValue == ""
        StrCpy $LanguageValue "nil"
    ${EndIf}
FunctionEnd

Function ExtractLanguageIdentifier
    Exch $0
    Push $1
    Push $2
    Push $3

    StrCpy $1 0
    ${Do}
        StrCpy $2 $0 1 $1
        ${If} $2 == "("
            IntOp $1 $1 + 1
            ${Break}
        ${EndIf}
        IntOp $1 $1 + 1
    ${LoopWhile} $1 < ${NSIS_MAX_STRLEN}

    StrCpy $2 0
    StrLen $3 $0
    ${Do}
        IntOp $3 $3 - 1
        StrCpy $2 $0 1 $3
        ${If} $2 == ")"
            ${Break}
        ${EndIf}
    ${LoopWhile} $3 > 0

    IntOp $3 $3 - $1
    StrCpy $0 $0 $3 $1

    Pop $3
    Pop $2
    Pop $1
    Exch $0
FunctionEnd

; ---------------------------------------------------------------------------
; Install pages
; ---------------------------------------------------------------------------

Function OptionsPageCreate
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
    SetOutPath "$INSTDIR"
    File /r "${PUBLISH_DIR}\*"

    ; Froststrap app registry keys (used by the app to locate itself)
    WriteRegStr HKCU "Software\Froststrap" "InstallLocation" "$INSTDIR"
    WriteRegStr HKCU "Software\Froststrap" "AppPath" "$INSTDIR\${APP_EXE}"
    WriteRegStr HKCU "Software\Froststrap" "Language" "$LanguageValue"

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
    DeleteRegValue HKCU "Software\Froststrap" "Language"
    DeleteRegKey /IfEmpty HKCU "Software\Froststrap"

    ; Step 3: remove the install directory.
    ${If} $KeepUserData == ${BST_CHECKED}
        Delete "$INSTDIR\${APP_EXE}"
        Delete "$INSTDIR\Uninstall.exe"
    ${Else}
        RMDir /r "$INSTDIR"
    ${EndIf}
SectionEnd