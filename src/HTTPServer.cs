using System;
using System.IO;
using System.Threading.Tasks;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Modules;
using Unosquare.Labs.EmbedIO.Constants;

namespace HTTPGateway
{
  public class LocalServerService: IDisposable
  {
    public string ResourcePath;
    public string ServerUrl = "http://localhost:4200/";
    public WebServer WebServer;
    public LocalServerService(string resourcePath)
    {
      ResourcePath = Path.Combine(resourcePath, "httpgateway/");
    }

    public Task RunServer()
    {
      WebServer = new WebServer(ServerUrl);
      WebServer.WithLocalSession();
      WebServer.RegisterModule(new StaticFilesModule(ResourcePath));
      WebServer.Module<StaticFilesModule>().UseRamCache = true;
      WebServer.WithWebApiController<APIController>();
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
    // You need to add a default constructor where the first argument
    // is an IHttpContext
    public APIController(IHttpContext context)
      : base(context)
    {
    }

    // You need to include the WebApiHandler attribute to each method
    // where you want to export an endpoint. The method should return
    // bool or Task<bool>.
    [WebApiHandler(HttpVerbs.Get, "/api/stats")]
    public Task<bool> GetStats()
    {
      try
      {
        return this.JsonResponseAsync("{ \"response\": \"ok\" }");
      }
      catch (Exception ex)
      {
        return this.JsonResponseAsync(ex);
      }
    }
    
    // You can override the default headers and add custom headers to each API Response.
    //public override void SetDefaultHeaders() => HttpContext.NoCache();
  }
}