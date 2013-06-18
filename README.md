XenGuestAgent - The XenServer Windows Guest Agent Service
==========================================

XenGuestAgent is a Windows service which provides support for user-level
operations within Windows guests.

Specific features of the guest agent include

*    Providing information about the guest VM to the Host VM
*    Shutting down and rebooting VMs when requested
*    Resetting system time following resume-from-suspend and migration
*    Reporting the capabilities of a guest VM, drivers and agent
*    Providing and communicating with the Clipboard deamon XenDpriv.exe
*    Supporting the production of quiesced disk snapshots

Quick Start
===========

Prerequisites to build
----------------------

*   Visual Studio 2012 or later 
*   Python 3 or later 

Environment variables used in building driver
-----------------------------

BUILD\_NUMBER Build number

VS location of visual studio

Commands to build
-----------------

    git clone http://github.com/xenserver/win-xenguestagent
    cd win-xenguestagent
    .\build.py [checked | free]

Runtime Dependencies
--------------------

To communicate with the host domains, XenGuestAgent requires that the
XenIface driver (and pre-requites of the XenIface driver) are installed
on the Guest VM.

The XenGuestAgent requires Microsoft .Net Framework 3.5 or 4 and above
to be installed ont he guest VM
