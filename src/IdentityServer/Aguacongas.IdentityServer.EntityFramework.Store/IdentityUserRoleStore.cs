﻿// Project: Aguafrommars/TheIdServer
// Copyright (c) 2021 @Olivier Lefebvre
using Aguacongas.IdentityServer.Store;
using Aguacongas.IdentityServer.Store.Entity;
using Community.OData.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Edm;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Aguacongas.IdentityServer.EntityFramework.Store
{
    public class IdentityUserRoleStore<TUser> : IAdminStore<UserRole>
        where TUser : IdentityUser, new()
    {
        private readonly UserManager<TUser> _userManager;
        private readonly IdentityDbContext<TUser> _context;
        private readonly ILogger<IdentityUserRoleStore<TUser>> _logger;
        [SuppressMessage("Major Code Smell", "S2743:Static fields should not be used in generic types", Justification = "We use only one type of TUser")]
        private static readonly IEdmModel _edmModel = GetEdmModel();
        public IdentityUserRoleStore(UserManager<TUser> userManager, 
            IdentityDbContext<TUser> context,
            ILogger<IdentityUserRoleStore<TUser>> logger)
        {
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<UserRole> CreateAsync(UserRole entity, CancellationToken cancellationToken = default)
        {
            var user = await GetUserAsync(entity.UserId)
                .ConfigureAwait(false);
            var role = await GetRoleAsync(entity.RoleId, cancellationToken)
                .ConfigureAwait(false);


            var result = await _userManager.AddToRoleAsync(user, role.Name)
                .ConfigureAwait(false);                
            if (result.Succeeded)
            {
                entity.Id = $"{user.Id}@{entity.RoleId}";
                _logger.LogInformation("Entity {EntityId} created", entity.Id, entity);
                return entity;
            }
            throw new IdentityException
            {
                Errors = result.Errors
            };
        }

        public async Task<object> CreateAsync(object entity, CancellationToken cancellationToken = default)
        {
            return await CreateAsync(entity as UserRole, cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            var info = id.Split('@');
            var role = await GetRoleAsync(info[1], cancellationToken).ConfigureAwait(false);
            var user = await GetUserAsync(info[0])
                .ConfigureAwait(false);
            var result = await _userManager.RemoveFromRoleAsync(user, role.Name)
                .ConfigureAwait(false);
            if (!result.Succeeded)
            {
                throw new IdentityException
                {
                    Errors = result.Errors
                };
            }
            _logger.LogInformation("Entity {EntityId} deleted", id, role);
        }

        public async Task<UserRole> UpdateAsync(UserRole entity, CancellationToken cancellationToken = default)
        {
            await DeleteAsync(entity.Id, cancellationToken).ConfigureAwait(false);
            return await CreateAsync(entity, cancellationToken).ConfigureAwait(false);
        }

        public async Task<object> UpdateAsync(object entity, CancellationToken cancellationToken = default)
        {
            return await UpdateAsync(entity as UserRole, cancellationToken).ConfigureAwait(false);
        }

        public async Task<UserRole> GetAsync(string id, GetRequest request, CancellationToken cancellationToken = default)
        {
            var info = id.Split('@');
            var role = await _context.UserRoles.AsNoTracking().FirstOrDefaultAsync(r => r.UserId == info[0] && r.RoleId == info[1], cancellationToken)
                            .ConfigureAwait(false);
            if (role == null)
            {
                return null;
            }

            return role.ToEntity();
        }

        public async Task<PageResponse<UserRole>> GetAsync(PageRequest request, CancellationToken cancellationToken = default)
        {
            request = request ?? throw new ArgumentNullException(nameof(request));
            var odataQuery = _context.UserRoles.AsNoTracking().GetODataQuery(request, _edmModel);

            var count = await odataQuery.CountAsync(cancellationToken).ConfigureAwait(false);

            var page = odataQuery.GetPage(request);

            var items = await page.ToListAsync(cancellationToken).ConfigureAwait(false);

            return new PageResponse<UserRole>
            {
                Count = count,
                Items = items.Select(r => r.ToEntity())
            };
        }

        private async Task<TUser> GetUserAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId)
                .ConfigureAwait(false);
            if (user == null)
            {
                throw new IdentityException($"User {userId} not found");
            }

            return user;
        }

        private async Task<IdentityRole> GetRoleAsync(string id, CancellationToken cancellationToken)
        {
            var role = await _context.Roles.FindAsync(new object[] { id }, cancellationToken)
                .ConfigureAwait(false);

            if (role == null)
            {
                throw new DbUpdateException($"Entity type {typeof(UserRole).Name} at id {id} is not found");
            }

            return role;
        }

        private static IEdmModel GetEdmModel()
        {
            var builder = new ODataConventionModelBuilder();
            var entitySet = builder.EntitySet<IdentityUserRole<string>>(typeof(IdentityUserRole<string>).Name);
            entitySet.EntityType.HasKey(e => e.UserId);
            entitySet.EntityType.HasKey(e => e.RoleId);
            return builder.GetEdmModel();
        }
    }
}
