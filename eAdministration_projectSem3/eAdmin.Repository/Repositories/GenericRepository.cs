using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using eAdmin.Domain.Interfaces;
using eAdmin.Repository.Data;
using Microsoft.EntityFrameworkCore;

namespace eAdmin.Repository.Repositories
{
    // =====================================================================
    // GenericRepository<T>  –  Triển khai CRUD cho bất kỳ Entity nào
    // =====================================================================
    public class GenericRepository<T> : IRepository<T> where T : class
    {
        protected readonly AppDbContext _context;
        protected readonly DbSet<T> _dbSet;

        public GenericRepository(AppDbContext context)
        {
            _context = context;
            _dbSet   = context.Set<T>();
        }

        /// <summary>Tìm theo khóa chính</summary>
        public async Task<T?> GetByIdAsync(int id)
            => await _dbSet.FindAsync(id);

        /// <summary>Lấy toàn bộ bản ghi</summary>
        public async Task<IEnumerable<T>> GetAllAsync()
            => await _dbSet.AsNoTracking().ToListAsync();

        /// <summary>Tìm theo điều kiện lambda</summary>
        public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
            => await _dbSet.AsNoTracking().Where(predicate).ToListAsync();

        /// <summary>Thêm entity mới (chưa SaveChanges)</summary>
        public async Task AddAsync(T entity)
            => await _dbSet.AddAsync(entity);

        /// <summary>Cập nhật entity (chưa SaveChanges)</summary>   
        public void Update(T entity)
            => _dbSet.Update(entity);

        /// <summary>Xóa entity (chưa SaveChanges)</summary>
        public void Remove(T entity)
            => _dbSet.Remove(entity);
        public IQueryable<T> Query()
            => _dbSet.AsNoTracking();
    }
}
