using System;
using System.Threading;
using System.Threading.Tasks;

using AutoMapper;
using MediatR;

using OpenSim.Data.Models;
using OpenSim.GridServices.AssetService.Models;

namespace OpenSim.GridServices.AssetService.Events.AssetDb
{
    public class GetAssetById {
        public class Request : IRequest<AssetDto> 
        { 
            public string Id { get; set; }
        }

        public class Command : IRequestHandler<Request, AssetDto>
        {
            private readonly OpenSimDatabaseContext _database;
            private readonly IMapper _mapper;

            public Command(OpenSimDatabaseContext database, IMapper mapper)
            {
                this._database = database;
                this._mapper = mapper;
            }

            public async Task<AssetDto> Handle(Request request, CancellationToken cancellationToken)
            {
                var asset = await _database.Assets.FindAsync(request.Id);
                if (asset == null)
                    return null;

                return _mapper.Map<AssetDto>(asset);
            }
        }
    }
}