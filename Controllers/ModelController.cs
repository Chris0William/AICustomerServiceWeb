using Microsoft.AspNetCore.Mvc;
using AICustomerServiceWeb.Models.Dto;

namespace AICustomerServiceWeb.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ModelController : ControllerBase
{
    [HttpGet]
    public IActionResult GetAvailableModels()
    {
        var models = new List<ModelInfo>
        {
            new ModelInfo
            {
                ModelId = "qwen-max",
                ModelName = "Qwen Max（旗舰版）",
                Description = "🏆 最强大的模型，适合复杂推理、多步骤任务、高难度问题。性能最佳但成本最高。"
            },
            new ModelInfo
            {
                ModelId = "qwen-max-latest",
                ModelName = "Qwen Max Latest（最新旗舰版）",
                Description = "🚀 最新版本的Max模型，持续更新优化，推荐用于测试最新能力。"
            },
            new ModelInfo
            {
                ModelId = "qwen-plus",
                ModelName = "Qwen Plus（标准版）",
                Description = "⭐ 推荐！能力均衡，性价比高。适合中等复杂度任务，推理效果、成本和速度均衡。"
            },
            new ModelInfo
            {
                ModelId = "qwen-plus-latest",
                ModelName = "Qwen Plus Latest（最新标准版）",
                Description = "🆕 最新版本的Plus模型，持续迭代优化。"
            },
            new ModelInfo
            {
                ModelId = "qwen-turbo",
                ModelName = "Qwen Turbo（经济版）",
                Description = "💨 响应速度快，成本低。适合简单对话、快速响应场景。"
            },
            new ModelInfo
            {
                ModelId = "qwen-turbo-latest",
                ModelName = "Qwen Turbo Latest（最新经济版）",
                Description = "⚡ 最新版本的Turbo模型，速度优化。"
            },
            new ModelInfo
            {
                ModelId = "qwen-long",
                ModelName = "Qwen Long（长文本版）",
                Description = "📚 超长上下文窗口（最高1000万tokens），适合长文本分析、文档处理、信息抽取。"
            },
            new ModelInfo
            {
                ModelId = "qwen2.5-72b-instruct",
                ModelName = "Qwen 2.5 72B Instruct",
                Description = "🔧 72B参数开源模型，适合需要本地部署或自定义场景。"
            },
            new ModelInfo
            {
                ModelId = "qwen2.5-32b-instruct",
                ModelName = "Qwen 2.5 32B Instruct",
                Description = "🛠️ 32B参数开源模型，平衡性能和资源消耗。"
            },
            new ModelInfo
            {
                ModelId = "qwen2.5-14b-instruct",
                ModelName = "Qwen 2.5 14B Instruct",
                Description = "⚙️ 14B参数开源模型，轻量级部署。"
            },
            new ModelInfo
            {
                ModelId = "qwen2.5-7b-instruct",
                ModelName = "Qwen 2.5 7B Instruct",
                Description = "💡 7B参数开源模型，适合资源受限场景。"
            },
            new ModelInfo
            {
                ModelId = "qwen2.5-3b-instruct",
                ModelName = "Qwen 2.5 3B Instruct",
                Description = "🪶 3B参数超轻量级模型，极低成本。"
            },
            new ModelInfo
            {
                ModelId = "qwen2.5-1.5b-instruct",
                ModelName = "Qwen 2.5 1.5B Instruct",
                Description = "🐣 1.5B参数最小模型，边缘设备可用。"
            },
            new ModelInfo
            {
                ModelId = "qwen2.5-0.5b-instruct",
                ModelName = "Qwen 2.5 0.5B Instruct",
                Description = "🌱 0.5B参数极小模型，IoT设备适用。"
            },
            new ModelInfo
            {
                ModelId = "qwen-vl-max",
                ModelName = "Qwen VL Max（视觉理解-旗舰）",
                Description = "👁️ 多模态模型，支持图片+文字理解，视觉理解能力最强。"
            },
            new ModelInfo
            {
                ModelId = "qwen-vl-plus",
                ModelName = "Qwen VL Plus（视觉理解-标准）",
                Description = "📷 多模态模型，图文理解能力均衡，性价比高。"
            },
            new ModelInfo
            {
                ModelId = "qwen-math-plus",
                ModelName = "Qwen Math Plus（数学专用）",
                Description = "🧮 数学推理专用模型，适合数学题、逻辑推理、科学计算。"
            },
            new ModelInfo
            {
                ModelId = "qwen-math-turbo",
                ModelName = "Qwen Math Turbo（数学快速）",
                Description = "➕ 数学推理快速版本，简单数学问题响应更快。"
            },
            new ModelInfo
            {
                ModelId = "qwen-coder-turbo",
                ModelName = "Qwen Coder Turbo（代码生成）",
                Description = "💻 代码生成专用模型，支持多种编程语言的代码生成、解释、优化。"
            }
        };

        return Ok(models);
    }
}