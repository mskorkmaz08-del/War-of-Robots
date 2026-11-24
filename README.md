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

**If the build log says "Failed to login - please check your username or password":** the email/password secrets don't work for signing in to Unity. Check for typos. If you created your Unity account with **Google or Apple sign-in**, it has no password yet: go to [id.unity.com](https://id.unity.com), use **Forgot password** to set one, then update `UNITY_PASSWORD`. Two-factor sign-in on the Unity account also blocks cloud builds.

### 2. GitHub Pages

**Settings > Pages > Build and deployment > Source:** choose **GitHub Actions**.

### 3. Run a build

**Actions > Build game > Run workflow**, or just push to `main`. The browser version takes about 10 to 20 minutes and the iPhone app takes a bit longer.

## The game

Pick your machine, then destroy the two enemy robots in the arena.

- **TITAN**: a 15-metre walking giant. Heavy arm cannons, a 12-missile homing swarm, and jump jets. Landing from a jump sends out a shockwave.
- **RAPTOR**: an armoured six-wheeler. Rapid pulse lasers, a charge-up plasma railgun, and ULTRA THRUST rockets for ramming. Its front is armoured, so ramming hurts the enemy more than you.

Everything is physics-driven. Robots lose armour plates as they take damage, catch fire, and blow apart when destroyed. Shipping containers can be smashed around. Robots, sounds and effects are all generated in code, with no art or audio files yet.

## Controls

| Action | Keyboard / mouse | Gamepad | Touch |
|---|---|---|---|
| Move / turn | W A S D or arrows | Left stick | Joystick (bottom left) |
| Main gun | Space or left click | Right trigger / A | FIRE |
| Special weapon | E or right click | Left trigger / X | SPECIAL |
| Boost | Left Shift | Bumpers / B | BOOST |

Aiming is automatic: the upper body turns toward the nearest enemy in front of you.

## Project layout

- `Assets/Scripts/Robot`: robot construction (`RobotFactory`), walking (`WalkerLocomotion`, procedural IK legs), wheels (`WheeledLocomotion`, raycast suspension), health and damage (`Damageable`), controls (`RobotInput`)
- `Assets/Scripts/Weapons`: auto-aim `Turret`, cannons, missiles, lasers, railgun, projectiles
- `Assets/Scripts/Core`: generated meshes, materials, particle effects (`Fx`) and synthesized sound (`GameAudio`)
- `Assets/Scripts/AI`: enemy behaviour
- `Assets/Scripts/Game`: match flow and HUD
- `Assets/Editor/ArenaBuilder.cs`: builds the arena scene (menu **Robot Arena > Build Arena Scene**)
- `Assets/Editor/CIBuild.cs`: entry point used by the cloud builds
- `.github/workflows/build.yml`: cloud builds for the browser (GitHub Pages) and iPhone (.ipa)

## Opening it in Unity (optional)

Install **Unity 6000.0.47f1** with iOS and Web build support, open this folder as a project, choose **Robot Arena > Build Arena Scene**, and press Play.

## Roadmap

1. **Robot core**: driving and touch controls *(done)*
2. **Giant robot battles**: walker and wheeled giants, weapons, damage, AI, effects, sound *(done, waiting for first cloud build)*
3. **Garage and points**: earn points per match and spend them on parts and upgrades
4. **Polish and builds**: tune for iPhone performance, PC build
