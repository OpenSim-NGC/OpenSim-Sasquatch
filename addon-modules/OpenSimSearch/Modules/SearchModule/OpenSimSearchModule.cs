using Autofac;
using OpenSim.Region.Framework.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenSimSearch.Modules.OpenSearch
{
    public class OpenSimSearchModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<OpenSearchModule>()
                .Named<ISharedRegionModule>("OpenSimSearch")
                .AsImplementedInterfaces()
                .SingleInstance();
        }
    }
}
