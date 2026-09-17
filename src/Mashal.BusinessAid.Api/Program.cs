using Mashal.BusinessAid.Api.Configuration;
using Mashal.BusinessAid.Api.Endpoints;
using Mashal.BusinessAid.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);
var origin = builder.Configuration["App:Origin"] ?? "https://businessaid.mashalsystems.com";

builder.AddApiServices(origin);

var app = builder.Build();
app.UseApiMiddleware(origin);
app.MapApiEndpoints(origin);
app.Run();

public partial class Program;