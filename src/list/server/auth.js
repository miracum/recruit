import { expressjwt as jwt } from "express-jwt";
import { expressJwtSecret } from "jwks-rsa";

export function createJwtCheck(config) {
  // if auth is disabled, simply skip any access checks and allow full access.
  if (config.auth.disabled) {
    return (_req, _res, next) => next();
  }

  return jwt({
    // Provide a signing key based on the key identifier in the header
    // and the signing keys provided by your Auth0 JWKS endpoint.
    secret: expressJwtSecret({
      cache: true,
      rateLimit: true,
      jwksRequestsPerMinute: 5,
      jwksUri: `${config.auth.url}/realms/${config.auth.realm}/protocol/openid-connect/certs`,
    }),
    algorithms: ["RS256", "RS384", "RS512"],
  });
}
