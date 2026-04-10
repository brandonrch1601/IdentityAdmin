using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using IdentityAdministration.Application.Common.Behaviors;
using System.Reflection;

namespace IdentityAdministration.Application;

/// <summary>
/// Registra todos los servicios de la capa Application en el contenedor DI.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        // Registrar todos los validators del assembly Application usando reflection
        var validatorType = typeof(IValidator<>);
        var validators = assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == validatorType)
                .Select(i => (Interface: i, Implementation: t)));

        foreach (var (iface, impl) in validators)
            services.AddTransient(iface, impl);

        return services;
    }
}
