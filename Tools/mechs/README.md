# Robot models

Every robot is modelled from scratch in Blender by a Python script here and saved as
`Assets/Resources/RobotModels/<Name>.bytes`, which `RobotModel.cs` loads in the game.

Run with Blender's Python module (`pip install bpy`):

    python titan.py -- ../../Assets/Resources/RobotModels            # export
    python titan.py --render -- /tmp/out                             # export + Cycles previews
    python wheeled.py Raptor -- ../../Assets/Resources/RobotModels   # Raptor, Hornet, Juggernaut, Bastion

- `hardsurf.py`: hard-surface armour kit (second generation, Nov 2026). Faceted armour pieces are
  convex hulls of a few section points with chamfered edges and engraved dark panel seams; flat
  cut plates, flared side plates, wedges, grilles, hazard bands, lamps, bolts, hoses, drums, pistons.
  The first-generation models were smooth superellipse lofts that read as plastic toys; these are
  built the way heavy plant is: cut plate over an exposed dark frame.
- `parts.py`: assemblies with working hydraulics: walker leg (thigh/knee/shin/foot, shin styles
  crest/box/layered), spider leg, pelvis, arm with gauntlet, jets, spur gears. Live cylinders sit
  on every hip, knee, ankle and shoulder: barrel on one bone, rod on the other, so
  HydraulicRam.cs slides them as the robot walks and aims.
- `mechlib.py`: lathe/tube primitives and the exporter (faces may carry their own material).
- `rig.py`: the game skeleton (same two-bone IK as WalkerLocomotion), parts exported in the frame
  of the bone that moves them.
- One script per robot (titan, lancer, vanguard, arachne, mammoth, wheeled).
- `weapons.py`: the 10 weapons (13 files, `Weapon<Name>.bytes`, with Mortar/Swarm in 2- and
  3-or-4-tube versions and the Tesla coil plus capacitor bank). Built in the mount frame at the
  muzzle points WeaponKit.cs uses; WeaponModels.cs places them (mirrored on left mounts) and
  WeaponKit falls back to its primitive weapons when a file is missing.
- `showcase.py <robot> out.png`: preview render of a robot with its default weapons fitted.

File format (RBM3, little endian): `RBM3`, u16 part count; per part: u8 name length + name,
u32 vertex count, 6 f32 bounds (min, size), u16x3 quantised positions, i8x3 normals,
u8 submesh count, u8 32-bit-index flag, then per submesh u8 material + u32 index count + indices.
Then u32 length + JSON: stand height, leg lengths, frames (part placement and parent pivot),
anchors (weapon mounts, hips, jets, wheels), rams, legs, detachable parts.

Material ids: Primary, Secondary, Dark, Metal, Chrome, Accent, Glow, Black, Rubber, Hot.

Design notes, from research on legged-robot scaling (mass grows with height cubed, strength
with height squared; hip torque is the hardest load; heel strike is 3-4x body weight):
short, thick legs and a wide stance; big drum joints at the hips; paired rams across knees
and ankles; broad, padded feet; the upper body on a slewing ring; reactor, radiators and
exhausts at the back; the crew in a low armoured head with a lit visor.
