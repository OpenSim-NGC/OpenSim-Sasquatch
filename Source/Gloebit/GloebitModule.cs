using Autofac;
using OpenSim.Region.Framework.Interfaces;

namespace Gloebit.GloebitMoneyModule
{
    public class GloebitModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<GloebitMoneyModule>()
            .Named<ISharedRegionModule>("GloebitMoneyModule")
            .AsImplementedInterfaces()
            .SingleInstance();
        }
    }
}
