:: Copyright (c) Microsoft. All rights reserved.
:: Licensed under the MIT license. See LICENSE file in the project root for full license information.

@echo off

set _currentPath=%~dp0
call "%_currentPath%setVsPath.cmd"
if ErrorLevel 1 (
    exit /b 1
)

if not defined mstest (
    if exist "%vs2017path%\Common7\IDE\MSTest.exe" (
        set mstest="%vs2017path%\Common7\IDE\MSTest.exe"
    ) else (
        if exist "%VS140COMNTOOLS%\..\IDE\MSTest.exe" (
            set mstest="%VS140COMNTOOLS%\..\IDE\MSTest.exe"
        ) else (
            if exist "%VS120COMNTOOLS%\..\IDE\MSTest.exe" (
                set mstest="%VS120COMNTOOLS%\..\IDE\MSTest.exe"
            ) else (
				if exist "%VS110COMNTOOLS%\..\IDE\MSTest.exe" (
					set mstest="%VS110COMNTOOLS%\..\IDE\MSTest.exe"
				)
            )
        )
    )
)

if not defined mstest(
    echo Error: Visual Studio or Visual Studio test agent should be installed (version 2012, 2013, 2015 or 2017)
    exit /b 1
)
exit /b 0