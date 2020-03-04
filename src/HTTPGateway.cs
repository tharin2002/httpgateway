using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Newtonsoft.Json;

[assembly: ModInfo( "HTTPGateway",
	Description = "Enables an HTTP interface to interact with the server.",
	Website     = "https://github.com/tharin2002/httpgateway",
	Authors     = new []{ "Thaeryn" } )]

namespace HTTPGateway
{
	public class HTTPGatewayMod : ModSystem
	{
		public static event EventHandler<WSMessage> SendWSMessage;
		public static event EventHandler<ICoreServerAPI> ReferenceAPI;
		ICoreServerAPI api;
		WebService srv;
		Dictionary<string, JWTService.JWTUser> tokens = new Dictionary<string, JWTService.JWTUser>();
		new public bool AllowRuntimeReload = true;
		private string secret;
		public override bool ShouldLoad(EnumAppSide side)
		{
			return side == EnumAppSide.Server;
		}

		public override void Dispose()
		{
			this.srv.DisposeServer();
		}

		public override void StartServerSide(ICoreServerAPI api)
		{
			this.api = api;

			WSServer.WSLoaded += (s,e) => {
				ReferenceAPI?.Invoke(null, this.api);
			};

			if (!File.Exists(@"httpgateway.conf"))
			{
				RandomNumberGenerator rng = RandomNumberGenerator.Create();
				byte[] data = new byte[32];
				rng.GetBytes(data);
				this.secret = Encoding.UTF8.GetString(data, 0, data.Length);
				using (StreamWriter sw = File.CreateText(@"httpgateway.conf"))
				{
					sw.WriteLine(this.secret);
				}
			} else {
				using (StreamReader sr = File.OpenText(@"httpgateway.conf"))
				{
					string s;
					while ((s = sr.ReadLine()) != null)
					{
						this.secret = s;
					}
				}
			}

			var testcode = RandomString(6);
			JWTService.JWTUser testuser = new JWTService.JWTUser { UserId = "abc123", UserName = "Anti", RoleId = "1" };
			this.tokens.Add(testcode, testuser);
			api.Server.Logger.Warning("Your code: " + testcode);

			api.Event.PlayerJoin += OnPlayerJoin;
			this.srv = new WebService(GamePaths.AssetsPath, this.tokens, this.secret);
			this.srv.RunServer(api);
			api.Server.Logger.EntryAdded += OnServerLogEntry;
			api.RegisterCommand("httpgateway", "Configures the HTTP Gateway mod.", "",
				(IServerPlayer Player, int groupId, CmdArgs args) =>
				{
					if (args.Length > 0) {
						switch (args[0])
						{
							case "code":
								var code = RandomString(6);
								var uid = Player.PlayerUID;
								var uname = Player.PlayerName;
								var rid = Player.Role.Code;
								JWTService.JWTUser user = new JWTService.JWTUser { UserId = uid, UserName = uname, RoleId = rid };
								this.tokens.Add(code, user);
								Player.SendMessage(GlobalConstants.GeneralChatGroup, "Your code: " + code,EnumChatType.CommandSuccess);
								break;
							default:
								CmdError();
								break;
						}
					} else {
						CmdError();
					}

					void CmdError()
					{
						Player.SendMessage(GlobalConstants.GeneralChatGroup, "Usage: httpgateway code", EnumChatType.CommandError);
					}
					
				}, Privilege.controlserver);
		}

		private void OnPlayerJoin(IServerPlayer byPlayer)
		{
			byPlayer.SendMessage(GlobalConstants.GeneralChatGroup, "HTTPGateway v0.1 Loaded!", EnumChatType.Notification);
		}

		private void OnServerLogEntry(EnumLogType logType, string message, object[] args)
		{
			if (logType == EnumLogType.VerboseDebug) return;
			var msg = new WSMessage(logType.ToString(), BuildLogEntry(message, args));
			SendWSMessage?.Invoke(null, msg);
		}

		private string BuildLogEntry(string message, object[] args)
		{
			return String.Format(message, args);
		}
		static string RandomString(int length)
		{
			const string valid = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
			StringBuilder res = new StringBuilder();
			using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
			{
				byte[] uintBuffer = new byte[sizeof(uint)];

				while (length-- > 0)
				{
					rng.GetBytes(uintBuffer);
					uint num = BitConverter.ToUInt32(uintBuffer, 0);
					res.Append(valid[(int)(num % (uint)valid.Length)]);
				}
			}

			return res.ToString();
		}
	}
}
