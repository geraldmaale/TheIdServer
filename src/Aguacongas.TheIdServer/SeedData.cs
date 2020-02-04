﻿// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using Aguacongas.IdentityServer.EntityFramework.Store;
using Aguacongas.IdentityServer.Store;
using Aguacongas.TheIdServer.Data;
using Aguacongas.TheIdServer.Models;
using IdentityModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Aguacongas.TheIdServer
{
#pragma warning disable S1118 // Utility classes should not have public constructors
    public static class SeedData
#pragma warning restore S1118 // Utility classes should not have public constructors
    {
        public static void EnsureSeedData(string connectionString)
        {
            var migrationsAssembly = typeof(Startup).GetTypeInfo().Assembly.GetName().Name;

            var services = new ServiceCollection();
            services.AddLogging()
                .AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString))
                .AddIdentityServer4AdminEntityFrameworkStores<ApplicationUser, ApplicationDbContext>(options => 
                    options.UseSqlServer(connectionString,
                        sql => sql.MigrationsAssembly(migrationsAssembly)))
                .AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            services.AddIdentityServer()
                .AddAspNetIdentity<ApplicationUser>();

            using var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();

            SeedUsers(scope);
            SeedConfiguration(scope);
        }

        public static void SeedConfiguration(IServiceScope scope)
        {
            var context = scope.ServiceProvider.GetRequiredService<IdentityServerDbContext>();
            context.Database.EnsureCreated();
            context.Database.Migrate();
            
            if (!context.Clients.Any())
            {
                foreach (var client in Config.GetClients())
                {
                    context.Clients.Add(client.ToEntity());
                    Console.WriteLine($"Add client {client.ClientName}");
                }
            }

            if (!context.Identities.Any())
            {
                foreach (var resource in Config.GetIdentityResources())
                {
                    context.Identities.Add(resource.ToEntity());
                    Console.WriteLine($"Add identity resource {resource.DisplayName}");
                }
            }

            if (!context.Apis.Any())
            {
                foreach (var resource in Config.GetApis())
                {
                    context.Apis.Add(resource.ToEntity());
                    Console.WriteLine($"Add api resource {resource.DisplayName}");
                }
            }
            context.SaveChanges();
        }

        public static void SeedUsers(IServiceScope scope)
        {
            var context = scope.ServiceProvider.GetService<ApplicationDbContext>();
            context.Database.Migrate();

            var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            var roles = new string[]
            {
                AuthorizationOptionsExtensions.WRITER,
                AuthorizationOptionsExtensions.READER
            };
            foreach(var role in roles)
            {
                if (roleMgr.FindByNameAsync(role).GetAwaiter().GetResult() == null)
                {
                    ExcuteAndCheckResult(() => roleMgr.CreateAsync(new IdentityRole
                    {
                        Name = role
                    })).GetAwaiter().GetResult();
                }
            }

            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var alice = userMgr.FindByNameAsync("alice").Result;
            if (alice == null)
            {
                alice = new ApplicationUser
                {
                    UserName = "alice"
                };
                ExcuteAndCheckResult(() => userMgr.CreateAsync(alice, "Pass123$"))
                    .GetAwaiter().GetResult();

                ExcuteAndCheckResult(() => userMgr.AddClaimsAsync(alice, new Claim[]{
                        new Claim(JwtClaimTypes.Name, "Alice Smith"),
                        new Claim(JwtClaimTypes.GivenName, "Alice"),
                        new Claim(JwtClaimTypes.FamilyName, "Smith"),
                        new Claim(JwtClaimTypes.Email, "AliceSmith@email.com"),
                        new Claim(JwtClaimTypes.EmailVerified, "true", ClaimValueTypes.Boolean),
                        new Claim(JwtClaimTypes.WebSite, "http://alice.com"),
                        new Claim(JwtClaimTypes.Address, @"{ 'street_address': 'One Hacker Way', 'locality': 'Heidelberg', 'postal_code': 69118, 'country': 'Germany' }", IdentityServer4.IdentityServerConstants.ClaimValueTypes.Json)
                    })).GetAwaiter().GetResult();

                ExcuteAndCheckResult(() => userMgr.AddToRolesAsync(alice, roles))
                    .GetAwaiter().GetResult();

                Console.WriteLine("alice created");
            }
            else
            {
                Console.WriteLine("alice already exists");
            }

            var bob = userMgr.FindByNameAsync("bob").GetAwaiter().GetResult();
            if (bob == null)
            {
                bob = new ApplicationUser
                {
                    UserName = "bob"
                };
                ExcuteAndCheckResult(() => userMgr.CreateAsync(bob, "Pass123$"))
                    .GetAwaiter().GetResult();

                ExcuteAndCheckResult(() => userMgr.AddClaimsAsync(bob, new Claim[]{
                        new Claim(JwtClaimTypes.Name, "Bob Smith"),
                        new Claim(JwtClaimTypes.GivenName, "Bob"),
                        new Claim(JwtClaimTypes.FamilyName, "Smith"),
                        new Claim(JwtClaimTypes.Email, "BobSmith@email.com"),
                        new Claim(JwtClaimTypes.EmailVerified, "true", ClaimValueTypes.Boolean),
                        new Claim(JwtClaimTypes.WebSite, "http://bob.com"),
                        new Claim(JwtClaimTypes.Address, @"{ 'street_address': 'One Hacker Way', 'locality': 'Heidelberg', 'postal_code': 69118, 'country': 'Germany' }", IdentityServer4.IdentityServerConstants.ClaimValueTypes.Json),
                        new Claim("location", "somewhere")
                    })).GetAwaiter().GetResult();
                ExcuteAndCheckResult(() => userMgr.AddToRoleAsync(bob, AuthorizationOptionsExtensions.READER))
                    .GetAwaiter().GetResult();
                Console.WriteLine("bob created");
            }
            else
            {
                Console.WriteLine("bob already exists");
            }
            context.SaveChanges();
        }

        [SuppressMessage("Major Code Smell", "S112:General exceptions should never be thrown", Justification = "Seeding")]
        private static async Task ExcuteAndCheckResult(Func<Task<IdentityResult>> action)
        {
            var result = await action.Invoke();
            if (!result.Succeeded)
            {
                throw new Exception(result.Errors.First().Description);
            }

        }
    }
}
