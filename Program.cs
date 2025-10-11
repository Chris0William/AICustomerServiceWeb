using AICustomerServiceWeb2.Core.Agent;
using AICustomerServiceWeb2.Core.Tools;
using AICustomerServiceWeb2.Core.Interfaces;
using AICustomerServiceWeb2.Application.Services;
using AICustomerServiceWeb2.Infrastructure.Tools;
using Microsoft.SemanticKernel;

var builder = WebApplication.CreateBuilder(args);

// 添加控制器
builder.Services.AddControllers()
    .AddNewtonsoftJson(); // 使用Newtonsoft.Json

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS（允许前端访问）
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Semantic Kernel配置
var apiKey = builder.Configuration["DashScope:ApiKey"] ?? "";
var endpoint = builder.Configuration["DashScope:Endpoint"] ?? "https://dashscope.aliyuncs.com/compatible-mode/v1";
var model = builder.Configuration["DashScope:DefaultModel"] ?? "qwen-plus";

var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.AddOpenAIChatCompletion(
    modelId: model,
    apiKey: apiKey,
    endpoint: new Uri(endpoint)
);

builder.Services.AddSingleton(kernelBuilder.Build());

// 注册 HttpClient (用于 RAGFlowService)
builder.Services.AddHttpClient<IRAGFlowService, RAGFlowService>();

// 注册核心服务
builder.Services.AddScoped<IRAGFlowService, RAGFlowService>();
builder.Services.AddScoped<IDatabaseService, DatabaseService>();
builder.Services.AddScoped<IConversationService, ConversationService>();

// 注册Agent组件
builder.Services.AddScoped<IRequestClassifier, RequestClassifier>();
builder.Services.AddScoped<IPlanner, Planner>();
builder.Services.AddScoped<IExecutor, Executor>();
builder.Services.AddScoped<IReflector, Reflector>();
builder.Services.AddScoped<IReActAgent, ReActAgent>();

// 注册工具
builder.Services.AddScoped<IAgentTool, DatabaseTool>();

// 日志配置
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

var app = builder.Build();

// 配置HTTP请求管道
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 静态文件支持
app.UseStaticFiles();

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthorization();

app.MapControllers();

// 默认路由到静态页面
app.MapFallbackToFile("index.html");

Console.WriteLine("=================================================");
Console.WriteLine("AICustomerServiceWeb2 - ReAct Agent 启动成功");
Console.WriteLine("=================================================");
Console.WriteLine($"API地址: {app.Urls.FirstOrDefault()}");
Console.WriteLine($"Swagger文档: {app.Urls.FirstOrDefault()}/swagger");
Console.WriteLine($"使用模型: {model}");
Console.WriteLine("=================================================");

app.Run();
