using System.Security.Claims;
using AmbientContextTest;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages().AddMvcOptions(opt =>
{
    opt.AddAmbientContextModelBinding();
});

builder.Services.AddAmbientContext();
builder.Services.Configure<AmbientContextOptions>(opt =>
{
    opt.Policies.Add("Partial", a => a.Hid.HasValue || a.Pid.HasValue);
    opt.Policies.Add("ParentIdRequired", a => a.Pid.HasValue);
    opt.Policies.Add("AllowEmpty", _ => true);
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie();

builder.Services.Configure<ForwardedHeadersOptions>(opt =>
{
    opt.KnownNetworks.Clear();
    opt.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UsePathBase("/Ambient");
// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();
app.UseAmbientContext("/SetupContext"); 

app.MapStaticAssets();
app.MapRazorPages()
    .WithStaticAssets();

app.MapGet("/Login", (Delegate)Login);
app.MapGet("/Logout", (Delegate)Logout);

app.Run();
return;

async Task<IResult> Login(HttpContext ctx)
{
    var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
    identity.AddClaim(new Claim(ClaimTypes.Upn, "123456"));
    identity.AddClaim(new Claim(ClaimTypes.Name, "Parent Account"));
    identity.AddClaim(new Claim("UserType", "Parent"));
    identity.AddClaim(new Claim("PersonId", "123456"));
    identity.AddClaim(new Claim("PrimaryHouseholdId", "31412"));

    var principal = new ClaimsPrincipal(identity);

    await ctx.SignInAsync(principal);
    
    return Results.Redirect("~/");
}

async Task<IResult> Logout(HttpContext ctx)
{
    await ctx.SignOutAsync();
    return Results.Redirect("~/");
}