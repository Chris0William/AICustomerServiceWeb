const API_BASE = '/api';

let currentConversationId = null;
let selectedModel = null;
let isProcessing = false;

document.addEventListener('DOMContentLoaded', () => {
    initEventListeners();
    loadConversations();
});

function initEventListeners() {
    document.getElementById('newConversationBtn').addEventListener('click', showModelSelection);
    document.getElementById('sendBtn').addEventListener('click', sendMessage);
    document.getElementById('userInput').addEventListener('keydown', handleInputKeydown);
    document.getElementById('cancelModelBtn').addEventListener('click', hideModelSelection);
    document.getElementById('confirmModelBtn').addEventListener('click', createNewConversation);
    document.getElementById('exportBtn').addEventListener('click', exportConversation);
}

function handleInputKeydown(e) {
    if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        sendMessage();
    }
}

async function loadConversations() {
    try {
        const response = await fetch(`${API_BASE}/conversation`);
        const conversations = await response.json();
        renderConversations(conversations);
    } catch (error) {
        console.error('加载会话列表失败:', error);
        document.getElementById('conversationList').innerHTML = '<div class="loading">加载失败</div>';
    }
}

function renderConversations(conversations) {
    const listEl = document.getElementById('conversationList');

    if (conversations.length === 0) {
        listEl.innerHTML = '<div class="loading">暂无会话</div>';
        return;
    }

    listEl.innerHTML = conversations.map(conv => `
        <div class="conversation-item ${conv.conversationId === currentConversationId ? 'active' : ''}"
             data-id="${conv.conversationId}">
            <div class="conversation-item-title">${escapeHtml(conv.title)}</div>
            <div class="conversation-item-meta">
                <span>${conv.modelId}</span>
                <span>${conv.totalTokens} tokens</span>
            </div>
        </div>
    `).join('');

    document.querySelectorAll('.conversation-item').forEach(item => {
        item.addEventListener('click', () => {
            const conversationId = item.dataset.id;
            loadConversation(conversationId);
        });
    });
}

async function showModelSelection() {
    try {
        const response = await fetch(`${API_BASE}/model`);
        const models = await response.json();

        const optionsEl = document.getElementById('modelOptions');
        optionsEl.innerHTML = models.map(model => `
            <div class="model-option" data-model-id="${model.modelId}">
                <div class="model-option-name">${escapeHtml(model.modelName)}</div>
                <div class="model-option-desc">${escapeHtml(model.description)}</div>
            </div>
        `).join('');

        document.querySelectorAll('.model-option').forEach(option => {
            option.addEventListener('click', () => {
                document.querySelectorAll('.model-option').forEach(o => o.classList.remove('selected'));
                option.classList.add('selected');
                selectedModel = option.dataset.modelId;
                document.getElementById('confirmModelBtn').disabled = false;
            });
        });

        document.getElementById('modelModal').classList.add('show');
    } catch (error) {
        console.error('加载模型列表失败:', error);
        alert('加载模型列表失败');
    }
}

function hideModelSelection() {
    document.getElementById('modelModal').classList.remove('show');
    selectedModel = null;
}

async function createNewConversation() {
    if (!selectedModel) return;

    try {
        const response = await fetch(`${API_BASE}/conversation`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ modelId: selectedModel })
        });

        const result = await response.json();
        hideModelSelection();
        await loadConversations();
        loadConversation(result.conversationId);
    } catch (error) {
        console.error('创建会话失败:', error);
        alert('创建会话失败');
    }
}

async function loadConversation(conversationId) {
    try {
        const response = await fetch(`${API_BASE}/conversation/${conversationId}`);
        const conversation = await response.json();

        currentConversationId = conversationId;
        document.getElementById('chatTitle').textContent = conversation.title;
        document.getElementById('modelInfo').textContent = `模型: ${conversation.modelId}`;
        document.getElementById('tokenInfo').textContent = `${conversation.totalTokens} tokens`;
        document.getElementById('exportBtn').style.display = 'block';

        document.getElementById('userInput').disabled = false;
        document.getElementById('sendBtn').disabled = false;

        renderMessages(conversation.messages);

        document.querySelectorAll('.conversation-item').forEach(item => {
            item.classList.toggle('active', item.dataset.id === conversationId);
        });
    } catch (error) {
        console.error('加载会话失败:', error);
        alert('加载会话失败');
    }
}

function renderMessages(messages) {
    // 保存到sessionStorage以便后续使用
    sessionStorage.setItem(`messages_${currentConversationId}`, JSON.stringify(messages));

    const messagesEl = document.getElementById('chatMessages');

    if (messages.length === 0) {
        messagesEl.innerHTML = '<div class="welcome-message"><p>会话已创建，开始提问吧！</p></div>';
        return;
    }

    messagesEl.innerHTML = messages.map((msg, index) => {
        const time = new Date(msg.createdTime).toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' });
        const hasExecutionLog = msg.role === 'assistant' && msg.executionDetails;

        return `
            <div class="message ${msg.role}" data-message-id="${msg.id || index}">
                <div class="message-avatar">${msg.role === 'user' ? '👤' : '🤖'}</div>
                <div>
                    <div class="message-content">${formatMessage(msg.content)}</div>
                    <div class="message-meta">
                        ${time} · ${msg.tokenCount} tokens
                        ${hasExecutionLog ?
                            `<button class="message-logs-btn" onclick="viewExecutionDetails(${msg.id || index})">📊 查看执行详情</button>`
                            : ''}
                    </div>
                </div>
            </div>
        `;
    }).join('');

    messagesEl.scrollTop = messagesEl.scrollHeight;
}

async function sendMessage() {
    if (!currentConversationId || isProcessing) return;

    const input = document.getElementById('userInput');
    const message = input.value.trim();
    if (!message) return;

    isProcessing = true;
    input.value = '';
    input.disabled = true;
    document.getElementById('sendBtn').disabled = true;

    addMessageToUI('user', message);
    showTypingIndicator();

    try {
        const response = await fetch(`${API_BASE}/chat`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                conversationId: currentConversationId,
                message: message
            })
        });

        const result = await response.json();
        removeTypingIndicator();

        // 重新加载会话详情以获取executionDetails
        const detailResponse = await fetch(`${API_BASE}/conversation/${currentConversationId}`);
        const detail = await detailResponse.json();
        const latestMessage = detail.messages[detail.messages.length - 1];

        addMessageToUI('assistant', result.response, result.messageId, latestMessage.executionDetails);
        document.getElementById('tokenInfo').textContent = `${result.totalTokens} tokens`;

        await loadConversations();
    } catch (error) {
        console.error('发送消息失败:', error);
        removeTypingIndicator();
        alert('发送消息失败: ' + error.message);
    } finally {
        isProcessing = false;
        input.disabled = false;
        document.getElementById('sendBtn').disabled = false;
        input.focus();
    }
}

function addMessageToUI(role, content, messageId, executionDetails) {
    const messagesEl = document.getElementById('chatMessages');
    const time = new Date().toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' });
    const tokenCount = Math.ceil(content.length / 2);

    // 更新sessionStorage中的消息列表
    const messages = JSON.parse(sessionStorage.getItem(`messages_${currentConversationId}`) || '[]');
    messages.push({
        id: messageId,
        role: role,
        content: content,
        executionDetails: executionDetails,
        tokenCount: tokenCount,
        createdTime: new Date().toISOString()
    });
    sessionStorage.setItem(`messages_${currentConversationId}`, JSON.stringify(messages));

    const hasExecutionLog = role === 'assistant' && executionDetails;

    const messageHtml = `
        <div class="message ${role}" data-message-id="${messageId || ''}">
            <div class="message-avatar">${role === 'user' ? '👤' : '🤖'}</div>
            <div>
                <div class="message-content">${formatMessage(content)}</div>
                <div class="message-meta">
                    ${time} · ${tokenCount} tokens
                    ${hasExecutionLog && messageId ?
                        `<button class="message-logs-btn" onclick="viewExecutionDetails(${messageId})">📊 查看执行详情</button>`
                        : ''}
                </div>
            </div>
        </div>
    `;

    if (messagesEl.querySelector('.welcome-message')) {
        messagesEl.innerHTML = '';
    }

    messagesEl.insertAdjacentHTML('beforeend', messageHtml);
    messagesEl.scrollTop = messagesEl.scrollHeight;
}

function showTypingIndicator() {
    const messagesEl = document.getElementById('chatMessages');
    const indicator = document.createElement('div');
    indicator.id = 'typingIndicator';
    indicator.className = 'message assistant';
    indicator.innerHTML = `
        <div class="message-avatar">🤖</div>
        <div>
            <div class="message-content">
                <div class="typing-indicator">
                    <div class="typing-dot"></div>
                    <div class="typing-dot"></div>
                    <div class="typing-dot"></div>
                </div>
            </div>
        </div>
    `;
    messagesEl.appendChild(indicator);
    messagesEl.scrollTop = messagesEl.scrollHeight;
}

function removeTypingIndicator() {
    const indicator = document.getElementById('typingIndicator');
    if (indicator) {
        indicator.remove();
    }
}

async function exportConversation() {
    if (!currentConversationId) return;

    try {
        const response = await fetch(`${API_BASE}/conversation/${currentConversationId}/export`);
        const data = await response.json();

        const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `conversation_${currentConversationId}_${Date.now()}.json`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    } catch (error) {
        console.error('导出失败:', error);
        alert('导出失败');
    }
}


// 查看消息的执行详情
function viewExecutionDetails(messageId) {
    console.log('viewExecutionDetails called with messageId:', messageId);

    // 从sessionStorage获取消息
    const messages = JSON.parse(sessionStorage.getItem(`messages_${currentConversationId}`) || '[]');
    console.log('Messages from sessionStorage:', messages);
    console.log('Looking for message with id:', messageId);

    const message = messages.find(m => m.id == messageId);
    console.log('Found message:', message);

    if (!message || !message.executionDetails) {
        console.log('No execution details found. Message:', message);
        alert('该消息没有执行详情');
        return;
    }

    try {
        const details = typeof message.executionDetails === 'string'
            ? JSON.parse(message.executionDetails)
            : message.executionDetails;
        showExecutionDetailsModal(details);
    } catch (error) {
        console.error('解析执行详情失败:', error);
        alert('执行详情数据格式错误');
    }
}

// 显示执行详情弹窗
function showExecutionDetailsModal(details) {
    const modal = document.getElementById('logModal');
    const modalBody = document.getElementById('logModalBody');

    modal.classList.add('active');
    modalBody.innerHTML = `
        <div class="loading-logs">
            <div class="spinner"></div>
            <p>加载中...</p>
        </div>
    `;

    try {
        let html = `
            <div class="task-info">
                <p><strong>执行状态：</strong>
                    <span class="badge ${(details.Status || 'success').toLowerCase()}">
                        ${details.Status || 'Success'}
                    </span>
                </p>
                ${details.TotalExecutionTime ?
                    `<p><strong>总耗时：</strong> ${details.TotalExecutionTime}ms</p>` : ''}
                ${details.ErrorMessage ?
                    `<p><strong>错误信息：</strong> <span style="color: red;">${escapeHtml(details.ErrorMessage)}</span></p>` : ''}
            </div>
        `;

        // RAGFlow检索详情
        if (details.RAGFlowSteps && details.RAGFlowSteps.length > 0) {
            html += `
                <div class="log-steps">
                    <h4>📚 RAGFlow 知识库检索</h4>
                    ${details.RAGFlowSteps.map((step, idx) => `
                        <div class="log-step" id="step-${idx}">
                            <div class="log-step-header" onclick="toggleStep('step-${idx}')">
                                <div>
                                    <span class="log-step-name">步骤${step.StepNumber}: ${step.StepName}</span>
                                    <span class="expand-icon">▶</span>
                                </div>
                                <div class="log-step-badge">
                                    <span class="badge">检索到 ${step.RetrievedCount} 条</span>
                                    <span class="badge">${step.ExecutionTimeMs}ms</span>
                                </div>
                            </div>
                            <div class="log-step-content">
                                <p><strong>查询：</strong> ${escapeHtml(step.QueryText)}</p>
                                <div class="retrieved-items">
                                    ${formatRetrievedItems(step.RetrievedItems)}
                                </div>
                            </div>
                        </div>
                    `).join('')}
                </div>
            `;
        }

        // SQL执行详情
        if (details.GeneratedSQL) {
            html += `
                <div class="log-steps">
                    <h4>💾 SQL 执行详情</h4>
                    <div class="log-step expanded">
                        <div class="log-step-content" style="display: block;">
                            <p><strong>生成的SQL：</strong></p>
                            <div class="sql-display">${escapeHtml(details.GeneratedSQL)}</div>
                            ${details.ResultRowCount !== null && details.ResultRowCount !== undefined ?
                                `<p><strong>查询结果：</strong> 返回 ${details.ResultRowCount} 行数据</p>` : ''}
                        </div>
                    </div>
                </div>
            `;
        }

        if (!details.RAGFlowSteps?.length && !details.GeneratedSQL) {
            html += '<div class="no-logs">没有详细的执行日志</div>';
        }

        modalBody.innerHTML = html;
    } catch (error) {
        console.error('加载任务详情失败:', error);
        modalBody.innerHTML = '<div class="no-logs">加载失败</div>';
    }
}

// 关闭日志弹窗
function closeLogModal() {
    document.getElementById('logModal').classList.remove('active');
}

// 切换步骤展开/收起
function toggleStep(stepId) {
    const step = document.getElementById(stepId);
    if (step) {
        step.classList.toggle('expanded');
    }
}

// 获取任务类型标签
function getTaskTypeLabel(type) {
    const labels = {
        'DatabaseQuery': '数据库查询',
        'KnowledgeSearch': '知识检索',
        'General': '通用对话'
    };
    return labels[type] || type;
}

// 获取状态标签
function getStatusLabel(status) {
    const labels = {
        'Running': '运行中',
        'Success': '成功',
        'Failed': '失败'
    };
    return labels[status] || status;
}

// 格式化检索内容
// 格式化检索到的条目
function formatRetrievedItems(items) {
    if (!items || !items.length) {
        return '<div>无检索结果</div>';
    }

    return items.map((item, index) =>
        `<div class="retrieved-item">
            <strong>[${index + 1}]</strong> ${escapeHtml(item.Content)}
            <div class="retrieved-item-meta">
                相似度: ${item.Similarity ? item.Similarity.toFixed(3) : 'N/A'} |
                来源: ${escapeHtml(item.DocumentName || '未知')}
            </div>
        </div>`
    ).join('');
}

// 兼容旧的函数名
function formatRetrievedContent(content) {
    try {
        const parsed = JSON.parse(content);
        if (parsed.RetrievedItems) {
            return formatRetrievedItems(parsed.RetrievedItems);
        }
        return `<pre>${escapeHtml(JSON.stringify(parsed, null, 2))}</pre>`;
    } catch {
        return `<pre>${escapeHtml(content || '(无内容)')}</pre>`;
    }
}

function formatMessage(content) {
    const parts = [];
    let lastIndex = 0;

    const codeBlockRegex = /```(?:sql)?\n?([\s\S]+?)\n?```/g;
    let match;

    while ((match = codeBlockRegex.exec(content)) !== null) {
        if (match.index > lastIndex) {
            parts.push({ type: 'text', content: content.substring(lastIndex, match.index) });
        }
        parts.push({ type: 'code', content: match[1].trim() });
        lastIndex = match.index + match[0].length;
    }

    if (lastIndex < content.length) {
        parts.push({ type: 'text', content: content.substring(lastIndex) });
    }

    let result = parts.map(part => {
        if (part.type === 'code') {
            return '<pre><code class="language-sql">' + escapeHtml(part.content) + '</code></pre>';
        } else {
            let text = escapeHtml(part.content);
            text = text.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');
            text = text.replace(/\n/g, '<br>');
            return text;
        }
    }).join('');

    return result;
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}