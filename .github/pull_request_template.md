## Summary of Changes

<!-- Briefly describe the purpose of this PR and what problem it solves. -->

### Type of Change
- [ ] `feat`: New feature or capability
- [ ] `fix`: Bug fix
- [ ] `test`: New or updated tests
- [ ] `refactor`: Code change that neither fixes a bug nor adds a feature
- [ ] `docs`: Documentation updates
- [ ] `chore`: Tooling, dependencies, or repository hygiene

---

## Pre-Merge Quality Verification (Local Gate)

Please ensure the following commands pass locally before submitting:

- [ ] `dotnet build -c Release` passes with **0 warnings, 0 errors**
- [ ] `dotnet test` passes with **100% of test suites green**
- [ ] `dotnet format Ledgr.sln --verify-no-changes --no-restore` reports clean formatting
- [ ] Architecture and schema changes are documented in `/docs` if applicable

---

## Related Issue / Tracking

Fixes # <!-- Issue number if applicable -->
