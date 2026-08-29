using Cinema.Business.DTO.Catalog;

namespace Cinema.Business.Contracts;

public interface IUserTypeManager : ICatalogManager<UserTypeDTO, CreateUserTypeRequest, UpdateUserTypeRequest> { }
