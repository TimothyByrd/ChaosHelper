; This command tells the ChaosHelper to toggle displaying the test pattern
; test mode puts up a pattern to check that we got the screen resolution for the tab overlay.
; once that has been check you won't need this unless you get a different monitor.

; Ctrl-P
^P::
ControlSend,,t,ChaosHelper.exe  ; Send directly to the command prompt window.
Return