# PoR_L10n
This is very rough work that I did when helping to localize the original (GOG version) Pool of Radiance.

This repo is also a general dumping ground for sharing data that I am extracting / importing / sharing with other members of the effort.

The BinaryStringReplacement project is a very rough very simple project that automated the replacement of strings in the binaries.  Usage flags are shown when running the binary with no parameters.
There were really two situations where we needed a way to get the strings substituted into the binaries in an automated fashion.  1) The START.EXE/GAME.OVR binaries, which pretty much just had the plain ASCII strings in the file and 2) the ECL.DAX files.  Monsters were handled through GBC.
BinaryStringReplacement has two modes, a default mode that operates on the START.EXE or GAME.OVR file (or any other binary file with ASCII strings), and a -E mode that operates on EXTRACTED ECL files.   The difference is that strings in ECL files use compression.  See Simeon Pilgrim's cotab project for routines for compression/decompression.  The ECL mode will compress the "from" string, look for it in the binary, and then replace that with the compressed version of the "to" string.  Note that THIS IS MEANT TO OPERATE ON UNPACKED OR "EXTRACTED" ECL FILES ONLY.  Do not run this on the game's ECL*.DAX files,  Instead, you must first unpack the ECL file using GoldBox Companion's DAXBuilder.exe to get a .dat file.  And after modifying, you should re-pack it into a final ECL*.DAX using the same tool.

Note: The BinaryStringReplacement routine attempts to use extra 00 null bytes that were present in the UNPACKED version of start .exe.  These will not be present in the packed version, but the same routine should work.  Use UNP from http://unp.bencastricum.nl/ inside DOSBox to unpack the START.EXE which is packed using EXEPACK.

None of these tools are meant to be distributed to an end-user, they are only put here in case I die or get distracted before the project is complete.


