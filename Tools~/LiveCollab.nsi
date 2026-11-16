; LiveCollab beta installer - NSIS 3.x
; All strings are ASCII to avoid mojibake in ACP builds.
; Build: makensis.exe LiveCollab.nsi

Unicode true
RequestExecutionLevel admin
SetCompressor /SOLID lzma
SetCompressorDictSize 64
ShowInstDetails show

!define APPNAME "LiveCollab"
!define VERSION "1.0.1"
!define PUBLISHER "LiveCollab contributors"
!define URL "https://github.com/vexthatguy/LiveCollab"
!define REGUNINST "Software\Microsoft\Windows\CurrentVersion\Uninstall\LiveCollab"

Name "${APPNAME} ${VERSION}"
OutFile "..\release\LiveCollab-Setup-${VERSION}.exe"
InstallDir "$PROGRAMFILES64\${APPNAME}"
InstallDirRegKey HKLM "${REGUNINST}" "InstallLocation"

!include "MUI2.nsh"
!include "nsDialogs.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "WordFunc.nsh"

!define MUI_ICON "livecollab_icon.ico"
!define MUI_UNICON "livecollab_icon.ico"

!define MUI_WELCOMEPAGE_TITLE "Welcome to LiveCollab ${VERSION}"
!define MUI_WELCOMEPAGE_TEXT "This wizard will install LiveCollab, the real-time multiplayer editor add-on.$\r$\n$\r$\nIt installs the Unity package, the standalone companion host, and the LiveCollab Manager app.$\r$\n$\r$\nYour Unity installation is detected automatically - you can also point the wizard to it manually."

!define MUI_LICENSEPAGE_RADIOBUTTONS
!define MUI_LICENSEPAGE_TEXT_TOP "LiveCollab is released under the MIT license. Read it below."

!define MUI_FINISHPAGE_TITLE "Installation complete"
!define MUI_FINISHPAGE_TEXT "LiveCollab has been installed.$\r$\n$\r$\nOpen your Unity project and use Tools > LiveCollab > Open Panel to host or join a session.$\r$\nThe desktop and Start menu shortcuts launch the LiveCollab Manager."
!define MUI_FINISHPAGE_RUN "$INSTDIR\LiveCollabManager.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Open LiveCollab Manager"
!define MUI_FINISHPAGE_NOREBOOTSUPPORT

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "..\LICENSE"
!insertmacro MUI_PAGE_DIRECTORY
Page custom UnityPageCreate UnityPageLeave
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_INSTFILES
!define MUI_INSTFILESPAGE_SHOWCHANGESONLY
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

!insertmacro MUI_LANGUAGE "English"

; ---------------- installer state ----------------
Var UnityPath
Var ProjectPath
Var ProjectInstall
Var UnityList
Var UnityBrowseBtn
Var ProjectBrowseBtn
Var ProjectEdit
Var ProjectLabel
Var DetectedLabel
Var FoundList

; ---------------- language strings (ASCII only) ----------------
LangString TEXT_UNITY_TITLE ${LANG_ENGLISH} "Unity folder"
LangString TEXT_UNITY_SUBTITLE ${LANG_ENGLISH} "LiveCollab detects your Unity installation and can install the package into a project."
LangString TEXT_UNITY_TOP ${LANG_ENGLISH} "Unity Editor folder (auto-detected - change it if wrong):"
LangString TEXT_UNITY_BROWSE ${LANG_ENGLISH} "Browse..."
LangString TEXT_PROJECT_TOP ${LANG_ENGLISH} "Install the LiveCollab package into this Unity project (optional):"
LangString TEXT_PROJECT_BROWSE ${LANG_ENGLISH} "Browse..."
LangString TEXT_UNITY_NONE ${LANG_ENGLISH} "No Unity installation was detected. Browse to the folder containing Unity.exe (usually ...\Unity\Hub\Editor\<version>\Editor)."

Section -CleanupOld
  ; Remove an older LiveCollab install (same app) before installing this version.
  ; Plain recursive removal - does NOT wait on the old uninstaller (that could
  ; hang the wizard if the child process stalls).
  ReadRegStr $0 HKLM "${REGUNINST}" "InstallLocation"
  StrCmp $0 "" done
  StrCmp $0 $INSTDIR same
  ${If} ${FileExists} "$0\Uninstall.exe"
    RMDir /r "$0"
  ${EndIf}
  Goto done
  same:
  ; target dir equals the new install dir - just overwrite in place
  done:
SectionEnd

Section "-Core" SecCore
  SetOutPath "$INSTDIR"
  File "..\LICENSE"
  File "..\package.json"
  File "..\README.md"
  File "..\CHANGELOG.md"
  File /oname=livecollab_guide.html "..\build\stage\livecollab_guide.html"
  File /oname=LiveCollabHost.exe "..\build\stage\LiveCollabHost.exe"
  File /oname=LiveCollabManager.exe "..\build\stage\LiveCollabManager.exe"

  ; full Unity package
  SetOutPath "$INSTDIR\package"
  File /r "..\Editor\*"
  File "..\package.json"
  File "..\LICENSE"
  File "..\README.md"
  File "..\CHANGELOG.md"
  SetOutPath "$INSTDIR\package\Tools~"
  File "livecollab_host.py"
  File "livecollab_manager.py"
  File "build_host_exe.bat"
  File "gen_icon.py"
  File "livecollab_icon.ico"

  ; uninstaller
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  ; registry
  WriteRegStr HKLM "${REGUNINST}" "DisplayName" "${APPNAME} ${VERSION}"
  WriteRegStr HKLM "${REGUNINST}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKLM "${REGUNINST}" "Publisher" "${PUBLISHER}"
  WriteRegStr HKLM "${REGUNINST}" "URLInfoAbout" "${URL}"
  WriteRegStr HKLM "${REGUNINST}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${REGUNINST}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKLM "${REGUNINST}" "NoModify" 1
  WriteRegDWORD HKLM "${REGUNINST}" "NoRepair" 1

  ; shortcuts
  CreateDirectory "$SMPROGRAMS\${APPNAME}"
  CreateShortCut "$SMPROGRAMS\${APPNAME}\LiveCollab Manager.lnk" "$INSTDIR\LiveCollabManager.exe"
  CreateShortCut "$SMPROGRAMS\${APPNAME}\LiveCollab User Guide.lnk" "$INSTDIR\livecollab_guide.html"
  CreateShortCut "$SMPROGRAMS\${APPNAME}\LiveCollab Homepage.lnk" "${URL}"
  CreateShortCut "$SMPROGRAMS\${APPNAME}\Uninstall LiveCollab.lnk" "$INSTDIR\Uninstall.exe"
SectionEnd

Section "Install package into a Unity project" SecProject
  ${If} $ProjectInstall == 1
    ${IfNot} $ProjectPath == ""
      CreateDirectory "$ProjectPath\Packages\com.livecollab.editor"
      CopyFiles /silent "$INSTDIR\package\*" "$ProjectPath\Packages\com.livecollab.editor\"
    ${EndIf}
  ${EndIf}
SectionEnd

Section "Desktop shortcut" SecDesktop
  CreateShortCut "$DESKTOP\LiveCollab Manager.lnk" "$INSTDIR\LiveCollabManager.exe"
SectionEnd

Section "Enable LiveCollab in new Unity projects" SecTemplates
  ; copies the package into the Unity project templates, so every future
  ; project created from those templates already contains LiveCollab.
  ${If} $FoundList == ""
    Return
  ${EndIf}
  StrCpy $2 1
  ${Do}
    ${WordFind} "$FoundList" "|" "+$2" $4
    ${If} $4 == ""
      ${ExitDo}
    ${EndIf}
    Push $4
    Call InstallTemplates
    IntOp $2 $2 + 1
  ${Loop}
SectionEnd

Function InstallTemplates
  Pop $8
  ${IfNot} ${FileExists} "$8\Data\Resources\PackageManager\ProjectTemplates"
    Return
  ${EndIf}
  FindFirst $5 $6 "$8\Data\Resources\PackageManager\ProjectTemplates\*"
  ${Do}
    ${If} $6 == ""
      ${ExitDo}
    ${EndIf}
    ${If} ${FileExists} "$8\Data\Resources\PackageManager\ProjectTemplates\$6\Packages"
      CreateDirectory "$8\Data\Resources\PackageManager\ProjectTemplates\$6\Packages\com.livecollab.editor"
      CopyFiles /silent "$INSTDIR\package\*" "$8\Data\Resources\PackageManager\ProjectTemplates\$6\Packages\com.livecollab.editor\"
    ${EndIf}
    FindNext $5 $6
  ${Loop}
  FindClose $5
FunctionEnd

; ---------------- custom Unity page ----------------
Function UnityPageCreate
  nsDialogs::Create 1018
  Pop $0
  ${If} $0 == error
    Abort
  ${EndIf}

  ${NSD_CreateLabel} 0 0 100% 12u "$(TEXT_UNITY_TOP)"
  Pop $0

  ${NSD_CreateText} 0 14u 80% 12u "$UnityPath"
  Pop $UnityList

  ${NSD_CreateButton} 82% 14u 18% 12u "$(TEXT_UNITY_BROWSE)"
  Pop $UnityBrowseBtn
  ${NSD_OnClick} $UnityBrowseBtn UnityBrowse

  ${NSD_CreateLabel} 0 32u 100% 22u ""
  Pop $DetectedLabel
  ${If} $FoundList != ""
    ${NSD_SetText} $DetectedLabel "Detected installs on this PC: $FoundList"
  ${Else}
    ${NSD_SetText} $DetectedLabel "$(TEXT_UNITY_NONE)"
  ${EndIf}

  ${NSD_CreateLabel} 0 58u 100% 12u "$(TEXT_PROJECT_TOP)"
  Pop $ProjectLabel

  ${NSD_CreateCheckBox} 0 72u 100% 12u "Install into this project"
  Pop $ProjectInstall
  ${NSD_OnClick} $ProjectInstall ProjectCheck

  ${NSD_CreateText} 0 88u 80% 12u "$ProjectPath"
  Pop $ProjectEdit

  ${NSD_CreateButton} 82% 88u 18% 12u "$(TEXT_PROJECT_BROWSE)"
  Pop $ProjectBrowseBtn
  ${NSD_OnClick} $ProjectBrowseBtn ProjectBrowse

  nsDialogs::Show
FunctionEnd

Function UnityBrowse
  ${NSD_GetText} $UnityList $UnityPath
  nsDialogs::SelectFolderDialog "Select your Unity Editor folder (the one containing Unity.exe)" "$UnityPath"
  Pop $0
  ${If} $0 != error
    StrCpy $UnityPath $0
    ${NSD_SetText} $UnityList $UnityPath
    Call RefreshDetectedLabel
  ${EndIf}
FunctionEnd

Function ProjectBrowse
  ${NSD_GetText} $ProjectEdit $ProjectPath
  nsDialogs::SelectFolderDialog "Select your Unity project folder (the one containing Assets and Packages)" "$ProjectPath"
  Pop $0
  ${If} $0 != error
    StrCpy $ProjectPath $0
    ${NSD_SetText} $ProjectEdit $ProjectPath
  ${EndIf}
FunctionEnd

Function ProjectCheck
  ${NSD_GetState} $ProjectInstall $0
  ${If} $0 == 1
    EnableWindow $ProjectEdit 1
    EnableWindow $ProjectBrowseBtn 1
  ${Else}
    EnableWindow $ProjectEdit 0
    EnableWindow $ProjectBrowseBtn 0
  ${EndIf}
FunctionEnd

Function RefreshDetectedLabel
  StrCmp $UnityPath "" skip
  ${If} $FoundList == ""
    StrCpy $FoundList "$UnityPath"
  ${Else}
    StrCpy $2 1
    ${Do}
      ${WordFind} "$FoundList" "|" "+$2" $4
      ${If} $4 == ""
        ${ExitDo}
      ${EndIf}
      StrCmp $4 "$UnityPath" found
      IntOp $2 $2 + 1
    ${Loop}
    StrCpy $FoundList "$FoundList|$UnityPath"
    found:
  ${EndIf}
  ${If} $DetectedLabel != ""
    ${NSD_SetText} $DetectedLabel "Detected installs on this PC: $FoundList"
  ${EndIf}
  skip:
FunctionEnd

Function UnityPageLeave
  ${NSD_GetText} $UnityList $UnityPath
  ${NSD_GetText} $ProjectEdit $ProjectPath
  ${NSD_GetState} $ProjectInstall $ProjectInstall
  Call RefreshDetectedLabel

  ${If} $ProjectInstall == 1
    ${If} $ProjectPath == ""
      MessageBox MB_ICONEXCLAMATION|MB_OK "Choose a Unity project folder (or uncheck the project option)."
      Abort
    ${EndIf}
  ${EndIf}

  ${If} $UnityPath != ""
    ${IfNot} ${FileExists} "$UnityPath\Unity.exe"
      ${IfNot} ${FileExists} "$UnityPath\Editor\Unity.exe"
        MessageBox MB_ICONEXCLAMATION|MB_YESNO "The selected folder does not contain Unity.exe. Continue anyway?" IDYES +2
        Abort
      ${EndIf}
    ${EndIf}
  ${EndIf}
FunctionEnd

; ============================================================
;  Unity detection
; ============================================================

Function .onInit
  StrCpy $ProjectInstall 0
  StrCpy $UnityPath ""
  StrCpy $ProjectPath ""
  StrCpy $FoundList ""

  ; 1) registry (HKLM then HKCU, both views)
  SetRegView 64
  Call DetectUnityReg
  Call DetectUnityRegLegacy
  SetRegView 32
  Call DetectUnityReg
  Call DetectUnityRegLegacy

  ; 2) scan all fixed drives for Unity Hub installs
  ${GetDrives} "HDD" AddDriveCallback

  ; 3) pick the highest installed version
  Call PickBestUnity
FunctionEnd

Function DetectUnityReg
  StrCpy $2 0
  ${Do}
    EnumRegKey $1 HKLM "SOFTWARE\Unity Technologies\Installer" $2
    ${If} $1 == ""
      ${ExitDo}
    ${EndIf}
    ReadRegStr $3 HKLM "SOFTWARE\Unity Technologies\Installer\$1" "Location"
    ${If} $3 != ""
      ${If} ${FileExists} "$3\Unity.exe"
        Call AddCandidate
      ${ElseIf} ${FileExists} "$3\Editor\Unity.exe"
        StrCpy $3 "$3\Editor"
        Call AddCandidate
      ${EndIf}
    ${EndIf}
    IntOp $2 $2 + 1
  ${Loop}
  StrCpy $2 0
  ${Do}
    EnumRegKey $1 HKCU "SOFTWARE\Unity Technologies\Installer" $2
    ${If} $1 == ""
      ${ExitDo}
    ${EndIf}
    ReadRegStr $3 HKCU "SOFTWARE\Unity Technologies\Installer\$1" "Location"
    ${If} $3 != ""
      ${If} ${FileExists} "$3\Unity.exe"
        Call AddCandidate
      ${ElseIf} ${FileExists} "$3\Editor\Unity.exe"
        StrCpy $3 "$3\Editor"
        Call AddCandidate
      ${EndIf}
    ${EndIf}
    IntOp $2 $2 + 1
  ${Loop}
FunctionEnd

Function DetectUnityRegLegacy
  ; older installers may live under HKLM\SOFTWARE\Unity Technologies\<key> with a Location value
  StrCpy $2 0
  ${Do}
    EnumRegKey $1 HKLM "SOFTWARE\Unity Technologies" $2
    ${If} $1 == ""
      ${ExitDo}
    ${EndIf}
    ReadRegStr $3 HKLM "SOFTWARE\Unity Technologies\$1" "Location"
    ${If} $3 != ""
      ${If} ${FileExists} "$3\Editor\Unity.exe"
        StrCpy $3 "$3\Editor"
        Call AddCandidate
      ${ElseIf} ${FileExists} "$3\Unity.exe"
        Call AddCandidate
      ${EndIf}
    ${EndIf}
    IntOp $2 $2 + 1
  ${Loop}
  StrCpy $2 0
  ${Do}
    EnumRegKey $1 HKCU "SOFTWARE\Unity Technologies" $2
    ${If} $1 == ""
      ${ExitDo}
    ${EndIf}
    ReadRegStr $3 HKCU "SOFTWARE\Unity Technologies\$1" "Location"
    ${If} $3 != ""
      ${If} ${FileExists} "$3\Editor\Unity.exe"
        StrCpy $3 "$3\Editor"
        Call AddCandidate
      ${ElseIf} ${FileExists} "$3\Unity.exe"
        Call AddCandidate
      ${EndIf}
    ${EndIf}
    IntOp $2 $2 + 1
  ${Loop}
FunctionEnd

Function AddDriveCallback
  ; $0 contains a drive like "C:\"
  Push $0
  ${If} ${FileExists} "$0Program Files\Unity\Hub\Editor"
    StrCpy $1 "$0Program Files\Unity\Hub\Editor\*"
    Call ScanHubDir
  ${EndIf}
  ${If} ${FileExists} "$0Program Files (x86)\Unity\Hub\Editor"
    StrCpy $1 "$0Program Files (x86)\Unity\Hub\Editor\*"
    Call ScanHubDir
  ${EndIf}
  ${If} ${FileExists} "$0Unity\Hub\Editor"
    StrCpy $1 "$0Unity\Hub\Editor\*"
    Call ScanHubDir
  ${EndIf}
  ${If} ${FileExists} "$0Unity\Editor\Unity.exe"
    StrCpy $3 "$0Unity\Editor"
    Call AddCandidate
  ${EndIf}

  ; per-user installs (Unity Hub installed for current user only)
  ${If} ${FileExists} "$LOCALAPPDATA\Programs\Unity\Hub\Editor"
    StrCpy $1 "$LOCALAPPDATA\Programs\Unity\Hub\Editor\*"
    StrCpy $0 "$LOCALAPPDATA\Programs\Unity\Hub\Editor\"
    Call ScanHubDirFlat
  ${EndIf}
  ${If} ${FileExists} "$LOCALAPPDATA\Programs\Unity\Editor\Unity.exe"
    StrCpy $3 "$LOCALAPPDATA\Programs\Unity\Editor"
    Call AddCandidate
  ${EndIf}
  Pop $0
FunctionEnd

Function ScanHubDirFlat
  ; $1 = path with trailing \* ; $0 = parent dir for prefixing
  FindFirst $2 $4 $1
  ${Do}
    ${If} $4 == ""
      ${ExitDo}
    ${EndIf}
    ${If} ${FileExists} "$0$4\Editor\Unity.exe"
      StrCpy $3 "$0$4\Editor"
      Call AddCandidate
    ${ElseIf} ${FileExists} "$0$4\Unity.exe"
      StrCpy $3 "$0$4"
      Call AddCandidate
    ${EndIf}
    FindNext $2 $4
  ${Loop}
  FindClose $2
FunctionEnd

Function ScanHubDir
  ; $1 = path with trailing \* to scan
  FindFirst $2 $4 $1
  ${Do}
    ${If} $4 == ""
      ${ExitDo}
    ${EndIf}
    ${If} ${FileExists} "$0$4\Editor\Unity.exe"
      StrCpy $3 "$0$4\Editor"
      Call AddCandidate
    ${ElseIf} ${FileExists} "$0$4\Unity.exe"
      StrCpy $3 "$0$4"
      Call AddCandidate
    ${EndIf}
    FindNext $2 $4
  ${Loop}
  FindClose $2
FunctionEnd

Function AddCandidate
  ; $3 = full path to a Unity editor. Append to $FoundList.
  ${If} $FoundList == ""
    StrCpy $FoundList "$3"
  ${Else}
    StrCpy $FoundList "$FoundList|$3"
  ${EndIf}
FunctionEnd

; pick the newest version from $FoundList; result in $UnityPath
Function PickBestUnity
  ${If} $FoundList == ""
    Return
  ${EndIf}
  StrCpy $UnityPath ""
  StrCpy $2 1
  ${Do}
    ${WordFind} "$FoundList" "|" "+$2" $4
    ${If} $4 == ""
      ${ExitDo}
    ${EndIf}
    ${If} $UnityPath == ""
      StrCpy $UnityPath $4
    ${Else}
      Push $4
      Push $UnityPath
      Call CompareVersion
      Pop $5
      ${If} $5 == 1
        StrCpy $UnityPath $4
      ${EndIf}
    ${EndIf}
    IntOp $2 $2 + 1
  ${Loop}
FunctionEnd

; CompareVersion: Pop B, Pop A -> Push 1 if A newer than B, else 0
Function CompareVersion
  Pop $7
  Pop $6
  Call GetVersionTuple
  ; $8 = tuple of A, $9 = tuple of B (joined by '.')
  ${If} $8 == $9
    Push 0
    Return
  ${EndIf}
  ; compare segment by segment
  StrCpy $1 1
  ${Do}
    ${WordFind} "$8" "." "+$1" $2
    ${WordFind} "$9" "." "+$1" $3
    ${If} $2 == ""
      Push 0
      Return
    ${EndIf}
    ${If} $3 == ""
      Push 1
      Return
    ${EndIf}
    StrCpy $4 $2
    StrCpy $5 $3
    Call CompareNumeric
    ${If} $6 == 1
      Push 1
      Return
    ${EndIf}
    ${If} $6 == -1
      Push 0
      Return
    ${EndIf}
    IntOp $1 $1 + 1
  ${Loop}
FunctionEnd

; GetVersionTuple: uses $6 (path) -> $8 (tuple) ; also $7 path -> $9
Function GetVersionTuple
  ; version folder is one above the Editor segment: ...\Hub\Editor\<version>\Editor
  ${WordFind} "$6" "\" "-2" $8
  ${WordFind} "$7" "\" "-2" $9
FunctionEnd

; CompareNumeric: $4 and $5 are strings like "2022" or "45f1" -> $6 = 1/0/-1
Function CompareNumeric
  ; strip leading non-digit from build segment
  StrCpy $0 $4
  Call StripNum
  StrCpy $4 $0
  StrCpy $0 $5
  Call StripNum
  StrCpy $5 $0
  StrCpy $0 $4
  StrCpy $1 $5
  Call IntCompare
FunctionEnd

Function StripNum
  ; $0 in -> digits-only prefix out
  StrCpy $2 ""
  StrLen $3 $0
  StrCpy $4 0
  ${Do}
    ${If} $4 >= $3
      ${ExitDo}
    ${EndIf}
    StrCpy $5 $0 1 $4
    ${If} $5 < "0"
      ${ExitDo}
    ${EndIf}
    ${If} $5 > "9"
      ${ExitDo}
    ${EndIf}
    StrCpy $2 "$2$5"
    IntOp $4 $4 + 1
  ${Loop}
  ${If} $2 == ""
    StrCpy $2 "0"
  ${EndIf}
  StrCpy $0 $2
FunctionEnd

Function IntCompare
  ; compares numeric strings $0 and $1 -> $6 (1 if $0>$1, -1 if <, 0 if ==)
  ; pad both to equal length
  StrLen $2 $0
  StrLen $3 $1
  ${If} $2 > $3
    StrCpy $4 $2
  ${Else}
    StrCpy $4 $3
  ${EndIf}
  StrCpy $5 ""
  ${Do}
    StrLen $6 $0
    ${If} $6 >= $4
      ${ExitDo}
    ${EndIf}
    StrCpy $0 "0$0"
  ${Loop}
  ${Do}
    StrLen $6 $1
    ${If} $6 >= $4
      ${ExitDo}
    ${EndIf}
    StrCpy $1 "0$1"
  ${Loop}
  StrCmp $0 $1 equal
  ${If} $0 > $1
    StrCpy $6 1
  ${Else}
    StrCpy $6 -1
  ${EndIf}
  Return
  equal:
  StrCpy $6 0
FunctionEnd

; ---------------- uninstaller ----------------
Section "Uninstall"
  Delete "$INSTDIR\Uninstall.exe"
  Delete "$INSTDIR\LiveCollabManager.exe"
  Delete "$INSTDIR\LiveCollabHost.exe"
  Delete "$INSTDIR\livecollab_guide.html"
  Delete "$INSTDIR\LICENSE"
  Delete "$INSTDIR\package.json"
  Delete "$INSTDIR\README.md"
  Delete "$INSTDIR\CHANGELOG.md"
  RMDir /r "$INSTDIR\package"
  RMDir "$INSTDIR"

  Delete "$SMPROGRAMS\${APPNAME}\LiveCollab Manager.lnk"
  Delete "$SMPROGRAMS\${APPNAME}\LiveCollab User Guide.lnk"
  Delete "$SMPROGRAMS\${APPNAME}\LiveCollab Homepage.lnk"
  Delete "$SMPROGRAMS\${APPNAME}\Uninstall LiveCollab.lnk"
  RMDir "$SMPROGRAMS\${APPNAME}"
  Delete "$DESKTOP\LiveCollab Manager.lnk"

  ; remove any package copies we injected into Unity templates
  ${GetDrives} "HDD" un.UnInstallTemplatesDrive

  DeleteRegKey HKLM "${REGUNINST}"
SectionEnd

Function un.UnInstallTemplatesDrive
  Push $0
  ${If} ${FileExists} "$0Program Files\Unity\Hub\Editor"
    StrCpy $1 "$0Program Files\Unity\Hub\Editor\*"
    Call un.RemoveTemplatesFromEditor
  ${EndIf}
  ${If} ${FileExists} "$0Program Files (x86)\Unity\Hub\Editor"
    StrCpy $1 "$0Program Files (x86)\Unity\Hub\Editor\*"
    Call un.RemoveTemplatesFromEditor
  ${EndIf}
  ${If} ${FileExists} "$0Unity\Hub\Editor"
    StrCpy $1 "$0Unity\Hub\Editor\*"
    Call un.RemoveTemplatesFromEditor
  ${EndIf}
  Pop $0
FunctionEnd

Function un.RemoveTemplatesFromEditor
  FindFirst $5 $6 $1
  ${Do}
    ${If} $6 == ""
      ${ExitDo}
    ${EndIf}
    ${If} ${FileExists} "$0$6\Editor\Data\Resources\PackageManager\ProjectTemplates"
      StrCpy $2 "$0$6\Editor\Data\Resources\PackageManager\ProjectTemplates\*"
      FindFirst $7 $8 $2
      ${Do}
        ${If} $8 == ""
          ${ExitDo}
        ${EndIf}
        RMDir /r "$0$6\Editor\Data\Resources\PackageManager\ProjectTemplates\$8\Packages\com.livecollab.editor"
        FindNext $7 $8
      ${Loop}
      FindClose $7
    ${EndIf}
    FindNext $5 $6
  ${Loop}
  FindClose $5
FunctionEnd
