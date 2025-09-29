using Microsoft.SemanticKernel;
using Microsoft.Extensions.AI;
using AICustomerServiceWeb.Services;
using AICustomerServiceWeb.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var config = builder.Configuration;

var dashScopeApiKey = config["DashScope:ApiKey"]!;
var dashScopeEndpoint = config["DashScope:Endpoint"]!;
var defaultModel = config["DashScope:DefaultModel"]!;

var ragflowApiKey = config["RAGFlow:ApiKey"]!;
var ragflowEndpoint = config["RAGFlow:Endpoint"]!;
var q2sqlKbId = config["RAGFlow:Q2SQLKbId"]!;
var ddlKbId = config["RAGFlow:DDLKbId"]!;
var businessRulesKbId = config["RAGFlow:BusinessRulesKbId"]!;

var aiConnectionString = config.GetConnectionString("AICustomerService")!;
var productionConnectionString = config.GetConnectionString("Production")!;

var maxContextMessages = config.GetValue<int>("AISettings:MaxContextMessages");
var maxTokens = config.GetValue<int>("AISettings:MaxTokensPerRequest");
var temperature = config.GetValue<double>("AISettings:Temperature");
var systemPrompt = config["AISettings:SystemPrompt"]!;

builder.Services.AddSingleton(sp =>
{
    return new RAGFlowService(ragflowApiKey, ragflowEndpoint, q2sqlKbId, ddlKbId, businessRulesKbId);
});

builder.Services.AddSingleton(sp =>
{
    return new ConversationService(aiConnectionString, maxContextMessages);
});

builder.Services.AddScoped(sp =>
{
    var kernelBuilder = Kernel.CreateBuilder();

    kernelBuilder.AddOpenAIChatCompletion(
        modelId: defaultModel,
        endpoint: new Uri(dashScopeEndpoint),
        apiKey: dashScopeApiKey);

    var kernel = kernelBuilder.Build();

    var ragflow = sp.GetRequiredService<RAGFlowService>();
    var dbTool = new DatabaseTool(productionConnectionString, kernel, ragflow);
    kernel.Plugins.AddFromObject(dbTool, "DatabaseTool");

    return kernel;
});

builder.Services.AddScoped(sp =>
{
    var kernel = sp.GetRequiredService<Kernel>();
    var conversationService = sp.GetRequiredService<ConversationService>();
    return new AIService(kernel, conversationService, systemPrompt, maxTokens, temperature);
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

app.Run();
