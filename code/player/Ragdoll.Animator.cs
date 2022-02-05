
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
		private int[] boneIndices { get; set; }

		private void SetBoneIndices()
		{
			List<int> indices = new List<int>();

			for ( int i = 0; i < BoneCount; i++ )
				if ( GetBonePhysicsBody( i ).IsValid() )
					indices.Add( i );

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
			ent.Position = Vector3.Up * 64f + Vector3.Right * 64f;
			ent.Transmit = TransmitType.Owner;

			Animator = ent;
		}

		private void Animate( Client cl )
		{
			Animator.SetAnimBool( "b_sit", false );
			Animator.SetAnimFloat( "move_x", 150f );

			int holdType = 0;// (int)(Time.Now * 0.5f) % 6;


			Animator.SetAnimInt( "holdtype", holdType );
			Animator.SetAnimFloat( "aim_body_weight", 0.5f );

			/*
			float t = MathF.Sin( Time.Now * 4f ) * 0.5f;
			float lookSine = MathF.Sin( MathF.PI * t );
			float lookCosine = MathF.Cos( MathF.PI * t );

			Vector3 aimPos = new Vector3( lookCosine, lookSine, 0f ) * 64f;
	
			Animator.SetAnimVector( "aim_eyes", aimPos );
			Animator.SetAnimVector( "aim_head", aimPos );
			Animator.SetAnimVector( "aim_body", aimPos );
			*/
		}

		private void AlignBodyPart( BodyPart bodyPart )
		{
			// cant align pelvis
			if ( bodyPart == BodyPart.Pelvis )
				return;

			int arrayIndex = (int)bodyPart;
			int boneIndex = boneIndices[arrayIndex];

			Transform transform = Animator.GetBoneTransform( boneIndex, false );
			Rotation rotation = transform.Rotation;

			SphericalJoint joint = GetJoint( bodyPart );

			float distance = MathF.Abs( rotation.Distance( joint.JointFrame2 ) );

			float frequency = distance * 0.75f;
			frequency = frequency > 20f ? 20f : frequency;

			float damping = 150f - distance * 3f;
			damping = damping < 20f ? 20f : damping;

			var pelvis = GetBody( BodyPart.Pelvis );
			Rotation pelvisRot = pelvis.Rotation * Rotation.From( 0, 90, 90 );
			Rotation targetRotation = pelvisRot * rotation;

			joint.MotorTargetRotation = joint.JointFrame1.Inverse * targetRotation;
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
