using Sandbox;
using System.Linq;
using System.Threading.Tasks;

partial class SandboxGame : Game
{
	public SandboxGame()
	{
	}

	public override void ClientJoined( Client cl )
	{
		base.ClientJoined( cl );
		var player = new SandboxPlayer( cl );
		player.Respawn();

		cl.Pawn = player;
	}

	public override void DoPlayerNoclip( Client player )
	{
		if ( player.Pawn is Player basePlayer )
		{
			if ( basePlayer.DevController is NoclipController )
			{
				Log.Info( "Noclip Mode Off" );
				basePlayer.DevController = null;
			}
			else
			{
				Log.Info( "Noclip Mode On" );
				basePlayer.DevController = new NoclipController();
			}
		}
	}
}
