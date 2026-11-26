using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

public class MovieTypeDetailManager : IMovieTypeDetailManager
{
    private readonly IApplicationUnitOfWork _uow;

    public MovieTypeDetailManager(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<DefaultSearchResults<MovieTypeDetailDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var page = search.PageIndex > 0 ? search.PageIndex : 1;
        var pageSize = search.PageSize > 0 ? search.PageSize : 20;
        var (rows, total) = await _uow.MovieTypeDetailStore.GetPagedAsync(page, pageSize);

        return new DefaultSearchResults<MovieTypeDetailDTO>
        {
            Results = rows.Select(x => new MovieTypeDetailDTO
            {
                MovieId = x.MovieId,
                MovieTypeId = x.MovieTypeId,
                MovieTitle = x.MovieTitle,
                MovieTypeName = x.MovieTypeName,
            }).ToList(),
            TotalCount = total,
            CountPerPage = pageSize,
            Page = page,
        };
    }

    public async Task<MovieTypeDetailDTO> CreateAsync(CreateMovieTypeDetailRequest request)
    {
        if (await _uow.MovieTypeDetailStore.ExistsAsync(request.MovieId, request.MovieTypeId))
        {
            throw new InvalidOperationException("This movie / movie-type pairing already exists.");
        }
        await _uow.MovieTypeDetailStore.AddAsync(new MovieTypeDetail
        {
            MovieId = request.MovieId,
            MovieTypeId = request.MovieTypeId,
        });
        return new MovieTypeDetailDTO { MovieId = request.MovieId, MovieTypeId = request.MovieTypeId };
    }

    public Task DeleteAsync(Guid movieId, Guid movieTypeId)
    {
        return _uow.MovieTypeDetailStore.DeleteAsync(movieId, movieTypeId);
    }
}
