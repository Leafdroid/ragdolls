
using Sandbox;
using Sandbox.UI.Construct;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Sandbox.Joints;

namespace Ragdolls
{
	public partial class Ragdoll : Player
	{
		private SphericalJoint[] motorJoints;

		private void SetFriction( BodyPart bodyPart, float friction )
		{
			var motorJoint = GetJoint( bodyPart );
			if ( !motorJoint.IsValid )
			{
				motorJoint.Remove();
				return;
			}

			motorJoint.MotorFriction = friction;
		}

		private SphericalJoint GetJoint( BodyPart bodyPart )
		{
			int index = (int)bodyPart - 1;
			if ( index > 14 )
			{
				Log.Error( "Couldn't find motor joint! (Invalid bodypart)" );
				return new SphericalJoint();
			}

			if ( bodyPart == BodyPart.Pelvis )
			{
				Log.Error( "Couldn't find motor joint! (Pelvis doesn't have one)" );
				return new SphericalJoint();
			}

			var existingJoint = motorJoints[index];
			if ( existingJoint.IsValid )
				return existingJoint;

			Log.Error( "Couldn't find motor joint! (Joint at given index was invalid)" );
			return new SphericalJoint();
		}

		private void ResetJoints( float friction = 0f )
		{
			if ( motorJoints != null )
				foreach ( SphericalJoint joint in motorJoints )
					if ( joint.IsValid )
						joint.Remove();

			motorJoints = new SphericalJoint[PhysicsGroup.JointCount];
			for ( int i = 0; i < PhysicsGroup.JointCount; i++ )
			{
				var realJoint = PhysicsGroup.GetJoint( i );

				var jointBuilder = PhysicsJoint.Spherical
					.From( realJoint.Body1, realJoint.LocalAnchor1, realJoint.LocalJointFrame1 )
					.To( realJoint.Body2, realJoint.LocalAnchor2, realJoint.LocalJointFrame2 );

				if ( friction != 0f )
					jointBuilder = jointBuilder.WithFriction( friction );

				motorJoints[i] = jointBuilder.Create();
			}
		}
	}
}
