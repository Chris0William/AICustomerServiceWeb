// 简化版的执行详情查看功能

// 查看消息的执行详情
function viewExecutionDetails(messageId) {
    // 从页面上查找消息元素
    const messages = JSON.parse(sessionStorage.getItem(`messages_${currentConversationId}`) || '[]');
    const message = messages.find(m => m.id == messageId);

    if (!message || !message.executionDetails) {
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

// 更新显示消息函数，确保保存executionDetails
function updateDisplayMessages(messages) {
    // 保存到sessionStorage以便后续使用
    sessionStorage.setItem(`messages_${currentConversationId}`, JSON.stringify(messages));

    const messagesEl = document.getElementById('chatMessages');
    if (!messages || messages.length === 0) {
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