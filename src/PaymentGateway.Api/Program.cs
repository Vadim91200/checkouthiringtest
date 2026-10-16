using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Models;
using PaymentGateway.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
    {
        // A body that fails to deserialize is reported on its JSON path only,
        // not a second time as "the request field is required".
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        // The web default accepts "1050" for an int; the brief says integer.
        options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// A request that fails validation is Rejected: 400 with the fields in error.
// The default ProblemDetails body is replaced because its shape is not the
// brief's and the custom one makes sure nothing submitted is echoed back.
builder.Services.Configure<ApiBehaviorOptions>(options =>
    options.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(RejectedResponse.From(context.ModelState)));

builder.Services.AddSingleton<PaymentsRepository>();
builder.Services.AddHttpClient<IAcquiringBank, BankClient>(client =>
{
    string bankUrl = builder.Configuration["Bank:BaseUrl"] ?? "http://localhost:8080";
    client.BaseAddress = new Uri(bankUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(5);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();

// Lets the tests host the real application through WebApplicationFactory<Program>.
public partial class Program
{
}