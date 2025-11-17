using Microsoft.EntityFrameworkCore;
using OcufiiAPI.Data;
using System.Linq.Expressions;

namespace OcufiiAPI.Repositories
{
    public class Repository<T> : IRepository<T> where T : class
    {
        protected readonly OcufiiDbContext _context;
        private readonly DbSet<T> _dbSet;

        public Repository(OcufiiDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        public async Task<T> GetByIdAsync(Guid id) => await _dbSet.FindAsync(id);
        public async Task<IEnumerable<T>> GetAllAsync() => await _dbSet.ToListAsync();
        public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate) => await _dbSet.Where(predicate).ToListAsync();
        public async Task AddAsync(T entity) => await _dbSet.AddAsync(entity);
        public void Update(T entity) => _dbSet.Update(entity);
        public void Delete(T entity) => _dbSet.Remove(entity);
        public async Task SaveAsync() => await _context.SaveChangesAsync();
    }
}
