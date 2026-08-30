using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

public interface IMemberShipManager
{
    Task<DefaultSearchResults<MemberShipDTO>> GetAsync(PagingSearchDTO search);
    Task<bool>                                ExistsAsync(Guid id);
    Task<MemberShipDTO>                       GetByIdAsync(Guid id);
    Task<MemberShipDTO>                       CreateAsync(CreateMemberShipRequest request);
    Task<MemberShipDTO>                       UpdateAsync(UpdateMemberShipRequest request);
    Task                                      DeleteAsync(Guid id);
}
