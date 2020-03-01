using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Config;

[assembly: ModInfo( "HTTPGateway",
	Description = "Enables an HTTP interface to interact with the server.",
	Website     = "https://github.com/tharin2002/httpgateway",
	Authors     = new []{ "Thaeryn" } )]

namespace HTTPGateway
{
	public class HTTPGatewayMod : ModSystem
	{
		ICoreServerAPI api;
        LocalServerService srv;

		public override bool ShouldLoad(EnumAppSide side)
		{
				return side == EnumAppSide.Server;
		}
	
		public override void StartServerSide(ICoreServerAPI api)
		{
			this.api = api;
			api.Event.PlayerJoin += OnPlayerJoin;
            this.srv = new LocalServerService(GamePaths.AssetsPath);
            this.srv.RunServer();

		}

		private void OnPlayerJoin(IServerPlayer byPlayer)
		{
			byPlayer.SendMessage(GlobalConstants.GeneralChatGroup, "HTTPGateway v0.1 Loaded!", EnumChatType.Notification);
		}
    }
}
