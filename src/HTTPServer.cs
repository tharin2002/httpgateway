using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Modules;
using Unosquare.Labs.EmbedIO.Constants;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Net;

namespace HTTPGateway
{
  public class WebService: IDisposable
  {
    public string ResourcePath;
    public string ServerUrl = "http://*";
    public WebServer WebServer;
    public ModSystem Gateway;
    public Dictionary<string, JWTService.JWTUser> tokens;
    public string secret;
    private string port = "80";
    public ICoreServerAPI api;
    public static event EventHandler<WebService> ReferenceWebService;
    public WebService(string resourcePath, ICoreServerAPI api, Dictionary<string, JWTService.JWTUser> tokens, string secret)
    {
      ResourcePath = Path.Combine(resourcePath, GamePaths.DataPath + "/Web");
      this.api = api;
      this.tokens = tokens;
      this.secret = secret;
      WSServer.WSLoaded += (s,e) => {
        ReferenceWebService?.Invoke(null, this);
      };
      const string keyFile = @"httpgateway-port.txt";
      if (!File.Exists(keyFile))
			{
				using (StreamWriter sw = File.CreateText(keyFile))
				{
					sw.WriteLine("80");
				}
			} else {
				using (StreamReader sr = File.OpenText(keyFile))
				{
          this.port = sr.ReadLine().Trim();
          if (!IsPort(this.port))
          {
            this.api.Server.Logger.Error("Invalid port in httpgateway-port.txt, falling back to port 80");
            this.port = "80";
          }
				}
			}
    }

    public Task RunServer(CancellationToken cancellationToken, ICoreServerAPI api)
    {
      WebServer = new WebServer(ServerUrl + ":" + port);
      WebServer.WithLocalSession();
      WebServer.EnableCors();
      WebServer.RegisterModule(new StaticFilesModule(ResourcePath));
      WebServer.Module<StaticFilesModule>().UseRamCache = true;
      WebServer.RegisterModule(new WebApiModule());
      WebServer.Module<WebApiModule>().RegisterController(ctx => new APIController(ctx, api, this.tokens, this.secret));
      WebServer.RegisterModule(new WebSocketsModule());
      WebServer.Module<WebSocketsModule>().RegisterWebSocketsServer<WSServer>("/ws/{token}");
      return WebServer.RunAsync(cancellationToken);
    }

    public void DisposeServer()
    {
      WebServer.Dispose();
    }

    public void Dispose()
    {

    }

    public static bool IsPort(string value)
    {
      if (string.IsNullOrEmpty(value))
        return false;

      Regex numeric = new Regex(@"^[0-9]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

      if (numeric.IsMatch(value))
      {
        try
        {
          if (Convert.ToInt32(value) < 65536)
            return true;
        }
        catch (OverflowException)
        {
        }
      }

      return false;
    }
  }

  // A controller is a class where the WebApi module will find available
  // endpoints. The class must extend WebApiController.
  public class APIController : WebApiController
  {
    private ICoreServerAPI api;
    private Dictionary<string, JWTService.JWTUser> tokens;
    private string secret;
    // You need to add a default constructor where the first argument
    // is an IHttpContext
    public APIController(IHttpContext context, ICoreServerAPI api, Dictionary<string, JWTService.JWTUser> tokens, string secret)
      : base(context)
    {
      this.api = api;
      this.tokens = tokens;
      this.secret = secret;
    }

    // You need to include the WebApiHandler attribute to each method
    // where you want to export an endpoint. The method should return
    // bool or Task<bool>.
    [WebApiHandler(HttpVerbs.Get, "/api/server")]
    public async Task<bool> GetServer()
    {
      if (this.HasRequestHeader("_auth"))
      {
        bool valid = false;
        valid = await JWTService.Validate(this.RequestHeader("_auth"), this.secret);
        if (valid)
        {
          var response = new IServerAPIToJSON(api.Server);
          return await this.JsonResponseAsync(JsonConvert.SerializeObject(response));
        }
      }
      return await this.JsonResponseAsync("{\"error\": \"Unauthorized\", \"code\": 3}"); // Try setting status to ok to clear up warnings
    }

    [WebApiHandler(HttpVerbs.Post, "/api/login")]
    public async Task<bool> Login()
    {
      var code = this.QueryString("code");
      if (code != null && code.Length == 6) 
      {
        if (this.tokens.ContainsKey(code))
        {
          var jwt = new JWTService();
          await jwt.Generate(this.tokens[code], this.secret);
          try
          {
            return await this.JsonResponseAsync("{ \"_auth\": \""+jwt.jwt+"\"}");
          }
          catch (Exception ex)
          {
            this.api.Server.Logger.Debug(ex.Message);
            return await this.JsonResponseAsync("{}");
          }
        }
      }
      try
      {
        return await this.JsonResponseAsync("{\"error\": \"Unauthorized\", \"code\": 2}");
      }
      catch (Exception ex)
      {
        this.api.Server.Logger.Debug(ex.Message);
        return await this.JsonResponseAsync("{}");
      }
    }

    [WebApiHandler(HttpVerbs.Get, "/api/login")]
    public async Task<bool> GetLogin()
    {
      try
      {
        return await this.JsonResponseAsync("{\"error\": \"Unauthorized\", \"code\": 4}");
      }
      catch (Exception ex)
      {
        this.api.Server.Logger.Debug(ex.Message);
        return await this.JsonResponseAsync("{}");
      }
    }
    
    // You can override the default headers and add custom headers to each API Response.
    //public override void SetDefaultHeaders() => HttpContext.NoCache();
  }

  /// <summary>
  /// Defines a very simple ws server
  /// </summary>
  public class WSServer : WebSocketsServer
  {
    public ICoreServerAPI api;
    public WebService web;
    public static event EventHandler<WSMessage> WSLoaded;
    
    public WSServer()
        : base(true)
        
    {
      
      HTTPGatewayMod.ReferenceAPI += (s,e) => {
        this.api = e;
      };
      WebService.ReferenceWebService += (s,e) => {
        this.web = e;
      };
      HTTPGatewayMod.SendWSMessage += (s,e) => {
        foreach (var ws in WebSockets)
        {
          Send(ws, e.ToJSON());
        }
      };
      WSLoaded?.Invoke(null, null);
    }

    public override string ServerName => "HTTPGateway WS Server";

    protected override void OnMessageReceived(IWebSocketContext context, byte[] rxBuffer, IWebSocketReceiveResult rxResult)
    {
        
    }

    protected override async void OnClientConnected(
      IWebSocketContext context,
      System.Net.IPEndPoint localEndPoint,
      System.Net.IPEndPoint remoteEndPoint)
    {
      context.RequestRegexUrlParams("/ws/{token}").TryGetValue("token", out object token);

      bool valid = false;
      try
      {
        valid = await JWTService.Validate(token.ToString(), this.web.secret);
      }
      catch (Exception e)
      {
        this.api.Server.Logger.Warning(e.Message); 
      }
      if (valid)
      {
        var response = new IServerAPIToJSON(api.Server);
        try
        {
          var msg = new WSMessage("Notification", "Websocket server started.");
          Send(context, msg.ToJSON());
          return;
        }
        catch (Exception e)
        {
          this.api.Server.Logger.Warning(e.Message);
        }
      }
      var err = new WSMessage("Error", "Unauthorized");
      Send(context, err.ToJSON());
      await context.WebSocket.CloseAsync();
    }

    protected override void OnFrameReceived(IWebSocketContext context, byte[] rxBuffer, IWebSocketReceiveResult rxResult)
    {
        
    }

    protected override void OnClientDisconnected(IWebSocketContext context)
    {
        
    }
  }

  public class WSMessage
  {
    public string type;
    public object data;
    
    public WSMessage(string type, object data)
    {
      this.type = type;
      this.data = data;
    }

    public string ToJSON()
    {
      var resp = "";
      try {
        resp = JsonConvert.SerializeObject(this);
      } catch (Exception e) {
        resp = JsonConvert.SerializeObject(e);
      }
      
      return resp;
    }
  }

  public class IServerAPIToJSON
  {
    public IServerConfig Config;
    public string CurrentRunPhase;
    public bool IsDedicated;
    public bool IsShuttingDown;
    public IServerPlayerToJSON[] Players;
    public long ServerUptimeMilliseconds;
    public int ServerUptimeSeconds;
    public long TotalReceivedBytes;
    public long TotalSentBytes;
    public int TotalWorldPlayTime;

    public IServerAPIToJSON(IServerAPI Server)
    {
      Config = Server.Config;
      CurrentRunPhase = Server.CurrentRunPhase.ToString();
      IsDedicated = Server.IsDedicated;
      IsShuttingDown = Server.IsShuttingDown;
      Players = new IServerPlayerToJSON[Server.Players.Length];
      for(var i = 0; i < Server.Players.Length; i++)
      {
        Players[i] = new IServerPlayerToJSON(Server.Players[i]);
      }
      ServerUptimeMilliseconds = Server.ServerUptimeMilliseconds;
      ServerUptimeSeconds = Server.ServerUptimeSeconds;
      TotalReceivedBytes = Server.TotalReceivedBytes;
      TotalSentBytes = Server.TotalSentBytes;
      TotalWorldPlayTime = Server.TotalWorldPlayTime;
    }
  }
  public class IServerPlayerToJSON
  {
    public int CurrentChunkSentRadius;
    public IPlayerRole Role;
    public IServerPlayerData ServerData;
    public float Ping;
    public string LanguageCode;
    public string IpAddress;
    public string ConnectionState;
    public PlayerGroupMembership[] Groups;
    public FuzzyEntityPos SpawnPosition;
    public IServerPlayerToJSON(IServerPlayer Player)
    {
      CurrentChunkSentRadius = Player.CurrentChunkSentRadius;
      Role = Player.Role;
      ServerData = Player.ServerData;
      Ping = Player.Ping;
      LanguageCode = Player.LanguageCode;
      IpAddress = Player.IpAddress;
      ConnectionState = Player.ConnectionState.ToString();
      Groups = new PlayerGroupMembership[Player.Groups.Length];
      for(var i = 0; i < Player.Groups.Length; i++)
      {
        Groups[i] = Player.Groups[i];
      }
      SpawnPosition = Player.SpawnPosition;
    }
  }
}