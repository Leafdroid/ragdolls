using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ragdolls
{
	[Library( "trigger_respawn", Description = "Respawns entities" )]
	public partial class TriggerRespawn : BaseTrigger
	{
		public override void Spawn()
		{
			base.Spawn();
			Transmit = TransmitType.Always;
		}

		public override void OnTouchStart( Entity toucher )
		{
			base.OnTouchStart( toucher );

			Respawn( toucher );
		}

		private void Respawn( Entity ent )
		{
			if ( ent is Ragdoll player )
				player.Respawn();
			else
			{
				Log.Error( $"No method for respawning this entity yet! ({ent})" );
			}
		}
	}
}
