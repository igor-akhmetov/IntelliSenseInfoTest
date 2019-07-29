Sample VS extension for an IntelliSense API issue in VS 16.2 Preview.

Steps to reproduce:
1. "File | Open | CMake" on CMakeProj\CMakeLists.txt.
2. Wait until CMake generation is done.
3. "File | Close Folder".
4. "File | Open | CMake" on CMakeProj\CMakeLists.txt.
5. Message box should appear. Note the IntelliSense errors in main.cpp. The errors disappear when CMake cache is regenerated.