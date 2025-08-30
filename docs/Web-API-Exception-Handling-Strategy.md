# Web API 异常处理策略

## 概述

本项目采用分层异常处理策略，将不同类型的异常映射到相应的HTTP状态码，为客户端提供清晰、一致的错误响应格式。

## 架构设计

### 1. 异常处理流程

```
Application Layer (MediatR) → Global Exception Middleware → HTTP Response
       ↓                              ↓                        ↓
UnhandledExceptionBehaviour → GlobalExceptionHandlingMiddleware → ProblemDetails JSON
```

### 2. 异常类型层次结构

```csharp
BaseApplicationException (抽象基类)
├── ValidationException (400 Bad Request)
├── UnauthorizedException (401 Unauthorized)
├── ForbiddenException (403 Forbidden)
├── NotFoundException (404 Not Found)
├── ConflictException (409 Conflict)
├── BusinessRuleViolationException (422 Unprocessable Entity)
├── ExternalServiceException (502 Bad Gateway)
└── ServiceUnavailableException (503 Service Unavailable)
```

## HTTP状态码映射

| 异常类型 | HTTP状态码 | 错误类型标识 | 使用场景 |
|---------|------------|-------------|----------|
| `ValidationException` | 400 | `validation_error` | 请求参数验证失败 |
| `UnauthorizedException` | 401 | `unauthorized` | 身份验证失败 |
| `ForbiddenException` | 403 | `forbidden` | 权限不足 |
| `NotFoundException` | 404 | `not_found` | 资源不存在 |
| `ConflictException` | 409 | `conflict` | 资源冲突 |
| `BusinessRuleViolationException` | 422 | `business_rule_violation` | 业务规则违反 |
| `ExternalServiceException` | 502 | `external_service_error` | 外部服务错误 |
| `ServiceUnavailableException` | 503 | `service_unavailable` | 服务不可用 |
| `TaskCanceledException` | 408 | `request_timeout` | 请求超时 |
| `ArgumentException` | 400 | `bad_request` | 参数错误 |
| 其他异常 | 500 | `internal_server_error` | 内部服务器错误 |

## 错误响应格式

所有错误响应都遵循 [RFC 7807 Problem Details](https://tools.ietf.org/html/rfc7807) 标准：

```json
{
  "type": "https://httpstatuses.com/400",
  "title": "Bad Request",
  "status": 400,
  "detail": "Product ID cannot be null or empty",
  "instance": "trace-id-123",
  "errorType": "validation_error",
  "traceId": "trace-id-123",
  "timestamp": "2023-12-01T10:00:00Z",
  "errors": {
    "productId": ["Product ID is required"]
  }
}
```

### 字段说明

- `type`: 错误类型的URI引用
- `title`: 人类可读的错误标题
- `status`: HTTP状态码
- `detail`: 具体错误描述
- `instance`: 请求的唯一标识符
- `errorType`: 应用程序定义的错误类型
- `traceId`: 链路跟踪ID
- `timestamp`: 错误发生时间
- `errors`: 验证错误详情（仅ValidationException）

## 实现细节

### 1. MediatR Pipeline Behavior

`UnhandledExceptionBehaviour<TRequest, TResponse>` 负责：
- 记录所有未处理的异常
- 保持请求上下文信息
- 重新抛出异常供全局中间件处理

### 2. 全局异常处理中间件

`GlobalExceptionHandlingMiddleware` 负责：
- 捕获所有异常
- 将异常转换为适当的HTTP响应
- 根据环境设置返回详细或简化的错误信息
- 使用适当的日志级别记录异常

### 3. 异常使用示例

```csharp
// 在Service层中使用
public async Task<ProductDto> GetProductAsync(string productId)
{
    if (string.IsNullOrWhiteSpace(productId))
        throw new ValidationException("Product ID cannot be null or empty");
        
    var product = await _repository.GetAsync(productId);
    if (product == null)
        throw new NotFoundException("Product", productId);
        
    return product;
}

// ERP服务调用失败
var response = await _erpService.GetProductAsync(productId);
if (!response.Success)
    throw new ExternalServiceException("ERP", response.ErrorMessage);
```

## 日志策略

异常按严重程度分级记录：

- **Error (>=500)**: 系统级错误，需要立即关注
- **Warning (400-499)**: 客户端错误，需要监控频率
- **Information (<400)**: 正常业务流程

## 环境差异

### 开发环境
- 返回详细的异常信息
- 包含完整的堆栈跟踪
- 记录更详细的调试信息

### 生产环境
- 返回简化的错误信息
- 隐藏内部实现细节
- 重点关注安全性和用户体验

## 最佳实践

1. **使用合适的异常类型**: 根据业务场景选择最合适的异常类型
2. **提供清晰的错误消息**: 错误消息应该对客户端开发者有意义
3. **保持一致性**: 相同类型的错误应该返回相同的状态码
4. **避免敏感信息泄露**: 生产环境不应暴露内部系统详情
5. **合理使用日志级别**: 根据异常严重程度选择合适的日志级别

## 与Clean Architecture的集成

- **Application层**: 定义业务异常类型，通过MediatR pipeline处理
- **Infrastructure层**: 处理外部服务异常，转换为领域异常
- **Presentation层**: 全局异常中间件处理所有异常，统一响应格式

这种设计确保了异常处理的一致性，提供了良好的开发体验，同时保持了系统的可维护性和安全性。