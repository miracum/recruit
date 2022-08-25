const { NodeTracerProvider } = require("@opentelemetry/sdk-trace-node");
const { getNodeAutoInstrumentations } = require("@opentelemetry/auto-instrumentations-node");
const { registerInstrumentations } = require("@opentelemetry/instrumentation");
const { BatchSpanProcessor } = require("@opentelemetry/sdk-trace-base");
const { Resource } = require("@opentelemetry/resources");
const { SemanticResourceAttributes } = require("@opentelemetry/semantic-conventions");
const { JaegerExporter } = require("@opentelemetry/exporter-jaeger");
const { OTLPTraceExporter } = require("@opentelemetry/exporter-trace-otlp-grpc");

exports.setupTracing = (tracingConfig) => {
  const provider = new NodeTracerProvider({
    resource: new Resource({
      [SemanticResourceAttributes.SERVICE_NAME]: tracingConfig.serviceName,
      [SemanticResourceAttributes.SERVICE_VERSION]: process.env.VERSION || "0.0.0",
    }),
  });

  const exporter =
    process.env.OTEL_TRACES_EXPORTER === "otlp" ? new OTLPTraceExporter() : new JaegerExporter();
  provider.addSpanProcessor(new BatchSpanProcessor(exporter));
  provider.register();

  registerInstrumentations({
    tracerProvider: provider,
    instrumentations: [
      getNodeAutoInstrumentations({
        "@opentelemetry/instrumentation-http": {
          ignoreIncomingPaths: [
            /^\/(api\/health\/.*|css|js|img|metrics|favicon|site\.webmanifest)/,
          ],
        },
        "@opentelemetry/instrumentation-express": {},
        "@opentelemetry/instrumentation-pino": {},
      }),
    ],
  });
};
