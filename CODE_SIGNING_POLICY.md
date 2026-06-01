# Code Signing Policy

Web Print Agent is code-signed using free code signing provided by [SignPath.io](https://about.signpath.io/), with a certificate issued by the [SignPath Foundation](https://signpath.org/).

The signature lets Windows (SmartScreen) and end users verify that the installer and binaries they run were built from this repository's source code and were not tampered with after the build.

## What is signed

Every public release on the [GitHub Releases page](https://github.com/dragonofmercy/web-print-agent/releases) is signed:

- `PrintAgent-win-Setup.exe` - the per-user installer
- `PrintAgent.exe` - the tray agent
- `Update.exe` - the Velopack updater stub
- the `*.nupkg` packages of the update feed

## Build and signing process

- Binaries are built from source by a trusted build system (GitHub Actions) - never on a developer machine.
- The release is packaged with [Velopack](https://velopack.io/) (`vpk pack`).
- The resulting artifacts are submitted to SignPath.io, where **every signing request requires manual approval** by an authorized team member before the certificate is applied.
- Signing happens per release; there is no automatic, unattended signing.

## Project roles

| Role | Members | Responsibility |
|------|---------|----------------|
| Authors | [DragonOfMercy](https://github.com/dragonofmercy) | Write and commit source code |
| Reviewers | [DragonOfMercy](https://github.com/dragonofmercy) | Review code changes before release |
| Approvers | [DragonOfMercy](https://github.com/dragonofmercy) | Approve each signing request in SignPath |

Web Print Agent is currently maintained by a single author; all roles are held by the maintainer. Access to both the source repository and the SignPath organization is protected by multi-factor authentication.

## Privacy policy

This program will not transfer any information to other networked systems unless specifically requested by the user or the person installing or operating it.

Web Print Agent runs entirely on the local machine. It exposes a `wss://` API bound to `127.0.0.1` only, and communicates exclusively with HTTPS web pages that the user has **explicitly authorized** through an on-screen pairing prompt. It does not collect, store, or transmit any telemetry, analytics, or personal data.
