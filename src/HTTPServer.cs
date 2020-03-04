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

namespace HTTPGateway
{
  public class WebService: IDisposable
  {
    public string ResourcePath;
    public string ServerUrl = "http://*:4200/";
    public WebServer WebServer;
    public ModSystem Gateway;
    public Dictionary<string, JWTService.JWTUser> tokens;
    public string secret;
    public static event EventHandler<WebService> ReferenceWebService;
    public WebService(string resourcePath, Dictionary<string, JWTService.JWTUser> tokens, string secret)
    {
      ResourcePath = Path.Combine(resourcePath, GamePaths.DataPath + "/Web");
      this.tokens = tokens;
      this.secret = secret;
      WSServer.WSLoaded += (s,e) => {
        ReferenceWebService?.Invoke(null, this);
      };
    }

    public Task RunServer(CancellationToken cancellationToken, ICoreServerAPI api)
    {
      WebServer = new WebServer(ServerUrl);
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
        try
        {
          valid = await JWTService.Validate(this.RequestHeader("_auth"), this.secret);
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
            return await this.JsonResponseAsync(JsonConvert.SerializeObject(response));
          }
          catch (Exception ex)
          {
            return await this.JsonExceptionResponseAsync(ex);
          }
        }
      }
      return await this.JsonResponseAsync("{\"error\": \"Unauthorized\", \"code\": 3}");
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
            return await this.JsonExceptionResponseAsync(ex);
          }
        }
      }
      try
      {
        return await this.JsonResponseAsync("{\"error\": \"Unauthorized\", \"code\": 2}");
      }
      catch (Exception ex)
      {
        return await this.JsonExceptionResponseAsync(ex);
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
        return await this.JsonExceptionResponseAsync(ex);
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