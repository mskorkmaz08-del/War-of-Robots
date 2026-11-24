# War of Robots

A 3D robot combat game for iPhone, browser and PC, built in Unity 6. Players build their own robot, fight in an arena, earn points and upgrade their machine.

The game is built **in the cloud** by GitHub Actions, so you don't need Unity on your own computer to play it.

## Play it

- **In the browser (iPhone or PC):** the GitHub Pages link for this repository, shown under *Settings > Pages* once the first build has finished. On iPhone, open it in Safari, tap **Share**, then **Add to Home Screen** for a full-screen app.
- **As an iPhone app:** open the latest run under the **Actions** tab and download the `WarOfRobots-ipa` file. Install it with [Sideloadly](https://sideloadly.io) using your Apple ID. With a free Apple ID the app has to be re-installed every 7 days.

## One-time setup

### 1. Unity licence (needed for cloud builds)

1. Create a free account at [unity.com](https://unity.com) if you don't have one.
2. Install **Unity Hub** (you don't need the Unity editor itself), sign in, then go to **Preferences > Licenses > Add > Get a free personal license**.
3. Find the licence file:
   - Mac: `/Library/Application Support/Unity/Unity_lic.ulf`
   - Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
4. In this repository on GitHub, go to **Settings > Secrets and variables > Actions > New repository secret** and add:
   - `UNITY_LICENSE`: the whole contents of `Unity_lic.ulf` (open it in a text editor and copy everything)
   - `UNITY_EMAIL`: your Unity account email
   - `UNITY_PASSWORD`: your Unity account password
5. Sign out of Unity Hub, so the licence seat is free for the cloud builds.

Secrets are encrypted by GitHub. Nobody, including Claude, can read them back.

### 2. GitHub Pages

**Settings > Pages > Build and deployment > Source:** choose **GitHub Actions**.

### 3. Run a build

**Actions > Build game > Run workflow**, or just push to `main`. The browser version takes about 10 to 20 minutes and the iPhone app takes a bit longer.

## Controls

| Action | Keyboard | Gamepad | Touch |
|---|---|---|---|
| Drive / turn | W A S D or arrows | Left stick | Joystick (bottom left) |
| Fire flipper | Space | A / Cross, or right trigger | FLIP button (bottom right) |

## Project layout

- `Assets/Scripts/Robot`: driving (`RobotDrive`), flipper weapon (`RobotFlipper`) and controls (`RobotInput`)
- `Assets/Scripts/Controls`: on-screen joystick and buttons for touch screens
- `Assets/Scripts/Camera`: chase camera
- `Assets/Editor/TestArenaBuilder.cs`: builds the test arena scene from code (menu **Robot Arena > Build Test Arena Scene**)
- `Assets/Editor/CIBuild.cs`: entry point used by the cloud builds
- `.github/workflows/build.yml`: cloud builds for the browser (GitHub Pages) and iPhone (.ipa)

## Opening it in Unity (optional)

If you get a computer that runs Unity 6, install **Unity 6000.0.47f1** with iOS and Web build support, open this folder as a project, choose **Robot Arena > Build Test Arena Scene**, and press Play.

## Roadmap

1. **Robot core**: driving, flipper, touch controls, test arena *(done, not yet tested)*
2. **Arena fighting**: impact damage, parts that break off, health, AI opponent, sparks, debris, camera shake
3. **Garage and points**: earn points per match and spend them on parts and upgrades
4. **Polish and builds**: tune for iPhone performance, PC build
