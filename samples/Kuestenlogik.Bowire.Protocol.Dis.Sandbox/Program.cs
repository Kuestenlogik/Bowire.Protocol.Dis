// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Sandbox;
using Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Components;
using Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Entities;
using Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Publishing;
using Microsoft.Extensions.Options;

// A map on which you place and move objects; every change goes out as DIS
// (Entity State, Comment) so Bowire's DIS tab has something to show that
// you control. See README.md.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<SandboxOptions>()
    .Bind(builder.Configuration.GetSection(SandboxOptions.Section))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<SandboxOptions>, SandboxOptionsValidator>();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<EntityStore>();
builder.Services.AddSingleton<PduFactory>();
builder.Services.AddSingleton<PublisherStatus>();
builder.Services.AddSingleton<IDatagramSink, UdpDatagramSink>();
builder.Services.AddSingleton<DisPublisher>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DisPublisher>());

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();

app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
