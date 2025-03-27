using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;

namespace AmbientContextTest;

public enum AmbientContextSource
{
    None,
    Claims,
    Cookie
}

public record AmbientContext(AmbientContextSource Source, int? Hid, int? Pid)
{
    public bool IsComplete => Source != AmbientContextSource.None && Hid.HasValue && Pid.HasValue;
    
    // Creates a new context by updating properties of the current one
    public AmbientContext With(int? hid = null, int? pid = null) => this with { Hid = hid ?? Hid, Pid = pid ?? Pid };

    public static AmbientContext Empty => new(AmbientContextSource.None, null, null);
}

// Attribute to mark page models that require ambient context
[AttributeUsage(AttributeTargets.Class)]
public class RequireAmbientContextAttribute(string? policy = null) : Attribute
{
    public string? Policy { get; } = policy;
}

// Service to manage ambient context
public class AmbientContextService(IHttpContextAccessor httpContextAccessor)
{
    private const string CookieName = "AmbientContext";
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public async Task<AmbientContext> GetContextAsync(HttpContext httpContext)
    {
        // First, check if the user is authenticated and has claims-based context
        if (httpContext.User.Identity?.IsAuthenticated == true && 
            httpContext.User.HasClaim(c => c is { Type: "UserType", Value: "Parent" }))
        {
            // For Type 1 users, get context from claims
            var hidClaim = httpContext.User.FindFirst("PrimaryHouseholdId");
            var pidClaim = httpContext.User.FindFirst("PersonId");
            
            return new AmbientContext(
                AmbientContextSource.Claims,
                hidClaim != null ? int.Parse(hidClaim.Value) : null,
                pidClaim != null ? int.Parse(pidClaim.Value) : null
            );
        }

        // For Type 2 users, get context from cookie
        if (!httpContext.Request.Cookies.TryGetValue(CookieName, out var contextJson))
            return AmbientContext.Empty;
        try
        {
            var contextValues = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(contextJson);
                    
            return new AmbientContext(
                AmbientContextSource.Cookie,
                contextValues.TryGetValue("hid", out var hid) ? hid : null,
                contextValues.TryGetValue("pid", out var pid) ? pid : null
            );
        }
        catch
        {
            // Invalid cookie format
            return AmbientContext.Empty;
        }
    }

    public async Task SetContextAsync(HttpContext httpContext, AmbientContext context)
    {
        // For type 1 users, we don't need to do anything as context comes from claims
        if (httpContext.User.Identity?.IsAuthenticated == true && 
            httpContext.User.HasClaim(c => c is { Type: "UserType", Value: "Parent" }))
        {
            return;
        }
        
        // For type 2 users, store in cookie
        var contextDict = new Dictionary<string, int>();
        
        if (context.Hid.HasValue)
            contextDict["hid"] = context.Hid.Value;
            
        if (context.Pid.HasValue)
            contextDict["pid"] = context.Pid.Value;
            
        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        };
        
        httpContext.Response.Cookies.Append(
            CookieName,
            System.Text.Json.JsonSerializer.Serialize(contextDict),
            options
        );
    }

    public async Task ClearContextAsync(HttpContext httpContext)
    {
        if (httpContext.User.Identity?.IsAuthenticated == true && 
            httpContext.User.HasClaim(c => c is { Type: "UserType", Value: "Parent" }))
        {
            return;
        }
        
        httpContext.Response.Cookies.Delete(CookieName);
    }

    public async Task<bool> HasCompleteContextAsync(HttpContext httpContext)
    {
        var context = await GetContextAsync(httpContext);
        return context.IsComplete;
    }
}

public class AmbientContextOptions
{
    public Dictionary<string, Predicate<AmbientContext>> Policies { get; } = new()
    {
        ["__DEFAULT__"] = ctx => ctx.IsComplete
    };
}

// Middleware to enforce ambient context
public class AmbientContextMiddleware(RequestDelegate next, string contextSetupPath)
{
    public async Task InvokeAsync(HttpContext context, AmbientContextService contextService, IOptions<AmbientContextOptions> options)
    {
        // Skip for the context setup page itself to avoid redirect loops
        if (context.Request.Path.StartsWithSegments(contextSetupPath, StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        // Get the endpoint for the current request
        var endpoint = context.GetEndpoint();
        
        // Check if the endpoint requires ambient context
        var requireContextAttribute = endpoint?.Metadata.GetMetadata<RequireAmbientContextAttribute>();
        
        if (requireContextAttribute != null)
        {
            var ambientContext = await contextService.GetContextAsync(context);

            var policyName = requireContextAttribute.Policy ?? "__DEFAULT__";
            if (!options.Value.Policies.TryGetValue(policyName, out var policyPredicate))
                throw new InvalidOperationException($"Could not find ambient context policy named {policyName}");
            
            // Check if context is complete or partial based on the attribute setting
            var contextSatisfied = policyPredicate.Invoke(ambientContext);
            
            if (!contextSatisfied)
            {
                // Redirect to context setup page
                context.Response.Redirect($"{contextSetupPath}?returnUrl={Uri.EscapeDataString(context.Request.Path)}");
                return;
            }
            
            // Store the ambient context in the HttpContext.Items for easy access in the page model
            context.Items["AmbientContext"] = ambientContext;
        }
        
        await next(context);
    }
}

public class AmbientContextValueBinder(string contextPropertyName) : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var httpContext = bindingContext.HttpContext;
        
        // Get the ambient context from HttpContext.Items
        if (httpContext.Items.TryGetValue("AmbientContext", out var contextObj) && contextObj is AmbientContext context)
        {
            object? value = null;
            
            // Extract the requested property from the context
            switch (contextPropertyName.ToLower())
            {
                case "hid":
                    value = context.Hid;
                    break;
                case "pid":
                    value = context.Pid;
                    break;
                default:
                    // Property not found in ambient context
                    bindingContext.Result = ModelBindingResult.Failed();
                    return Task.CompletedTask;
            }

            // If the value exists, bind it
            bindingContext.Result = value != null ? ModelBindingResult.Success(value) : ModelBindingResult.Failed();
        }
        else
        {
            // No ambient context found
            bindingContext.Result = ModelBindingResult.Failed();
        }

        return Task.CompletedTask;
    }
}

// Model binder provider to provide our custom binder
public class AmbientContextBinderProvider : IModelBinderProvider
{
    public IModelBinder GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Check if the binding source is marked with our custom attribute
        var bindingSourceId = context.BindingInfo.BindingSource?.Id;
        if (bindingSourceId == null || !bindingSourceId.StartsWith("AmbientContext_")) return null!;
        // Extract the property name from the binding source ID
        var propertyName = bindingSourceId.Substring("AmbientContext_".Length);
        return !string.IsNullOrEmpty(propertyName) ? new AmbientContextValueBinder(propertyName) : null!;
    }
}


// Custom binding source - similar to [FromQuery], [FromRoute], etc.
[AttributeUsage(AttributeTargets.Property)]
public class FromAmbientContextAttribute(string propertyName) : Attribute, IBindingSourceMetadata, IModelNameProvider
{
    public string PropertyName { get; } = propertyName;

    public BindingSource BindingSource => new BindingSource(
        $"AmbientContext_{PropertyName}", // id
        "AmbientContext", // display name
        isGreedy: false,
        isFromRequest: false);
    
    public string Name => null;
}


// Extension methods for service and middleware registration
public static class AmbientContextExtensions
{
    public static IServiceCollection AddAmbientContext(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<AmbientContextService>();
        return services;
    }
    
    public static IApplicationBuilder UseAmbientContext(this IApplicationBuilder app, string contextSetupPath = "/SetupContext")
    {
        return app.UseMiddleware<AmbientContextMiddleware>(contextSetupPath);
    }
    
    public static MvcOptions AddAmbientContextModelBinding(this MvcOptions options)
    {
        options.ModelBinderProviders.Insert(0, new AmbientContextBinderProvider());
        return options;
    }
}
