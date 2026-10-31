using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

public class PersonManager : IPersonManager
{
    private const int MaxNamesPerLookup = 50;

    protected readonly IApplicationUnitOfWork _uow;

    public PersonManager(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _uow.PersonStore.ExistsAsync(e => e.Id == id);
    }

    private IQueryable<Person> GetFilteredQuery(Dictionary<string, string>? filters)
    {
        var query = _uow.PersonStore.GetQuery();
        if (filters == null)
        {
            return query;
        }

        foreach (var key in filters.Keys)
        {
            if (string.IsNullOrEmpty(filters[key]))
            {
                continue;
            }

            switch (key)
            {
                case "keyword":
                    var keyword = filters[key];
                    query = _uow.PersonStore.FilterQuery(query, e => e.Name.Contains(keyword));
                    break;

                case "name":
                    var name = filters[key];
                    query = _uow.PersonStore.FilterQuery(query, e => e.Name.Contains(name));
                    break;
            }
        }
        return query;
    }

    private IQueryable<Person> ApplySort(IQueryable<Person> query, SortDTO? sort)
    {
        if (sort == null || string.IsNullOrEmpty(sort.Field))
        {
            return query;
        }

        return sort.Field switch
        {
            "name" => _uow.PersonStore.OrderQuery(query, e => e.Name, sort.Ascending),
            _ => query,
        };
    }

    public async Task<DefaultSearchResults<PersonDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);

        var query = GetFilteredQuery(search.Filters);
        query = ApplySort(query, search.Sort);
        var total = await _uow.PersonStore.CountAsync(query);
        var items = await _uow.PersonStore.AllPageAsync(query, page - 1, pageSize);
        return PagingHelper.ToPagedResult<Person, PersonDTO>(items, total, page, pageSize);
    }

    public async Task<PersonDTO> GetByIdAsync(Guid id)
    {
        var entity = await _uow.PersonStore.GetByIdAsync(id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"Person {id} not found.");
        }
        return entity.ToDTO<Person, PersonDTO>();
    }

    private static string? NormalizePhotoUrl(string? photoUrl)
    {
        return string.IsNullOrWhiteSpace(photoUrl) ? null : photoUrl.Trim();
    }

    public async Task<PersonDTO> CreateAsync(CreatePersonRequest request)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrEmpty(name))
        {
            throw new InvalidOperationException("Name is required.");
        }
        if (await _uow.PersonStore.ExistsAsync(e => e.Name == name))
        {
            throw new InvalidOperationException("A person with this name already exists.");
        }

        var entity = request.ToNewEntity<CreatePersonRequest, Person>();
        entity.Name = name;
        entity.PhotoUrl = NormalizePhotoUrl(request.PhotoUrl);
        await _uow.PersonStore.CreateAsync(entity);
        return entity.ToDTO<Person, PersonDTO>();
    }

    public async Task<PersonDTO> UpdateAsync(UpdatePersonRequest request)
    {
        var entity = await _uow.PersonStore.GetByIdAsync(request.Id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"Person {request.Id} not found.");
        }

        var name = request.Name.Trim();
        if (string.IsNullOrEmpty(name))
        {
            throw new InvalidOperationException("Name is required.");
        }
        if (await _uow.PersonStore.ExistsAsync(e => e.Name == name && e.Id != request.Id))
        {
            throw new InvalidOperationException("A person with this name already exists.");
        }

        entity.PatchEntity<Person, UpdatePersonRequest>(request);
        entity.Name = name;
        entity.PhotoUrl = NormalizePhotoUrl(request.PhotoUrl);
        await _uow.PersonStore.UpdateAsync(entity);
        return entity.ToDTO<Person, PersonDTO>();
    }

    public async Task DeleteAsync(Guid id)
    {
        if (await _uow.MovieCreditStore.ExistsAsync(c => c.PersonId == id))
        {
            throw new InvalidOperationException("This person is credited on one or more movies. Remove those credits first.");
        }
        await _uow.PersonStore.DeleteAsync(id);
    }

    public async Task<List<PersonDTO>> GetByNamesAsync(IList<string> names)
    {
        if (names == null || names.Count == 0)
        {
            return new List<PersonDTO>();
        }

        var distinctNames = names
            .Select(n => n?.Trim() ?? string.Empty)
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinctNames.Count == 0)
        {
            return new List<PersonDTO>();
        }
        if (distinctNames.Count > MaxNamesPerLookup)
        {
            throw new InvalidOperationException($"At most {MaxNamesPerLookup} names can be looked up at once.");
        }

        var entities = await _uow.PersonStore.GetWithPhotoByNamesAsync(distinctNames);
        return entities.Select(e => e.ToDTO<Person, PersonDTO>()).ToList();
    }
}
