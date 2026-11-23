using AutoMapper;
using Delivery.Application.Users.Dtos;
using Delivery.Domain.Entities;

namespace Delivery.Application.Users;

public sealed class UsersProfile : Profile
{
    public UsersProfile()
    {
        CreateMap<User, UserListItemDto>()
            .ForMember(d => d.Roles, o => o.MapFrom(s => s.Roles.Select(r => r.Role)))
            .ForMember(d => d.AgencyName, o => o.MapFrom(s => s.Agency != null ? s.Agency.Name : null))
            .ForMember(d => d.AgencyIds, o => o.MapFrom(s => s.Agencies.Select(a => a.AgencyId)));

        CreateMap<User, UserDetailDto>()
            .IncludeBase<User, UserListItemDto>()
            .ForMember(d => d.ClientId, o => o.MapFrom(s => s.Client != null ? s.Client.Id : null))
            .ForMember(d => d.CourierId, o => o.MapFrom(s => s.Courier != null ? s.Courier.Id : null));
    }
}
