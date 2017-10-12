@echo off

if "%1"=="" goto usage
if "%2"=="" goto usage
if "%3"=="" goto usage

if "%WindowsSdkDir%" neq "" goto build

if exist "%ProgramFiles(x86)%\Microsoft Visual Studio 14.0\VC\vcvarsall.bat" goto initialize2k8on64Dev14
if exist "%ProgramFiles%\Microsoft Visual Studio 14.0\VC\vcvarsall.bat" goto initialize2k8Dev14

if exist "%ProgramFiles(x86)%\Microsoft Visual Studio 12.0\VC\vcvarsall.bat" goto initialize2k8on64Dev12
if exist "%ProgramFiles%\Microsoft Visual Studio 12.0\VC\vcvarsall.bat" goto initialize2k8Dev12

if exist "%ProgramFiles(x86)%\Microsoft Visual Studio 10.0\VC\vcvarsall.bat" goto initialize2k8on64
if exist "%ProgramFiles%\Microsoft Visual Studio 10.0\VC\vcvarsall.bat" goto initialize2k8

if exist "%ProgramFiles(x86)%\Microsoft Visual Studio 11.0\VC\vcvarsall.bat" goto initialize2k8on64Dev11
if exist "%ProgramFiles%\Microsoft Visual Studio 11.0\VC\vcvarsall.bat" goto initialize2k8Dev11
echo "Unable to detect suitable environment. Build may not succeed."
goto build

:initialize2k8
call "%ProgramFiles%\Microsoft Visual Studio 10.0\VC\vcvarsall.bat" x86
goto build

:initialize2k8on64
call "%ProgramFiles(x86)%\Microsoft Visual Studio 10.0\VC\vcvarsall.bat" x86
goto build

:initialize2k8Dev11
call "%ProgramFiles%\Microsoft Visual Studio 11.0\VC\vcvarsall.bat" x86
goto build

:initialize2k8on64Dev11
call "%ProgramFiles(x86)%\Microsoft Visual Studio 11.0\VC\vcvarsall.bat" x86
goto build

:initialize2k8Dev12
call "%ProgramFiles%\Microsoft Visual Studio 12.0\VC\vcvarsall.bat" x86
goto build

:initialize2k8on64Dev12
call "%ProgramFiles(x86)%\Microsoft Visual Studio 12.0\VC\vcvarsall.bat" x86
goto build

:initialize2k8Dev14
call "%ProgramFiles%\Microsoft Visual Studio 14.0\VC\vcvarsall.bat" x86
goto build

:initialize2k8on64Dev14
call "%ProgramFiles(x86)%\Microsoft Visual Studio 14.0\VC\vcvarsall.bat" x86
goto build

:build
msbuild /t:publish /p:ApplicationVersion=%1 /p:PublishDir=%2 /p:MinimumRequiredVersion=%1 /p:InstallUrl="%3" /p:UpdateUrl="%3"
goto end

:usage
echo.
echo publish.bat [version] [directory] [url]
echo.
echo(	[version] 	The version you wish to publish Caching Dashboard as.
echo(			It will also be used for the MinimumRequiredVersion property
echo(			(so as to force clients to update)
echo(	[directory] 	The directory to which the ClickOnce package will be published.
echo(	[url] 	The url from which the ClickOnce package can be installed and updated.
echo.

:end