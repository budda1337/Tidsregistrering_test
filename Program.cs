using Microsoft.AspNetCore.Server.IISIntegration;
using Microsoft.EntityFrameworkCore;
using Tidsregistrering.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Add Windows Authentication - vigtigt for IIS
builder.Services.AddAuthentication(IISDefaults.AuthenticationScheme);
builder.Services.AddAuthorization();

// Add Entity Framework
builder.Services.AddDbContext<TidsregistreringContext>(options =>
    options.UseSqlite("Data Source=C:\\TidsregistreringData\\tidsregistrering_test.db"));

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<TidsregistreringContext>();
    context.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // Kun brug HSTS hvis du har SSL certifikat
    // app.UseHsts();
}

// Kun redirect til HTTPS hvis du har SSL certifikat
// app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

// IIS håndterer authentication, så vi skal bare enable det
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();