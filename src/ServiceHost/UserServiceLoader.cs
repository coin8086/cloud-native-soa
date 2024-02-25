﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Reflection;

namespace Cloud.Soa;

interface IUserServiceLoader
{
    IUserService CreateServiceInstance();
}

class ServiceLoaderOptions
{
    [Required]
    public string? AssemblyPath { get; set; }
}

class UserServiceLoader : IUserServiceLoader
{
    private readonly ILogger _logger;
    private readonly ILogger _userLogger;
    private readonly ServiceLoaderOptions _options;

    public UserServiceLoader(ILogger<UserServiceLoader> logger, ILogger<IUserService> userLogger, IOptions<ServiceLoaderOptions> options)
    {
        _logger = logger;
        _userLogger = userLogger;
        _options = options.Value;
    }

    public IUserService CreateServiceInstance()
    {
        try
        {
            var assembly = LoadAssembly(_options.AssemblyPath!);
            var type = GetUserServiceType(assembly);
            var instance = (Activator.CreateInstance(type, _userLogger) as IUserService)!;
            return instance;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error when creating user service instance: {ex}", ex);
            throw;
        }
    }

    static Assembly LoadAssembly(string path)
    {
        var loadContext = new UserAssemblyLoadContext(path);
        return loadContext.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(path)));
    }

    static Type GetUserServiceType(Assembly assembly)
    {
        foreach (Type type in assembly.GetTypes())
        {
            if (typeof(IUserService).IsAssignableFrom(type))
            {
                return type;
            }
        }

        throw new ApplicationException($"Can't find a type that implements IUserService in {assembly} from {assembly.Location}.");
    }
}

static class ServiceCollectionUserServiceExtensions
{
    public static IServiceCollection AddUserService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<IUserServiceLoader, UserServiceLoader>();

        services.AddOptionsWithValidateOnStart<ServiceLoaderOptions>()
            .Bind(configuration.GetSection("UserService"))
            .ValidateDataAnnotations();

        services.AddTransient<IUserService>(provider => provider.GetService<IUserServiceLoader>()!.CreateServiceInstance());
        return services;
    }
}
