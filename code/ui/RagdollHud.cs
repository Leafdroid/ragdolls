using Sandbox.UI;

//
// You don't need to put things in a namespace, but it doesn't hurt.
//
namespace Ragdolls
{
	/// <summary>
	/// This is the HUD entity. It creates a RootPanel clientside, which can be accessed
	/// via RootPanel on this entity, or Local.Hud.
	/// </summary>
	public partial class RagdollHudEntity : Sandbox.HudEntity<RootPanel>
	{
		public RagdollHudEntity()
		{
			if ( IsClient )
			{
				RootPanel.SetTemplate( "ui/RagdollHud.html" );

				RootPanel.AddChild<NameTags>();
				RootPanel.AddChild<Scoreboard<ScoreboardEntry>>();
			}
		}
	}

}
