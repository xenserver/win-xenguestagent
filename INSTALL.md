To install the XenServer Windows XenGuestAgent onto a XenServer Windows 
guest VM:

*    Install your choice od .net 3.5 or .net 4 or greater on the VM
*    Install xeniface.sys and xenbus.sys on the guest VM
*    Create a directory "c:\\Program Files\\Citrix\\XenTools" (the installdir)
*    If you want to use .Net 4.x , copy XenGuestAgent.exe.Config into the 
     installdir
*    If you want to use .Net 4.x , copy XenDpriv.exe.Config into the 
     installdir
*    Copy XenGuestAgent.exe into the installdir
*    Copy XenGuestLib.dll into the installdir
*    Copy XenDpriv.exe into the installdir

*    Set the following registry entries
        
     `
     HKLM\\Software\\Citrix\\XenTools\\MajorVersion DWORD 6
     HKLM\\Software\\Citrix\\XenTools\\MinorVersion DWORD 2
     HKLM\\Software\\Citrix\\XenTools\\MicroVersion DWORD 0
     HKLM\\Software\\Citrix\\XenTools\\BuildVersion DWORD 20
     HKLM\\Software\\Citrix\\XenTools\\InstallDir STRING "C:\\Program Files\\Citrix\\XenTools"
     HKLM\\SYSTEM\\CurrentControlSet\\Control\\ServicesPipeTimeout DWORD 300000
     `

*    Run the following command

     `sc create XenSvc binPath= "c:\\Program Files\\Citrix\\XenTools\\xenguestagent.exe" type= own start = auto depend= WinMgmt DisplayName= "Citrix Xen Guest Agent"`

*    Reboot the VM
