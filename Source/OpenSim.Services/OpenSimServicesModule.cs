using Autofac;
using OpenSim.Services.Interfaces;
using OpenSim.Services.FSAssetService;
using OpenSim.Services.UserAccountService;
using OpenSim.Services.EstateService;
using OpenSim.Framework;
using OpenSim.Framework.AssetLoader.Filesystem;
using OpenSim.Services.InventoryService;
using OpenSim.Services.AuthenticationService;
using OpenSim.Services.AuthorizationService;

namespace OpenSim.Services;

public class OpenSimServicesModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<AssetService.AssetService>()
            .Named<IAssetService>("AssetService")
            .AsImplementedInterfaces().SingleInstance();  

        // [Obsolete]
        // builder.RegisterType<AssetService.XAssetService>()
        //     .Named<IAssetService>("XAssetService")
        //     .AsImplementedInterfaces().SingleInstance(); 

        builder.RegisterType<AssetLoaderFileSystem>()
            .Named<IAssetLoader>("AssetLoaderFileSystem")
            .AsImplementedInterfaces().SingleInstance();

        builder.RegisterType<PasswordAuthenticationService>()
            .Named<IAuthenticationService>("PasswordAuthenticationService")
            .AsImplementedInterfaces().SingleInstance();

        builder.RegisterType<WebkeyAuthenticationService>()
            .Named<IAuthenticationService>("WebkeyAuthenticationService")
            .AsImplementedInterfaces().SingleInstance();

        builder.RegisterType<WebkeyOrPasswordAuthenticationService>()
            .Named<IAuthenticationService>("WebkeyOrPasswordAuthenticationService")
            .AsImplementedInterfaces().SingleInstance();

        builder.RegisterType<AuthorizationService.AuthorizationService>()
            .Named<IAuthorizationService>("AuthorizationService")
            .AsImplementedInterfaces().SingleInstance();

        builder.RegisterType<AgentPreferencesService>()
            .Named<IAgentPreferencesService>("AgentPreferencesService")
            .AsImplementedInterfaces().SingleInstance();

        builder.RegisterType<EstateDataService>()
            .Named<IEstateDataService>("EstateDataService")
            .AsImplementedInterfaces()
            .SingleInstance();

        builder.RegisterType<FSAssetConnector>()
            .Named<IAssetService>("FSAssetConnector")
            .AsImplementedInterfaces().SingleInstance();


        // builder.RegisterType<GridService.GridService>()
        //     .Named<IGridService>("GridService")
        //     .AsImplementedInterfaces().SingleInstance();

        builder.RegisterType<GridUserService>()
            .Named<IGridUserService>("GridUserService")
            .AsImplementedInterfaces().SingleInstance();

        builder.RegisterType<UserAccountService.UserAccountService>()
            .Named<IUserAccountService>("UserAccountService")
            .AsImplementedInterfaces().SingleInstance();

        builder.RegisterType<UserAliasService>()
            .Named<IUserAliasService>("UserAliasService")
            .AsImplementedInterfaces().SingleInstance();
        
        builder.RegisterType<XInventoryService>()
            .Named<IInventoryService>("XInventoryService")
            .AsImplementedInterfaces().SingleInstance();        
    }
}
