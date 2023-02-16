# Changelog

## [10.0.8](https://github.com/miracum/recruit/compare/v10.0.7...v10.0.8) (2023-02-16)


### Bug Fixes

* **list:** handle unset user object in admin role check ([#66](https://github.com/miracum/recruit/issues/66)) ([bcc3e0c](https://github.com/miracum/recruit/commit/bcc3e0c2f0613640090ab6f1486ba1c42a6def2e))


### Documentation

* enable permalink support ([51aff32](https://github.com/miracum/recruit/commit/51aff3235ec9aba2ad33ad5b253600ee4865b514))
* switch to OCI-based installation instructions for kubernetes ([0b5cbe6](https://github.com/miracum/recruit/commit/0b5cbe6da4408f40a5c846ced462a7f551128556))


### CI/CD

* added workflow to reset Chart.yaml changelog annotations on release ([7a8e7aa](https://github.com/miracum/recruit/commit/7a8e7aa730a8e7d576caf169aca7e8d361bc29fd))


### Miscellaneous Chores

* **deps:** update docker.io/bitnami/kubectl:1.24.8 docker digest to 9c1bd1f ([#61](https://github.com/miracum/recruit/issues/61)) ([d6d38ff](https://github.com/miracum/recruit/commit/d6d38ff2cf7b4cee93ab701061b4a4d4c9f8f970))
* **deps:** update docker.io/library/gradle:7.6.0 docker digest to bd88522 ([#62](https://github.com/miracum/recruit/issues/62)) ([6d1317a](https://github.com/miracum/recruit/commit/6d1317a89ee0fda8a2989f6738ac5f8fa9d8b411))

## [10.0.7](https://github.com/miracum/recruit/compare/v10.0.6...v10.0.7) (2023-01-18)


### Bug Fixes

* **docker-compose:** mount path for the notify rules config file ([fc654b8](https://github.com/miracum/recruit/commit/fc654b856f3e29c18a12c712a1385e61e95b895a))


### Miscellaneous Chores

* **helm:** reset Chart.yaml changelog ([862cf89](https://github.com/miracum/recruit/commit/862cf899ae5d223838147d1b0a803492fea789c4))

## [10.0.6](https://github.com/miracum/recruit/compare/v10.0.5...v10.0.6) (2023-01-17)


### CI/CD

* fix secret for helm sync job in reusable workflow ([e283cbb](https://github.com/miracum/recruit/commit/e283cbb7f21525973e299aadf2a54d71e0562185))

## [10.0.5](https://github.com/miracum/recruit/compare/v10.0.4...v10.0.5) (2023-01-16)


### CI/CD

* fixed sync helm chart permissions ([3c19e7b](https://github.com/miracum/recruit/commit/3c19e7bdf0f26471510da3de2b8da599bbc89b14))


### Miscellaneous Chores

* **deps:** refresh pip-compile outputs ([#56](https://github.com/miracum/recruit/issues/56)) ([efe24e6](https://github.com/miracum/recruit/commit/efe24e6a0c1af4bdb46f43a313434116cd0fb2b0))

## [10.0.4](https://github.com/miracum/recruit/compare/v10.0.3...v10.0.4) (2023-01-15)


### CI/CD

* moved helm chart sync job back to release ([30ec03d](https://github.com/miracum/recruit/commit/30ec03d91fc61652ddfc14b9101b4065b23c4ff3))
* run chart sync on master for testing ([734beee](https://github.com/miracum/recruit/commit/734beeea74406d4fa48ca03496063ec7304b19d9))

## [10.0.3](https://github.com/miracum/recruit/compare/v10.0.2...v10.0.3) (2023-01-15)


### CI/CD

* possibly fixed helm chart sync job ([51c1a87](https://github.com/miracum/recruit/commit/51c1a8751135db8036c9d7bfcda91b772bef382f))

## [10.0.2](https://github.com/miracum/recruit/compare/v10.0.1...v10.0.2) (2023-01-15)


### CI/CD

* added job to sync chart to central chart repo ([#52](https://github.com/miracum/recruit/issues/52)) ([f07a089](https://github.com/miracum/recruit/commit/f07a089486439cd22da459ace9a0a412ebb34431))

## [10.0.1](https://github.com/miracum/recruit/compare/v10.0.0...v10.0.1) (2023-01-14)


### CI/CD

* fixed helm dependency update and possibly ci permissions ([#49](https://github.com/miracum/recruit/issues/49)) ([6e73191](https://github.com/miracum/recruit/commit/6e7319154eb9f4e7b0530fb9189dd9ef572ce8ac))

## [10.0.0](https://github.com/miracum/recruit/compare/v9.16.0...v10.0.0) (2023-01-14)


### ⚠ BREAKING CHANGES

* moved source code to monorepo (#34)

### Features

* moved source code to monorepo ([#34](https://github.com/miracum/recruit/issues/34)) ([6005389](https://github.com/miracum/recruit/commit/6005389ead1129a22acb7dc8d69c11fdf838e8e8))


### Miscellaneous Chores

* **deps:** update dependency certifi to v2022.12.7 [security] ([bd58732](https://github.com/miracum/recruit/commit/bd587322a65572b3c2ef85171804055860251c7d))
* fixed list cve and disabled automerge ([#47](https://github.com/miracum/recruit/issues/47)) ([a437045](https://github.com/miracum/recruit/commit/a437045f23156fdb89701f621ab5bcea5f31625a))


### CI/CD

* added helm linting workflow ([#48](https://github.com/miracum/recruit/issues/48)) ([fcdd266](https://github.com/miracum/recruit/commit/fcdd266113d1f879d5c50a08d18a7a02952f5b28))
