using Autofac;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Services.SimulationService
{
    public class SimulationDataServiceModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<SimulationDataService>()
                .Named<ISimulationDataService>("SimulationDataService")
                .AsImplementedInterfaces()
                .SingleInstance();
        }
    }
}
