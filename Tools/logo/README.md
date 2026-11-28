# Game logo

The logo is rendered in Blender (the `bpy` pip module, Cycles on CPU), like the robot models.

- `title.py out.png`: the 3D title "WAR OF ROBOTS". Chrome letters (Oxanium ExtraBold) with a glowing
  teal slit, "WAR OF" (Chakra Petch Bold) on a tab, and an armoured back plate with a steel rim, seams,
  rivets, hazard caps and a teal light strip. Needs `fonts/Oxanium_wght_800.ttf` and
  `fonts/Chakra_Petch_wght_700.ttf` (Google Fonts, OFL) and `hdri/industrial_workshop_foundry.hdr`
  (Poly Haven 1k) under `$LOGO_WORK`.
  Used settings: `RES=2400,1200 SAMPLES=96 DIST=8 CAMZ=-2 YAW=-14 SPARKS=0`.
- `robot_hero.py colossus hero.png`: the Colossus from ground level, close and with a wide lens so it
  towers (`RES=1200,1600 SAMPLES=64 DIST=13 CAMZ=0.3 TZ=10 LENS=14 YAW=25`), teal rim lights,
  transparent background. `clean_shadow.py hero.png hero_clean.png` keeps only the contact shadow.
- `compose.py 1400`: the robot behind the title with the reactor glow and haze, giving `keyart.png`.

Where they go:
- `Assets/Resources/UI/LogoTitle.png`: the title alone, shown on the main menu (Hud.BuildMenu).
- `Assets/WebGLTemplates/WarOfRobots/TemplateData/logo-title.webp`: the key art (robot + title) on the loading page.
- `Assets/Art/AppIcon.png` (1024), `Resources/UI/Logo.png` (512), `apple-touch-icon.png`, `favicon.png`:
  the Colossus on a teal glow.
