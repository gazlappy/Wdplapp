Modeling the physics of a pool game involves equations for motion, ball-to-ball collisions, and interactions with cushions.
 
1. Fundamental Motion Equations 
These equations govern the movement of a ball across the table as it experiences friction. 
•	Linear Velocity (
 
 
𝑣
): 
 
 
𝑣=𝑣2𝑥+𝑣2𝑦
•	Deceleration (Rolling Resistance): 
 
 
𝑣𝑓=𝑣𝑖−(𝑎×𝑡)
, where 
 
 
𝑎
is the deceleration caused by rolling resistance.
•	Angular Velocity (
 
 
𝜔
): 
 
 
𝜔=𝑣𝑟
for pure rolling, where 
 
 
𝑟
is the ball radius. 
2. Ball-to-Ball Collisions (Elastic) 
In a typical pool game, collisions are nearly elastic, conserving both momentum (
 
 
𝑝
) and kinetic energy (
 
 
𝐾𝐸
). 
•	Conservation of Momentum: 
 
 
𝑚1𝑣1+𝑚2𝑣2=𝑚1𝑣′1+𝑚2𝑣′2
.
•	90-Degree Rule: For a moving cue ball hitting a stationary object ball (if both have the same mass), the two balls will travel at a 90-degree angle to each other after impact.
•	Velocity Vector Resolve:
o	Tangential Component: Stays with the cue ball.
o	Normal Component: Transferred to the object ball along the line of centers. 
3. Cushion (Rail) Interactions 
When a ball hits the cushion, it loses a small amount of energy, governed by the Coefficient of Restitution (
 
 
𝑒
). 
•	Reflection Equation: 
 
 
𝑉𝑓𝑖𝑛𝑎𝑙=𝑒×𝑉𝑖𝑛𝑖𝑡𝑖𝑎𝑙
.
•	Angle of Reflection: Ideally, 
 
 
𝜃𝑖𝑛=𝜃𝑜𝑢𝑡
, but sidespin (English) will alter this. 
4. Advanced Spin & Trajectory 
•	The "Sweet Spot": To avoid developing friction immediately upon impact (pure rolling), the cue should strike the ball at height 
 
 
ℎ=75𝑟
from the table.
•	30-Degree Rule: For a rolling cue ball (not a "stun" shot), the cue ball will deflect at approximately 30 degrees from its original path after hitting an object ball at most typical cut angles.
•	Parabolic Path: A ball with top or bottom spin (follow or draw) will follow a parabolic curve after impact due to the ongoing friction with the cloth. 
For detailed technical derivations, resources like Dr. Dave's Pool Physics or Real World Physics Problems provide full mathematical models
5. Advanced Collision: "Throw" Equations 
"Throw" is the change in an object ball's path due to friction during impact with the cue ball. 
•	Friction-Induced Throw (FIT): The object ball is "thrown" slightly off the line-of-centers due to the cue ball's sliding friction. The maximum throw typically occurs at a 1/2-ball hit (30-degree cut angle).
•	Spin-Induced Throw (SIT): Sidespin on the cue ball can push the object ball left or right. The displacement can be calculated based on the relative sliding velocity between the two ball surfaces at the point of contact. 
6. Cue Dynamics: "Squirt" and "Swerve" 
When you strike the cue ball off-center (English), it does not travel exactly along the cue's aiming line. 
•	Squirt (Cue Ball Deflection): The ball "squirts" away from the center of the cue. The squirt angle (
 
 
𝛼
) depends on the cue tip's effective mass (
 
 
𝑚𝑡𝑖𝑝
) and the ball's mass (
 
 
𝑀
):
o	 
 
𝛼≈arctan𝑚𝑡𝑖𝑝𝑀×𝑒𝑅
where 
 
 
𝑒
is the off-center offset distance.
•	Swerve: A ball with sidespin and an elevated cue will follow a curved path (a mini-massé) due to friction with the cloth. The path is roughly parabolic, governed by the interaction between the downward force component and the horizontal spin. 
7. Cushion Impact Dynamics (Han-Mathavan Model) 
Cushion collisions are more complex than simple reflection because the cushion height is typically above the ball's center (
 
 
ℎ=75𝑅
). 
•	Vertical Impulse (
 
 
𝑃𝑐
): The cushion exerts a downward force that increases the friction with the table during the bounce.
•	Rebound Angle Modification:
o	Rolling Rebound: 
 
 
𝑉𝑓𝑖𝑛𝑎𝑙=𝑒×𝑉𝑖𝑛𝑖𝑡𝑖𝑎𝑙sin(𝛼)
, where 
 
 
𝑒
is the coefficient of restitution (typically 
 
 
≈0.98
for professional cushions).
o	Spin-Rebound: Sidespin (English) adds a tangential impulse, drastically changing the exit angle 
 
 
𝜃𝑜𝑢𝑡
. 
8. "The Sweet Spot" (No-Friction Height) 
To strike a ball so it begins moving without immediate sliding friction, the cue must hit it at exactly: 
•	Height (
 
 
ℎ
): 
 
 
ℎ=𝑅+𝐼𝑀𝑅=1.4𝑅
(or 
 
 
75
of the radius).
o	At this height, the linear acceleration and angular acceleration match perfectly for pure rolling (
 
 
𝑣=𝜔𝑟
) from the moment of impact. 
9. Summary Reference Table for 2026 
Phenomenon 	Primary Variable	Key Effect
Squirt	Cue Tip Mass	Ball deflects away from off-center hit.
Throw	Friction (
 
 
𝜇
)	Object ball pushed off the line-of-centers.
Swerve	Cue Elevation	Ball follows a curved path on the cloth.
Rail Grab	Speed & Spin	Harder shots or more spin create non-linear rebounds.
For simulation development, the Alciatore Pool Physics Articles and the Kiefl Billiards Theory remain the gold standards for full system integration. 
These technical analyses detail the physics of billiard ball dynamics, including cushion impacts, squirt, and throw, for advanced simulation modeling.

