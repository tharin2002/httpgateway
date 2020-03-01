using System;
using System.IO;
using System.Threading.Tasks;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Modules;
using Unosquare.Labs.EmbedIO.Constants;
using Vintagestory.API.Server;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Newtonsoft.Json;

namespace HTTPGateway
{
  public class LocalServerService: IDisposable
  {
    public string ResourcePath;
    public string ServerUrl = "http://*:4200/";
    public WebServer WebServer;
    public LocalServerService(string resourcePath)
    {
      ResourcePath = Path.Combine(resourcePath, "httpgateway/");
    }

    public Task RunServer(ICoreServerAPI api)
    {
      WebServer = new WebServer(ServerUrl);
      WebServer.WithLocalSession();
      WebServer.RegisterModule(new StaticFilesModule(ResourcePath));
      WebServer.Module<StaticFilesModule>().UseRamCache = true;
      WebServer.RegisterModule(new WebApiModule());
      WebServer.Module<WebApiModule>().RegisterController(ctx => new APIController(ctx, api));
      return WebServer.RunAsync();
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
    // You need to add a default constructor where the first argument
    // is an IHttpContext
    public APIController(IHttpContext context, ICoreServerAPI api)
      : base(context)
    {
      this.api = api;
    }

    // You need to include the WebApiHandler attribute to each method
    // where you want to export an endpoint. The method should return
    // bool or Task<bool>.
    [WebApiHandler(HttpVerbs.Get, "/api/server")]
    public Task<bool> GetStats()
    {
      var response = new IServerAPIToJSON(api.Server);
      
      try
      {
        return this.JsonResponseAsync(JsonConvert.SerializeObject(response));
      }
      catch (Exception ex)
      {
        return this.JsonResponseAsync(ex);
      }
    }
    
    // You can override the default headers and add custom headers to each API Response.
    //public override void SetDefaultHeaders() => HttpContext.NoCache();
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