# Robot models

Every robot is modelled from scratch in Blender by a Python script here and saved as
`Assets/Resources/RobotModels/<Name>.bytes`, which `RobotModel.cs` loads in the game.

Run with Blender's Python module (`pip install bpy`):

    python titan.py -- ../../Assets/Resources/RobotModels            # export
    python titan.py --render -- /tmp/out                             # export + Cycles previews
    python wheeled.py Raptor -- ../../Assets/Resources/RobotModels   # Raptor, Hornet, Juggernaut, Bastion

- `mechlib.py`: modelling toolkit (lofted superellipse volumes, lathed parts, bevelled blocks,
  curved armour plates, hoses, vents) and the exporter.
- `rig.py`: the game skeleton (same two-bone IK as WalkerLocomotion), parts exported in the frame
  of the bone that moves them, live hydraulic rams, a parametric armoured leg, spider leg, pelvis and arm.
- One script per robot (titan, lancer, vanguard, arachne, mammoth, wheeled).

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
