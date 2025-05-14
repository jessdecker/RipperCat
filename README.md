# RipperCat

A simple tool to capture the audio you're listening to.

## Notes
Only tested with Blackhole audio loopback for Mac, but should work well on Windows and Linux with other audio loopback virtual devices.

## Setup for audio loopback capture
brew install blackhole-16ch
REBOOT COMPUTER!
After Installation and reboot: Set up a Multi-Output Device
To hear system audio and record/stream it at the same time:

Open Audio MIDI Setup (Cmd+Space → search for it).

Click the + button at the bottom-left → choose Create Multi-Output Device.

In the right panel:

Check both your physical output device (like "MacBook Speakers") and BlackHole 16ch.

Set the physical device (your actual speakers) as Master.

Rename the Multi-Output device (e.g., “BlackHole Mix”).

Make sure Drift Correction is enabled for your output device (but not BlackHole).

Now go to System Settings > Sound > Output, and select your Multi-Output Device as the system output.

In the "Audio Midi Setup" settings, Blackhole output device has an "output" tab. set volume slider for "primary" to 100% to capture full audio range.

To adjust volume for the physical output device (your laptop speakers for example) while you're using the Blackhole Mix output you have to use the 
volume slider in Audio Midi Setup, MacBook Pro Speakers, Output tab, Primary volume slider.

Now all system audio is routed through BlackHole and can be captured in your app

