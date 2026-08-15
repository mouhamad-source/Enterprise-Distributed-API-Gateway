var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
var app = builder.Build();
app.UseHttpsRedirection();
app.MapControllers();
app.Use(async (context, next) =>
{
    context.Response.Headers.Remove("Transfer-Encoding");
    await next();
});

app.Run();
