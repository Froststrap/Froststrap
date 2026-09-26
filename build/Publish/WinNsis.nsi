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

!define MUI_ICON "..\..\Froststrap\Froststrap.ico"
!define MUI_UNICON "..\..\Froststrap\Froststrap.ico"

!ifndef PUBLISH_DIR
  !define PUBLISH_DIR "..\..\.build"
!endif

!ifndef OUT_DIR
  !define OUT_DIR "..\..\.build\dist"
!endif

!ifndef APP_VERSION
  !define APP_VERSION "Unknown"
!endif

!ifdef SELFCONTAINED
  OutFile "${OUT_DIR}\Froststrap-windows-x64.exe"
!else
  OutFile "${OUT_DIR}\Froststrap-windows-x64.exe"
!endif

Icon "..\..\Froststrap\Froststrap.ico"
UninstallIcon "..\..\Froststrap\Froststrap.ico"
InstallDir "$LOCALAPPDATA\Froststrap"
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
; Undo BlockState()
; ---------------------------------------------------------------------------

Function un.UndoBlockState
    Exch $R0

    IfFileExists "$R0\*.*" done

    IfFileExists "$R0" 0 checkBackup

    System::Call 'kernel32::GetFileAttributesW(w "$R0") i .R1'
    IntOp $R2 $R1 & 1
    StrCmp $R2 0 done

    SetFileAttributes "$R0" NORMAL
    Delete "$R0"

checkBackup:
    IfFileExists "$R0 (Before Blocking)\*.*" 0 done
    Rename "$R0 (Before Blocking)" "$R0"

done:
    Pop $R0
FunctionEnd

; ---------------------------------------------------------------------------
; Visual C++ Redistributable (x64)
; ---------------------------------------------------------------------------

; Sets $0 to 1 if the x64 VC++ 2015-2022 runtime is already installed.
Function IsVCRedistInstalled
    SetRegView 64
    ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64" "Installed"
    SetRegView 32
FunctionEnd

Function InstallVCRedist
    Call IsVCRedistInstalled
    ${If} $0 == 1
        Return
    ${EndIf}

!ifdef VCREDIST
    InitPluginsDir
    StrCpy $1 "$PLUGINSDIR\vc_redist.x64.exe"
    File "/oname=$PLUGINSDIR\vc_redist.x64.exe" "${VCREDIST}"

    DetailPrint "Installing Microsoft Visual C++ Redistributable..."
    ExecShellWait "runas" "$1" "/install /passive /norestart" SW_SHOWNORMAL
    Delete "$1"

    Call IsVCRedistInstalled
!endif

    ${If} $0 != 1
        DetailPrint "Microsoft Visual C++ Redistributable was not installed."
        MessageBox MB_OK|MB_ICONEXCLAMATION \
            "The Microsoft Visual C++ Redistributable could not be installed.$\nFroststrap may not start until you install it from https://aka.ms/vc14/vc_redist.x64.exe" \
            /SD IDOK
    ${EndIf}
FunctionEnd

; ---------------------------------------------------------------------------
; Install section
; ---------------------------------------------------------------------------

Section "Froststrap"
    ${If} $CreateDesktopShortcut == ""
        StrCpy $CreateDesktopShortcut ${BST_CHECKED}
    ${EndIf}

    ${If} $CreateStartMenuShortcut == ""
        StrCpy $CreateStartMenuShortcut ${BST_CHECKED}
    ${EndIf}

    ${If} $ExistingInstallChoice == 1
        RMDir /r "$INSTDIR"
    ${EndIf}

    SetOutPath "$INSTDIR"
    File /r "${PUBLISH_DIR}\Froststrap.exe"
    File /r "${PUBLISH_DIR}\FluentAvalonia.xml"
    File /r "..\..\Froststrap\Froststrap.ico"

    Call InstallVCRedist

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
        CreateShortCut "$SMPROGRAMS\Froststrap.lnk" "$INSTDIR\${APP_EXE}"
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

    ; Step 2: undo any BlockState() the app applied. Roblox folders that were
    Push "$PROFILE\Videos\Roblox"
    Call un.UndoBlockState

    Push "$PROFILE\Pictures\Roblox"
    Call un.UndoBlockState

    ; Step 3: NSIS cleans up what it owns

    ; Shortcuts
    Delete "$DESKTOP\Froststrap.lnk"
    Delete "$SMPROGRAMS\Froststrap.lnk"

    ; Registry keys written by NSIS
    DeleteRegKey   HKCU "${APP_UNINSTALL_KEY}"
    DeleteRegKey /IfEmpty HKCU "Software\Froststrap"

    ; Step 4: remove the install directory.
    ${If} $KeepUserData == ${BST_CHECKED}
        Delete "$INSTDIR\${APP_EXE}"
        Delete "$INSTDIR\Uninstall.exe"
    ${Else}
        RMDir /r "$INSTDIR"
    ${EndIf}
SectionEnd
