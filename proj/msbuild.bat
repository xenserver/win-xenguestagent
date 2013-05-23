rem Copyright (c) Citrix Systems Inc.
rem All rights reserved.
rem
rem Redistribution and use in source and binary forms, 
rem with or without modification, are permitted provided 
rem that the following conditions are met:
rem
rem *   Redistributions of source code must retain the above 
rem     copyright notice, this list of conditions and the 
rem     following disclaimer.
rem *   Redistributions in binary form must reproduce the above 
rem     copyright notice, this list of conditions and the 
rem     following disclaimer in the documentation and/or other 
rem     materials provided with the distribution.
rem
rem THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND 
rem CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, 
rem INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF 
rem MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE 
rem DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR 
rem CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, 
rem SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, 
rem BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR 
rem SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
rem INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, 
rem WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING 
rem NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE 
rem OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF 
rem SUCH DAMAGE.


call "%VS%\VC\vcvarsall.bat" x86
msbuild.exe /p:Configuration="%CONFIGURATION%" /p:Platform="%PLATFORM%" /t:"%TARGET%" %SOLUTION%.sln

