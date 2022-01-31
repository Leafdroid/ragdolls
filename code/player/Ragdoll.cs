
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
		public Entity LeftGrabEntity { get; private set; }
		public PhysicsJoint LeftGrabJoint { get; private set; }

		public Entity RightGrabEntity { get; private set; }
		public PhysicsJoint RightGrabJoint { get; private set; }

		public bool Balanced { get; private set; }
		public PhysicsJoint BalanceJoint { get; private set; }

		public float Mass => PhysicsGroup.Mass;

		private SphericalJoint[] frictionJoints;


		private void SetFriction( BodyPart bodyPart, float friction )
		{
			int index = (int)bodyPart - 1;
			if ( index > 14 )
			{
				Log.Error( "Can't set friction to invalid bodyparts!" );
				return;
			}

			if ( bodyPart == BodyPart.Pelvis )
			{
				Log.Error( "Can't set friction to pelvis!" );
				return;
			}

			var existingJoint = frictionJoints[index];
			if ( existingJoint.IsValid )
			{
				existingJoint.MotorFriction = friction;
			}
			else if ( friction != 0f )
			{
				var realJoint = PhysicsGroup.Joints.ElementAt( index );

				frictionJoints[index] = PhysicsJoint.Spherical
					.From( realJoint.Body1, realJoint.LocalAnchor1, realJoint.LocalJointFrame1 )
					.To( realJoint.Body2, realJoint.LocalAnchor2, realJoint.LocalJointFrame2 )
					.WithFriction( friction )
					.Create();
			}
		}

		private void ResetFriction()
		{
			if ( frictionJoints != null )
				foreach ( SphericalJoint joint in frictionJoints )
					if ( joint.IsValid )
						joint.Remove();

			frictionJoints = new SphericalJoint[PhysicsGroup.Joints.Count()];
		}

		public override void Respawn()
		{
			Host.AssertServer();

			SetModel( "models/citizen/citizen.vmdl" );

			Camera = new RagdollCamera();

			SetupPhysicsFromModel( PhysicsMotionType.Dynamic );
			EnableAllCollisions = true;
			EnableShadowCasting = true;
			Transmit = TransmitType.Always;

			foreach ( PhysicsBody body in PhysicsGroup.Bodies )
				body.Mass *= 150f;

			ResetFriction();

			for ( int i = 1; i < 16; i++ )
				SetFriction( (BodyPart)i, 100f );

			ClearCollisionLayers();
			AddCollisionLayer( CollisionLayer.Player );

			LifeState = LifeState.Alive;

			Velocity = Vector3.Zero;

			SetSpawnpoint();
			ResetInterpolation();
		}

		private void SetSpawnpoint()
		{
			Position = Vector3.Up * 32;

			var spawnpoint = All.OfType<SpawnPoint>().OrderBy( x => Guid.NewGuid() ).FirstOrDefault();
			if ( spawnpoint != null )
				Position += spawnpoint.Position;
		}

		private void Stance()
		{
			Vector3 force = Vector3.Up * 550f * PhysicsGroup.Mass;

			GetBody( BodyPart.Head ).ApplyForce( force * 0.2f );
			GetBody( BodyPart.UpperSpine ).ApplyForce( force * 0.4f );
			GetBody( BodyPart.LowerSpine ).ApplyForce( force * 0.4f );

			GetBody( BodyPart.LeftFoot ).ApplyForce( -force * 0.35f );
			GetBody( BodyPart.LeftShin ).ApplyForce( -force * 0.15f );

			GetBody( BodyPart.RightFoot ).ApplyForce( -force * 0.35f );
			GetBody( BodyPart.RightShin ).ApplyForce( -force * 0.15f );

		}

		private void FaceForward()
		{
			GetBody( BodyPart.Head ).RotateTowards( Input.Rotation, 50f * Mass );
			GetBody( BodyPart.UpperSpine ).RotateTowards( Input.Rotation, 50f * Mass );

			Vector3 forwardDirection = Input.Rotation.Forward.WithZ( 0 ).Normal;

			GetBody( BodyPart.RightFoot ).RotateTowards( forwardDirection, 0.25f * Mass );
			GetBody( BodyPart.LeftFoot ).RotateTowards( forwardDirection, 0.25f * Mass );
		}

		private void Reach( bool leftHand )
		{
			var upperArm = GetBody( leftHand ? BodyPart.LeftUpperArm : BodyPart.RightUpperArm );
			var forearm = GetBody( leftHand ? BodyPart.LeftForearm : BodyPart.RightForearm );
			var hand = GetBody( leftHand ? BodyPart.LeftHand : BodyPart.RightHand );

			Vector3 reachDir = Input.Rotation.Forward;

			float reachForce = 2000f * Mass;

			GetBody( BodyPart.UpperSpine ).ApplyForce( -reachDir * reachForce );
			upperArm.ApplyForce( reachDir * reachForce * 0.5f );
			forearm.ApplyForce( reachDir * reachForce * 0.45f );
			hand.ApplyForce( reachDir * reachForce * 0.05f );

			hand.RotateTowards( reachDir, Mass );

			Vector3 grabDirection = leftHand ? hand.Rotation.Up : hand.Rotation.Down;
			Vector3 grabPosition = hand.MassCenter;// + grabDirection * 2f;


			var grabbedBody = leftHand ? LeftGrabEntity : RightGrabEntity;
			bool alreadyGrabbed = grabbedBody.IsValid();

			if ( alreadyGrabbed )
				return;

			TraceResult grab = Trace.Sphere( 5f, grabPosition, grabPosition + grabDirection )
				.Ignore( this )
				.Run();

			if ( grab.Hit && grab.Body.IsValid() )
				Grab( leftHand, grab.Body );

		}

		private void Grab( bool leftHand, PhysicsBody grabBody )
		{
			var hand = GetBody( leftHand ? BodyPart.LeftHand : BodyPart.RightHand );

			Vector3 localPos = grabBody.Transform.PointToLocal( hand.Position );
			Rotation localRot = grabBody.Transform.RotationToLocal( hand.Rotation );
			PhysicsJoint joint = PhysicsJoint.Weld.From( hand ).To( grabBody, localPos, localRot ).Create();

			if ( leftHand )
			{
				LeftGrabEntity = grabBody.Entity;
				LeftGrabJoint = joint;
			}
			else
			{
				RightGrabEntity = grabBody.Entity;
				RightGrabJoint = joint;
			}
		}

		private void Release( bool leftHand )
		{
			var joint = (leftHand ? LeftGrabJoint : RightGrabJoint);
			if ( joint != null )
				joint.Remove();

			if ( leftHand )
			{
				if ( LeftGrabEntity != null )
					LeftGrabEntity = null;
				if ( LeftGrabJoint != null )
					LeftGrabJoint = null;
			}
			else
			{
				if ( RightGrabEntity != null )
					RightGrabEntity = null;
				if ( RightGrabJoint != null )
					RightGrabJoint = null;
			}
		}

		float walkTime = 0f;
		float walkCycle = 0f;
		Vector3 walkDir;
		private void Walk()
		{
			Vector3 inputDir = (new Vector3( Input.Forward, Input.Left ) * Input.Rotation).WithZ( 0 ).Normal;
			walkDir = walkDir.LerpTo( inputDir, Time.Delta * 8f );

			walkTime += (walkDir.Length > 0f) ? Time.Delta * 6f : -Time.Delta * 4f;
			walkTime = walkTime < 0f ? 0f : walkTime > 1f ? 1f : walkTime;

			walkCycle += Time.Delta * 2.5f;
			walkCycle %= 2f;

			float cosine = MathF.Cos( walkCycle * MathF.PI );
			float sine = MathF.Sin( (walkCycle + 1.5f) * MathF.PI );

			Vector3 leftMove = walkDir * cosine * walkTime * 16f;
			Vector3 rightMove = walkDir * sine * walkTime * 16f;

			var pelvis = GetBody( BodyPart.Pelvis );
			Vector3 leftPos = pelvis.MassCenter + pelvis.Rotation.Up * 6f - pelvis.Rotation.Forward * 16f;
			Vector3 rightPos = pelvis.MassCenter + pelvis.Rotation.Down * 6f - pelvis.Rotation.Forward * 16f;


			/*
			DebugOverlay.Line( leftPos, leftPos + leftMove, Color.Red );
			DebugOverlay.Sphere( leftPos + leftMove, 3f, Color.Red );

			DebugOverlay.Line( rightPos, rightPos + rightMove, Color.Blue );
			DebugOverlay.Sphere( rightPos + rightMove, 3f, Color.Blue );
			*/

			GetBody( BodyPart.RightThigh ).PushTo( rightPos + rightMove, Mass * 400f * walkTime );
			GetBody( BodyPart.LeftThigh ).PushTo( leftPos + leftMove, Mass * 400f * walkTime );
		}

		private void Balance()
		{
			var pelvis = GetBody( BodyPart.Pelvis );

			Vector3 massDelta = (PhysicsGroup.MassCenter - pelvis.MassCenter).WithZ( 0f );
			float distance = massDelta.Length;

			DebugOverlay.Line( pelvis.MassCenter, pelvis.MassCenter + massDelta, Color.Red, 0f, false );
			DebugOverlay.Text( pelvis.MassCenter + massDelta, $"{distance}" );

			float dot = pelvis.Rotation.Forward.Dot( Vector3.Up );
			DebugOverlay.Text( pelvis.MassCenter + pelvis.Rotation.Forward * 16f, $"{dot}" );
			DebugOverlay.Line( pelvis.MassCenter, pelvis.MassCenter + pelvis.Rotation.Forward * 16f, Color.Green, 0f, false );

			Balanced = dot > 0.95f && distance < 4f;
			DebugOverlay.Sphere( pelvis.MassCenter, 1f, Balanced ? Color.White : Color.Black, false );

			if ( Balanced )
			{
				if ( BalanceJoint.IsValid() )
					return;

				BalanceJoint = PhysicsJoint.Generic
					.From( pelvis )
					.To( PhysicsWorld.WorldBody, pelvis.Position, pelvis.Rotation )
					.WithAngularMotionX( JointMotion.Free )
					.WithAngularMotionY( JointMotion.Locked )
					.WithAngularMotionZ( JointMotion.Locked )
					.Create();
			}
			else if ( BalanceJoint.IsValid() )
				BalanceJoint.Remove();
		}

		private bool frozen = false;
		private void RigorMortis()
		{
			float friction = (frozen = !frozen) ? 100000f : 100f;
			for ( int i = 1; i < 16; i++ )
				SetFriction( (BodyPart)i, friction );
		}

		float holdTime = 0f;
		public override void Simulate( Client cl )
		{
			//Stance();
			Balance();

			FaceForward();

			if ( Input.Down( InputButton.Jump ) )
				GetBody( BodyPart.Head ).ApplyForce( Vector3.Up * Mass * 750f );

			if ( Input.Down( InputButton.Attack1 ) )
				Reach( true );
			else
				Release( true );

			if ( Input.Down( InputButton.Attack2 ) )
				Reach( false );
			else
				Release( false );


			Vector3 inputDir = (new Vector3( Input.Forward, Input.Left ) * Input.Rotation).WithZ( 0 ).Normal;
			GetBody( BodyPart.Pelvis ).ApplyForce( inputDir * Mass * 150 );

			if ( Input.Pressed( InputButton.Run ) )
				RigorMortis();

			/*
			if ( Input.Down( InputButton.Run ) )
			{
				GetBody( BodyPart.UpperSpine ).ApplyForce( Input.Rotation.Forward * Mass * 1250f );
				GetBody( BodyPart.Pelvis ).ApplyForce( Input.Rotation.Backward * Mass * 1250f );
			}

			holdTime += Input.Down( InputButton.Forward ) ? Time.Delta : -Time.Delta;
			holdTime = holdTime < 0f ? 0f : holdTime > 1f ? 1f : holdTime;


			
			*/

			//Stance();
			//

			//Walk();

		}

		protected override void OnDestroy()
		{
			base.OnDestroy();

			if ( IsClient )
				return;

			ResetFriction();

			if ( BalanceJoint.IsValid() )
				BalanceJoint.Remove();

			if ( RightGrabJoint.IsValid() )
				RightGrabJoint.Remove();

			if ( LeftGrabJoint.IsValid() )
				LeftGrabJoint.Remove();
		}

		public enum BodyPart
		{
			Pelvis,
			LowerSpine,
			UpperSpine,
			Head,
			LeftUpperArm,
			LeftForearm,
			LeftHand,
			RightUpperArm,
			RightForearm,
			RightHand,
			LeftThigh,
			LeftShin,
			LeftFoot,
			RightThigh,
			RightShin,
			RightFoot,
		}

		private PhysicsBody GetBody( BodyPart bodyPart ) => PhysicsGroup.GetBody( (int)bodyPart );

		public async void RespawnAsync( float time )
		{
			await GameTask.DelaySeconds( time );
			Respawn();
		}

		public void Kill( bool isPredicted = true )
		{
			if ( Host.IsServer )
				KillRpc( isPredicted );
			if ( Host.IsServer || (Local.Client == Client && isPredicted) )
				DoKill();
		}

		private void DoKill()
		{
			LifeState = LifeState.Dead;
			Client.AddInt( "deaths" );

			if ( IsServer )
				RespawnAsync( 1f );
		}

		[ClientRpc]
		public void KillRpc( bool isPredicted = true )
		{
			if ( !isPredicted || Client != Local.Client )
				DoKill();
		}


		[ServerCmd( "kill" )]
		public static void KillCommand()
		{
			if ( ConsoleSystem.Caller != null && ConsoleSystem.Caller.Pawn is Ragdoll player )
				player.Kill( false );
		}

		[ServerCmd( "SpawnEntity" )]
		public static void SpawnEntity( string modelName )
		{
			if ( ConsoleSystem.Caller == null || ConsoleSystem.Caller.Pawn is not Ragdoll player )
				return;

			Model model = Model.Load( modelName );
			if ( model == null )
			{
				Log.Error( "Model doesn't exist!" );
				return;
			}

			Vector3 spawnPos = player.Position + Vector3.Up * 64;

			ModelEntity newEntity = new ModelEntity( modelName );
			newEntity.SetupPhysicsFromModel( PhysicsMotionType.Dynamic );
			newEntity.Position = spawnPos;
		}
	}

	public static class PhysicsBodyExtension
	{
		public static void RotateTowards( this PhysicsBody body, Rotation rotation, float force )
		{
			body.ApplyForceAt( body.MassCenter + body.Rotation.Left * 64, rotation.Forward * force );
			body.ApplyForceAt( body.MassCenter + body.Rotation.Right * 64, rotation.Backward * force );
		}

		public static void RotateTowards( this PhysicsBody body, Vector3 direction, float force )
		{
			body.ApplyForceAt( body.MassCenter + body.Rotation.Left * 64, direction * force );
			body.ApplyForceAt( body.MassCenter + body.Rotation.Right * 64, -direction * force );
		}

		public static void PushTo( this PhysicsBody body, Vector3 position, float force, float fallOff = 16f )
		{
			Vector3 delta = position - body.MassCenter;
			float distance = delta.Length;
			Vector3 direction = delta / distance;

			float pushForce = force * distance / fallOff;
			pushForce = pushForce < 0f ? 0f : pushForce > force ? force : pushForce;

			body.ApplyForce( direction * pushForce );
		}
	}
}
