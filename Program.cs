using Microsoft.SemanticKernel;
using Microsoft.Extensions.AI;
using AICustomerServiceWeb.Services;
using AICustomerServiceWeb.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 添加HttpContextAccessor以支持请求级别的数据存储
builder.Services.AddHttpContextAccessor();

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

// 注册ExecutionContext为Scoped服务（每个请求一个实例）
builder.Services.AddScoped<AICustomerServiceWeb.Services.ExecutionContext>();

// 注册ToolCallTracker为Scoped服务（每个请求一个实例）
builder.Services.AddScoped<AICustomerServiceWeb.Tools.ToolCallTracker>();

builder.Services.AddScoped(sp =>
{
    var kernelBuilder = Kernel.CreateBuilder();

    kernelBuilder.AddOpenAIChatCompletion(
        modelId: defaultModel,
        endpoint: new Uri(dashScopeEndpoint),
        apiKey: dashScopeApiKey);

    var kernel = kernelBuilder.Build();

    var ragflow = sp.GetRequiredService<RAGFlowService>();
    var executionContext = sp.GetRequiredService<AICustomerServiceWeb.Services.ExecutionContext>();
    var tracker = sp.GetRequiredService<AICustomerServiceWeb.Tools.ToolCallTracker>();

    // ReAct模式：注册独立的工具
    // 1. RAGFlow知识库查询工具
    var ragflowTool = new RAGFlowTool(ragflow, executionContext, tracker);
    kernel.Plugins.AddFromObject(ragflowTool, "RAGFlowTool");

    // 2. SQL生成和执行工具
    var sqlTool = new SQLTool(productionConnectionString, executionContext, kernel, tracker);
    kernel.Plugins.AddFromObject(sqlTool, "SQLTool");

    // 3. 保留原有的DatabaseTool作为备用（可选）
    // var dbTool = new DatabaseTool(productionConnectionString, kernel, ragflow, executionContext);
    // kernel.Plugins.AddFromObject(dbTool, "DatabaseTool");

    return kernel;
});

builder.Services.AddScoped(sp =>
{
    var kernel = sp.GetRequiredService<Kernel>();
    var conversationService = sp.GetRequiredService<ConversationService>();
    var executionContext = sp.GetRequiredService<AICustomerServiceWeb.Services.ExecutionContext>();
    return new AIService(kernel, conversationService, executionContext, systemPrompt, maxTokens, temperature);
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

app.Run();
