using System;
using Sandbox;

namespace Ragdolls
{
	public class RagdollCamera : Camera
	{
		private int zoom = 20;

		private const int minZoom = 10;
		private const int maxZoom = 30;

		private static Trace cameraTrace = Trace.Ray( 0, 0 )
			.Radius( 10f )
			.HitLayer( CollisionLayer.Debris, false )
			.HitLayer( CollisionLayer.Player, false );

		public override void Update()
		{
			if ( Local.Client.Pawn is not Ragdoll player )
				return;

			zoom -= Input.MouseWheel;
			zoom = zoom.Clamp( minZoom, maxZoom );


			//Transform bone = player.GetBoneTransform( 0 );
			Vector3 position = player.Position + Vector3.Up * 16f;

			Rotation = Input.Rotation;

			Vector3 camPos = position + (Rotation.Backward * 10 * zoom + Rotation.Up * 0.5f * zoom);

			Position = camPos;// cameraTrace.FromTo( position, camPos ).Run().EndPos;

			FieldOfView = 80;

			Viewer = null;
		}
	}
}
