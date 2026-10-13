// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Sandbox;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<SandboxOptions>()
    .Bind(builder.Configuration.GetSection(SandboxOptions.Section))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<SandboxOptions>, SandboxOptionsValidator>();

var app = builder.Build();
app.Run();
