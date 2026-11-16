; LiveCollab beta installer — NSIS 3.x
; Build: makensis.exe LiveCollab.nsi

Unicode true
RequestExecutionLevel admin
SetCompressor /SOLID lzma
SetCompressorDictSize 64

!define APPNAME "LiveCollab"
!define VERSION "0.3.0-beta.1"
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
!include "WordFunc.nsh"

!define MUI_ICON "livecollab_icon.ico"
!define MUI_UNICON "livecollab_icon.ico"

!define MUI_WELCOMEPAGE_TITLE "Welcome to LiveCollab ${VERSION}"
!define MUI_WELCOMEPAGE_TEXT "This wizard will install LiveCollab — real-time multiplayer editing inside the Unity Editor.$\r$\n$\r$\nIt will install the Unity package, the standalone companion host (LiveCollabHost.exe), and can optionally install the package directly into a Unity project.$\r$\n$\r$\nUnity versions are detected automatically; you can also point the wizard to your Unity folder manually."

!define MUI_LICENSEPAGE_RADIOBUTTONS
!define MUI_LICENSEPAGE_TEXT_TOP "LiveCollab is released under the MIT license. Read it below."

!define MUI_FINISHPAGE_TITLE "Installation complete"
!define MUI_FINISHPAGE_TEXT "LiveCollab has been installed.$\r$\n$\r$\nOpen your Unity project, then use the LiveCollab toolbar button (or Tools ▸ LiveCollab) to host or join a session."
!define MUI_FINISHPAGE_SHOWREADME "$INSTDIR\livecollab_guide.html"
!define MUI_FINISHPAGE_SHOWREADME_TEXT "Open the LiveCollab user guide"
!define MUI_FINISHPAGE_NOREBOOTSUPPORT

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "..\LICENSE"
!insertmacro MUI_PAGE_DIRECTORY
Page custom UnityPageCreate UnityPageLeave
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_INSTFILES
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
Var ProjectLabel
Var ProjectEdit

; ---------------- language strings ----------------
LangString TEXT_UNITY_TITLE ${LANG_ENGLISH} "Unity folder"
LangString TEXT_UNITY_SUBTITLE ${LANG_ENGLISH} "LiveCollab detects your Unity installation and can install the package into a project."
LangString TEXT_UNITY_TOP ${LANG_ENGLISH} "Unity Editor folder (auto-detected — change it if wrong):"
LangString TEXT_UNITY_BROWSE ${LANG_ENGLISH} "Browse…"
LangString TEXT_PROJECT_TOP ${LANG_ENGLISH} "Install the LiveCollab package into this Unity project (optional):"
LangString TEXT_PROJECT_BROWSE ${LANG_ENGLISH} "Browse…"
LangString TEXT_UNITY_NONE ${LANG_ENGLISH} "No Unity installation was detected. Please browse to the folder containing Unity.exe (usually ...\Unity\Hub\Editor\<version>\Editor)."

Section "-Core" SecCore
  SetOutPath "$INSTDIR"
  File "..\LICENSE"
  File "..\package.json"
  File "..\README.md"
  File "..\CHANGELOG.md"
  File /oname=livecollab_guide.html "..\build\stage\livecollab_guide.html"
  File /oname=LiveCollabHost.exe "..\build\stage\LiveCollabHost.exe"

  ; full Unity package
  SetOutPath "$INSTDIR\package"
  File /r "..\Editor\*"
  File "..\package.json"
  File "..\LICENSE"
  File "..\README.md"
  File "..\CHANGELOG.md"
  SetOutPath "$INSTDIR\package\Tools~"
  File "livecollab_host.py"
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
  CreateShortCut "$DESKTOP\LiveCollab User Guide.lnk" "$INSTDIR\livecollab_guide.html"
SectionEnd

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

  ${NSD_CreateLabel} 0 34u 100% 12u "$(TEXT_PROJECT_TOP)"
  Pop $ProjectLabel

  ${NSD_CreateCheckBox} 0 48u 100% 12u "Install into this project"
  Pop $ProjectInstall
  ${NSD_OnClick} $ProjectInstall ProjectCheck

  ${NSD_CreateText} 0 64u 80% 12u "$ProjectPath"
  Pop $ProjectEdit

  ${NSD_CreateButton} 82% 64u 18% 12u "$(TEXT_PROJECT_BROWSE)"
  Pop $ProjectBrowseBtn
  ${NSD_OnClick} $ProjectBrowseBtn ProjectBrowse

  ${NSD_CreateLabel} 0 84u 100% 36u ""
  Pop $0
  ${If} $UnityPath == ""
    ${NSD_SetText} $0 "$(TEXT_UNITY_NONE)"
  ${EndIf}

  nsDialogs::Show
FunctionEnd

Function UnityBrowse
  ${NSD_GetText} $UnityList $UnityPath
  nsDialogs::SelectFolderDialog "Select your Unity Editor folder (the one containing Unity.exe)" "$UnityPath"
  Pop $0
  ${If} $0 != error
    StrCpy $UnityPath $0
    ${NSD_SetText} $UnityList $UnityPath
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

Function UnityPageLeave
  ${NSD_GetText} $UnityList $UnityPath
  ${NSD_GetText} $ProjectEdit $ProjectPath
  ${NSD_GetState} $ProjectInstall $ProjectInstall

  ${If} $ProjectInstall == 1
    ${If} $ProjectPath == ""
      MessageBox MB_ICONEXCLAMATION|MB_OK "Choose a Unity project folder (or uncheck the project option)."
      Abort
    ${EndIf}
  ${EndIf}

  ; validate Unity path only if the user actually picked something
  ${If} $UnityPath != ""
    ${IfNot} ${FileExists} "$UnityPath\Unity.exe"
      ${IfNot} ${FileExists} "$UnityPath\Editor\Unity.exe"
        MessageBox MB_ICONEXCLAMATION|MB_YESNO "The selected folder does not contain Unity.exe. Continue anyway?" IDYES +2
        Abort
      ${EndIf}
    ${EndIf}
  ${EndIf}
FunctionEnd

; ---------------- auto-detect Unity in .onInit ----------------
Function .onInit
  StrCpy $ProjectInstall 0
  StrCpy $UnityPath ""
  StrCpy $ProjectPath ""

  ; registry (64-bit view then 32-bit view)
  SetRegView 64
  Call DetectUnityReg
  ${If} $UnityPath == ""
    SetRegView 32
    Call DetectUnityReg
  ${EndIf}

  ; fallback: Unity Hub default install dirs
  ${If} $UnityPath == ""
    ${If} ${FileExists} "$PROGRAMFILES64\Unity\Hub\Editor"
      FindFirst $0 $1 "$PROGRAMFILES64\Unity\Hub\Editor\*"
      ${Do}
        ${If} $1 == ""
          ${ExitDo}
        ${EndIf}
        ${If} ${FileExists} "$PROGRAMFILES64\Unity\Hub\Editor\$1\Editor\Unity.exe"
          StrCpy $UnityPath "$PROGRAMFILES64\Unity\Hub\Editor\$1\Editor"
          ${ExitDo}
        ${EndIf}
        FindNext $0 $1
      ${Loop}
      FindClose $0
    ${EndIf}
  ${EndIf}
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
        StrCpy $UnityPath $3
        ${ExitDo}
      ${ElseIf} ${FileExists} "$3\Editor\Unity.exe"
        StrCpy $UnityPath "$3\Editor"
        ${ExitDo}
      ${EndIf}
    ${EndIf}
    IntOp $2 $2 + 1
  ${Loop}
FunctionEnd

; ---------------- uninstaller ----------------
Section "Uninstall"
  Delete "$INSTDIR\Uninstall.exe"
  Delete "$INSTDIR\livecollab_guide.html"
  Delete "$INSTDIR\LiveCollabHost.exe"
  Delete "$INSTDIR\LICENSE"
  Delete "$INSTDIR\package.json"
  Delete "$INSTDIR\README.md"
  Delete "$INSTDIR\CHANGELOG.md"
  RMDir /r "$INSTDIR\package"
  RMDir "$INSTDIR"

  Delete "$SMPROGRAMS\${APPNAME}\LiveCollab User Guide.lnk"
  Delete "$SMPROGRAMS\${APPNAME}\LiveCollab Homepage.lnk"
  Delete "$SMPROGRAMS\${APPNAME}\Uninstall LiveCollab.lnk"
  RMDir "$SMPROGRAMS\${APPNAME}"
  Delete "$DESKTOP\LiveCollab User Guide.lnk"

  DeleteRegKey HKLM "${REGUNINST}"
SectionEnd
