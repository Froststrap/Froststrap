!include "MUI2.nsh"
!include "nsDialogs.nsh"
!include "LogicLib.nsh"

Var DesktopShortcutCheckbox
Var StartMenuShortcutCheckbox
Var CreateDesktopShortcut
Var CreateStartMenuShortcut
Var KeepUserDataCheckbox
Var KeepUserData

Name "Froststrap"

!ifndef PUBLISH_DIR
  !define PUBLISH_DIR "..\build"
!endif

OutFile "${PUBLISH_DIR}\Froststrap-Setup.exe"
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
Page Custom OptionsPageCreate OptionsPageLeave
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
UninstPage Custom un.OptionsPageCreate un.OptionsPageLeave
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

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

Section "Froststrap"
    SetOutPath "$INSTDIR"
    File /r "${PUBLISH_DIR}\*"
    WriteRegStr HKCU "Software\Froststrap" "InstallLocation" "$INSTDIR"
    WriteRegStr HKCU "Software\Froststrap" "AppPath" "$INSTDIR\${APP_EXE}"
    WriteRegStr HKCU "${APP_UNINSTALL_KEY}" "DisplayName" "${APP_NAME}"
    WriteRegStr HKCU "${APP_UNINSTALL_KEY}" "DisplayVersion" "2.0.0"
    WriteRegStr HKCU "${APP_UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
    WriteRegStr HKCU "${APP_UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\${APP_EXE},0"
    WriteRegStr HKCU "${APP_UNINSTALL_KEY}" "Publisher" "Froststrap"
    WriteRegStr HKCU "${APP_UNINSTALL_KEY}" "InstallDate" ""
    WriteRegStr HKCU "${APP_UNINSTALL_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
    WriteRegStr HKCU "${APP_UNINSTALL_KEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
    WriteRegStr HKCU "${APP_UNINSTALL_KEY}" "ModifyPath" '"$INSTDIR\${APP_EXE}" -settings'
    WriteRegStr HKCU "${APP_UNINSTALL_KEY}" "URLInfoAbout" "https://github.com/Froststrap/Froststrap/issues/new"
    WriteRegStr HKCU "${APP_UNINSTALL_KEY}" "URLUpdateInfo" "https://github.com/Froststrap/Froststrap/releases"
    ${If} $CreateStartMenuShortcut == ${BST_CHECKED}
        CreateDirectory "$SMPROGRAMS\Froststrap"
        CreateShortCut "$SMPROGRAMS\Froststrap\Froststrap.lnk" "$INSTDIR\${APP_EXE}"
    ${EndIf}
    ${If} $CreateDesktopShortcut == ${BST_CHECKED}
        CreateShortCut "$DESKTOP\Froststrap.lnk" "$INSTDIR\${APP_EXE}"
    ${EndIf}
    WriteUninstaller "$INSTDIR\Uninstall.exe"
SectionEnd

Section "Uninstall"
    Delete "$DESKTOP\Froststrap.lnk"
    Delete "$SMPROGRAMS\Froststrap\Froststrap.lnk"
    RMDir "$SMPROGRAMS\Froststrap"
    DeleteRegKey HKCU "${APP_UNINSTALL_KEY}"
    DeleteRegValue HKCU "Software\Froststrap" "InstallLocation"
    DeleteRegValue HKCU "Software\Froststrap" "AppPath"
    DeleteRegKey /IfEmpty HKCU "Software\Froststrap"
    ${If} $KeepUserData != ${BST_CHECKED}
        RMDir /r "$INSTDIR"
    ${Else}
        Delete "$INSTDIR\${APP_EXE}"
        Delete "$INSTDIR\Uninstall.exe"
    ${EndIf}
SectionEnd
