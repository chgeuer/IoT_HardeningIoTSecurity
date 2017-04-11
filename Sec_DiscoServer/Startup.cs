namespace Sec_DiscoServer
{
    using Owin;
    using System.Web.Http;

    public class Startup
    {
        public void Configuration(IAppBuilder appBuilder)
        {
            HttpConfiguration config = new HttpConfiguration();
            config.MapHttpAttributeRoutes();
            appBuilder.UseWebApi(config); 
        }
    }
}
