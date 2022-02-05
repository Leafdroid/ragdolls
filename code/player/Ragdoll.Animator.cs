
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
		public new AnimEntity Animator { get; set; }

		private Rotation[] initialRotations { get; set; }
		private int[] boneIndices { get; set; }

		private void SetInitialRotations()
		{
			// get t-pose rotations
			List<Rotation> rotations = new List<Rotation>();
			List<int> indices = new List<int>();
			AnimEntity initial = new AnimEntity( Model.Name );
			for ( int i = 0; i < BoneCount; i++ )
			{
				var body = GetBonePhysicsBody( i );
				if ( body.IsValid() )
				{
					indices.Add( i );
					rotations.Add( initial.GetBoneTransform( i, false ).Rotation );
				}
			}
			initial.Delete();
			initialRotations = rotations.ToArray();
			boneIndices = indices.ToArray();
		}

		private void CreateAnimator()
		{
			if ( Animator.IsValid() )
				Animator.Delete();

			AnimEntity ent = new AnimEntity( Model.Name );
			ent.Owner = this;
			ent.EnableAllCollisions = false;
			ent.EnableTraceAndQueries = false;
			ent.Position = Vector3.Up * 32f + Vector3.Right * 64f;
			ent.Transmit = TransmitType.Owner;

			Animator = ent;
		}

		private void Animate( Client cl )
		{
			Animator.SetAnimBool( "b_sit", false );
			Animator.SetAnimFloat( "move_x", 100f );

			int holdType = 0;// (int)(Time.Now * 0.5f) % 6;

			Animator.SetAnimInt( "holdtype", holdType );
			Animator.SetAnimFloat( "aim_body_weight", 0.5f );

			float t = MathF.Sin( Time.Now * 4f ) * 0.5f;
			float lookSine = MathF.Sin( MathF.PI * t );
			float lookCosine = MathF.Cos( MathF.PI * t );

			Vector3 aimPos = new Vector3( lookCosine, lookSine, 0f ) * 64f;
			//DebugOverlay.Sphere( Animator.EyePos + aimPos, 4f, Color.White );

			Animator.SetAnimVector( "aim_eyes", aimPos );
			Animator.SetAnimVector( "aim_head", aimPos );
			Animator.SetAnimVector( "aim_body", aimPos );
		}

		private void AlignBodyPart( BodyPart bodyPart )
		{
			// cant align pelvis
			if ( bodyPart == BodyPart.Pelvis )
				return;

			int index = (int)bodyPart;

			Transform transform = Animator.GetBoneTransform( boneIndices[index], false );
			Rotation rotation = transform.Rotation;
			Rotation initialRotation = initialRotations[index];
			SphericalJoint joint = GetJoint( bodyPart );

			// this is what the target rotation would've been if the joints didn't have different rotations than the bones
			Rotation localDelta = initialRotation.Inverse * rotation;
			Transform animTransform = new Transform( Vector3.Zero, initialRotation );
			Rotation worldDelta = animTransform.RotationToWorld( localDelta );

			Transform jointTransform = new Transform( Vector3.Zero, joint.JointFrame1 );
			Rotation targetRotation = jointTransform.RotationToLocal( worldDelta );

			/*

			Transform worldTrans = Animator.GetBoneTransform( index );
			DebugOverlay.Sphere( worldTrans.Position, 1f, Color.White, false );
			string baseText = $"{(int)deltaRotation.Pitch()}, {(int)deltaRotation.Yaw()}, {(int)deltaRotation.Roll()}";
			DebugOverlay.Text( worldTrans.Position, baseText );

			DebugOverlay.Sphere( joint.Anchor1, 1f, Color.White, false );
			string targetText = $"{(int)targetRotation.Pitch()}, {(int)targetRotation.Yaw()}, {(int)targetRotation.Roll()}";
			DebugOverlay.Text( joint.Anchor1, targetText );
			*/

			float distance = MathF.Abs( targetRotation.Distance( Rotation.Identity ) );

			float frequency = distance * 0.75f;
			frequency = frequency > 16f ? 16f : frequency;


			joint.MotorTargetRotation = targetRotation;
			joint.MotorFrequency = frequency;
			joint.MotorDampingRatio = 16f;
			joint.MotorMaxTorque = 100000f;
			joint.MotorMode = PhysicsJointMotorMode.Position;
		}

		private void Align()
		{


			int index = 0;
			for ( int i = 0; i < BoneCount; i++ )
			{
				var body = GetBonePhysicsBody( i );
				if ( body.IsValid() ) // bone has physics body, physically align
				{
					AlignBodyPart( (BodyPart)index );
					index++;
				}
				else // bone has no physics body, slap into position
				{
					// this doesn't seem to work :(
					SetBoneTransform( i, Animator.GetBoneTransform( i ) );
				}
			}
		}
	}
}
