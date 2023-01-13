const yaml = require("js-yaml");
const fs = require("fs");
const { createAccessFilter } = require("../../../server/fhirAccessFilter");

const doc = yaml.load(fs.readFileSync("tests/unit/fixtures/test-notify-rules.yaml", "utf8"));

const singleList = require("./fixtures/bundle-with-single-list.json");
const multipleLists = require("./fixtures/bundle-with-multiple-lists.json");
const listsWithoutStudyExtension = require("./fixtures/bundle-with-list-without-study-extension.json");
const bundleWithoutEntries = require("./fixtures/bundle-without-entries.json");

const userWithoutAnyAccess = {
  preferred_username: "userWithoutAccess",
  email: "user.without.access@example.com",
};

const userAllowedToAccessPROSaStudy = {
  preferred_username: "user.one",
};

const userAllowedToAccessAllStudies = {
  email: "user.all-access@example.com",
};

const userWithAdminRole = {
  resource_access: {
    "uc1-recruit-list": { roles: ["admin"] },
  },
};

const filterAcessibleResources = createAccessFilter(doc.notify.rules.trials, {
  clientId: "uc1-recruit-list",
});

describe("fhirAccessFilter", () => {
  it("should update the 'total' in a bundle when removing a resource", () => {
    const filtered = filterAcessibleResources(singleList, userWithoutAnyAccess);

    expect(filtered.entry).toHaveLength(0);
    expect(filtered.total).toBe(0);
  });
  it("should keep only the List resources the user has access to", () => {
    const filtered = filterAcessibleResources(multipleLists, userAllowedToAccessPROSaStudy);

    expect(filtered.entry).toHaveLength(1);
    expect(filtered.entry[0].resource.extension[0].valueReference.display).toBe("PROSa");
  });
  it("should not include Lists with a missing study reference extension", () => {
    const filtered = filterAcessibleResources(listsWithoutStudyExtension, userAllowedToAccessPROSaStudy);

    expect(filtered.entry).toHaveLength(0);
  });
  it("should include all Lists if the user has a subscription to the '*' wildcard study", () => {
    const filtered = filterAcessibleResources(multipleLists, userAllowedToAccessAllStudies);

    expect(filtered.entry).toHaveLength(multipleLists.entry.length);
  });
  it("should allow access to everything if the user has the 'admin' role", () => {
    const filtered = filterAcessibleResources(multipleLists, userWithAdminRole);

    expect(filtered.entry).toHaveLength(multipleLists.entry.length);
  });
  it("should block access to users without a username and/or email", () => {
    const userWithoutUsernameAndEmail = {};
    const filtered = filterAcessibleResources(multipleLists, userWithoutUsernameAndEmail);

    expect(filtered.entry).toHaveLength(0);
  });
  it("should return empty list if bundle contains no entries", () => {
    const filtered = filterAcessibleResources(bundleWithoutEntries, userWithAdminRole);

    expect(filtered.total).toBe(0);
    expect(filtered.entry).toBeUndefined();
  });
});
