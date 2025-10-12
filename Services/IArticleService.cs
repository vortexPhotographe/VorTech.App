using System.Collections.Generic;
using System.Threading.Tasks;
using VorTech.App.Models;

namespace VorTech.App.Services
{
    public interface IArticleService
    {
        Task<IReadOnlyList<Article>> GetAllAsync();
        Task<Article?> GetByIdAsync(int id);
        Task<Article> AddAsync(Article article);
        Task<Article> UpdateAsync(Article article);
        Task<bool> DeleteAsync(int id);
        Task<IReadOnlyList<Article>> SearchAsync(string? text);
    }
}