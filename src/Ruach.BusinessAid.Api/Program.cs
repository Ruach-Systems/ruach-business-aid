using Ruach.BusinessAid.Api.Configuration;
using Ruach.BusinessAid.Api.Endpoints;
using Ruach.BusinessAid.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);
var origins = new AppOrigins(builder.Configuration, builder.Environment);

builder.AddApiServices(origins);

var app = builder.Build();
app.UseApiMiddleware(origins);
app.MapApiEndpoints(origins);
app.Run();

public partial class Program;