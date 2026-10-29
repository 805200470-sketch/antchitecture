#!/bin/zsh
# quest.sh <what> - the standalone headset (Meta Quest, developer mode on, USB debugging allowed) over USB, with the adb
# Unity's Android tools bring:
#   devices         is it there (and allowed)
#   install         install or replace the app ($ANTGARDEN_APK, default 05_Builds.nosync/AntGarden_Quest/AntGarden.apk)
#   start | stop    start the app on the headset / stop it
#   push <json>     QuestLook's values (Scripts/Runtime/QuestLook.cs) over the built ones, read as the app starts
#                   (Tools/vr/quest.json holds the defaults: copy, change, push, then stop and start)
#   log             the game's lines from now on (frame rate every 5 s: "[AntGarden] quest: ...")
#   shot <png>      what the headset shows, as a still
A=/Applications/Unity/Hub/Editor/2022.3.62f3c1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
PKG=com.ucldfpi.antgarden
APK="${ANTGARDEN_APK:-${0:A:h}/../../../05_Builds.nosync/AntGarden_Quest/AntGarden.apk}"
case "$1" in
  devices) "$A" devices -l ;;
  install) "$A" install -r "$APK" ;;
  start) "$A" shell am start -n $PKG/com.unity3d.player.UnityPlayerActivity ;;
  stop) "$A" shell am force-stop $PKG ;;
  push) "$A" shell mkdir -p /sdcard/Android/data/$PKG/files && "$A" push "$2" /sdcard/Android/data/$PKG/files/quest.json ;;
  log) "$A" logcat -v time -T 1 -s Unity | grep --line-buffered -E "AntGarden|Exception|rror" ;;
  shot) "$A" exec-out screencap -p > "$2" && echo "$2: $(stat -f%z "$2") bytes" ;;
  *) sed -n 2,11p "$0" ;;
esac
