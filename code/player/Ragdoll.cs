
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
		public Clothing.Container Clothing = new();

		public Entity LeftGrabEntity { get; private set; }
		public PhysicsJoint LeftGrabJoint { get; private set; }

		public Entity RightGrabEntity { get; private set; }
		public PhysicsJoint RightGrabJoint { get; private set; }

		public bool Balanced { get; private set; }
		public PhysicsJoint BalanceJoint { get; private set; }

		public float Mass => PhysicsGroup.Mass;

		private TimeSince timeSinceGrab = 0f;
		public static readonly SoundEvent GrabSound = new( "sounds/ragdoll/grab.vsnd" )
		{
			DistanceMax = 512f,
			Pitch = 2f,
			Volume = 0.15f

		};

		private TimeSince timeSinceRelease = 0f;
		public static readonly SoundEvent ReleaseSound = new( "sounds/ragdoll/release.vsnd" )
		{
			DistanceMax = 512f,
			Pitch = 2f,
			Volume = 0.15f
		};

		public override void Respawn()
		{
			Host.AssertServer();

			SetSpawnpoint();

			SetModel( "models/citizen/citizen.vmdl" );

			Camera = new RagdollCamera();

			SetupPhysicsFromModel( PhysicsMotionType.Dynamic );
			EnableAllCollisions = true;
			EnableShadowCasting = true;
			Transmit = TransmitType.Always;

			CreateAnimator();
			SetBoneIndices();

			if ( IsServer )
				Clothing.LoadFromClient( Client );
			Clothing.DressEntity( this );

			foreach ( PhysicsBody body in PhysicsGroup.Bodies )
				body.Mass *= 150f;

			ResetJoints();

			ClearCollisionLayers();
			AddCollisionLayer( CollisionLayer.Player );

			LifeState = LifeState.Alive;

			Velocity = Vector3.Zero;

			ResetInterpolation();
		}

		private void SetSpawnpoint()
		{
			Position = Vector3.Up * 46f + Vector3.Right * 128f * Client.Id;

			var spawnpoint = All.OfType<SpawnPoint>().OrderBy( x => Guid.NewGuid() ).FirstOrDefault();
			if ( spawnpoint != null )
			{
				Position += spawnpoint.Position;
				Rotation = spawnpoint.Rotation;
			}
		}

		private void Reach( bool leftHand )
		{
			var upperArm = leftHand ? BodyPart.LeftUpperArm : BodyPart.RightUpperArm;
			var forearm = leftHand ? BodyPart.LeftForearm : BodyPart.RightForearm;
			var hand = leftHand ? BodyPart.LeftHand : BodyPart.RightHand;

			Vector3 reachDir = Input.Rotation.Forward;

			float reachForce = 400f * Mass;

			PushLimb( upperArm, reachDir * reachForce );
			PushLimb( forearm, reachDir * reachForce );
			PushLimb( hand, reachDir * reachForce );

			var handBody = GetBody( hand );
			Vector3 grabDirection = leftHand ? handBody.Rotation.Up : handBody.Rotation.Down;
			Vector3 grabPosition = handBody.MassCenter;

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
			using ( Prediction.Off() )
			{
				if ( timeSinceGrab > 0.1f )
				{
					PlaySound( GrabSound.Name );
					timeSinceGrab = 0f;
				}
			}

			// special interactions with entities
			switch ( grabBody.Entity )
			{
				case ButtonEntity button:
					button.OnUse( this );
					break;
			}

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
			{
				using ( Prediction.Off() )
				{
					if ( timeSinceRelease > 0.1f )
					{
						PlaySound( ReleaseSound.Name );
						timeSinceRelease = 0f;
					}
				}
				joint.Remove();
			}


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
		private void Balance()
		{
			var pelvis = GetBody( BodyPart.Pelvis );

			Vector3 massDelta = (PhysicsGroup.MassCenter - pelvis.MassCenter).WithZ( 0f );
			float distance = massDelta.Length;

			PushLimb( BodyPart.RightHand, -massDelta * Mass * 100f );
			PushLimb( BodyPart.UpperSpine, -massDelta * Mass * 300f );
			PushLimb( BodyPart.Head, Vector3.Up * Mass * 500f );
			PushLimb( BodyPart.LeftHand, -massDelta * Mass * 100f );

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

		public override void Simulate( Client cl )
		{
			Animate( cl );
			Align();


			Vector3 inputDir = (new Vector3( Input.Forward, Input.Left ) * Input.Rotation).Normal;
			var pelvis = GetBody( BodyPart.Pelvis );
			pelvis.ApplyForce( inputDir * Mass * 150 );

			//pelvis.ApplyForceAt( pelvis.MassCenter + pelvis.Rotation.Left * 16f, Input.Rotation.Forward * Mass * 100f );
			//pelvis.ApplyForceAt( pelvis.MassCenter + pelvis.Rotation.Right * 16f, -Input.Rotation.Forward * Mass * 100f );

			//Stance();
			//Balance();

			/*
			if ( Input.Down( InputButton.Jump ) )
				GetBody( BodyPart.Head ).ApplyForce( Vector3.Up * Mass * 750f );
			
			*/

			if ( Input.Down( InputButton.Attack1 ) )
				Reach( true );
			else
				Release( true );

			if ( Input.Down( InputButton.Attack2 ) )
				Reach( false );
			else
				Release( false );


		}

		protected override void OnDestroy()
		{
			base.OnDestroy();

			if ( IsClient )
				return;

			if ( Animator.IsValid() )
				Animator.Delete();

			foreach ( SphericalJoint joint in motorJoints )
				joint.Remove();

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

		public void PushLimb( BodyPart bodyPart, Vector3 force )
		{
			int index = (int)bodyPart - 1;
			if ( bodyPart == BodyPart.Pelvis || index > 14 )
				return;

			PhysicsBody body = GetBody( bodyPart );

			var joint = PhysicsGroup.GetJoint( index );
			PhysicsBody parentBody = joint.Body1;

			if ( body == null || parentBody == null )
				return;

			body.ApplyForce( force );
			parentBody.ApplyForce( -force );
			//parentBody.ApplyForceAt( joint.Anchor1, -direction * force );

			/*
			DebugOverlay.Sphere( body.MassCenter, 1f, Color.White, false );
			DebugOverlay.Line( body.MassCenter, joint.Anchor1, Color.White, 0f, false );
			DebugOverlay.Line( joint.Anchor1, parentBody.MassCenter, Color.Gray, 0f, false );
			DebugOverlay.Sphere( joint.Anchor1, 1f, Color.Gray, false );
			*/
		}

		[ServerCmd]
		public static void BreakFingers()
		{
			if ( ConsoleSystem.Caller == null || ConsoleSystem.Caller.Pawn is not Player player )
				return;

			AnimEntity victim = new AnimEntity( "models/citizen/citizen.vmdl" );
			victim.SetupPhysicsFromModel( PhysicsMotionType.Dynamic );
			victim.Position = player.Position;

			for ( int i = 0; i < victim.BoneCount; i++ )
			{
				if ( !victim.GetBoneName( i ).Contains( "finger" ) )
					continue;

				Transform oldTransform = victim.GetBoneTransform( i );
				Rotation twisty = Rotation.FromPitch( Rand.Float( -60f, 60f ) );
				Transform transform = new Transform( oldTransform.Position, oldTransform.Rotation * twisty );
				victim.SetBone( i, transform );
			}
		}
	}
}
