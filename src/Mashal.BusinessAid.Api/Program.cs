using Mashal.BusinessAid.Api.Configuration;
using Mashal.BusinessAid.Api.Endpoints;
using Mashal.BusinessAid.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);
var origins = new AppOrigins(builder.Configuration, builder.Environment);

builder.AddApiServices(origins);

var app = builder.Build();
app.UseApiMiddleware(origins);
app.MapApiEndpoints(origins);
app.Run();

public partial class Program;