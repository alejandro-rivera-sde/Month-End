using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(Close_Portal.Startup))]

namespace Close_Portal {
    public class Startup {
        public void Configuration(IAppBuilder app) {
            app.MapSignalR();
        }
    }
}
