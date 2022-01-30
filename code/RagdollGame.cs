
using Sandbox;
using Sandbox.UI;
using Sandbox.UI.Construct;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace Ragdolls
{
	public partial class RagdollGame : Sandbox.Game
	{
		public RagdollGame()
		{
			if ( IsServer )
				new RagdollHudEntity();
		}

		public override void ClientJoined( Client client )
		{
			base.ClientJoined( client );

			var ragdoll = new Ragdoll();
			client.Pawn = ragdoll;
			ragdoll.Respawn();
		}

		public override void ClientDisconnect( Client cl, NetworkDisconnectionReason reason )
		{
			base.ClientDisconnect( cl, reason );
		}
	}
}
