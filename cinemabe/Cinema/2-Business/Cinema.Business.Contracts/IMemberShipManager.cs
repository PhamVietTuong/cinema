using Cinema.Business.DTO.Catalog;

namespace Cinema.Business.Contracts;

public interface IMemberShipManager : ICatalogManager<MemberShipDTO, CreateMemberShipRequest, UpdateMemberShipRequest> { }
