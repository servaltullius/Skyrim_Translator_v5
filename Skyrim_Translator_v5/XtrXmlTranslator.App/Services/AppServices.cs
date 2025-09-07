using System;
using Microsoft.Extensions.DependencyInjection;

namespace XtrXmlTranslator.App.Services;

public static class AppServices
{
    public static IServiceProvider? Provider { get; internal set; }
}
