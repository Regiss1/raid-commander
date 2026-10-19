TASK: Build the first playable 3D test scene.

Requirements:

Create a flat 3D arena with a ground mesh, collider, lighting, and simple 3D obstacles.

Build an elevated camera looking downward at approximately 60 degrees from the horizontal.

Use orthographic projection, with adjustable zoom.

Support camera panning with WASD and zooming with the mouse wheel using Unity Input System.

Make camera movement relative to the ground XZ plane, not the camera's vertical orientation.

Use 3D primitive meshes as placeholders for three party characters and one boss.

Ensure each character stands on the ground and has a suitable 3D collider.

Configure distinct layers for ground, friendly units, and hostile units.

Use a 3D render pipeline and standard 3D scene components.
Do not use sprites or Unity 2D physics.

If direct Unity scene manipulation is not possible, provide an idempotent Editor setup utility to generate the scene.

Acceptance criteria:

A playable 3D scene opens without errors.

Camera pans and zooms smoothly.

Characters are visible from the top-down angle.

Obstacles visibly have 3D height and depth.

All objects occupy coherent world-space coordinates.