using System;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Sandbox;
using Sandbox.Joints;
using System.ComponentModel.DataAnnotations;

namespace Ragdolls
{
	[Library( "ent_lever" )]
	[Hammer.EditorModel( "models/ragdolls/lever.vmdl", FixedBounds = true )]
	[Hammer.EntityTool( "Lever", "Ragdolls", "Pullable physics lever" )]
	[Display( Name = "Lever" ), Icon( "place" )]
	public partial class LeverEntity : ModelEntity
	{
		/// <summary>
		/// How hard it is to move.
		/// </summary>
		[Property( "friction", Title = "Friction" )]
		public float Friction { get; set; } = 25f;

		/// <summary>
		/// How dampened the return force is.
		/// </summary>
		[Property( "dampening", Title = "Dampening" )]
		public float Dampening { get; set; } = 1f;

		/// <summary>
		/// How much force is used to return the lever to its initial state.
		/// </summary>
		[Property( "force", Title = "Force" )]
		public float Force { get; set; } = 0.75f;

		/// <summary>
		/// Can be pulled in both directions? Enables use of OnPulledUp and WhilePulledUp.
		/// </summary>
		[Property( "doublesided", Title = "Double Sided" )]
		public bool DoubleSided { get; set; } = false;

		/// <summary>
		/// What to multiply the pull fraction output by.
		/// </summary>
		[Property( "pullmultiplier", Title = "Pull Amount Multiplier" )]
		public float PullAmountMultiplier { get; set; } = 0f;

		/// <summary>
		/// Fired when the lever gets pulled up. (Double Sided has to be enabled to use this)
		/// </summary>
		protected Output OnPulledUp { get; set; }

		/// <summary>
		/// Fires while the lever is pulled up. (Double Sided has to be enabled to use this)
		/// </summary>
		protected Output WhilePulledUp { get; set; }

		/// <summary>
		/// Fired when the lever gets pulled down.
		/// </summary>
		protected Output OnPulledDown { get; set; }
		/// <summary>
		/// Fires while the lever is pulled down.
		/// </summary>
		protected Output WhilePulledDown { get; set; }

		/// <summary>
		/// Fired when the lever gets returned to its neutral state.
		/// </summary>
		protected Output OnNeutral { get; set; }

		/// <summary>
		/// Fires while the lever is neutral.
		/// </summary>
		protected Output WhileNeutral { get; set; }

		/// <summary>
		/// How pulled is it? Ranges from -1 to 1 if double sided and 0 to 1 by default.
		/// </summary>
		protected Output PullAmount { get; set; }

		private enum LeverState
		{
			PulledUp,
			Neutral,
			PulledDown
		}

		private float pullFraction { get; set; } = 0f;
		private LeverState state = LeverState.Neutral;
		private RevoluteJoint leverJoint;

		public override void Spawn()
		{
			base.Spawn();

			SetModel( "models/ragdolls/lever.vmdl" );
			SetupPhysicsFromModel( PhysicsMotionType.Dynamic );

			foreach ( PhysicsBody body in PhysicsGroup.Bodies )
				body.Mass *= 100f;

			var baseBody = PhysicsGroup.GetBody( 0 );
			if ( !baseBody.IsValid() )
			{
				Log.Error( "Lever model has no main physicsbody!" );
				return;
			}

			for ( int i = 1; i < PhysicsGroup.BodyCount; i++ )
				PhysicsGroup.GetBody( i ).GravityEnabled = false;

			if ( Parent == null )
				baseBody.MotionEnabled = false;
			else
			{
				// connect to parent using a joint instead, since parenting is being weird
				var parentBody = Parent.PhysicsGroup.GetBody( 0 );
				if ( parentBody.IsValid() )
				{
					Parent = null;

					Vector3 localPos = parentBody.Transform.PointToLocal( baseBody.Position );
					Rotation localRot = parentBody.Transform.RotationToLocal( baseBody.Rotation );

					PhysicsJoint.Weld
					.From( baseBody )
					.To( parentBody, localPos, localRot )
					.Create();
				}
			}

			leverJoint = PhysicsGroup.GetJoint( 0 );
			if ( leverJoint.IsValid )
			{
				leverJoint.MotorFriction = Friction;
				leverJoint.MotorDampingRatio = Dampening;
				leverJoint.MotorFrequency = Force;
				leverJoint.MotorTargetAngle = DoubleSided ? 0f : leverJoint.LimitRange.y;
				leverJoint.MotorMode = PhysicsJointMotorMode.Position;
			}

			FireOutput( "OnNeutral", null, 0f );
		}

		[Event.Tick]
		public void Tick()
		{
			if ( leverJoint.IsValid )
			{
				float totalRange = MathF.Abs( leverJoint.LimitRange.x ) + MathF.Abs( leverJoint.LimitRange.y );
				float t = 1f - (MathF.Abs( leverJoint.LimitRange.x ) + leverJoint.Angle) / totalRange;

				pullFraction = DoubleSided ? t * 2f - 1f : t;

				int min = DoubleSided ? 0 : 1;

				int newState = DoubleSided ? (int)(t * 3f) : (int)(t * 2f) + 1;
				newState = newState < min ? min : newState > 2 ? 2 : newState;

				FireOutput( (LeverState)newState, null );
			}
		}

		private static SoundEvent switchSound = new SoundEvent( "sounds/ragdoll/lever.vsnd" )
		{
			DistanceMax = 768f
		};

		private void FireOutput( LeverState state, Entity activator )
		{
			LeverState oldState = this.state;
			this.state = state;

			bool repeated = oldState == state;

			/* play sound if state was just switched, sound ugly atm so ill comment it out
			if ( !repeated )
				PlaySound( switchSound.Name );
			*/

			string output = $"{(repeated ? "While" : "On")}{state}";

			FireOutput( output, activator, pullFraction * PullAmountMultiplier );
			FireOutput( "PullAmount", activator, pullFraction * PullAmountMultiplier );
		}
	}
}
