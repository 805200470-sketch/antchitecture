#!/bin/zsh
# copy_from_T9.sh - copy the Ant Garden material that lived on the T9 drive into this project folder.
# Copies only: nothing on T9 is changed or deleted, and nothing here is deleted. Safe to run again (only new or
# changed files are copied). The macOS ._ helper files of the exFAT drive and the old .secrets folder are skipped.
#
#   T9                                 ->  here
#   Ant_Garden_Demo                    ->  06_Prototypes/Ant_Garden_Web_Demo          (the Three.js web prototype)
#   Ant_00N_Resource                   ->  03_Models/Ants_Source.nosync/Ant_00N_Resource   (rigged .blend + FBX sources)
#   Antchitecture_Ants                 ->  03_Models/Ants_Source.nosync/Antchitecture_Ants (the ant modelling pipeline)
#   Claude outputs                     ->  03_Models/Ants_Source.nosync/Claude_outputs
#   Path_Recognize_Package             ->  04_Paths/Path_Recognize_Package
# Already here before: Ant_00N_Reference (01_References/Ants), path image (04_Paths/Images).
# Not copied (not part of Ant Garden): Antchitecture_Web (the team's earlier website), light of tomorrow, Magician.
T9=/Volumes/T9
A="${0:A:h:h}"
if [ ! -d "$T9/Ant_Garden_Demo" ]; then echo "T9 is not plugged in" >&2; exit 1; fi
copy() {  # copy <source dir> <destination dir>
  mkdir -p "$2"
  rsync -a --exclude '._*' --exclude '.DS_Store' --exclude '.secrets' --exclude 'node_modules' "$1/" "$2/" \
    && echo "ok   $(du -sh "$2" | cut -f1)  ${2#$A/}" || echo "FAILED  $1"
}
copy "$T9/Ant_Garden_Demo" "$A/06_Prototypes/Ant_Garden_Web_Demo"
for n in 001 002 003 004 005; do
  copy "$T9/Ant_${n}_Resource" "$A/03_Models/Ants_Source.nosync/Ant_${n}_Resource"
done
copy "$T9/Antchitecture_Ants" "$A/03_Models/Ants_Source.nosync/Antchitecture_Ants"
copy "$T9/Claude outputs" "$A/03_Models/Ants_Source.nosync/Claude_outputs"
copy "$T9/Path_Recognize_Package" "$A/04_Paths/Path_Recognize_Package"
# keep the big source folder out of iCloud (like UnityProject.nosync and 05_Builds.nosync)
xattr -w 'com.apple.fileprovider.ignore#P' 1 "$A/03_Models/Ants_Source.nosync" 2>/dev/null
