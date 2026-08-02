using System.Buffers;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using NilDev.BridgeLM.Application.Services;
using NilDev.BridgeLM.Domain.Abstractions;
using NilDev.BridgeLM.Domain.Models;
using NilDev.BridgeLM.Hubs;
using NilDev.BridgeLM.Infrastructure;
using NilDev.BridgeLM.Serialization;
using NilDev.BridgeLM.Services;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.Configure<BridgeRuntimeOptions>(
	builder.Configuration.GetSection(BridgeRuntimeOptions.SectionName));
builder.Services.ConfigureHttpJsonOptions(options =>
	options.SerializerOptions.TypeInfoResolverChain.Insert(0, BridgeJsonSerializerContext.Default));
builder.Services.AddSignalR();
builder.Services.AddHealthChecks();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<IBridgeRuntimeSettingsStore, InMemoryBridgeRuntimeSettingsStore>();
builder.Services.AddSingleton<IProxyEventSink, SignalRProxyEventSink>();
builder.Services.AddSingleton<IRequestTransform, NoOpRequestTransform>();
builder.Services.AddSingleton<IResponseTransform, NoOpResponseTransform>();
builder.Services.AddSingleton<BridgeConfigurationService>();
builder.Services.AddSingleton<BridgeProxyService>();
builder.Services.AddBridgeInfrastructure();

var app = builder.Build();

await app.Services.GetRequiredService<BridgeConfigurationService>().InitializeAsync(app.Lifetime.ApplicationStopping);
await app.Services.GetRequiredService<IRequestLogStore>().InitializeAsync(app.Lifetime.ApplicationStopping);

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/", (IWebHostEnvironment environment) => ServeDashboard(environment));
app.MapGet("/api/config", (BridgeConfigurationService service) =>
	Results.Json(service.GetActiveConfiguration(), BridgeJsonSerializerContext.Default.BridgeConfigurationView));
app.MapPut("/api/config", async (HttpContext context, BridgeConfigurationService service, CancellationToken cancellationToken) =>
{
	var update = await DeserializeBodyAsync(context.Request, BridgeJsonSerializerContext.Default.BridgeConfigurationUpdate, cancellationToken);

	if (update is null)
	{
		return Results.Json(
			new ApiError
			{
				Error = "invalid_payload",
				Detail = "The configuration update payload could not be parsed."
			},
			BridgeJsonSerializerContext.Default.ApiError,
			statusCode: StatusCodes.Status400BadRequest);
	}

	try
	{
		var updated = await service.UpdateActiveConfigurationAsync(update, cancellationToken);
		return Results.Json(updated, BridgeJsonSerializerContext.Default.BridgeConfigurationView);
	}
	catch (InvalidOperationException exception)
	{
		return ToConfigurationErrorResult(exception);
	}
});
app.MapGet("/api/configs", async (BridgeConfigurationService service, CancellationToken cancellationToken) =>
{
	var configurations = (await service.ListAsync(cancellationToken)).ToList();
	return Results.Json(configurations, BridgeJsonSerializerContext.Default.ListBridgeNamedConfigurationSummary);
});
app.MapGet("/api/configs/{name}", async (string name, BridgeConfigurationService service, CancellationToken cancellationToken) =>
{
	var configuration = await service.GetAsync(name, cancellationToken);
	return configuration is null
		? Results.Json(
			new ApiError { Error = "not_found", Detail = $"Proxy configuration '{name}' was not found." },
			BridgeJsonSerializerContext.Default.ApiError,
			statusCode: StatusCodes.Status404NotFound)
		: Results.Json(configuration, BridgeJsonSerializerContext.Default.BridgeNamedConfigurationView);
});
app.MapPost("/api/configs", async (HttpContext context, BridgeConfigurationService service, CancellationToken cancellationToken) =>
{
	var create = await DeserializeBodyAsync(context.Request, BridgeJsonSerializerContext.Default.BridgeNamedConfigurationCreate, cancellationToken);
	if (create is null)
	{
		return Results.Json(
			new ApiError
			{
				Error = "invalid_payload",
				Detail = "The configuration create payload could not be parsed."
			},
			BridgeJsonSerializerContext.Default.ApiError,
			statusCode: StatusCodes.Status400BadRequest);
	}

	try
	{
		var created = await service.CreateAsync(create, cancellationToken);
		return Results.Json(created, BridgeJsonSerializerContext.Default.BridgeNamedConfigurationView, statusCode: StatusCodes.Status201Created);
	}
	catch (InvalidOperationException exception)
	{
		return ToConfigurationErrorResult(exception);
	}
});
app.MapPut("/api/configs/{name}", async (string name, HttpContext context, BridgeConfigurationService service, CancellationToken cancellationToken) =>
{
	var update = await DeserializeBodyAsync(context.Request, BridgeJsonSerializerContext.Default.BridgeConfigurationUpdate, cancellationToken);
	if (update is null)
	{
		return Results.Json(
			new ApiError
			{
				Error = "invalid_payload",
				Detail = "The configuration update payload could not be parsed."
			},
			BridgeJsonSerializerContext.Default.ApiError,
			statusCode: StatusCodes.Status400BadRequest);
	}

	try
	{
		var updated = await service.UpdateNamedConfigurationAsync(name, update, cancellationToken);
		return Results.Json(updated, BridgeJsonSerializerContext.Default.BridgeNamedConfigurationView);
	}
	catch (InvalidOperationException exception)
	{
		return ToConfigurationErrorResult(exception);
	}
});
app.MapPost("/api/configs/{name}/duplicate", async (string name, HttpContext context, BridgeConfigurationService service, CancellationToken cancellationToken) =>
{
	var duplicate = await DeserializeBodyAsync(context.Request, BridgeJsonSerializerContext.Default.BridgeDuplicateConfigurationRequest, cancellationToken);
	if (duplicate is null)
	{
		return Results.Json(
			new ApiError
			{
				Error = "invalid_payload",
				Detail = "The duplicate configuration payload could not be parsed."
			},
			BridgeJsonSerializerContext.Default.ApiError,
			statusCode: StatusCodes.Status400BadRequest);
	}

	try
	{
		var created = await service.DuplicateAsync(name, duplicate.Name, cancellationToken);
		return Results.Json(created, BridgeJsonSerializerContext.Default.BridgeNamedConfigurationView, statusCode: StatusCodes.Status201Created);
	}
	catch (InvalidOperationException exception)
	{
		return ToConfigurationErrorResult(exception);
	}
});
app.MapPost("/api/configs/{name}/rename", async (string name, HttpContext context, BridgeConfigurationService service, CancellationToken cancellationToken) =>
{
	var rename = await DeserializeBodyAsync(context.Request, BridgeJsonSerializerContext.Default.BridgeRenameConfigurationRequest, cancellationToken);
	if (rename is null)
	{
		return Results.Json(
			new ApiError
			{
				Error = "invalid_payload",
				Detail = "The rename configuration payload could not be parsed."
			},
			BridgeJsonSerializerContext.Default.ApiError,
			statusCode: StatusCodes.Status400BadRequest);
	}

	try
	{
		var updated = await service.RenameAsync(name, rename.Name, cancellationToken);
		return Results.Json(updated, BridgeJsonSerializerContext.Default.BridgeNamedConfigurationView);
	}
	catch (InvalidOperationException exception)
	{
		return ToConfigurationErrorResult(exception);
	}
});
app.MapPost("/api/configs/{name}/select", async (string name, BridgeConfigurationService service, CancellationToken cancellationToken) =>
{
	try
	{
		var selected = await service.SelectAsync(name, cancellationToken);
		return Results.Json(selected, BridgeJsonSerializerContext.Default.BridgeNamedConfigurationView);
	}
	catch (InvalidOperationException exception)
	{
		return ToConfigurationErrorResult(exception);
	}
});
app.MapDelete("/api/configs/{name}", async (string name, BridgeConfigurationService service, CancellationToken cancellationToken) =>
{
	try
	{
		await service.DeleteAsync(name, cancellationToken);
		return Results.NoContent();
	}
	catch (InvalidOperationException exception)
	{
		return ToConfigurationErrorResult(exception);
	}
});
app.MapGet("/api/requests", async (BridgeProxyService service, CancellationToken cancellationToken) =>
{
	var requests = (await service.ListRecentAsync(cancellationToken)).ToList();
	return Results.Json(requests, BridgeJsonSerializerContext.Default.ListProxyRequestSummary);
});
app.MapGet("/api/requests/{requestId}", async (string requestId, BridgeProxyService service, CancellationToken cancellationToken) =>
{
	var log = await service.GetAsync(requestId, cancellationToken);
	return log is null
		? Results.Json(
			new ApiError { Error = "not_found", Detail = $"Request '{requestId}' was not found." },
			BridgeJsonSerializerContext.Default.ApiError,
			statusCode: StatusCodes.Status404NotFound)
		: Results.Json(log, BridgeJsonSerializerContext.Default.ProxyRequestLog);
});
app.MapHealthChecks("/health");
app.MapHub<BridgeHub>("/hubs/bridge");
app.MapMethods("/proxy/{**path}", ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"], ProxyAsync);
app.MapFallback((IWebHostEnvironment environment) => ServeDashboard(environment));

app.Run();

static async Task ProxyAsync(HttpContext context, string? path, BridgeProxyService service, CancellationToken cancellationToken)
{
	var body = await ReadBodyAsync(context.Request, cancellationToken);
	var request = new ProxyInboundRequest
	{
		Method = context.Request.Method,
		Path = "/" + (path ?? string.Empty),
		QueryString = context.Request.QueryString.Value ?? string.Empty,
		ContentType = context.Request.ContentType ?? string.Empty,
		Body = body,
		Headers = context.Request.Headers.ToDictionary(
			static header => header.Key,
			static header => header.Value.Select(static value => value ?? string.Empty).ToArray(),
			StringComparer.OrdinalIgnoreCase)
	};

	ProxyForwardSession? session = null;
	try
	{
		session = await service.StartProxyAsync(request, cancellationToken);
		context.Response.StatusCode = (int)session.UpstreamResponse.StatusCode;
		CopyHeaders(session.UpstreamResponse.Headers, context.Response.Headers);
		CopyHeaders(session.UpstreamResponse.Content.Headers, context.Response.Headers);
		context.Response.Headers.Remove("transfer-encoding");
		if (session.UpstreamResponse.Content.Headers.ContentType is { } contentType)
		{
			context.Response.ContentType = contentType.ToString();
		}

		await using var upstreamStream = await session.UpstreamResponse.Content.ReadAsStreamAsync(cancellationToken);
		using var responseCapture = new MemoryStream();
		var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);

		try
		{
			while (true)
			{
				var read = await upstreamStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
				if (read == 0)
				{
					break;
				}

				var slice = buffer.AsMemory(0, read);
				await context.Response.Body.WriteAsync(slice, cancellationToken);
				await context.Response.Body.FlushAsync(cancellationToken);
				await responseCapture.WriteAsync(slice, cancellationToken);
				await service.PublishChunkAsync(session.RequestId, slice, cancellationToken);
			}
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}

		var responseBody = Encoding.UTF8.GetString(responseCapture.ToArray());
		var responseHeaders = SerializeHeaders(session.UpstreamResponse.Headers, session.UpstreamResponse.Content.Headers);
		await service.CompleteAsync(session, responseHeaders, responseBody, cancellationToken);
	}
	catch (Exception exception)
	{
		if (session is not null)
		{
			await service.FailAsync(
				session.RequestId,
				session.StartedAtUtc,
				session.StartedTimestamp,
				session.Method,
				session.Path,
				session.BackendName,
				exception,
				cancellationToken);
		}

		if (!context.Response.HasStarted)
		{
			context.Response.StatusCode = StatusCodes.Status502BadGateway;
			await JsonSerializer.SerializeAsync(
				context.Response.Body,
				new ApiError
				{
					Error = "proxy_forward_failed",
					Detail = exception.Message
				},
				BridgeJsonSerializerContext.Default.ApiError,
				cancellationToken);
		}
	}
	finally
	{
		if (session is not null)
		{
			await session.DisposeAsync();
		}
	}
}

static async Task<byte[]> ReadBodyAsync(HttpRequest request, CancellationToken cancellationToken)
{
	if (request.ContentLength is 0)
	{
		return [];
	}

	await using var buffer = new MemoryStream();
	await request.Body.CopyToAsync(buffer, cancellationToken);
	return buffer.ToArray();
}

static Task<TValue?> DeserializeBodyAsync<TValue>(
	HttpRequest request,
	System.Text.Json.Serialization.Metadata.JsonTypeInfo<TValue> jsonTypeInfo,
	CancellationToken cancellationToken) => JsonSerializer.DeserializeAsync(request.Body, jsonTypeInfo, cancellationToken).AsTask();

static void CopyHeaders(HttpHeaders source, IHeaderDictionary destination)
{
	foreach (var header in source)
	{
		if (string.Equals(header.Key, "transfer-encoding", StringComparison.OrdinalIgnoreCase))
		{
			continue;
		}

		destination[header.Key] = header.Value.ToArray();
	}
}

static string SerializeHeaders(HttpHeaders primaryHeaders, HttpHeaders contentHeaders)
{
	var headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

	foreach (var header in primaryHeaders)
	{
		headers[header.Key] = header.Value.ToArray();
	}

	foreach (var header in contentHeaders)
	{
		headers[header.Key] = header.Value.ToArray();
	}

	return JsonSerializer.Serialize(headers, BridgeJsonSerializerContext.Default.DictionaryStringStringArray);
}

static IResult ToConfigurationErrorResult(InvalidOperationException exception)
{
	var (error, detail, statusCode) = exception switch
	{
		BridgeConfigurationNotFoundException => ("not_found", exception.Message, StatusCodes.Status404NotFound),
		BridgeConfigurationConflictException => ("conflict", exception.Message, StatusCodes.Status409Conflict),
		BridgeConfigurationValidationException => ("validation_error", exception.Message, StatusCodes.Status400BadRequest),
		_ => ("config_error", exception.Message, StatusCodes.Status400BadRequest)
	};

	return Results.Json(
		new ApiError
		{
			Error = error,
			Detail = detail
		},
		BridgeJsonSerializerContext.Default.ApiError,
		statusCode: statusCode);
}

static IResult ServeDashboard(IWebHostEnvironment environment)
{
	var dashboardPath = Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "index.html");
	return File.Exists(dashboardPath)
	? Results.File(dashboardPath, "text/html; charset=utf-8")
		: Results.Content(DashboardPlaceholder.Html, "text/html; charset=utf-8");
}

static class DashboardPlaceholder
{
	public const string Html = """
		<!DOCTYPE html>
		<html lang="en">
		<head>
			<meta charset="utf-8" />
			<meta name="viewport" content="width=device-width, initial-scale=1" />
			<title>NilDev.BridgeLM</title>
			<style>
				:root {
					color-scheme: dark;
					font-family: 'Segoe UI', sans-serif;
					background: radial-gradient(circle at top, #243b53, #102a43 45%, #0b1724 100%);
					color: #f0f4f8;
				}
				body {
					margin: 0;
					min-height: 100vh;
					display: grid;
					place-items: center;
				}
				main {
					max-width: 840px;
					padding: 2rem;
					background: rgba(12, 24, 36, 0.84);
					border: 1px solid rgba(130, 195, 255, 0.24);
					border-radius: 20px;
					box-shadow: 0 24px 80px rgba(0, 0, 0, 0.35);
				}
				h1 { margin-top: 0; font-size: 2.5rem; }
				p, li { line-height: 1.6; }
				code {
					color: #9bd2ff;
					font-family: Consolas, monospace;
				}
			</style>
		</head>
		<body>
			<main>
				<h1>NilDev.BridgeLM</h1>
				<p>The backend proxy is running, but the static dashboard assets were not found in wwwroot.</p>
				<ul>
					<li><code>GET /api/config</code> exposes the effective runtime configuration.</li>
					<li><code>GET /api/requests</code> lists recent proxied calls stored in SQLite through Dapper.</li>
					<li><code>GET /api/requests/{id}</code> returns the captured request/response payloads for one call.</li>
					<li><code>/hubs/bridge</code> broadcasts live request lifecycle events over SignalR.</li>
					<li><code>/proxy/{**path}</code> forwards traffic to the configured upstream LLM endpoint.</li>
				</ul>
			</main>
		</body>
		</html>
		""";
}

	public partial class Program;
