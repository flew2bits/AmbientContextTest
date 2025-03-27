using AmbientContextTest;

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

var app = builder.Build();

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

app.Run();