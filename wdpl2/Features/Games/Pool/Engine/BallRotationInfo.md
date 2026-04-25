# Ball Rotation Reference Info

Paste any reference information, images descriptions, or specifications here that you want to be used for implementing ball rotation.

## Notes:



3D mathematics and computer graphics, calculating the rotation of a ball typically falls into two categories: 
kinematic rolling (finding how much a ball rotates as it moves along a surface) and point rotation (calculating the new position of a point on the sphere after a rotation). 
1. Kinematic Rolling Calculation 
To calculate the rotation of a ball of radius 
 
 
𝑟
as it rolls a distance 
 
 
𝑑
across a flat surface, use the relationship between linear distance and angular displacement. 
•	Rotation Angle (
 
 
𝜃
): The angle (in radians) the ball rotates is the distance moved divided by the radius:

 
 
𝜃=𝑑𝑟
•	Rotation Axis (
 
 
𝑎⃗
): If the ball is moving with a velocity vector 
 
 
𝑣⃗
on a surface with a normal vector 
 
 
𝑛⃗
(typically 
 
 
(0,1,0)
for a flat floor), the axis of rotation is the cross product of the normal and the velocity:

 
 
𝑎⃗=normalize(𝑛⃗×𝑣⃗)
•	Combined Rotation: For a realistic "no-slip" look, you can update the ball's rotation matrix or quaternion each frame using this axis and angle. 
2. Rotating a Point on a Sphere 
To find the new 3D coordinates 
 
 
(𝑥′,𝑦′,𝑧′)
of a point 
 
 
(𝑥,𝑦,𝑧)
after rotating it around the center, you can use Rodrigues' Rotation Formula. For a rotation by angle 
 
 
𝛼
around a unit axis 
 
 
𝑘⃗
:

 
 
𝑣⃗′=𝑣⃗cos𝛼+(𝑘⃗×𝑣⃗)sin𝛼+𝑘⃗(𝑘⃗⋅𝑣⃗)(1−cos𝛼)
Alternatively, if you are rotating around the principal axes (X, Y, or Z), use standard rotation matrices: 
•	Around X (Pitch): 
 
 
𝑦′=𝑦cos𝜃−𝑧sin𝜃
; 
 
 
𝑧′=𝑦sin𝜃+𝑧cos𝜃
•	Around Y (Yaw): 
 
 
𝑥′=𝑥cos𝜃+𝑧sin𝜃
; 
 
 
𝑧′=−𝑥sin𝜃+𝑧cos𝜃
•	Around Z (Roll): 
 
 
𝑥′=𝑥cos𝜃−𝑦sin𝜃
; 
 
 
𝑦′=𝑥sin𝜃+𝑦cos𝜃
 
3. Advanced Methods 
•	Quaternions: Preferred in 2026 for smooth animations to avoid "gimbal lock" (where two axes align and you lose a degree of freedom).
•	Arcball Rotation: Often used for user interfaces, where mouse drag coordinates are mapped to a virtual sphere to determine the rotation axis and angle.
•	Physical Simulation: For complex movements involving friction, gravity, and bouncing, developers often use 6-DOF (six degrees of freedom) solvers or rigid body physics engines rather than manual math. 
