const path = require("path");
const bearerToken = require("express-bearer-token");
const promBundle = require("express-prom-bundle");
const history = require("connect-history-api-fallback");
const helmet = require("helmet");
const pino = require("pino-http")();
const modifyResponse = require("node-http-proxy-json");
const logger = require("pino")({ level: process.env.LOG_LEVEL || "info" });
const yaml = require("js-yaml");
const fs = require("fs");
const cors = require("cors");

const { createProxyMiddleware } = require("http-proxy-middleware");

const { createJwtCheck } = require("./auth");
const { createAccessFilter, createPatchFilter, createDeleteFilter } = require("./fhirAccessFilter");
const { setupTracing } = require("./tracing");

const { config } = require("./config");

if (!config.auth.disabled && (!config.auth.url || !config.auth.clientId || !config.auth.realm)) {
  logger.error("Keycloak is not configured correctly: URL, client id, and realm are required.");
  process.exit(1);
}

const checkJwt = createJwtCheck(config);

// by default, do not do any filtering, simply returing the resource to be filtered.
// eslint-disable-next-line no-unused-vars
let filterAcessibleResources = (resource, _user) => resource;
let isPatchRequestAllowed = (_resourceType, _user) => true;
let isDeleteAllowed = (_user) => true;

try {
  logger.child({ path: config.rulesFilePath }).debug("Trying to load trials config");
  const configString = fs.readFileSync(config.rulesFilePath, "utf8");
  const rulesConfig = yaml.load(configString);
  if (!rulesConfig.notify?.rules?.trials) {
    throw new Error("Invalid configuration file structure. Should be: notify.rules.trials.");
  }

  filterAcessibleResources = createAccessFilter(rulesConfig.notify.rules.trials, config.auth);
  isPatchRequestAllowed = createPatchFilter(config.auth);
  isDeleteAllowed = createDeleteFilter(config.auth);
} catch (error) {
  logger.child({ error }).error("Failed to load the trial rules config. Defaulting to no filtering.");
}

if (config.tracing.enabled) {
  logger.child({ serviceName: config.tracing.serviceName }).info("Tracing is enabled.");
  setupTracing(config.tracing);
} else {
  logger.info("Tracing is disabled");
}

// express is required to be imported after the OTEL SDK is setup so the plugins work correctly
// eslint-disable-next-line import/order
const express = require("express");

// eslint-disable-next-line import/order
const http = require("http");

const dePseudonymizer = require("./dePseudonymizer");

const metricsMiddleware = promBundle({
  includeMethod: true,
  includePath: true,
  normalizePath: [
    ["^/css/.*", "/css/#file"],
    ["^/img/.*", "/img/#file"],
    ["^/js/.*", "/js/#file"],
    ["^/recommendations/.*", "/recommendations/#recommendation-id"],
    ["^/patients/.*/record", "/patients/#subject-id/record"],
  ],
});

const app = express();

if (config.shouldLogRequests) {
  app.use(pino);
}

app.use(
  helmet({
    contentSecurityPolicy: false,
  })
);
app.use(cors());
app.use(bearerToken());
app.use(express.json());
app.use(metricsMiddleware);

const allowedResourcesToPatch = /^\/\/(?<resourceType>ResearchSubject|List)/;

const proxyRequestFilter = (_pathname, req) => req.method === "GET" || req.method === "PATCH" || req.method === "DELETE";
const proxy = createProxyMiddleware(proxyRequestFilter, {
  target: config.fhirUrl,
  changeOrigin: false,
  pathRewrite: {
    "^/fhir": "/",
  },
  secure: config.proxy.isSecureBackend,
  xfwd: true,
  onProxyReq(proxyReq, req, res) {
    // the ApacheProxyAddressStrategy used by HAPI FHIR
    // constructs the server URL from both the X-Forwarded-Host and X-Forwarded-Port
    // since the X-Forwarded-Host created by HPM already contains the port (eg. localhost:8443)
    // the resulting FHIR server URL would end with the port number twice (eg. https://localhost:8443:8443)
    proxyReq.removeHeader("X-Forwarded-Port");

    const proto = proxyReq.getHeader("X-Forwarded-Proto");

    if (proto) {
      if (proto.includes("https")) {
        proxyReq.setHeader("X-Forwarded-Proto", "https");
      } else {
        proxyReq.setHeader("X-Forwarded-Proto", "http");
      }
    }

    // DELETE operations are only allowed for admins
    if (req.method === "DELETE" && !config.auth.disabled) {
      if (!isDeleteAllowed(req.user)) {
        res
          .writeHead(403, {
            "Content-Type": "text/plain",
          })
          .end(`Operation unauthorized. DELETEing resources requires the "admin" role.`);
      }
    }

    // PATCH operations are only allowed on ResearchSubject (to set the recruitment status
    // and custom note and List resources (to mark them as retired/active)
    if (req.method === "PATCH" && !config.auth.disabled) {
      const match = req.url.match(allowedResourcesToPatch);
      if (match) {
        logger.child({ url: req.url, match }).info("PATCHing a valid resource");
        if (!isPatchRequestAllowed(match.groups.resourceType, req.user)) {
          res
            .writeHead(403, {
              "Content-Type": "text/plain",
            })
            .end(`Operation unauthorized. Unauthorized to PATCH resources of type ${match.groups.resourceType}`);
        }
      } else {
        res
          .writeHead(403, {
            "Content-Type": "text/plain",
          })
          .end("Operation unauthorized. Attempted to patch an invalid resource type");
      }
    }
  },
  onProxyRes(proxyRes, req, res) {
    // eslint-disable-next-line no-param-reassign
    proxyRes.headers["Cache-Control"] = "no-store";
    return modifyResponse(res, proxyRes, async (body) => {
      if (!body) {
        return body;
      }

      let modifiedBody = body;

      if (!config.auth.disabled) {
        modifiedBody = filterAcessibleResources(body, req.user);
      }

      if (config.pseudonymization.enabled) {
        logger.debug("De-pseudonymization is enabled.");
        if (body.resourceType === "Patient") {
          logger.child({ resourceId: body.id }).debug("De-pseudonymizing Patient resource");
          try {
            modifiedBody = await dePseudonymizer.dePseudonymize(config.pseudonymization, body);
          } catch (error) {
            logger.child({ error }).error("De-pseudonymization failed. Returning original resource.");
          }
        }
      }
      return modifiedBody;
    });
  },
});

app.use((req, res, next) => {
  if (req.path.endsWith("/metrics")) {
    const expectedToken = config.metrics.bearerToken;
    if (expectedToken) {
      if (!req.token) {
        return res.sendStatus(403);
      }
      if (req.token === expectedToken) {
        return next();
      }
      return res.sendStatus(403);
    }
  }
  return next();
});

app.use("^/fhir", checkJwt, proxy);

app.get("/config", (_req, res) =>
  res.json({
    hideDemographics: config.ui.hideDemographics,
    hideLastVisit: config.ui.hideLastVisit,
    hideEhrButton: config.ui.hideEhrButton,
    isKeycloakDisabled: config.auth.disabled,
    hideRecommendationDate: config.auth.hideRecommendationDate,
    authClientId: config.auth.clientId,
    authUrl: config.auth.url,
    authRealm: config.auth.realm,
    realm: config.auth.realm,
    url: config.auth.url,
    clientId: config.auth.clientId,
    checkLoginIframeDisabled: config.auth.checkLoginIframeDisabled,
  })
);

app.get("/api/health/:probe", (req, res) => {
  res.send({ probe: req.params.probe, status: "ok" }).end();
});

app.use(history());

app.use(express.static(path.join(__dirname, "..", "dist")));

app.get("/", (_req, res) => {
  res.render(path.join(__dirname, "..", "dist/index.html"));
});

const port = process.env.PORT || 8080;

function onError(error) {
  if (error.syscall !== "listen") {
    throw error;
  }
  logger.child({ error }).error(error);
  process.exit(1);
}

const server = http.createServer(app);

function onListening() {
  const addr = server.address();
  logger.info(`Listening on '${addr.address}:${addr.port}'`);
}

function shutdown(signal) {
  return (err) => {
    logger.child({ signal }).info(`Shutdown due to signal.`);
    if (err) {
      logger.error({ err });
    }
    process.exit(err ? 1 : 0);
  };
}

process
  .on("SIGTERM", shutdown("SIGTERM"))
  .on("SIGINT", shutdown("SIGINT"))
  .on("uncaughtException", shutdown("uncaughtException"));

server.listen(port);
server.on("error", onError);
server.on("listening", onListening);

module.exports = app;
