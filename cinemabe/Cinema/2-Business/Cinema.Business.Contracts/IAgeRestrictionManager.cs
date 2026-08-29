using Cinema.Business.DTO.Catalog;

namespace Cinema.Business.Contracts;

public interface IAgeRestrictionManager : ICatalogManager<AgeRestrictionDTO, CreateAgeRestrictionRequest, UpdateAgeRestrictionRequest> { }
