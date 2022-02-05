using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ragdolls
{
	[Library( "trigger_reset", Description = "Resets entities" )]
	public partial class TriggerReset : BaseTrigger
	{
		public override void Spawn()
		{
			base.Spawn();
			Transmit = TransmitType.Always;
		}

		public override void OnTouchStart( Entity toucher )
		{
			base.OnTouchStart( toucher );

			Reset( toucher );
		}

		private void Reset( Entity ent )
		{
			if ( ent is Ragdoll player )
				player.Respawn();
			else
			{
				Log.Error( $"No method for resetting this entity yet! ({ent})" );
			}
		}
	}
}
