# WinUI smoke tests

Use an isolated local copy only. These tests deliberately replace and restart their own executable to verify the single-file update helper. Never use a production installation as the test executable.

1. Publish src.WinUI/SnipIt.WinUI.csproj with -c Release -p:PublishSingleFile=true -p:EnableSmokeTests=true -o <scratch app directory>.
2. Set SNIPIT_DATA_DIRECTORY to a new empty scratch profile directory, separate from the app directory.
3. Start the scratch app. It writes smoke.log and complete in that profile. All checks must pass, including the second-process restart check.
4. For normal distribution, publish again with -p:EnableSmokeTests=false to a different output directory.

The suite exercises the native image bridge, history, screen capture, GIF callback/encoding, image exports, large-image OCR mapping and update helper. It does not replace interactive UI testing or mixed-DPI hardware tests.
