#!python -u

# Copyright (c) Citrix Systems Inc.
# All rights reserved.
#
# Redistribution and use in source and binary forms, 
# with or without modification, are permitted provided 
# that the following conditions are met:
#
# *   Redistributions of source code must retain the above 
#     copyright notice, this list of conditions and the 
#     following disclaimer.
# *   Redistributions in binary form must reproduce the above 
#     copyright notice, this list of conditions and the 
#     following disclaimer in the documentation and/or other 
#     materials provided with the distribution.
#
# THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND 
# CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, 
# INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF 
# MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE 
# DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR 
# CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, 
# SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, 
# BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR 
# SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
# INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, 
# WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING 
# NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE 
# OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF 
# SUCH DAMAGE.


import os, sys
import datetime
import glob
import shutil
import tarfile

def make_header():
    now = datetime.datetime.now()

    file = open('src\\xenguestlib\\VerInfo.cs', 'w')

    file.write('public class XenVersions {')
    file.write('public const string Version="'+os.environ['MAJOR_VERSION']+'.'+os.environ['MINOR_VERSION']+'.'+os.environ['MICRO_VERSION']+'.'+os.environ['BUILD_NUMBER']+'";')

    rest = """
        public const string ShortName = "Citrix";
        public const string LongName = "Citrix Systems, Inc.";
        public const string CopyrightYears = "2012";
    }
    """
    file.write(rest)

    file.close()

def shell(command):
    print (command)
    sys.stdout.flush()
    pipe = os.popen(command, 'r', 1)
    for line in pipe:
        print(line.rstrip())

    return pipe.close()


def msbuild(name, debug = False):
    cwd = os.getcwd()
    configuration=''
    if debug:
        configuration = 'Debug'
    else:
        configuration = 'Release'

    os.environ['CONFIGURATION'] = configuration

    os.environ['PLATFORM'] = 'Any CPU'

    os.environ['SOLUTION'] = name
    os.environ['TARGET'] = 'Build'

    os.chdir('proj')
    status=shell('msbuild.bat')
    os.chdir(cwd)

def archive(name):
    tar = tarfile.open(name+'.tar','w')
    tar.add(name)
    tar.close()

def copyfiles(name, subproj, debug=False):

    configuration=''
    if debug:
        configuration = 'Debug'
    else:
        configuration = 'Release'
    
    src_path = os.sep.join(['proj',subproj,'bin', configuration ])

    if not os.path.lexists(name):
        os.mkdir(name)

    dst_path = os.sep.join([name, subproj])

    if not os.path.lexists(dst_path):
        os.mkdir(dst_path)

    for file in glob.glob(os.sep.join([src_path, '*'])):
        print("%s -> %s" % (file, dst_path))
        shutil.copy(file, dst_path)

    sys.stdout.flush()


if __name__ == '__main__':
    os.environ['MAJOR_VERSION'] = '7'
    os.environ['MINOR_VERSION'] = '0'
    os.environ['MICRO_VERSION'] = '1'
    if 'BUILD_NUMBER' not in os.environ.keys():
        os.environ['BUILD_NUMBER'] = '0'

    make_header()

    debug = { 'checked': True, 'free': False }

    msbuild('xenguestagent', debug[sys.argv[1]])
    copyfiles('xenguestagent','xenguestagent', debug[sys.argv[1]])
    copyfiles('xenguestagent','xendpriv', debug[sys.argv[1]])
    archive('xenguestagent')
