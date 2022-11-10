using CatalogService.API.Entities;
using MongoDB.Driver;

namespace CatalogService.API.Data
{
    public interface ICatalogContext
    {
        IMongoCollection<Product> Products { get; }
    }
}