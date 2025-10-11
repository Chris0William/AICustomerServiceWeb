# ReAct Agent 开发规划文档

## 功能需求总览

### 核心能力要求

1. **Agent 反思机制 (Reflection)**
   - 能够评估自己的执行结果质量
   - 判断是否需要重试或调整策略
   - 给出置信度评分

2. **任务规划期 (Planning)**
   - 自动拆解复杂请求为多步骤
   - 生成执行计划
   - 决定使用哪些工具以及调用顺序

3. **子 Agent 控制**
   - 支持多个专门的 Agent (DatabaseAgent, AnalysisAgent, ReportAgent)
   - Agent 之间的协调和通信
   - 父 Agent 可以调用子 Agent 完成子任务

4. **上下文持久记忆 (Context Memory)**
   - 对话历史保存
   - 跨轮次的上下文理解
   - 能够引用之前的对话内容

5. **自检与重构能力**
   - 发现错误时自动修正
   - SQL 生成失败时重新生成
   - 工具调用失败时切换策略

6. **扩展能力**
   - 知识库检索 (RAGFlow)
   - 数据库查询 (MySQL)
   - 其他工具可扩展

---

## 实现阶段

### Phase 1: MVP Implementation (当前)

**目标**: 让 DatabaseTool 真正工作起来

#### 任务列表

1. **RAGFlowService** ⭐⭐⭐⭐⭐
   - HTTP 客户端调用 RAGFlow API
   - 支持三个知识库: Q2SQL、DDL、BusinessRules
   - 返回相似度排序的结果
   - 错误处理和重试机制

2. **DatabaseService** ⭐⭐⭐⭐⭐
   - MySQL 连接管理
   - SQL 执行和结果解析
   - 错误处理和超时控制

3. **ConversationService** ⭐⭐⭐⭐
   - 保存对话历史到数据库
   - 加载历史上下文
   - 管理 ConversationId

4. **DatabaseTool (完整实现)** ⭐⭐⭐⭐⭐
   - 集成 RAGFlowService 和 DatabaseService
   - 完整的 RAG Pipeline
   - 记录 ExecutionDetails

5. **Program.cs 服务注册** ⭐⭐⭐⭐⭐
   - 注册所有服务
   - 配置依赖注入

6. **控制台测试** ⭐⭐⭐⭐⭐
   - 执行测试用例
   - 验证功能完整性

**成果**: 可以真正执行数据库查询，完成基本的 ReAct 流程

---

### Phase 2: Agent Enhancement

**目标**: 提升智能能力

1. **Planner 增强** - 复杂任务拆解
2. **Reflector 增强** - 多维度评估
3. **Self-Healing** - 自动修复
4. **ConversationService 完整版** - 上下文压缩

**成果**: Agent 更智能，能处理复杂场景

---

### Phase 3: Sub-Agent System

**目标**: 实现 Agent 层级调用

1. **ISubAgent 接口定义**
2. **DatabaseAgent** - 专门处理数据库
3. **AgentRegistry** - 管理子 Agent
4. **父子通信机制**

**成果**: 支持复杂的多 Agent 协作

---

### Phase 4: Production Features

**目标**: 生产级特性

1. 记忆检索
2. 并行工具执行
3. 性能优化
4. 监控和日志

---

## 架构设计

### 当前架构

```
ReActAgent (主框架)
├── Planner (规划器) ✅
│   └── 生成执行计划
├── Executor (执行器) ✅
│   └── 按步骤执行工具
├── Reflector (反思器) ✅
│   └── 评估结果质量
└── Retry 机制 ✅
    └── 根据反思结果决定是否重试
```

### 目标架构 (Phase 1)

```
ReActAgent
├── Planner
├── Executor
│   └── DatabaseTool (完整实现)
│       ├── RAGFlowService ⭐ 新增
│       ├── DatabaseService ⭐ 新增
│       └── LLM (SQL Generation)
├── Reflector
└── ConversationService ⭐ 新增
```

### 目标架构 (Phase 3)

```
ReActAgent (主 Agent)
├── 接收复杂任务
├── 拆解为子任务
├── 调用子 Agent
│   ├── DatabaseAgent (数据库查询)
│   ├── AnalysisAgent (数据分析)
│   └── ReportAgent (报告生成)
└── 汇总结果
```

---

## 关键设计决策

### 1. 子 Agent 实现方式
**决策**: 方案A - 每个子 Agent 是独立的 ReActAgent 实例
**理由**: 更灵活，子 Agent 也有反思能力

### 2. 上下文管理
**决策**: 先实现基础版，后续扩展语义检索
**配置**:
- 上下文长度限制: 20 轮
- 短期记忆: 内存
- 长期记忆: 数据库

### 3. 工具扩展
**优先级**:
1. DatabaseTool (优先)
2. AnalysisTool (可选)
3. ReportTool (可选)

---

## 测试策略

### Phase 1 测试
使用 `测试用例.md` 中的场景:
- Category 1: 系统管理 - 简单查询 (3个用例)
- Category 2: 系统管理 - 关联查询 (2个用例)
- Category 3: 员工管理 - 统计查询 (2个用例)

### 验收标准
- ✅ SQL 生成正确
- ✅ 查询返回真实数据
- ✅ ReAct 流程完整 (Planning → Executing → Reflecting)
- ✅ ExecutionDetails 记录完整
- ✅ 错误处理正确

---

## 开发循环流程

### 自动化开发循环

1. **读取任务状态** (`task_state.json`)
2. **执行开发** (实现当前任务)
3. **编译验证** (`dotnet build`)
4. **运行测试** (控制台测试或单元测试)
5. **分析结果** (检查日志和输出)
6. **更新状态** (更新 `task_state.json`)
7. **继续下一步** (直到用户输入"停止")

### 任务状态追踪

文件: `task_state.json`
- 每个任务有明确的状态: pending / in_progress / completed / failed
- 记录依赖关系
- 记录验收标准
- 记录测试结果

---

## 快速参考

### 启动开发循环
智能 Agent 会自动:
1. 读取 `task_state.json`
2. 执行下一个待完成任务
3. 测试验证
4. 更新状态
5. 继续循环

### 停止开发循环
用户输入: `停止`

### 查看当前状态
```bash
# 查看任务状态
cat task_state.json

# 查看测试结果
dotnet test
```

---

**文档创建时间**: 2025-10-11
**当前阶段**: Phase 1 - MVP Implementation
**下一步**: 实现 RAGFlowService
