using System;
using System.Threading;
using System.Threading.Tasks;

using AutoMapper;

using OpenSim.Data.Models;
using OpenSim.GridServices.AssetService.Models;

namespace OpenSim.GridServices.AssetService.Events.AssetDb
{
    public class AssetProfile : Profile
    {
        public AssetProfile()
        {
            CreateMap<Asset, AssetDto>();
        }
    }
}

