import { NodeTracerProvider } from "@opentelemetry/sdk-trace-node";
import { BatchSpanProcessor } from "@opentelemetry/sdk-trace-base";
import { Resource } from "@opentelemetry/resources";
import { SemanticResourceAttributes } from "@opentelemetry/semantic-conventions";
import { JaegerExporter } from "@opentelemetry/exporter-jaeger";
import { OTLPTraceExporter } from "@opentelemetry/exporter-trace-otlp-grpc";
import { registerInstrumentations } from "@opentelemetry/instrumentation";
import { HttpInstrumentation } from "@opentelemetry/instrumentation-http";
import { ExpressInstrumentation } from "@opentelemetry/instrumentation-express";
import { PinoInstrumentation } from "@opentelemetry/instrumentation-pino";

export function setupTracing(tracingConfig) {
  const provider = new NodeTracerProvider({
    resource: new Resource({
      [SemanticResourceAttributes.SERVICE_NAME]: tracingConfig.serviceName,
      [SemanticResourceAttributes.SERVICE_VERSION]: process.env.VERSION || "0.0.0",
    }),
  });

  const exporter = process.env.OTEL_TRACES_EXPORTER === "otlp" ? new OTLPTraceExporter() : new JaegerExporter();
  provider.addSpanProcessor(new BatchSpanProcessor(exporter));
  provider.register();

  registerInstrumentations({
    tracerProvider: provider,
    instrumentations: [
      new HttpInstrumentation({
        ignoreIncomingRequestHook(req) {
          // Ignore spans from static assets and health checks.
          const isStaticAssetOrHealthCheck = !!req.url.match(
            /^\/(api\/health\/.*|css|js|img|metrics|favicon|site\.webmanifest)/
          );
          return isStaticAssetOrHealthCheck;
        },
      }),
      new ExpressInstrumentation(),
      new PinoInstrumentation(),
    ],
  });
}
