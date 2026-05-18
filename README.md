# MobileAP
This is an [Archipelago](https://archipelago.gg/) Client built with [Archipelago.Multiclient.Net](https://github.com/ArchipelagoMW/Archipelago.MultiClient.Net) for Android and iOS in Unity 6 that works as both a Text Client and a [Manual Client](https://github.com/ManualForArchipelago/Manual). It's currently in an early development state, and the Manual Client should only be used in testing sessions.
This client has only been tested to work when connecting to servers hosted on the archipelago.gg website, though any webhost should work without issue. Connecting to games hosted over unsecure websockets may or may not work depending on the device you are using, and its security settings. If the connection doesn't work on MobileAP but does elsewhere, chances are your device doesn't support or explicity blocks insecure websockets.

# Features
- Fully functional Text Client, including dedicated pages for hints and received items.
- Functional Manual Client in testing, with built in logic highlighting. All you need is an `.apmanual` file.
- Activate Deathlink on any Manual, even ones that don't have it enabled in the official client.
- Font Size adjustment, out of logic sending confirmation, Deathlink vibration, and more options.
- Lock Screen Bypassing on Android, which keeps the app open even after the phone locks automatically after the screen goes to sleep. Can be disabled in the app options.
