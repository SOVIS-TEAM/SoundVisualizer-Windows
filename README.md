<div align="center">

[English](README.md) | [한국어](README.ko.md)

<br/>

# SoundVisualizer

<img width="1024" height="818" alt="SoundVisualizer" src="https://github.com/user-attachments/assets/b11aa5b3-c995-4e36-8ff6-3e2f2c2b2388" />

<img width="1024" alt="SoundVisualizer overlay: ambient, human sounds, danger, sound from the left, and sound from the right" src="docs/images/readme_screens.png" />

[![License: AGPL v3](https://img.shields.io/badge/License-AGPL_v3-blue.svg)](https://www.gnu.org/licenses/agpl-3.0)
[![WPF](https://img.shields.io/badge/WPF-blue?style=for-the-badge&logo=windows)](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)
[![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![.NET](https://img.shields.io/badge/.NET-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)
[![ONNX](https://img.shields.io/badge/ONNX-005CED?style=for-the-badge&logo=onnx&logoColor=white)](https://onnxruntime.ai/)
[![XERJ Corpus Hub contributor](https://hub.xerj.org/contributors/amophi/badge.svg)](https://hub.xerj.org/contributors/amophi)

**Real-Time Audio Visualization & AI Sound Analysis Overlay Engine**

**[Website](https://sovis-team.github.io/en/) · [Demo video](https://youtu.be/NBRV5AWpb38) · [Download](https://github.com/SOVIS-Team/SoundVisualizer-Windows/releases/latest) · [Microsoft Store](https://apps.microsoft.com/detail/9ngh48vcrfw2) · [Android version](https://github.com/SOVIS-Team/SoundVisualizer-Android) · [Instagram](https://www.instagram.com/sovis.official/)**

</div>

<br/>

> **SoundVisualizer** is a desktop application that captures real-time system audio and translates its direction, intensity, and meaning into a graphical overlay. 
Going beyond simple waveform rendering, it integrates an AI sound classifier powered by **YAMNet** and **ONNX Runtime** to instantly identify the type of sound playing (ambient, human sounds, danger) and provide adaptive visual feedback.

> 📱 There is also an Android version: [SoundVisualizer for Android](https://github.com/SOVIS-Team/SoundVisualizer-Android).

> 🔎 Listed on the [XERJ Corpus Hub](https://hub.xerj.org/corpus/sovis-team--soundvisualizer-windows), an open code index that AI coding agents search before they write code. Our work on making the color picker readable by screen readers is there as a worked example.

---

## 📖 Table of Contents
- [Background & Objectives](#-background--objectives)
- [Key Features](#-key-features)
- [System & AI Architecture](#-system--ai-architecture)
- [Installation & Quick Start](#-installation--quick-start)
- [Build Guide (For Developers)](#-build-guide-for-developers)
- [License](#-license)

---

## 🎯 Background & Objectives

### Enhancing Media Accessibility for the Deaf and Hard of Hearing
Modern media content, including YouTube, OTT, and gaming, relies heavily on spatial audio to maximize immersion. However, this creates a severe barrier for the deaf and hard of hearing. Traditional subtitle systems only deliver dialogue, completely omitting crucial acoustic information such as **sound direction, footsteps, or urgent sound effects (e.g., gunshots).**
SoundVisualizer was created to break down these barriers by **visualizing the intensity, classification, and spatial direction of invisible sounds.** Our primary goal is to resolve this information asymmetry and guarantee an equal, fully accessible media consumption environment for everyone.

### Expanding Utility for All Users
This overlay technology is also highly beneficial for gamers, providing a **tactical visual indicator (situational awareness)** in competitive environments. Additionally, it serves as a perfect alternative for users consuming media in public or silent environments where audio output is restricted.

---

## ✨ Key Features

### 1. Diverse Spatial and Intensity Visualizer Modes
We provide 4 unique rendering modes that intuitively map sound intensity and direction (supporting 2.0, 5.1, and 7.1 channels):
- **Wave Mode**: Renders dynamic audio waves along the screen edges that fluctuate based on intensity.
- **Circle Mode**: Radiates circular ripples outward from the center of the screen.
- **Pad Mode**: Displays glowing pads anchored to specific spatial grid directions.
- **Outline Mode**: A minimalist variation of Wave mode, showing only the thin borders of the wave to minimize screen occlusion.
- Animations move at the same speed at any frame rate, from 30 FPS up to your monitor's refresh rate.
- **Easy settings**: The Settings tab opens with a picture-based view where you choose the shape, the size (Small, Medium, Large), and which sound types to show. Switch to “Detailed” for every other setting.
- **Choose the display**: With two or more displays connected, choose which one shows the overlay in the Settings tab. The maximum frame rate goes up to that display's refresh rate. If that display is disconnected, the overlay is shown on the main display and returns when the display is connected again.

### 2. On-Screen Real-Time Editor (F4)
Modify settings instantly while in a full-screen application or game without minimizing the window.
- **F2 / F3 Hotkeys**: Switch between sound modes and visualization modes on the fly. Hotkeys can be customized.
- **Editor Mode (Default F4)**: Drag the guideline boundaries on your screen to physically resize the rendering limits in real-time. Adjust colors, opacity, and all other settings directly from the pop-up control panel.

### 3. Hardware-Aware Multi-Channel Support
- Automatically detects the system's audio configuration (Stereo, 5.1, 7.1 Surround) and accurately maps the sound's origin (Front/Back/Left/Right) to produce a 3D visual effect on a 2D screen.
- The sound mode switches to match your output device whenever the visualizer starts or the device changes, and you can still change it yourself.
- With stereo output, sound common to both channels is drawn at the top and only the difference between left and right goes to the sides, so a centred sound no longer looks like sound coming from both sides.

### 4. Real-Time AI Sound Classification (ONNX & YAMNet)
- **3-Class Detection**: Analyzes all incoming audio into `Ambient`, `Human sounds`, and `Danger` categories. Each category triggers independent, customizable UI colors and dynamic opacity changes.
- **Warning cues**: Sirens, alarms, explosions, fireworks and similar warning sounds count as `Danger` even when they are only among the model's top five candidates (at 5% or more), so music or game audio can't drown them out. Gunshot labels are not promoted this way, to avoid false alarms.
- **Stable colors**: `Danger` appears on the first qualifying result; `Human sounds` and `Ambient` need two in a row, so the color doesn't flicker. The AI pauses while it's quiet and resumes as soon as sound returns.
- **Faster human sounds**: When a voice starts, a small voice detector (Silero VAD) checks the newest audio and can show `Human sounds` one step (about 0.25 s) earlier. It only changes what you see: `Danger` and your choices in the Sounds tab always win.
- **Every channel counts**: On 5.1/7.1 systems, the AI hears the rear and side channels too, not just the front.
- **Late danger**: If a `Danger` result arrives a moment after the sound, the overlay still draws that sound at the size it had, even when its earlier type was hidden.

### 5. Choose How Each Sound Counts (Sounds Tab)
- Browse the 521 sounds the AI knows, grouped like the Android version, and choose whether each group (or, in advanced mode, each sound) counts as `Ambient`, `Human sounds`, or `Danger`.
- Search by name, filter by type or by the sounds you changed, and reset everything in one step. The overlay's AI follows your changes immediately.

### 6. Help, Notices, and Tray Icon
- **Help tab**: Getting started, visual modes, sound types, hotkeys, FAQ, a report button that opens a prefilled GitHub issue or email, and the open source licenses.
- **Notices**: The Home tab tells you when there is no playback device to capture, when sound type detection is unavailable, or why the visualizer stopped.
- **Tray icon**: Start or stop the visualizer, return to the launcher, or exit from the notification area.
- **Tutorial**: The first time you open the app, a short tutorial shows how sound, direction, and sound types are drawn, using the real renderer. You can open it again from the Home tab.
- **Developer mode**: Shows what the AI heard (its top five sounds, how sure it was, and how long it took) at the top left of the overlay, and can record every result to a CSV file in the `ai-log` folder next to the app.
- **Keyboard and screen readers**: Every control in the launcher can be reached with the Tab key. On the tab row, the arrow keys switch tabs, and `Ctrl + Tab` moves to the next tab from anywhere. Dialogs and the overlay editor close with `Esc`. The language list opens with the arrow keys so you can browse it, and the language only changes when you choose one with `Enter`. Screen readers such as Narrator and NVDA read icon buttons, hotkey buttons, color buttons, and the sound list by name, and announce the running status, notices, and the number of changed sounds as they change. Everything you can drag with the mouse in the overlay editor can also be changed with the sliders in its panel.

### 7. Comprehensive Localization
- Supports 18 languages: Korean, English, Japanese, Chinese (Simplified and Traditional), Vietnamese, Thai, Indonesian, Spanish, Portuguese (Brazil), French, Italian, German, Russian, Polish, Turkish, Hindi, and Arabic (right-to-left). By default it follows the Windows display language.
- The interface uses the Pretendard font, with Windows fonts for scripts Pretendard doesn't cover.

---

## ⚙️ System & AI Architecture

The project is rigorously engineered for high-performance, real-time background processing with minimal system overhead.

* **Core Audio API (WASAPI)**: Loopback captures system-wide audio with zero latency.
* **Per-Channel Levels**: The capture thread measures each channel's peak level for every buffer, and the render loop takes the loudest value since the previous frame.
* **Zero-Allocation Rendering**: The WPF/C# rendering loop is designed to minimize Garbage Collector (GC) allocation, entirely preventing frame drops.
* **Frame-Rate Independent and Idle-Aware**: Smoothing is normalized to a 60 FPS reference. When nothing has been visible for a second, the overlay stops redrawing every frame and wakes as soon as sound arrives.
* **ONNX Inference Pipeline**: Every 250 ms, the last 0.975 seconds of audio is downmixed to mono, resampled to 16 kHz through an anti-aliasing filter, turned into the log-mel spectrogram the model was exported with, and classified by YAMNet via `Microsoft.ML.OnnxRuntime`. The model files are checked against pinned SHA-256 hashes at load. Each step is unit-tested against reference outputs.

---

## 🚀 Installation & Quick Start

SoundVisualizer runs on Windows 10/11 (64-bit). You don't need to install .NET. Each **[GitHub Release](https://github.com/SOVIS-Team/SoundVisualizer-Windows/releases)** has two zips:

- **Standard version** (`SoundVisualizer_vX.Y.Z.zip`): `SoundVisualizer.exe` with its DLLs and the `AIModel` folder. Extract all the files, then run the exe in the folder.
- **Portable version** (`SoundVisualizer_vX.Y.Z_portable.zip`): one `SoundVisualizer.exe` that runs on its own. Run it straight from the zip, or copy just the exe anywhere. Use it where the standard version won't run, such as PC bangs, schools and other managed PCs, or from a USB drive.

You can also install it from the **[Microsoft Store](https://apps.microsoft.com/detail/9ngh48vcrfw2)** or with **Chocolatey** (`choco install soundvisualizer`).

1. Go to the **[GitHub Releases](https://github.com/SOVIS-Team/SoundVisualizer-Windows/releases)** page.
2. Download the standard or the portable zip.
3. Standard: extract all the files and run **`SoundVisualizer.exe`** in the folder. Portable: run **`SoundVisualizer.exe`** from the zip, or copy it anywhere first.
4. Configure your initial settings and language in the launcher, then click **Start** to activate the overlay.

Your settings are saved in `settings.json` next to `SoundVisualizer.exe`. When it starts, the portable version unpacks its graphics and AI engine files (about 40 MB) into your temp folder (`%TEMP%\.net\SoundVisualizer` and `%TEMP%\SoundVisualizer`). If Windows clears them, the app unpacks them again.

Some PC bangs and managed PCs block programs they don't know, and Windows SmartScreen may show "Windows protected your PC" because the app is not code-signed. If **More info → Run anyway** is offered, you can use it.

### Updates

- **Standard and portable versions**: once a day, when you open the launcher, the app checks GitHub for the latest version. If there is a new one, a notice appears on Home; click **Update now** and the app downloads the new zip, checks its SHA-256, replaces `SoundVisualizer.exe` in the same folder, and reopens. Your settings file stays. The update installs the standard version; on a portable copy it adds the DLLs and the `AIModel` folder next to the exe. You can also check from Help > About > **Check for updates**, and turn off the automatic check with Settings > Advanced > **Check for updates on start**.
- **Microsoft Store version**: the Store updates the app. The app does not connect to the internet.
- **Chocolatey version**: update with `choco upgrade soundvisualizer -y` in an administrator Command Prompt. The app does not connect to the internet.

When the app checks for updates, it only sends a GitHub API request (with a User-Agent containing the app name and version). It never sends sound, settings or personal information.

---

## 🛠 Build Guide (For Developers)

To build from source to modify the code or contribute to the project:

1. **Prerequisites**: Windows 10/11 and the .NET 10 SDK. Visual Studio 2026 (with the .NET Desktop Development workload) is optional.
2. **Clone the repository**:
   ```bash
   git clone https://github.com/SOVIS-Team/SoundVisualizer-Windows.git
   ```
3. Build and test from the repository folder:
   ```bash
   dotnet build SoundVisualizer.slnx -c Release
   dotnet test SoundVisualizer.Tests/SoundVisualizer.Tests.csproj
   ```
   Or open `SoundVisualizer.slnx` in Visual Studio, build (`Ctrl + Shift + B`), and press `F5` to run.
4. To make the portable release zip (one exe with the DLLs and AI models inside), run `./packaging/portable/build-portable.ps1`. It writes `artifacts/SoundVisualizer_vX.Y.Z_portable.zip`. The standard zip comes from `./packaging/standard/build-standard.ps1` (`artifacts/SoundVisualizer_vX.Y.Z.zip`), and the Microsoft Store package from `./packaging/msix/build-msix.ps1`.

The solution has three projects: `SoundVisualizer` (the WPF app), `SoundVisualizer.Core` (audio, AI, rendering logic, settings, and the 18 language tables, with no UI dependency), and `SoundVisualizer.Tests` (xUnit tests, including reference outputs for each AI step).

---

## 📄 License

SoundVisualizer is distributed under the **AGPL v3** license to encourage a virtuous cycle in the open-source community. Anyone is welcome to modify the code and share custom UI themes or streaming plugins. See the [LICENSE](LICENSE) file for more information.

For licensing and copyright information regarding third-party libraries (NAudio, ONNX Runtime, etc.) and AI models used in this project, please refer to the [THIRD_PARTY.md](THIRD_PARTY.md) file.

<br/>
<div align="center">
  <b>Built for Accessibility.</b>
</div>
