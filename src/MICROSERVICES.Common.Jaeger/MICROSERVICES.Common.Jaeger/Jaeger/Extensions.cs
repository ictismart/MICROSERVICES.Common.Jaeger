﻿using Jaeger;
using Jaeger.Reporters;
using Jaeger.Samplers;
using Jaeger.Senders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTracing;
using OpenTracing.Util;

namespace MICROSERVICES.Common.Jaeger.Jaeger
{
    public static class Extensions
    {
        private static bool _initialized;
        private static readonly string JaegerSectionName = "jaeger";
        public static IServiceCollection AddJaeger(this IServiceCollection services)
        {
            if (_initialized)
            {
                return services;
            }

            _initialized = true;

            var options = GetJaegerOptions(services);

            if (!options.Enabled)
            {
                var defaultTracer = DShopDefaultTracer.Create();
                services.AddSingleton(defaultTracer);
                return services;
            }

            services.AddSingleton<ITracer>(sp =>
            {
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

                var reporter = new RemoteReporter
                        .Builder()
                    .WithSender(new UdpSender(options.UdpHost, options.UdpPort, options.MaxPacketSize))
                    .WithLoggerFactory(loggerFactory)
                    .Build();

                var sampler = GetSampler(options);

                var tracer = new Tracer
                        .Builder(options.ServiceName)
                    .WithReporter(reporter)
                    .WithSampler(sampler)
                    .Build();

                GlobalTracer.Register(tracer);

                return tracer;
            });

            return services;
        }

        private static JaegerOptions GetJaegerOptions(IServiceCollection services)
        {
            using (var serviceProvider = services.BuildServiceProvider())
            {
                var configuration = serviceProvider.GetService<IConfiguration>();
                return configuration.GetOptions<JaegerOptions>("jaeger");
            }
        }

        private static ISampler GetSampler(JaegerOptions options)
        {
            switch (options.Sampler)
            {
                case "const": return new ConstSampler(true);
                case "rate": return new RateLimitingSampler(options.MaxTracesPerSecond);
                case "probabilistic": return new ProbabilisticSampler(options.SamplingRate);
                default: return new ConstSampler(true);
            }
        }
    }
}
