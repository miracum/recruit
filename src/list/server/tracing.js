const { NodeTracerProvider } = require("@opentelemetry/sdk-trace-node");
const { BatchSpanProcessor } = require("@opentelemetry/sdk-trace-base");
const { Resource } = require("@opentelemetry/resources");
const { SemanticResourceAttributes } = require("@opentelemetry/semantic-conventions");
const { JaegerExporter } = require("@opentelemetry/exporter-jaeger");
const { OTLPTraceExporter } = require("@opentelemetry/exporter-trace-otlp-grpc");
const { registerInstrumentations } = require("@opentelemetry/instrumentation");
const { HttpInstrumentation } = require("@opentelemetry/instrumentation-http");
const { ExpressInstrumentation } = require("@opentelemetry/instrumentation-express");
const { PinoInstrumentation } = require("@opentelemetry/instrumentation-pino");

exports.setupTracing = (tracingConfig) => {
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
};
